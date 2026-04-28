# SPIKE-006 — Inline diff rendering in VS.Extensibility

> Status: **closed**  •  Date: 2026-04-27  •  SDK: Microsoft.VisualStudio.Extensibility 17.14  •  Owner: project plan

## Question

Can VS.Extensibility editor APIs render an inline diff adornment (suggested-change overlay with Accept/Reject) in the editor viewport? Or do we need `VsBridge` (net48 VSSDK in-proc interop, ruled out)?

## Finding

**Inline diff adornments: NOT possible in OOP. Text edits (Accept): fully supported. Viable alternative exists.**

| Capability | Available in OOP? | Evidence |
|---|---|---|
| `IntraTextAdornmentTag` / intra-line overlays | ❌ | VSSDK only — not exposed in VS.Extensibility SDK 17.14 |
| Built-in "suggested edit" / diff decoration | ❌ | No equivalent in OOP API surface |
| Text view margins (left/right/top/bottom) | ✅ | `ITextViewMarginProvider`, renderable with `RemoteUserControl` |
| Line classification / background tagger | ✅ | `ITaggerProvider<IClassificationTag>` — colours changed lines |
| Apply text edit programmatically | ✅ | `EditorExtensibility.EditAsync()` — fully async, atomic |
| Get active text view / document path | ✅ | `IClientContext.GetActiveTextViewAsync()` |

VsBridge is still ruled out (requires `net48`). **Phase 4 stays net8.**

## Accepted alternative: Chat-bubble diff + line tagger + `EditAsync`

Since intra-text adornments are unavailable, the diff UI lives in the chat panel (not floating inline in the editor). The editor gets visual feedback via line classification only.

### Rendering the diff

`AssistantTurnEvent` already carries full `ToolUseBlock` content including the `Edit` tool's `old_string` / `new_string`. Generate a unified diff string and display it in a dedicated `ChatMessage` bubble (monospace, line-coloured via `DataTrigger` on line prefix `+`/`-`).

### Accept flow

```csharp
// Accept button → apply the edit to the open document
await this.Extensibility.Editor().EditAsync(
    batch =>
    {
        var editor = document.AsEditable(batch);
        editor.Replace(matchedRange, newText);
    },
    cancellationToken);
```

`EditAsync` is atomic; if the document changed since the snapshot was taken, the SDK returns a conflict result and the extension retries on the newer snapshot.

### Line highlighting (optional, Phase 4.2+)

Register an `ITaggerProvider<IClassificationTag>` that marks added lines green and removed lines red while a pending edit is awaiting Accept/Reject. Clear the tags on Accept or Reject.

## Implications for Phase 4

1. **No VsBridge needed** — OOP is sufficient for the full Accept flow.
2. **Diff UI is in the chat panel** — not floating in the editor. This is a conscious trade-off vs. VS Code's inline diff; acceptable for Phase 4.
3. **`EditAsync` is the Accept mechanism** — file path + range from the `Edit` tool call; the extension locates the document via `IClientContext` or `WorkspacesExtensibility`.
4. **Line tagger** is a nice-to-have (Phase 4.2+); not required for the core Accept/Reject flow.

## Exit criteria (all passed 2026-04-27)

| # | Criterion | Result |
|---|---|---|
| 1 | Confirmed inline diff adornments not available in OOP SDK 17.14 | ✅ |
| 2 | Confirmed `EditAsync()` supports arbitrary text range replacement | ✅ |
| 3 | Confirmed text view margin + RemoteUserControl viable for diff UI | ✅ |
| 4 | Phase 4 implementation plan adjusted (diff in chat, not inline) | ✅ |
| 5 | VsBridge still not required; project stays on net8 | ✅ |
