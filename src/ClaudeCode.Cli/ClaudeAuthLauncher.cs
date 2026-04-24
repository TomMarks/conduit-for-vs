using System.ComponentModel;
using System.Diagnostics;

namespace Conduit.Cli;

/// <summary>
/// Launches <c>claude /login</c> in an external console window so the user can
/// complete the browser-based OAuth flow.
///
/// The VS.Extensibility SDK (17.14) provides no terminal API and cannot invoke VSCT
/// commands (e.g. <c>View.Terminal</c>) from an OOP extension.  The solution is
/// <see cref="Process.Start"/> with <see cref="ProcessStartInfo.UseShellExecute"/> =
/// <see langword="true"/>, which spawns via <c>ShellExecuteEx</c> in the user's full
/// desktop session — ensuring the correct PATH and environment variables are inherited
/// rather than the constrained ServiceHub host environment.
///
/// See <c>docs/spikes/SPIKE-005-terminal-handoff.md</c> for the full investigation.
/// </summary>
public static class ClaudeAuthLauncher
{
    /// <summary>
    /// Opens a console window running <c>claude /login</c>.
    /// Tries Windows Terminal (<c>wt.exe</c>) first; falls back to <c>cmd.exe</c>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if a console window was successfully launched;
    /// <see langword="false"/> if neither <c>wt.exe</c> nor <c>cmd.exe</c> could be found.
    /// </returns>
    public static bool LaunchLogin()
    {
        // Prefer Windows Terminal — better UX: colour, scroll, mouse support.
        if (TryLaunch("wt.exe", "claude /login"))
        {
            return true;
        }

        // Classic cmd.exe fallback.  /k runs the command then leaves an interactive
        // shell open so the user can read any output before closing the window.
        if (TryLaunch("cmd.exe", "/k claude /login"))
        {
            return true;
        }

        return false;
    }

    private static bool TryLaunch(string exe, string args)
    {
        try
        {
            Process.Start(new ProcessStartInfo(exe, args)
            {
                UseShellExecute = true,
                CreateNoWindow = false,
            });
            return true;
        }
        catch (Win32Exception)
        {
            // Executable not found on PATH; try the next option.
            return false;
        }
    }
}
