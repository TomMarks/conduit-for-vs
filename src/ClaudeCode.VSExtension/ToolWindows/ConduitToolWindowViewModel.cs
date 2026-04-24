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
    private string diagnosticText = "Bridge: starting…";

    [DataMember]
    public string WebViewSource
    {
        get => this.webViewSource;
        set => this.SetProperty(ref this.webViewSource, value);
    }

    /// <summary>
    /// SPIKE-001 diagnostic — remove this property (and its binding in XAML) before Phase 1 ships.
    /// Shows bridge URL once the server is ready so the window being blank can be distinguished
    /// from the bridge not starting vs. WebView2 not rendering.
    /// </summary>
    [DataMember]
    public string DiagnosticText
    {
        get => this.diagnosticText;
        set => this.SetProperty(ref this.diagnosticText, value);
    }
}
