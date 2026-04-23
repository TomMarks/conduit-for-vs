using System.Diagnostics;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Commands;
using Microsoft.VisualStudio.Extensibility.Shell;
using Conduit.ToolWindows;

namespace Conduit.Commands;

/// <summary>
/// Opens (or activates) the Conduit chat tool window.
/// </summary>
/// <remarks>
/// Available from <c>View → Other Windows → Conduit</c> and from the command palette
/// as "Conduit: Open chat window". Maps to <c>Ctrl+Alt+C, Ctrl+Alt+C</c> by default.
/// </remarks>
[VisualStudioContribution]
internal sealed class OpenConduitCommand : Command
{
    private readonly TraceSource logger;

    public OpenConduitCommand(TraceSource logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc />
    public override CommandConfiguration CommandConfiguration => new("%Conduit.OpenConduit.DisplayName%")
    {
        // Show in View → Other Windows. Could add an Activity Bar / status bar entry in Phase 8.
        Placements =
        [
            CommandPlacement.KnownPlacements.ViewOtherWindowsMenu,
        ],
        Icon = new(ImageMoniker.KnownValues.OfficeWebExtension, IconSettings.IconAndText),
        Shortcuts =
        [
            new CommandShortcutConfiguration(ModifierKey.ControlAlt, Key.C, ModifierKey.ControlAlt, Key.C),
        ],
    };

    /// <inheritdoc />
    public override async Task ExecuteCommandAsync(IClientContext context, CancellationToken cancellationToken)
    {
        this.logger.TraceInformation("Opening Conduit tool window.");
        await this.Extensibility.Shell().ShowToolWindowAsync<ConduitToolWindow>(activate: true, cancellationToken);
    }
}
