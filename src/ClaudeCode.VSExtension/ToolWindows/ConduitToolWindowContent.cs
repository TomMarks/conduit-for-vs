using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.UI;

namespace Conduit.ToolWindows;

/// <summary>
/// Remote UI control that hosts the Conduit chat surface.
/// All chat logic lives in <see cref="ConduitToolWindowViewModel"/>; this class is
/// just the glue that hands the VM to the Remote UI infrastructure.
/// </summary>
internal sealed class ConduitToolWindowContent : RemoteUserControl
{
    public ConduitToolWindowContent(VisualStudioExtensibility extensibility)
        : base(dataContext: new ConduitToolWindowViewModel(extensibility))
    {
    }
}
