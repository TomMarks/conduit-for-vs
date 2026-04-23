# SPIKE-001 — WebView2 inside a Remote UI tool window

> Status: **closed**  •  Date: 2026-04-23  •  Owner: project plan

## Question

Can a `WebView2` WPF control be hosted inside a VS.Extensibility (OOP) Remote UI tool window, and is two-way communication feasible without the `CoreWebView2.WebMessageReceived` event handler?

## Finding

**Yes — with one constraint and one architectural adaptation.**

1. **WebView2 in Remote UI XAML** works because Remote UI XAML is rendered inside `devenv.exe`, which loads `Microsoft.Web.WebView2.Wpf.dll` for its own features (Copilot, browser windows, etc.).  The XAML `xmlns:wv2` namespace resolves at runtime in that process.  Our extension project does **not** need its own NuGet reference — the type is resolved from the VS process.

2. **Two-way bridge via CoreWebView2.WebMessageReceived is not possible** from an OOP extension.  Remote UI has no code-behind, so there is no place in the extension host process to attach .NET event handlers to the `WebView2` instance that lives in `devenv.exe`.

3. **Solution: local WebSocket server (`ConduitWebSocketBridge`).**  The OOP extension starts an `HttpListener` on a random loopback port.  The chat page's JavaScript connects to `ws://localhost:<port>/ws`.  This gives full-duplex, low-latency comms between the webview (running in devenv) and the extension host — without touching devenv's address space.

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
| WebView2 in Remote UI XAML | **Yes** — reference `Microsoft.Web.WebView2.Wpf` from VS process; no NuGet in extension project |
| Bridge mechanism | **Local WebSocket** (`ConduitWebSocketBridge` on `http://localhost:<dynamic-port>/`) |
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

1. **SPIKE-001 closed** — Phase 1 (`ClaudeCode.VSExtension.UI` shell) can proceed.
2. **SPIKE-002 (CSP + assets)** is the next gate: replace `ConduitWebSocketBridge.ChatHtml` with a Vite bundle served via `SetVirtualHostNameToFolderMapping` or a local static file server.
3. **No `VsBridge` component needed for Phase 1** — the WebSocket pattern sidesteps every in-proc API that would have required it.  `VsBridge` remains available for Phase 4 (inline diff adornments) and Phase 3 (terminal handoff).
4. **Phase 1 `ConduitWebSocketBridge` is the seed for `ICliHost`** (Phase 2): replace the echo handler with NDJSON framing to/from the Claude CLI process.

## Runtime exit criterion

`F5` in VS 2026 Experimental Instance → open Conduit tool window → WebView2 renders the chat page → type a message → press Send → echo appears as `[echo] {...}` in the chat log.

## Recheck cadence

- If VS ships a version that removes WebView2 WPF from `devenv.exe`, this spike re-opens.  Watch VS release notes.
