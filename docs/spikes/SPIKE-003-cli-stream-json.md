# SPIKE-003 — CLI stream-json event schema

> Status: **closed**  •  Date: 2026-04-24  •  CLI version: 2.1.91  •  Owner: project plan

## Question

What is the complete NDJSON event schema emitted by:
```
claude -p <prompt> --output-format stream-json --verbose [--include-partial-messages]
```

Can we build a typed C# parser over it that is safe to evolve, covers Phase 2's streaming needs, and has golden-file test coverage?

## Finding

**Yes — schema pinned, parser built, 12 tests green.**

## Command-line flags

| Flag | Effect |
|---|---|
| `--output-format stream-json` | Emit NDJSON instead of plain text. **Required.** |
| `--verbose` | Include `system/init`, `assistant`, `user`, and `result` envelope events. **Required** — without it the CLI errors. |
| `--include-partial-messages` | Also emit raw `stream_event` lines (Anthropic streaming API events). Needed for token-by-token text streaming. Optional for Phase 2. |
| `--resume <session_id>` | Resume a prior session. Session ID comes from `SystemInitEvent.SessionId`. |

## Top-level event taxonomy

Every NDJSON line has a `type` field. Observed values:

| `type` | Frequency | C# type | Purpose |
|---|---|---|---|
| `system` | Once, first | `SystemInitEvent` | Session start; model, cwd, session_id, tool list |
| `stream_event` | Many (with `--include-partial-messages`) | `TextTokenEvent` / `ToolUseStartEvent` / `UnknownEvent` | Raw Anthropic API stream events |
| `assistant` | Once per turn | `AssistantTurnEvent` | Full assembled message snapshot |
| `user` | After each tool call | `ToolResultEvent` | Tool execution result |
| `result` | Once, last | `SessionCompleteEvent` | Session complete; final text, cost, error flag |
| `rate_limit_event` | Occasional | `UnknownEvent` | Rate limit info; safe to ignore in UI |

## `stream_event` inner taxonomy

The `event.type` field within a `stream_event` line:

| `event.type` | Relevant? | Notes |
|---|---|---|
| `message_start` | No | API message envelope open |
| `content_block_start` | **Yes** (tool_use only) | When `content_block.type == "tool_use"` → `ToolUseStartEvent` |
| `content_block_delta` | **Yes** (text only) | When `delta.type == "text_delta"` → `TextTokenEvent` |
| `content_block_delta` | No (input_json) | `delta.type == "input_json_delta"` — tool input streaming; wait for `AssistantTurnEvent` |
| `content_block_delta` | No (thinking) | `delta.type == "thinking_delta"` / `signature_delta` — extended thinking internals |
| `content_block_stop` | No | Content block closed |
| `message_delta` | No | Stop reason / usage |
| `message_stop` | No | API message envelope close |

## Event schema (key fields)

### `system` → `SystemInitEvent`
```json
{
  "type": "system",
  "subtype": "init",
  "session_id": "...",
  "uuid": "...",
  "model": "claude-sonnet-4-6",
  "cwd": "/path/to/workspace",
  "claude_code_version": "2.1.91",
  "tools": ["Bash", "Edit", ...],
  "mcp_servers": []
}
```

### `stream_event` + `text_delta` → `TextTokenEvent`
```json
{
  "type": "stream_event",
  "event": {
    "type": "content_block_delta",
    "index": 0,
    "delta": { "type": "text_delta", "text": "Hello" }
  },
  "session_id": "...",
  "uuid": "..."
}
```

### `stream_event` + `content_block_start` (tool_use) → `ToolUseStartEvent`
```json
{
  "type": "stream_event",
  "event": {
    "type": "content_block_start",
    "index": 1,
    "content_block": {
      "type": "tool_use",
      "id": "toolu_...",
      "name": "Bash",
      "input": {},
      "caller": { "type": "direct" }
    }
  },
  "session_id": "...",
  "uuid": "..."
}
```

### `assistant` → `AssistantTurnEvent`
```json
{
  "type": "assistant",
  "message": {
    "model": "claude-sonnet-4-6",
    "id": "msg_...",
    "role": "assistant",
    "content": [
      { "type": "text", "text": "I'll list the files" },
      { "type": "tool_use", "id": "toolu_...", "name": "Bash", "input": { "command": "ls" } }
    ],
    "stop_reason": "tool_use",
    "usage": { "input_tokens": 3, "output_tokens": 42 }
  },
  "session_id": "...",
  "uuid": "..."
}
```

`stop_reason` values observed: `"end_turn"`, `"tool_use"`, `null` (mid-stream snapshots).

### `user` → `ToolResultEvent`
```json
{
  "type": "user",
  "message": {
    "role": "user",
    "content": [{
      "type": "tool_result",
      "tool_use_id": "toolu_...",
      "content": "file1.txt\nfile2.txt",
      "is_error": false
    }]
  },
  "timestamp": "2026-04-24T00:00:00.000Z",
  "tool_use_result": {
    "stdout": "file1.txt\nfile2.txt",
    "stderr": "",
    "interrupted": false,
    "isImage": false
  },
  "session_id": "...",
  "uuid": "..."
}
```

