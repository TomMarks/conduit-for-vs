using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.ToolWindows;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;

namespace Conduit.ToolWindows;

/// <summary>
/// Conduit's primary chat surface.
/// </summary>
[VisualStudioContribution]
internal sealed class ConduitToolWindow : ToolWindow
{
    private readonly VisualStudioExtensibility extensibility;

    // Content is created lazily in GetContentAsync so that this.extensibility
    // is fully initialised (DI complete) before it is passed down the chain.
    private ConduitToolWindowContent? content;

    public ConduitToolWindow(VisualStudioExtensibility extensibility)
    {
        this.extensibility = extensibility;
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
    {
        this.content ??= new ConduitToolWindowContent(this.extensibility);
        return Task.FromResult<IRemoteUserControl>(this.content);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.content?.Dispose();
        }

        base.Dispose(disposing);
    }
}
