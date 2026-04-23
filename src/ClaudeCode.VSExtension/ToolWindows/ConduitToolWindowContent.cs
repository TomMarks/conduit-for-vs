using Conduit.Bridge;
using Microsoft.VisualStudio.Extensibility.UI;

namespace Conduit.ToolWindows;

/// <summary>
/// Remote UI control that hosts the Conduit chat surface.
///
/// The XAML embeds a WebView2 control (runs in devenv.exe; the assembly is available
/// because VS ships it for Copilot).  Two-way communication uses a local WebSocket
/// server (<see cref="ConduitWebSocketBridge"/>) because Remote UI has no code-behind
/// and CoreWebView2.WebMessageReceived is therefore unreachable from the OOP host.
///
/// See docs/spikes/SPIKE-001-webview2-in-remote-ui.md for the full analysis.
/// </summary>
internal sealed class ConduitToolWindowContent : RemoteUserControl
{
    private readonly ConduitToolWindowViewModel viewModel;
    private ConduitWebSocketBridge? bridge;

    private ConduitToolWindowContent(ConduitToolWindowViewModel vm)
        : base(dataContext: vm)
    {
        viewModel = vm;
    }

    public ConduitToolWindowContent()
        : this(new ConduitToolWindowViewModel())
    {
    }

    /// <inheritdoc />
    public override async Task ControlLoadedAsync(CancellationToken cancellationToken)
    {
        await base.ControlLoadedAsync(cancellationToken);

        bridge = new ConduitWebSocketBridge();
        bridge.Start();

        // Update the binding; devenv proxy picks up the INotifyPropertyChanged notification
        // and the WebView2 navigates to the chat page.
        viewModel.WebViewSource = bridge.SourceUrl;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            bridge?.Dispose();
            bridge = null;
        }

        base.Dispose(disposing);
    }
}
