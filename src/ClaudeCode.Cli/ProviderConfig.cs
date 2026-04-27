namespace Conduit.Cli;

/// <summary>
/// The Claude API provider to use for a CLI invocation.
/// Maps to the string values stored in the VS settings provider dropdown.
/// </summary>
public enum CliProvider
{
    /// <summary>Anthropic API (default). Uses <c>ANTHROPIC_API_KEY</c>.</summary>
    Anthropic,

    /// <summary>AWS Bedrock. Sets <c>CLAUDE_CODE_USE_BEDROCK=1</c>.</summary>
    AwsBedrock,

    /// <summary>Google Vertex AI. Sets <c>CLAUDE_CODE_USE_VERTEX=1</c>.</summary>
    GoogleVertex,

    /// <summary>
    /// Azure AI. Requires <c>ANTHROPIC_API_URL</c> to be set in the system environment
    /// pointing at the Azure endpoint — no extra env vars are injected automatically.
    /// </summary>
    Azure,
}

/// <summary>
/// Provider configuration passed to <see cref="CliProcessHost.RunAsync"/>.
/// Encapsulates the environment variables the Claude CLI requires for each provider.
/// </summary>
public sealed record ProviderConfig(CliProvider Provider)
{
    /// <summary>Default configuration: Anthropic API, no extra env vars.</summary>
    public static readonly ProviderConfig Default = new(CliProvider.Anthropic);

    /// <summary>
    /// Creates a <see cref="ProviderConfig"/> from the string value stored in
    /// the VS settings dropdown (e.g. <c>"bedrock"</c>).
    /// </summary>
    public static ProviderConfig FromSettingValue(string? value) => value switch
    {
        "bedrock" => new ProviderConfig(CliProvider.AwsBedrock),
        "vertex"  => new ProviderConfig(CliProvider.GoogleVertex),
        "azure"   => new ProviderConfig(CliProvider.Azure),
        _         => Default,
    };

    /// <summary>
    /// Environment variables to inject into the <c>claude</c> CLI process.
    /// These supplement (rather than replace) the parent process environment.
    /// </summary>
    public IReadOnlyDictionary<string, string> ExtraEnvironment => this.Provider switch
    {
        CliProvider.AwsBedrock   => new Dictionary<string, string> { ["CLAUDE_CODE_USE_BEDROCK"] = "1" },
        CliProvider.GoogleVertex => new Dictionary<string, string> { ["CLAUDE_CODE_USE_VERTEX"]  = "1" },
        // Azure and Anthropic: rely entirely on environment variables the user has already set.
        _ => new Dictionary<string, string>(),
    };
}
