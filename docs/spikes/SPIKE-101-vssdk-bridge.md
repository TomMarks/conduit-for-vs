# SPIKE-101 — VSSDK bridge: WebView2 in a ToolWindowPane

> Status: **abandoned**  •  Date: 2026-04-23  •  Owner: project plan

## Question

Can `ClaudeCode.VsBridge` (a VSSDK in-proc package running inside devenv.exe) host a `WebView2` `ToolWindowPane` and connect it to `ConduitWebSocketBridge`?

## Context

SPIKE-001 established that `Microsoft.Web.WebView2.Wpf.dll` is not in devenv's assembly search path, so Remote UI XAML cannot instantiate a `WebView2` control. The proposed fix was to move WebView2 hosting into a VSSDK in-proc component that runs directly inside devenv — where it can reference any WPF type.

## Finding

**Technically feasible, but abandoned due to TFM constraint.**

The VSSDK SDK meta-package (`Microsoft.VisualStudio.SDK`) targets `.NETFramework` only. A VsBridge project requires `net48`; it cannot target `net8.0-windows`. The project mandates net8+ throughout; `net48` is a non-starter.

Restore against `net8.0-windows` produced:

```
NU1701: Package 'Microsoft.VisualStudio.SDK 17.x' was restored using '.NETFramework,v4.8.1'
        instead of the project target framework 'net8.0-windows10.0.22621.0'. This package
        may not be fully compatible with your project.
```

All VSSDK shell packages carry the same restriction; there is no net8 variant.

## Abandoned in favour of

**SPIKE-002** — WPF Remote UI chat. Standard WPF controls inside a Remote UI `DataTemplate` run inside devenv and can reference any devenv-loaded assembly. The ViewModel in the OOP host streams updates across the process boundary via `[DataMember]` change notifications. No secondary in-proc package is needed for the chat UI.

`ClaudeCode.VsBridge` may still be added in a later phase if OOP gaps are encountered (e.g., advanced editor margins, in-proc terminal handoff in Phase 3). If added, it will target `net48` with a clear in-proc-only scope. It is **not** a Phase 1 requirement.

## Artifacts (never committed — all deleted)

| File | Status |
|---|---|
| `src/ClaudeCode.VsBridge/ClaudeCode.VsBridge.csproj` | Deleted |
| `src/ClaudeCode.VsBridge/VsBridgePackage.cs` | Deleted |
| `src/ClaudeCode.VsBridge/ToolWindows/ConduitChatPane.cs` | Deleted |
| `src/ClaudeCode.VsBridge/ToolWindows/ConduitChatControl.xaml/.cs` | Deleted |
| `src/ClaudeCode.VsBridge/VsBridge.vsct` | Deleted |

## Recheck cadence

- Reopen if OOP gaps require in-proc workarounds (Phase 3 terminal, Phase 4 editor margins).
- If VS.Extensibility SDK gains first-class WebView2 support, re-evaluate whether VsBridge is needed at all.
