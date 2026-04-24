# SPIKE-001 — WebView2 inside a Remote UI tool window

> Status: **closed**  •  Date: 2026-04-23  •  Owner: project plan

## Question

Can a `WebView2` WPF control be hosted inside a VS.Extensibility (OOP) Remote UI tool window, and is two-way communication feasible without the `CoreWebView2.WebMessageReceived` event handler?

## Finding

**Partial — bridge confirmed, WebView2-in-XAML refuted.**

1. **WebView2 in Remote UI XAML does NOT work.**  At runtime, `devenv.exe` does not have `Microsoft.Web.WebView2.Wpf.dll` in its assembly search path.  VS uses WebView2 internally via the native COM/WinRT layer, *not* via the managed WPF wrapper.  Remote UI's XAML parser silently skips the unresolvable `wv2:WebView2` element; standard WPF controls in the same DataTemplate (e.g., `Grid`, `Border`, `TextBlock`) render normally.  This was verified by the diagnostic status bar: the pure-Remote-UI footer appeared correctly while the `wv2:WebView2` row above it remained blank.

2. **Two-way bridge via local WebSocket is confirmed working.**  The `ConduitWebSocketBridge` `HttpListener` serves the chat HTML and accepts WebSocket connections.  Verified end-to-end in a browser: page loads, `onopen` fires, echo round-trip completes.

3. **Consequence: WebView2 must be hosted in-process by `ClaudeCode.VsBridge`.**  A VSSDK in-proc component running inside `devenv.exe` can create a `WebView2` WPF control directly, point it at `ConduitWebSocketBridge`'s URL, and embed the result in a `ToolWindowPane`.  See **SPIKE-101**.

## Evidence

### Remote UI XAML constraints (from Microsoft docs, Jan 2026)

> *"A Remote user control is instantiated in the Visual Studio process… the XAML can't reference types and assemblies from the **extension** but can reference types and assemblies from the **Visual Studio process**."*

VS 2026 loads `Microsoft.Web.WebView2.Wpf.dll` because Copilot Chat uses it.  That makes the type available to Remote UI XAML with no extra packaging.

### String → Uri binding

`WebView2.Source` is typed `System.Uri`.  Our data context exposes a `string WebViewSource` property.  WPF's binding engine applies `UriTypeConverter` automatically (standard WPF behavior), so `{Binding WebViewSource}` on the `Source` property works without a value converter.

### HttpListener on localhost — no ACL required

`HttpListener` with a `http://localhost:<port>/` prefix does not require a URL ACL or admin rights on Windows Vista+.  The OOP extension host process can listen freely.  WebView2's default content security policy allows `ws://localhost` WebSocket connections.

### Build confirmation

`dotnet build` on `ClaudeCode.VSExtension.csproj` passes with 0 warnings, 0 errors (TreatWarningsAsErrors=true).

## Artifacts

| File | Description |
|---|---|
| [`src/ClaudeCode.VSExtension/Bridge/ConduitWebSocketBridge.cs`](../../src/ClaudeCode.VSExtension/Bridge/ConduitWebSocketBridge.cs) | HttpListener + WebSocket server; serves inline chat HTML; echoes messages back |
| [`src/ClaudeCode.VSExtension/ToolWindows/ConduitToolWindowContent.xaml`](../../src/ClaudeCode.VSExtension/ToolWindows/ConduitToolWindowContent.xaml) | DataTemplate with `<wv2:WebView2 Source="{Binding WebViewSource}"/>` |
| [`src/ClaudeCode.VSExtension/ToolWindows/ConduitToolWindowContent.cs`](../../src/ClaudeCode.VSExtension/ToolWindows/ConduitToolWindowContent.cs) | Starts bridge in `ControlLoadedAsync`; pushes source URL to VM |
| [`src/ClaudeCode.VSExtension/ToolWindows/ConduitToolWindowViewModel.cs`](../../src/ClaudeCode.VSExtension/ToolWindows/ConduitToolWindowViewModel.cs) | Extends `NotifyPropertyChangedObject`; exposes observable `WebViewSource` string |

## Decision

| | Value |
|---|---|
| WebView2 in Remote UI XAML | **No** — `Microsoft.Web.WebView2.Wpf.dll` is not in devenv's assembly path; element silently skipped |
| WebView2 host | **`ClaudeCode.VsBridge`** — VSSDK in-proc `ToolWindowPane` running inside devenv |
| Bridge mechanism | **Local WebSocket** (`ConduitWebSocketBridge` on `http://localhost:<dynamic-port>/`) — confirmed working |
| Bridge → VsBridge URL handoff | Port written to `%TEMP%\conduit-bridge.port` by OOP; read by VsBridge on init |
| JS ↔ extension direction | JS: `ws.send(JSON.stringify({type,text}))` → extension: `MessageReceived` event |
| Extension → JS direction | `bridge.BroadcastAsync(json)` pushes to all connected sockets |
| Phase 1 inline HTML | Inline string in `ConduitWebSocketBridge` — replaced by Vite bundle when SPIKE-002 closes |

## Risk mitigations for Phase 1

| Risk | Mitigation |
|---|---|
| VS version ships without `Microsoft.Web.WebView2.Wpf.dll` | Very unlikely for VS 2026; if it occurs, package the DLL in the VSIX and register an `AppDomain.AssemblyResolve` handler via VsBridge |
| `HttpListener` port conflict | `FindFreePort()` probes with `TcpListener(0)`; retried per session |
| WebView2 environment conflicts with VS's Copilot instance | Default `CoreWebView2Environment` creates a separate browser process; no shared state with Copilot |
| Content Security Policy blocks `ws://localhost` | WebView2 default CSP permits localhost; lock down in SPIKE-002 once we control the page |

## Implications for the plan

1. **SPIKE-001 closed** — bridge mechanism confirmed; WebView2-in-XAML refuted.
2. **SPIKE-101 opened** — `ClaudeCode.VsBridge` must host the `WebView2` `ToolWindowPane` in devenv.  See `docs/spikes/SPIKE-101-vssdk-bridge.md`.
3. **SPIKE-002 (CSP + assets)** moves to after SPIKE-101 — the Vite bundle will be served by `ConduitWebSocketBridge` and loaded by VsBridge's WebView2.
4. **`VsBridge` is now a Phase 1 dependency**, not a Phase 4 optional component.
5. **Phase 1 `ConduitWebSocketBridge` is the seed for `ICliHost`** (Phase 2): replace the echo handler with NDJSON framing to/from the Claude CLI process.

## Runtime exit criterion

`F5` in VS 2026 Experimental Instance → open Conduit tool window → WebView2 renders the chat page → type a message → press Send → echo appears as `[echo] {...}` in the chat log.

## Recheck cadence

- If VS ships a version that removes WebView2 WPF from `devenv.exe`, this spike re-opens.  Watch VS release notes.
