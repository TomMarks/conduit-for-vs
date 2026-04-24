namespace Conduit.Cli.Events;

/// <summary>
/// Discriminated union of every event the Claude CLI emits in
/// <c>--output-format stream-json --verbose</c> mode.
///
/// Top-level <c>type</c> field determines the subclass:
///
/// | type            | subclass                  | when                                    |
/// |-----------------|---------------------------|-----------------------------------------|
/// | system          | <see cref="SystemInitEvent"/>    | First line; one per session      |
/// | stream_event    | <see cref="TextTokenEvent"/>     | content_block_delta / text_delta |
/// |                 | <see cref="ToolUseStartEvent"/>  | content_block_start / tool_use   |
/// |                 | <see cref="UnknownEvent"/>       | all other stream_event subtypes  |
/// | assistant       | <see cref="AssistantTurnEvent"/> | Full assembled turn snapshot     |
/// | user            | <see cref="ToolResultEvent"/>    | Tool execution result            |
/// | result          | <see cref="SessionCompleteEvent"/>| Session finished (success/error)|
/// | rate_limit_event| <see cref="UnknownEvent"/>       | Rate-limit info; ignore for UI  |
/// | (other)         | <see cref="UnknownEvent"/>       | Forward-compat catch-all         |
/// </summary>
public abstract record CliEvent(string SessionId, string Uuid);

/// <summary>First event in every session. Carries model and session ID.</summary>
public sealed record SystemInitEvent(
    string SessionId,
    string Uuid,
    string Model,
    string Cwd,
    string ClaudeCodeVersion)
    : CliEvent(SessionId, Uuid);

/// <summary>
/// A text token delta from the assistant.  Hot path for streaming UI updates.
/// Emitted for each <c>content_block_delta</c> where <c>delta.type == "text_delta"</c>.
/// </summary>
public sealed record TextTokenEvent(
    string SessionId,
    string Uuid,
    string Text)
    : CliEvent(SessionId, Uuid);

/// <summary>
/// The assistant has started calling a tool.
/// Emitted when a <c>content_block_start</c> event has <c>content_block.type == "tool_use"</c>.
/// The tool input accumulates via subsequent <c>input_json_delta</c> events (not individually
/// surfaced — wait for the paired <see cref="AssistantTurnEvent"/> for the full input).
/// </summary>
public sealed record ToolUseStartEvent(
    string SessionId,
    string Uuid,
    string ToolUseId,
    string ToolName)
    : CliEvent(SessionId, Uuid);

/// <summary>
/// A fully assembled assistant message snapshot.  Emitted after each turn (both text-only
/// and tool-use turns).  Use <see cref="StopReason"/> to distinguish:
/// <list type="bullet">
/// <item><c>"end_turn"</c> — assistant finished; no more events until the next user message.</item>
/// <item><c>"tool_use"</c> — tool call in progress; a <see cref="ToolResultEvent"/> follows.</item>
/// </list>
/// </summary>
public sealed record AssistantTurnEvent(
    string SessionId,
    string Uuid,
    string MessageId,
    string Model,
    string? StopReason,
    IReadOnlyList<ContentBlock> Content)
    : CliEvent(SessionId, Uuid);

/// <summary>A single block of content within an assistant message.</summary>
public abstract record ContentBlock(string Type);

/// <summary>A span of assistant text (may be partial during streaming).</summary>
public sealed record TextBlock(string Text) : ContentBlock("text");

/// <summary>A tool call the assistant is making.</summary>
public sealed record ToolUseBlock(string Id, string Name, string InputJson) : ContentBlock("tool_use");

/// <summary>Extended thinking (present when extended thinking is enabled; safe to ignore for UI).</summary>
public sealed record ThinkingBlock(string Thinking) : ContentBlock("thinking");

/// <summary>
/// A tool execution result — the CLI ran the tool and is feeding back the output.
/// The <see cref="ToolUseId"/> links this back to the originating <see cref="ToolUseStartEvent"/>.
/// </summary>
public sealed record ToolResultEvent(
    string SessionId,
    string Uuid,
    string ToolUseId,
    string Content,
    bool IsError,
    string? Timestamp)
    : CliEvent(SessionId, Uuid);

/// <summary>
/// The session has finished — either successfully or with an error.
/// <see cref="ResultText"/> contains the final assistant response text.
/// </summary>
public sealed record SessionCompleteEvent(
    string SessionId,
    string Uuid,
    bool IsError,
    string? ErrorSubtype,
    string ResultText,
    double CostUsd,
    int NumTurns,
    string? StopReason)
    : CliEvent(SessionId, Uuid);

/// <summary>
/// Catch-all for any event type this parser does not specifically handle.
/// Preserved as raw JSON so callers can log or inspect forward-compat events.
/// </summary>
public sealed record UnknownEvent(
    string SessionId,
    string Uuid,
    string EventType,
    string RawJson)
    : CliEvent(SessionId, Uuid);
