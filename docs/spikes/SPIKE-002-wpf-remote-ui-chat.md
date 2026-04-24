# SPIKE-002 — WPF Remote UI chat

> Status: **closed**  •  Date: 2026-04-23  •  Owner: project plan

## Question

Can standard WPF controls inside a VS.Extensibility Remote UI `DataTemplate` deliver a functional chat UI with streaming updates, without WebView2 or any in-proc VSSDK component?

## Context

SPIKE-001 proved WebView2 cannot render in Remote UI XAML. SPIKE-101 (VSSDK VsBridge) was abandoned because it requires `net48`. This spike asks whether the WPF controls that Remote UI *does* support are sufficient for a chat UI.

Remote UI constraints:
- XAML runs inside devenv.exe; data context lives in the OOP extension host.
- XAML can only reference types from assemblies devenv loads (standard WPF, VS shell).
- No code-behind, no event handlers, no custom controls from the extension assembly.
- `[DataMember]`-decorated properties on `NotifyPropertyChangedObject` are serialised across the process boundary and update the UI automatically.

## Finding

**Yes — fully sufficient for Phases 1–2.**

Standard WPF controls (`ItemsControl`, `DataTemplate`, `DataTrigger`, `ScrollViewer`, `TextBox`, `Button`, `KeyBinding`) compose a functional chat UI. Streaming token-by-token updates work by mutating a `[DataMember] string Text` property on a `ChatMessage` item in place; Remote UI propagates the change without recreating the item.

## Architecture

```
OOP extension host (net8.0-windows)                    devenv.exe (Remote UI host)
─────────────────────────────────────                  ──────────────────────────
ConduitToolWindowViewModel                             ConduitToolWindowContent.xaml
  ObservableList<ChatMessage>  ──── [DataMember] ────▶  ItemsControl
  ChatMessage.Text (grows)     ──── [DataMember] ────▶    TextBlock (streaming)
  ChatMessage.IsStreaming      ──── [DataMember] ────▶    ▌ cursor (DataTrigger)
  ChatMessage.IsUser           ──── [DataMember] ────▶    HorizontalAlignment (DataTrigger)
  InputText                    ◀─── [DataMember] ────    TextBox (TwoWay binding)
  SendCommand (IAsyncCommand)  ◀─── Command binding ──    Button + KeyBinding(Enter)
```

No HTTP server. No WebSocket. No secondary process or package.

## Streaming update path

1. User sends message → `SendCommand.ExecuteAsync` fires in OOP host.
2. User `ChatMessage` (IsUser=true) appended to `ObservableList<ChatMessage>`.
3. Empty assistant `ChatMessage` (IsStreaming=true) appended.
4. Tokens arrive (stub: word-by-word with 60ms delay; Phase 2: CLI stdout).
5. `reply.Text += token` — single `[DataMember]` property change propagates to devenv.
6. On completion: `reply.IsStreaming = false`, `CanSend = true`.

## XAML approach for conditional styling (no custom types)

User vs. assistant bubbles use `DataTrigger` on `IsUser` to swap `HorizontalAlignment` and `Background`. The streaming cursor uses `DataTrigger` on `IsStreaming` to toggle `Visibility`. No `DataTemplateSelector` (custom type), no value converter (custom type) — only standard WPF triggers.

## Artifacts

| File | Description |
|---|---|
| [`src/ClaudeCode.VSExtension/ToolWindows/ChatMessage.cs`](../../src/ClaudeCode.VSExtension/ToolWindows/ChatMessage.cs) | `NotifyPropertyChangedObject`; `IsUser`, `Text`, `IsStreaming` |
| [`src/ClaudeCode.VSExtension/ToolWindows/ConduitToolWindowViewModel.cs`](../../src/ClaudeCode.VSExtension/ToolWindows/ConduitToolWindowViewModel.cs) | `ObservableList<ChatMessage>`, `InputText`, `CanSend`, `StatusText`, `AsyncCommand SendCommand` |
| [`src/ClaudeCode.VSExtension/ToolWindows/ConduitToolWindowContent.cs`](../../src/ClaudeCode.VSExtension/ToolWindows/ConduitToolWindowContent.cs) | One-liner constructor; passes VM to `RemoteUserControl` base |
| [`src/ClaudeCode.VSExtension/ToolWindows/ConduitToolWindowContent.xaml`](../../src/ClaudeCode.VSExtension/ToolWindows/ConduitToolWindowContent.xaml) | `ItemsControl` chat layout; status bar; input bar with Enter binding |

## Exit criteria (all passed 2026-04-23)

| # | Criterion | Result |
|---|---|---|
| 1 | Tool window opens via View → Other Windows → Conduit Chat | ✅ |
| 2 | User message appears right-aligned (blue bubble) | ✅ |
| 3 | Assistant stub response streams word-by-word left-aligned | ✅ |
| 4 | Streaming cursor `▌` visible during stream, hidden on completion | ✅ |
| 5 | Send button / Enter disabled while streaming, re-enabled after | ✅ |
| 6 | Multiple turns accumulate without clearing prior messages | ✅ |

## Decision

| | Value |
|---|---|
| Chat UI technology | WPF Remote UI (`ItemsControl` + `DataTrigger`) |
| Streaming mechanism | In-place `[DataMember]` property mutation on `ChatMessage` |
| WebView2 | Deferred — revisit for Phase 9 if rich rendering (images, complex markdown) is needed |
| VsBridge | Not needed for chat UI; may be added for OOP gaps in Phase 3–4 |

## Known limitations (Phase 1 backlog)

| Limitation | Notes |
|---|---|
| No auto-scroll | Remote UI has no code-behind; `ScrollViewer` won't follow new messages automatically. Workaround TBD in Phase 1. |
| Plain text only | Markdown arrives as raw `**text**`. Phase 2 option: walk Markdig AST in ViewModel, emit typed block records for richer DataTemplates. |
| Per-token Remote UI updates | Each token crosses the OOP boundary. Adequate for Phase 1; batch at 50ms intervals if perf is an issue at scale. |

## Implications for the plan

1. **Phase 1 closed.** WPF Remote UI chat with stub streaming is the Phase 1 deliverable.
2. **SPIKE-002-csp-and-assets** (original plan) is obsolete — no WebView2, no virtual host mapping needed.
3. **Phase 2** wires `SendCommand` to the real Claude CLI (`--output-format stream-json`); token arrival updates `ChatMessage.Text` exactly as the stub does.
4. **Markdown rendering** is a Phase 2/3 enhancement: parse in ViewModel (net8, no Remote UI constraint), emit structured block records, add DataTemplates.
5. **WebView2** remains an option for Phase 9 if richer rendering is needed (image paste, syntax-highlighted code, custom fonts). No dependency on it before then.
