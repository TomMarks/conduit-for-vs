using System.Runtime.Serialization;
using Microsoft.VisualStudio.Extensibility.UI;

namespace Conduit.ToolWindows;

/// <summary>
/// Phase 1 / Spike-001 view model.
/// Exposes <see cref="WebViewSource"/> so Remote UI can bind it to the WebView2.Source
/// property via standard WPF TypeConverter (string → Uri).
/// </summary>
[DataContract]
internal sealed class ConduitToolWindowViewModel : NotifyPropertyChangedObject
{
    private string webViewSource = "about:blank";

    /// <summary>
    /// URL navigated by the WebView2 control.  Starts as about:blank and is updated to
    /// the bridge's HTTP origin once <see cref="ConduitToolWindowContent.ControlLoadedAsync"/>
    /// starts the <see cref="Conduit.Bridge.ConduitWebSocketBridge"/>.
    /// </summary>
    [DataMember]
    public string WebViewSource
    {
        get => webViewSource;
        set => SetProperty(ref webViewSource, value);
    }
}