### `result` → `SessionCompleteEvent`
```json
{
  "type": "result",
  "subtype": "success",
  "is_error": false,
  "result": "Here are the files...",
  "stop_reason": "end_turn",
  "num_turns": 2,
  "total_cost_usd": 0.0005,
  "session_id": "...",
  "uuid": "..."
}
```

On error: `"subtype": "error"`, `"is_error": true`, `"result"` contains the error message.

## Parser design decisions

1. **`UnknownEvent` catch-all**: All unrecognised `type` values (and unrecognised `stream_event` subtypes) produce an `UnknownEvent` with the raw JSON preserved. This makes the parser safe to run against future CLI versions without crashing.

2. **`stream_event` filtering**: The parser only promotes two `stream_event` subtypes to first-class events (`TextTokenEvent`, `ToolUseStartEvent`). Everything else becomes `UnknownEvent`. Callers that only care about streaming text can filter for `TextTokenEvent` with a single `is` check.

3. **`AssistantTurnEvent` as the reliable truth**: For callers that don't need live streaming, filtering for `AssistantTurnEvent` where `StopReason == "end_turn"` gives the full, correct response text without any delta assembly logic.

4. **`TextReader` over `Stream`**: `ParseAsync` accepts a `TextReader` rather than a raw `Stream` to keep the API simple and testable (easy to wrap a `StringReader` in tests or a `StreamReader` in production).

5. **No source generators yet**: Reflection-based `JsonDocument.Parse` is used for simplicity. Phase 2 can add `[JsonSerializable]` source generators to the hot path if token throughput becomes a bottleneck.

## Artifacts

| File | Description |
|---|---|
| [`src/ClaudeCode.Cli/Events/CliEvent.cs`](../../src/ClaudeCode.Cli/Events/CliEvent.cs) | Discriminated union of all event types (sealed records) |
| [`src/ClaudeCode.Cli/StreamJsonParser.cs`](../../src/ClaudeCode.Cli/StreamJsonParser.cs) | `IAsyncEnumerable<CliEvent>` NDJSON parser |
| [`tests/fixtures/simple-text.ndjson`](../../tests/fixtures/simple-text.ndjson) | 3-event golden file: init → assistant → result |
| [`tests/fixtures/tool-use-with-streaming.ndjson`](../../tests/fixtures/tool-use-with-streaming.ndjson) | 23-event golden file: two-turn session with tool call + streaming deltas |
| [`tests/ClaudeCode.Cli.Tests/StreamJsonParserTests.cs`](../../tests/ClaudeCode.Cli.Tests/StreamJsonParserTests.cs) | 12 golden-file tests |

## Exit criteria (all passed 2026-04-24)

| # | Criterion | Result |
|---|---|---|
| 1 | `simple-text.ndjson` produces `SystemInitEvent`, `AssistantTurnEvent`, `SessionCompleteEvent` | ✅ |
| 2 | `SystemInitEvent` carries model and session_id | ✅ |
| 3 | `AssistantTurnEvent` has correct text and stop_reason | ✅ |
| 4 | `SessionCompleteEvent.IsError == false`, `NumTurns == 1` | ✅ |
| 5 | `tool-use-with-streaming.ndjson` produces `TextTokenEvent` instances | ✅ |
| 6 | Text tokens concatenate to expected string | ✅ |
| 7 | `ToolUseStartEvent` has correct name and ID | ✅ |
| 8 | `AssistantTurnEvent` with `stop_reason == "tool_use"` has a `ToolUseBlock` | ✅ |
| 9 | `ToolResultEvent` has correct content and tool use ID | ✅ |
| 10 | Two-turn session has `SessionCompleteEvent.NumTurns == 2` | ✅ |
| 11 | Unknown top-level type produces `UnknownEvent` | ✅ |
| 12 | Missing `session_id` field produces empty string (not null crash) | ✅ |

## Implications for Phase 2

1. **`ConduitToolWindowViewModel.ExecuteSendAsync`** replaces `StreamStubResponseAsync` with:
   - Spawn `claude -p --output-format stream-json --verbose --include-partial-messages`
   - Pipe stdout through `StreamJsonParser.ParseAsync`
   - `TextTokenEvent` → `reply.Text += token.Text`
   - `SessionCompleteEvent` → `reply.IsStreaming = false`, `CanSend = true`
   - `SessionCompleteEvent.IsError` → set `StatusText` to error message

2. **Session resumption**: Store `SystemInitEvent.SessionId` on the `ChatSession`; pass `--resume <id>` on subsequent turns.

3. **Tool calls in UI**: `ToolUseStartEvent` can show a "Running Bash..." indicator; `ToolResultEvent` can show collapsible output. Both are Phase 3 concerns.

## Recheck cadence

- Re-run live capture against the CLI on any CLI version bump to check for schema changes.
- `stream_event` subtypes not promoted to first-class events are preserved as `UnknownEvent` — if a new important subtype appears, add a new `CliEvent` subclass without breaking existing callers.
