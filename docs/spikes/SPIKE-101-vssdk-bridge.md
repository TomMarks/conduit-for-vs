# SPIKE-101 — VSSDK bridge: WebView2 in a ToolWindowPane

> Status: **closed**  •  Date: 2026-04-23  •  Owner: project plan

## Question

Can `ClaudeCode.VsBridge` (a VSSDK in-proc package running inside devenv.exe) host a `WebView2` `ToolWindowPane` and connect it to `ConduitWebSocketBridge`?

## Context

SPIKE-001 established that `Microsoft.Web.WebView2.Wpf.dll` is not in devenv's assembly search path, so Remote UI XAML cannot instantiate a `WebView2` control.  The fix is to move WebView2 hosting into a VSSDK in-proc component that runs directly inside devenv — where it can reference and instantiate any WPF type.

## Finding

**Yes.** A VSSDK `ToolWindowPane` with a WPF `UserControl` that embeds `WebView2` works.  The `WebView2` navigates to the `ConduitWebSocketBridge` HTTP endpoint; the existing JS/WebSocket bridge handles all two-way comms with no additional code.

## Architecture

```
devenv.exe
├── ClaudeCode.VsBridge (net8.0-windows, VSSDK in-proc)
│   └── ConduitChatPane  ──  ToolWindowPane
│       └── ConduitChatControl (WPF UserControl)
│           └── WebView2  ──  Source = "http://localhost:{PORT}/"
│                                      ↕ ws://localhost:{PORT}/ws
└── OOP extension host (net8.0, separate process)
    └── ConduitWebSocketBridge  ──  HttpListener on PORT
        ├── GET /   →  inline chat HTML
        └── /ws     →  WebSocket echo (→ CLI in Phase 2)

Port IPC: OOP writes PORT to %TEMP%\conduit-bridge.port on bridge start.
          VsBridge reads it when creating ConduitChatControl.
```

## Activation flow

1. User runs VS, Conduit extension loads in OOP host.
2. `ControlLoadedAsync` fires → `ConduitWebSocketBridge.Start()` → writes port to temp file.
3. User opens **View → Other Windows → Conduit Chat** (VSCT command registered by VsBridge).
4. VsBridge package auto-initialises on first command; `ConduitChatPane` is created.
5. `ConduitChatControl` reads port from temp file, sets `WebView2.Source`.
6. WebView2 navigates → chat page loads → JS connects WebSocket → two-way bridge live.

## Artifacts

| File | Description |
|---|---|
| `src/ClaudeCode.VsBridge/ClaudeCode.VsBridge.csproj` | VSSDK package project (net8.0-windows) |
| `src/ClaudeCode.VsBridge/VsBridgePackage.cs` | `AsyncPackage` — registers tool window and commands |
| `src/ClaudeCode.VsBridge/ToolWindows/ConduitChatPane.cs` | `ToolWindowPane` — returns `ConduitChatControl` as content |
| `src/ClaudeCode.VsBridge/ToolWindows/ConduitChatControl.xaml/.cs` | WPF `UserControl` with `WebView2`; reads port from temp file |
| `src/ClaudeCode.VSExtension/Bridge/ConduitWebSocketBridge.cs` | Updated to write port to `%TEMP%\conduit-bridge.port` on `Start()` |

## Decision

| | Value |
|---|---|
| VsBridge TFM | `net8.0-windows10.0.22621.0` (consistent with OOP extension) |
| Tool window GUID | `{A1B2C3D4-...}` — fixed, registered via `ProvideToolWindow` attribute |
| Port handoff | `%TEMP%\conduit-bridge.port` text file (spike); brokered service in Phase 1 |
| WebView2 NuGet | `Microsoft.Web.WebView2` in VsBridge — DLL ships with the VSIX, runs in devenv |
| OOP tool window | Kept as placeholder with diagnostic bar; Phase 1 will unify activation |

## Implications for the plan

1. `ClaudeCode.VsBridge` is now a **Phase 1 required project** alongside `ClaudeCode.VSExtension`.
2. SPIKE-002 (asset serving / virtual host) targets VsBridge's WebView2, not Remote UI XAML.
3. The temp-file port handoff is replaced by a **brokered service** in Phase 1 (`IConduitBridgeService` registered by OOP, consumed by VsBridge).
4. Phase 3 terminal handoff also goes through VsBridge (`IVsUIShell` is in-proc only).
5. Packaging: VsBridge ships as an additional `.dll` + `.pkgdef` inside the existing VSIX.

## Recheck cadence

- If VS.Extensibility SDK gains first-class WebView2 support (watch devblogs), re-evaluate whether VsBridge can be retired in favour of Remote UI.
