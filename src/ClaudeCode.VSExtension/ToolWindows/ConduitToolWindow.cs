using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.ToolWindows;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;

namespace Conduit.ToolWindows;

/// <summary>
/// Conduit's primary chat surface — Phase 0 shows a static greeting; Phase 1 embeds WebView2.
/// </summary>
[VisualStudioContribution]
internal sealed class ConduitToolWindow : ToolWindow
{
    private readonly ConduitToolWindowContent content = new();

    public ConduitToolWindow()
    {
        this.Title = "Conduit";
    }

    /// <inheritdoc />
    public override ToolWindowConfiguration ToolWindowConfiguration => new()
    {
        // Default placement: docked right of the editor — mirrors VS Code's secondary sidebar idiom.
        Placement = ToolWindowPlacement.DocumentWell,
        DockDirection = Dock.Right,
        AllowAutoCreation = true,
    };

    /// <inheritdoc />
    public override Task<IRemoteUserControl> GetContentAsync(CancellationToken cancellationToken)
        => Task.FromResult<IRemoteUserControl>(this.content);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.content.Dispose();
        }

        base.Dispose(disposing);
    }
}
