# SPIKE-005 — Terminal handoff from OOP extension

> Status: **closed**  •  Date: 2026-04-24  •  SDK: Microsoft.VisualStudio.Extensibility 17.14  •  Owner: project plan

## Question

What is the best way to invoke the VS integrated terminal and run `claude /login` from an OOP (out-of-process) VS.Extensibility extension?

## Finding

**No terminal API exists in the VS.Extensibility SDK. Use `Process.Start` with `UseShellExecute = true`.**

Neither a first-class terminal API nor a general VS command execution mechanism is available to OOP extensions:

| Mechanism | Available? | Evidence |
|---|---|---|
| `ITerminalService` / `.Terminal()` on `VisualStudioExtensibility` | ❌ | Not in SDK 17.14. Feature request open as [VSExtensibility#560](https://github.com/microsoft/VSExtensibility/issues/560). |
| Invoke VSCT commands (`View.Terminal`, etc.) from OOP | ❌ | Explicitly rejected by VS team — VSCT commands are synchronous; OOP extensions are async. [VSExtensibility#329](https://github.com/microsoft/VSExtensibility/issues/329), [#153](https://github.com/microsoft/VSExtensibility/issues/153). |
| VsBridge (net48 VSSDK in-proc) | ❌ | Ruled out project-wide — requires `net48`; project targets `net8.0`. |
| `Process.Start` with `UseShellExecute = true` | ✅ | Extension runs in its own net8 process; spawning child processes is fully allowed. |

## Implementation design

```csharp
/// <summary>
/// Launches <c>claude /login</c> in an external console window.
/// Tries Windows Terminal first; falls back to cmd.exe.
/// </summary>
internal static class ClaudeAuthLauncher
{
    public static bool LaunchLogin()
    {
        // Prefer Windows Terminal — cleaner UX, supports mouse/scroll
        if (TryLaunch("wt.exe", "claude /login")) return true;

        // Fall back to a classic cmd.exe window; & pause keeps it open
        // so the user can read the output before the window auto-closes.
        if (TryLaunch("cmd.exe", "/k claude /login")) return true;

        return false; // neither found — caller shows manual instructions
    }

    private static bool TryLaunch(string exe, string args)
    {
        try
        {
            Process.Start(new ProcessStartInfo(exe, args)
            {
                UseShellExecute = true,   // run via ShellExecuteEx in user desktop session
                CreateNoWindow  = false,  // must be false to get a visible console
            });
            return true;
        }
        catch (Win32Exception)
        {
            return false; // exe not on PATH; try next option
        }
    }
}
```

**Why `UseShellExecute = true`:** The extension host (ServiceHub) process has a constrained environment. `UseShellExecute = true` spawns the child via `ShellExecuteEx` in the user's full desktop session, ensuring the correct PATH, environment variables (`CLAUDE_API_KEY`, etc.), and console allocation are inherited.

**Why `/k` on `cmd.exe`:** Keeps the window open after `claude /login` completes so the user can read any confirmation or error output. (Use `/c ... & pause` as an alternative if `/k` leaves an interactive shell prompt behind.)

## UX flow

Before launching, show a `Shell().ShowPromptAsync` dialog:

> **Sign in to Claude**  
> A terminal window will open to authenticate with the Claude CLI. After signing in, return here and send your first message.  
> [OK] [Cancel]

After the window closes, the next send attempt will re-probe auth. No polling needed.

## Auth detection

To detect the unauthenticated state, attempt a minimal probe invocation before the first real send:

```
claude -p "" --output-format stream-json --verbose
```

If `SessionCompleteEvent.IsError == true` and `ResultText` contains "not logged in" / "authentication" / "API key", surface the sign-in prompt. Otherwise proceed normally.

Alternatively, watch for the same error pattern in the normal send path — if the first `SessionCompleteEvent` after any prompt is an auth error, show the sign-in prompt reactively.

## Implications for Phase 3

1. **No VsBridge needed** — `Process.Start` is sufficient. Phase 3 stays fully net8.
2. **`ClaudeAuthLauncher`** — new static class in `ClaudeCode.Cli` or `ClaudeCode.Core`.
3. **Auth probe** — `CliProcessHost` or a dedicated `AuthProbe` helper; check `SessionCompleteEvent.IsError` + result text.
4. **ViewModel change** — `ConduitToolWindowViewModel.ExecuteSendAsync` calls the probe on first send; if auth failure detected, shows dialog and launches login instead of surfacing a raw error message.
5. **Provider settings** — separate concern; no spike needed. VS.Extensibility `ISettingsManager` / options page is well-documented.

## Exit criteria (all passed 2026-04-24)

| # | Criterion | Result |
|---|---|---|
| 1 | Confirmed no terminal API in VS.Extensibility SDK 17.14 | ✅ |
| 2 | Confirmed VSCT command invocation not available from OOP | ✅ |
| 3 | `Process.Start(UseShellExecute=true)` identified as correct mechanism | ✅ |
| 4 | wt.exe → cmd.exe fallback chain specified | ✅ |
| 5 | UX flow (dialog before launch) specified | ✅ |
| 6 | Auth detection approach specified | ✅ |
| 7 | Phase 3 stays net8, no VsBridge required | ✅ |
