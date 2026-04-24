using Conduit.Cli;
using Conduit.Cli.Events;
using FluentAssertions;
using Xunit;

namespace Conduit.Cli.Tests;

public sealed class StreamJsonParserTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    // ── simple-text fixture ────────────────────────────────────────────────

    [Fact]
    public async Task SimpleText_ProducesExpectedEventSequence()
    {
        var events = await ParseFixtureAsync("simple-text.ndjson");

        events.Should().HaveCount(3);
        events[0].Should().BeOfType<SystemInitEvent>();
        events[1].Should().BeOfType<AssistantTurnEvent>();
        events[2].Should().BeOfType<SessionCompleteEvent>();
    }

    [Fact]
    public async Task SimpleText_SystemInit_CapturesModelAndSessionId()
    {
        var events = await ParseFixtureAsync("simple-text.ndjson");
        var init = events.OfType<SystemInitEvent>().Single();

        init.Model.Should().Be("claude-sonnet-4-6");
        init.SessionId.Should().Be("aaaaaaaa-0000-0000-0000-000000000001");
    }

    [Fact]
    public async Task SimpleText_AssistantTurn_HasTextContent()
    {
        var events = await ParseFixtureAsync("simple-text.ndjson");
        var turn = events.OfType<AssistantTurnEvent>().Single();

        turn.StopReason.Should().Be("end_turn");
        var text = turn.Content.OfType<TextBlock>().Single();
        text.Text.Should().Be("hello");
    }

    [Fact]
    public async Task SimpleText_SessionComplete_IsSuccess()
    {
        var events = await ParseFixtureAsync("simple-text.ndjson");
        var complete = events.OfType<SessionCompleteEvent>().Single();

        complete.IsError.Should().BeFalse();
        complete.ResultText.Should().Be("hello");
        complete.NumTurns.Should().Be(1);
    }

    // ── tool-use-with-streaming fixture ───────────────────────────────────

    [Fact]
    public async Task ToolUse_ContainsTextTokenEvents()
    {
        var events = await ParseFixtureAsync("tool-use-with-streaming.ndjson");

        events.OfType<TextTokenEvent>().Should().NotBeEmpty();
    }

    [Fact]
    public async Task ToolUse_TextTokens_ConcatenateToExpectedText()
    {
        var events = await ParseFixtureAsync("tool-use-with-streaming.ndjson");

        // First text turn: "I'll list the files" (2 tokens across 2 deltas)
        var firstTurnTokens = events
            .OfType<TextTokenEvent>()
            .TakeWhile(e => e.Text != "Here are")   // stop before second turn
            .ToList();

        var assembled = string.Concat(firstTurnTokens.Select(e => e.Text));
        assembled.Should().Be("I'll list the files");
    }

    [Fact]
    public async Task ToolUse_ContainsToolUseStartEvent()
    {
        var events = await ParseFixtureAsync("tool-use-with-streaming.ndjson");
        var toolStart = events.OfType<ToolUseStartEvent>().Single();

        toolStart.ToolName.Should().Be("Bash");
        toolStart.ToolUseId.Should().Be("toolu_00000000000000000001");
    }

    [Fact]
    public async Task ToolUse_AssistantTurnWithToolUse_HasToolUseBlock()
    {
        var events = await ParseFixtureAsync("tool-use-with-streaming.ndjson");

        var toolUseTurn = events
            .OfType<AssistantTurnEvent>()
            .First(t => t.StopReason == "tool_use");

        var toolBlock = toolUseTurn.Content.OfType<ToolUseBlock>().Single();
        toolBlock.Name.Should().Be("Bash");
        toolBlock.InputJson.Should().Contain("ls -la");
    }

    [Fact]
    public async Task ToolUse_ContainsToolResultEvent()
    {
        var events = await ParseFixtureAsync("tool-use-with-streaming.ndjson");
        var result = events.OfType<ToolResultEvent>().Single();

        result.ToolUseId.Should().Be("toolu_00000000000000000001");
        result.Content.Should().Contain("file1.txt");
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task ToolUse_SessionComplete_IsTwoTurns()
    {
        var events = await ParseFixtureAsync("tool-use-with-streaming.ndjson");
        var complete = events.OfType<SessionCompleteEvent>().Single();

        complete.IsError.Should().BeFalse();
        complete.NumTurns.Should().Be(2);
        complete.ResultText.Should().Contain("file1.txt");
    }

    // ── single-line parsing ───────────────────────────────────────────────

    [Fact]
    public void ParseLine_UnknownType_ReturnsUnknownEvent()
    {
        const string json = """{"type":"rate_limit_event","session_id":"s1","uuid":"u1"}""";
        var evt = StreamJsonParser.ParseLine(json);

        evt.Should().BeOfType<UnknownEvent>()
            .Which.EventType.Should().Be("rate_limit_event");
    }

    [Fact]
    public void ParseLine_BlankSessionId_ReturnsEmptyString()
    {
        const string json = """{"type":"result","subtype":"success","is_error":false,"result":"ok","num_turns":1,"total_cost_usd":0.001}""";
        var evt = StreamJsonParser.ParseLine(json);

        evt.SessionId.Should().Be(string.Empty);
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private static async Task<IReadOnlyList<CliEvent>> ParseFixtureAsync(string fileName)
    {
        var path = FixturePath(fileName);
        await using var file = File.OpenRead(path);
        using var reader = new StreamReader(file);
        var events = new List<CliEvent>();
        await foreach (var evt in StreamJsonParser.ParseAsync(reader))
        {
            events.Add(evt);
        }

        return events;
    }
}
