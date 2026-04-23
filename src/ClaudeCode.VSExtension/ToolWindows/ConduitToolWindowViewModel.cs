using System.Runtime.Serialization;

namespace Conduit.ToolWindows;

/// <summary>
/// Phase 0 view model. Remote UI requires <see cref="DataContractAttribute"/>.
/// </summary>
[DataContract]
internal sealed class ConduitToolWindowViewModel
{
    [DataMember]
    public string Tagline { get; init; } = "Agentic coding inside Visual Studio";

    [DataMember]
    public string Greeting { get; init; } = "Hello from Conduit.";

    [DataMember]
    public string PhaseLabel { get; init; } = "Phase 0 · scaffold smoke test";
}
