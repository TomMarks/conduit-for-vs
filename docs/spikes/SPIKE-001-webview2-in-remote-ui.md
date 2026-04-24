# SPIKE-001 — WebView2 inside a Remote UI tool window

> Status: **closed — approach abandoned**  •  Date: 2026-04-23  •  Owner: project plan

## Question

Can a `WebView2` WPF control be hosted inside a VS.Extensibility (OOP) Remote UI tool window, and is two-way communication feasible without the `CoreWebView2.WebMessageReceived` event handler?

## Finding

**No — both the WebView2-in-XAML path and the VSSDK VsBridge fallback were ruled out.**

1. **WebView2 in Remote UI XAML does NOT work.** At runtime, `devenv.exe` does not have `Microsoft.Web.WebView2.Wpf.dll` in its assembly search path. VS uses WebView2 internally via the native COM/WinRT layer, *not* via the managed WPF wrapper. Remote UI's XAML parser silently skips the unresolvable `wv2:WebView2` element; standard WPF controls in the same DataTemplate render normally. Verified: diagnostic status bar appeared correctly while the WebView2 row remained blank.

2. **VSSDK in-proc bridge (VsBridge / SPIKE-101) was also ruled out.** The VSSDK SDK meta-package targets `.NETFramework` only; a VsBridge project would require `net48`. The project requires net8+ throughout; `net48` is a non-starter.

3. **WPF Remote UI chat (SPIKE-002) is the adopted approach.** Standard WPF controls (`ItemsControl`, `DataTemplate`, `TextBox`, `Button`) inside a Remote UI `DataTemplate` work fully. The ViewModel lives in the OOP host process (net8), exposes an `ObservableList<ChatMessage>`, and streams updates across the Remote UI boundary via `[DataMember]` property changes. No bridge, no secondary in-proc package, no HTML/JS.

## Evidence

### Remote UI XAML constraints

> *"A Remote user control is instantiated in the Visual Studio process… the XAML can't reference types and assemblies from the **extension** but can reference types and assemblies from the **Visual Studio process**."*

`Microsoft.Web.WebView2.Wpf.dll` is not in devenv's managed assembly search path even in VS 2026. The constraint is not about Remote UI — it is about which assemblies devenv loads.

### HttpListener / WebSocket bridge (confirmed, but not used)

`ConduitWebSocketBridge` (now deleted) proved that a local HTTP + WebSocket server in the OOP process works. The chat HTML loaded and echo round-trips completed in a browser. The bridge was confirmed working but is not needed now that the UI is pure WPF.

## Artifacts (historical — all deleted)

| File | Status |
|---|---|
| `src/ClaudeCode.VSExtension/Bridge/ConduitWebSocketBridge.cs` | Deleted — replaced by direct ViewModel binding |
| `src/ClaudeCode.VSExtension/ToolWindows/ConduitToolWindowContent.xaml` | Rewritten — now WPF chat UI |
| `src/ClaudeCode.VSExtension/ToolWindows/ConduitToolWindowContent.cs` | Rewritten — one-liner constructor |
| `src/ClaudeCode.VSExtension/ToolWindows/ConduitToolWindowViewModel.cs` | Rewritten — `ObservableList<ChatMessage>`, `AsyncCommand` |

## Decision

| | Value |
|---|---|
| WebView2 in Remote UI XAML | **No** — `Microsoft.Web.WebView2.Wpf.dll` not in devenv's assembly path |
| VsBridge (VSSDK in-proc) | **No** — requires `net48`; project mandates net8+ |
| Chat UI | **WPF Remote UI** — `ItemsControl` + `DataTemplate` + `DataTrigger`; see SPIKE-002 |
| Bridge mechanism | **Not needed** — ViewModel binds directly to Remote UI XAML |

## Superseded by

**SPIKE-002** — WPF Remote UI chat. See `docs/spikes/SPIKE-002-wpf-remote-ui-chat.md`.

## Recheck cadence

- If VS.Extensibility SDK gains first-class WebView2 support, re-evaluate for Phase 9 (rich rendering, image paste). Watch VS devblogs.
