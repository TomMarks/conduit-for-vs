namespace Conduit.Core;

/// <summary>
/// Strongly-typed wrapper for a Claude Code session identifier.
/// </summary>
/// <remarks>
/// The CLI emits a <c>session_id</c> in every <c>stream-json</c> event. Round-tripping
/// it via <c>--resume &lt;id&gt;</c> restores the agent's full context.
/// Wrapping it as a record struct prevents accidental mixing with other string IDs.
/// </remarks>
public readonly record struct SessionId(string Value)
{
    public static SessionId NewEphemeral() => new(Guid.NewGuid().ToString("N"));

    public override string ToString() => this.Value;
}
