using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Extensibility;

namespace Conduit;

/// <summary>
/// Conduit extension entry point.
/// </summary>
/// <remarks>
/// One <see cref="Extension"/>-derived class per VSIX. The <c>[VisualStudioContribution]</c>
/// attribute is what makes the build targets discover this type and emit metadata into
/// the generated <c>extension.json</c>.
/// </remarks>
[VisualStudioContribution]
internal sealed class ConduitExtension : Extension
{
    /// <inheritdoc />
    public override ExtensionConfiguration ExtensionConfiguration => new()
    {
        Metadata = new(
            id: "Conduit.ClaudeCode",
            version: this.ExtensionAssemblyVersion,
            publisherName: "Conduit",
            displayName: "Conduit for Claude Code",
            description: "Agentic coding inside Visual Studio. Wraps the Claude Code CLI in a native chat experience."
        )
        {
            // Forward-declare net10 so the bump is metadata-only when the SDK supports it.
            // See docs/spikes/SPIKE-000-tfm.md.
            DotnetTargetVersions =
            [
                DotnetTarget.Net8,
                DotnetTarget.Custom("net10.0"),
            ],
        },
    };

    /// <inheritdoc />
    protected override void InitializeServices(IServiceCollection serviceCollection)
    {
        base.InitializeServices(serviceCollection);

        // Phase 0 has no services to register. Future phases will register:
        //   - ICliHost                (Phase 2)
        //   - ISessionOrchestrator    (Phase 6)
        //   - IEditorService          (Phase 4)
        //   - IMcpConfigService       (Phase 7)
        // Tool windows and commands resolve dependencies from this container.
    }
}
