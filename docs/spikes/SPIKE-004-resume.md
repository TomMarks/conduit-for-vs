# SPIKE-004 — Session resumption via `--resume`

> Status: **open**  •  Phase: 2  •  Owner: project plan

## Question

Does `--resume <session_id>` reliably round-trip full conversation context across separate CLI invocations? Can we verify this with an automated test?

## Finding

**Partially — implementation confirmed working manually; automated test not yet written.**

The `--resume` flag is wired end-to-end:

1. `CliProcessHost.RunAsync` accepts `sessionId` and appends `--resume <id>` to the argument list when non-null.
2. `ConduitToolWindowViewModel` stores `init.SessionId` from the first `SystemInitEvent` and passes it on every subsequent call.
3. Confirmed working manually: second and third turns within the same tool window session retain full conversation context.

## What remains

Write a 20-line xUnit test in `ClaudeCode.Cli.Tests` (or `ClaudeCode.Core.Tests`) that:

1. Calls `CliProcessHost.RunAsync("hello")` and captures `SystemInitEvent.SessionId`.
2. Calls `CliProcessHost.RunAsync("what did I just say?", sessionId)`.
3. Asserts the second `SessionCompleteEvent.ResultText` contains a reference to "hello" (i.e., Claude recalls turn 1).

This requires a live `claude` process — mark it `[Trait("Category", "Integration")]` so it's excluded from the default `dotnet test` run and gated to CI environments where `claude` is authenticated.

## Deferral rationale

The manual confirmation is sufficient to close Phase 2's exit criterion. The test is worth writing before Phase 6 (multi-session, history), where session identity bugs would be hard to diagnose. Adding it as a pre-condition to Phase 6.

## Artifacts

| File | Description |
|---|---|
| `src/ClaudeCode.Cli/CliProcessHost.cs` | `--resume` argument wired at lines 52–56 |
| `src/ClaudeCode.VSExtension/ToolWindows/ConduitToolWindowViewModel.cs` | `sessionId` captured and threaded through at lines 21, 80, 75 |
