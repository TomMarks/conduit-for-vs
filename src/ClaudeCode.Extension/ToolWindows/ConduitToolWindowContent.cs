using Microsoft.VisualStudio.Extensibility.UI;

namespace Conduit.ToolWindows;

/// <summary>
/// Remote UI control hosting the Phase 0 placeholder UI.
/// </summary>
/// <remarks>
/// The XAML file <c>ConduitToolWindowContent.xaml</c> sits next to this class and is
/// auto-discovered by the build targets. It is a single <c>DataTemplate</c> bound to
/// <see cref="ConduitToolWindowViewModel"/>; no code-behind is permitted by Remote UI.
/// </remarks>
internal sealed class ConduitToolWindowContent : RemoteUserControl
{
    public ConduitToolWindowContent()
        : base(dataContext: new ConduitToolWindowViewModel())
    {
    }
}
