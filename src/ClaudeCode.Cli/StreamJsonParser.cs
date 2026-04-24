using System.Runtime.CompilerServices;
using System.Text.Json;
using Conduit.Cli.Events;

namespace Conduit.Cli;

/// <summary>
/// Parses the NDJSON event stream produced by:
/// <c>claude -p ... --output-format stream-json --verbose [--include-partial-messages]</c>
///
/// Each newline-delimited JSON object is mapped to a <see cref="CliEvent"/> subclass.
/// Unknown event types are surfaced as <see cref="UnknownEvent"/> for forward-compat.
///
/// See docs/spikes/SPIKE-003-cli-stream-json.md for the full schema reference.
/// </summary>
public static class StreamJsonParser
{
    /// <summary>
    /// Reads NDJSON lines from <paramref name="source"/> and yields typed events.
    /// Blank lines are silently skipped.
    /// </summary>
    public static async IAsyncEnumerable<CliEvent> ParseAsync(
        TextReader source,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        string? line;
        while ((line = await source.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var evt = ParseLine(line);
            yield return evt;
        }
    }

    /// <summary>Parse a single NDJSON line to a typed event.</summary>
    public static CliEvent ParseLine(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var type = root.TryGetStringProperty("type") ?? string.Empty;
        var sessionId = root.TryGetStringProperty("session_id") ?? string.Empty;
        var uuid = root.TryGetStringProperty("uuid") ?? string.Empty;

        return type switch
        {
            "system" => ParseSystemEvent(root, sessionId, uuid),
            "stream_event" => ParseStreamEvent(root, sessionId, uuid, json),
            "assistant" => ParseAssistantEvent(root, sessionId, uuid),
            "user" => ParseUserEvent(root, sessionId, uuid),
            "result" => ParseResultEvent(root, sessionId, uuid),
            _ => new UnknownEvent(sessionId, uuid, type, json),
        };
    }

    private static SystemInitEvent ParseSystemEvent(JsonElement root, string sessionId, string uuid)
    {
        return new SystemInitEvent(
            sessionId,
            uuid,
            root.TryGetStringProperty("model") ?? string.Empty,
            root.TryGetStringProperty("cwd") ?? string.Empty,
            root.TryGetStringProperty("claude_code_version") ?? string.Empty);
    }

    private static CliEvent ParseStreamEvent(
        JsonElement root, string sessionId, string uuid, string rawJson)
    {
        if (!root.TryGetProperty("event", out var evt))
        {
            return new UnknownEvent(sessionId, uuid, "stream_event", rawJson);
        }

        var eventType = evt.TryGetStringProperty("type") ?? string.Empty;

        // Text token — hot path for streaming UI updates.
        if (eventType == "content_block_delta" &&
            evt.TryGetProperty("delta", out var delta) &&
            delta.TryGetStringProperty("type") == "text_delta")
        {
            return new TextTokenEvent(
                sessionId,
                uuid,
                delta.TryGetStringProperty("text") ?? string.Empty);
        }

        // Tool use start — tells the UI a tool call has begun.
        if (eventType == "content_block_start" &&
            evt.TryGetProperty("content_block", out var block) &&
            block.TryGetStringProperty("type") == "tool_use")
        {
            return new ToolUseStartEvent(
                sessionId,
                uuid,
                block.TryGetStringProperty("id") ?? string.Empty,
                block.TryGetStringProperty("name") ?? string.Empty);
        }

        // All other stream_event subtypes (message_start, message_delta, message_stop,
        // content_block_stop, input_json_delta, thinking_delta, signature_delta …)
        // are internal scaffolding that the UI does not need to handle directly.
        return new UnknownEvent(sessionId, uuid, $"stream_event/{eventType}", rawJson);
    }

    private static CliEvent ParseAssistantEvent(
        JsonElement root, string sessionId, string uuid)
    {
        if (!root.TryGetProperty("message", out var msg))
        {
            return new UnknownEvent(sessionId, uuid, "assistant", root.GetRawText());
        }

        var messageId = msg.TryGetStringProperty("id") ?? string.Empty;
        var model = msg.TryGetStringProperty("model") ?? string.Empty;
        var stopReason = msg.TryGetStringProperty("stop_reason");

        var content = new List<ContentBlock>();
        if (msg.TryGetProperty("content", out var contentArray))
        {
            foreach (var item in contentArray.EnumerateArray())
            {
                var blockType = item.TryGetStringProperty("type") ?? string.Empty;
                ContentBlock block = blockType switch
                {
                    "text" => new TextBlock(item.TryGetStringProperty("text") ?? string.Empty),
                    "tool_use" => new ToolUseBlock(
                        item.TryGetStringProperty("id") ?? string.Empty,
                        item.TryGetStringProperty("name") ?? string.Empty,
                        item.TryGetProperty("input", out var input)
                            ? input.GetRawText()
                            : "{}"),
                    "thinking" => new ThinkingBlock(
                        item.TryGetStringProperty("thinking") ?? string.Empty),
                    _ => new TextBlock(string.Empty),
                };
                content.Add(block);
            }
        }

        return new AssistantTurnEvent(sessionId, uuid, messageId, model, stopReason, content);
    }

    private static CliEvent ParseUserEvent(
        JsonElement root, string sessionId, string uuid)
    {
        var timestamp = root.TryGetStringProperty("timestamp");

        if (!root.TryGetProperty("message", out var msg) ||
            !msg.TryGetProperty("content", out var content) ||
            content.ValueKind != JsonValueKind.Array)
        {
            return new UnknownEvent(sessionId, uuid, "user", root.GetRawText());
        }

        // The user event wraps tool results as an array. Take the first tool_result entry.
        foreach (var item in content.EnumerateArray())
        {
            if (item.TryGetStringProperty("type") == "tool_result")
            {
                var toolUseId = item.TryGetStringProperty("tool_use_id") ?? string.Empty;
                var isError = item.TryGetProperty("is_error", out var errProp) &&
                              errProp.ValueKind == JsonValueKind.True;
                var resultContent = item.TryGetProperty("content", out var c)
                    ? c.ValueKind == JsonValueKind.String ? c.GetString() ?? string.Empty : c.GetRawText()
                    : string.Empty;

                return new ToolResultEvent(
                    sessionId, uuid, toolUseId, resultContent, isError, timestamp);
            }
        }

        return new UnknownEvent(sessionId, uuid, "user", root.GetRawText());
    }

    private static SessionCompleteEvent ParseResultEvent(
        JsonElement root, string sessionId, string uuid)
    {
        var isError = root.TryGetProperty("is_error", out var e) &&
                      e.ValueKind == JsonValueKind.True;
        var subtype = root.TryGetStringProperty("subtype");
        var result = root.TryGetStringProperty("result") ?? string.Empty;
        var stopReason = root.TryGetStringProperty("stop_reason");
        var numTurns = root.TryGetProperty("num_turns", out var nt) ? nt.GetInt32() : 0;
        var cost = root.TryGetProperty("total_cost_usd", out var c) ? c.GetDouble() : 0.0;

        return new SessionCompleteEvent(
            sessionId, uuid, isError, subtype, result, cost, numTurns, stopReason);
    }
}

/// <summary>Extension helpers for null-safe JSON property access.</summary>
internal static class JsonElementExtensions
{
    internal static string? TryGetStringProperty(this JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }
}
