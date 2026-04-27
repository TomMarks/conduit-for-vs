#pragma warning disable VSEXTPREVIEW_SETTINGS  // Settings API is preview in SDK 17.14

using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Settings;

namespace Conduit.Settings;

/// <summary>
/// Defines the Conduit settings that appear under Tools &gt; Options &gt; Conduit.
///
/// The Settings API is currently in preview (<c>VSEXTPREVIEW_SETTINGS</c>).
/// See https://learn.microsoft.com/en-us/visualstudio/extensibility/visualstudio.extensibility/settings/about-settings
/// </summary>
internal static class ConduitSettings
{
    /// <summary>
    /// The top-level category that groups all Conduit settings in Tools &gt; Options.
    /// </summary>
    [VisualStudioContribution]
    internal static SettingCategory General { get; } = new(
        id: "conduit",
        displayName: "%Conduit.Settings.Category%");

    /// <summary>
    /// Provider selection dropdown.  The string value matches a <c>CliProvider</c>
    /// key (e.g. <c>"anthropic"</c>, <c>"bedrock"</c>) and is converted via
    /// <see cref="Conduit.Cli.ProviderConfig.FromSettingValue"/>.
    /// </summary>
    [VisualStudioContribution]
    internal static Setting.Enum Provider { get; } = new(
        id: "conduitprovider",
        displayName: "%Conduit.Settings.Provider%",
        parentCategory: General,
        values:
        [
            new EnumSettingEntry("anthropic", "%Conduit.Settings.Provider.Anthropic%"),
            new EnumSettingEntry("bedrock",   "%Conduit.Settings.Provider.Bedrock%"),
            new EnumSettingEntry("vertex",    "%Conduit.Settings.Provider.Vertex%"),
            new EnumSettingEntry("azure",     "%Conduit.Settings.Provider.Azure%"),
        ],
        defaultValue: "anthropic")
    {
        Description = "%Conduit.Settings.Provider.Description%",
    };
}
