using System.Diagnostics;
using System.Runtime.CompilerServices;
using Conduit.Cli.Events;

namespace Conduit.Cli;

/// <summary>
/// Spawns a <c>claude</c> CLI subprocess and streams its NDJSON output as typed events.
///
/// Each call starts a new OS process.  Session context is preserved across calls via
/// <paramref name="sessionId"/> (passed as <c>--resume</c>), which the caller obtains
/// from the first <see cref="SystemInitEvent"/> emitted by a prior call.
/// </summary>
public static class CliProcessHost
{
    /// <summary>
    /// Runs <c>claude -p &lt;prompt&gt; --output-format stream-json --verbose
    /// --include-partial-messages [--resume &lt;sessionId&gt;]</c> and yields
    /// typed events until the process exits or <paramref name="ct"/> is cancelled.
    /// </summary>
    /// <param name="prompt">The user prompt text to send.</param>
    /// <param name="sessionId">
    /// Session ID from a prior <see cref="SystemInitEvent"/>.
    /// Pass <see langword="null"/> to start a fresh session.
    /// </param>
    /// <param name="ct">
    /// Cancellation kills the subprocess immediately (best-effort, entire process tree).
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the <c>claude</c> executable cannot be found or fails to start.
    /// </exception>
    public static async IAsyncEnumerable<CliEvent> RunAsync(
        string prompt,
        string? sessionId = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo("claude")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add(prompt);
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("stream-json");
        psi.ArgumentList.Add("--verbose");
        psi.ArgumentList.Add("--include-partial-messages");

        if (sessionId is not null)
        {
            psi.ArgumentList.Add("--resume");
            psi.ArgumentList.Add(sessionId);
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException(
                "Failed to start the claude CLI. " +
                "Ensure 'claude' is installed and on the system PATH.");

        // Kill the process tree if the caller cancels.
        using var killReg = ct.Register(() =>
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort — process may have already exited.
            }
        });

        await foreach (var evt in StreamJsonParser.ParseAsync(process.StandardOutput, ct))
        {
            yield return evt;
        }

        await process.WaitForExitAsync(ct);
    }
}
