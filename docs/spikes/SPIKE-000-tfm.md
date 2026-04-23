# SPIKE-000 — TFM verification

> Status: **closed**  •  Date: 2026-04-22  •  Owner: project plan

## Question

Can the **Conduit** extension target `net10.0-windows` against the current `Microsoft.VisualStudio.Extensibility.Sdk` on VS2026?

## Finding

**No — not as of April 2026.** TFM must be `net8.0-windows10.0.22621.0`. Use `net10.0` only via the metadata declaration, not as the actual `<TargetFramework>`.

## Evidence

[microsoft/VSExtensibility#544](https://github.com/microsoft/VSExtensibility/issues/544) (opened Dec 19, 2025, **still open** as of this spike, VS2026 Community Dec 2025 update):

- `<TargetFramework>net10.0-windows10.0.22621.0</TargetFramework>` → command does not run, regardless of `ExtensionMetaData.DotnetTargetVersions` value.
- `<TargetFramework>net8.0-windows10.0.22621.0</TargetFramework>` → works. `DotnetTargetVersions = [DotnetTarget.Custom("net10.0")]` is accepted as metadata but the actual build must be net8.

VisualStudio.Extensibility runtime tracks .NET LTS by design (per [MS devblogs, May 2025](https://devblogs.microsoft.com/visualstudio/visualstudio-extensibility-editor-classification-and-updates-to-user-prompt/)). Even though .NET 10 is now the latest LTS (Nov 2025 → Nov 2028), the SDK's host runtime hasn't moved yet.

## Decision

| | Value |
|---|---|
| Project TFM | `net8.0-windows10.0.22621.0` |
| Metadata `DotnetTargetVersions` | `[ DotnetTarget.Net8, DotnetTarget.Custom("net10.0") ]` (forward-declare) |
| C# `<LangVersion>` | `latest` (gets C# 12 features on net8) |
| Bump trigger | Issue #544 closes **or** .NET 8 LTS sunset window opens (Nov 2026) — whichever comes first |

## Implications for the plan

1. **`PROJECT_PLAN.md` updated** — TFM table changed from `net10.0-windows` to `net8.0-windows10.0.22621.0`, with a forward-bump SLA tied to issue #544.
2. **.NET 8 LTS ends Nov 2026.** That's ~7 months from project start. Add **SPIKE-100-runtime-bump** as a Q3 2026 ticket, not "as needed."
3. **C# 14 features (primary constructors on records, field keyword, etc.) are out of reach** until the SDK moves. Where we'd reach for them, document the workaround in code comments so the bump is mechanical.
4. **No impact on architecture** — out-of-process model is unchanged; only the runtime version is pinned.

## Reference csproj snippet

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows10.0.22621.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <UseWPF>true</UseWPF>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.VisualStudio.Extensibility.Sdk" Version="*" />
    <PackageReference Include="Microsoft.VisualStudio.Extensibility.Build" Version="*" />
  </ItemGroup>
</Project>
```

```csharp
[VisualStudioContribution]
internal sealed class ConduitExtension : Extension
{
    public override ExtensionConfiguration ExtensionConfiguration => new()
    {
        Metadata = new(
            id: "Conduit.ClaudeCode",
            version: this.ExtensionAssemblyVersion,
            publisherName: "TBD",
            displayName: "Conduit for Claude Code",
            description: "Agentic coding inside Visual Studio."
        )
        {
            // Forward-declare net10 so we can flip the TargetFramework cleanly when #544 closes.
            DotnetTargetVersions = [ DotnetTarget.Net8, DotnetTarget.Custom("net10.0") ],
        },
    };
}
```

## Recheck cadence

- Watch [microsoft/VSExtensibility releases](https://github.com/microsoft/VSExtensibility/releases) and issue #544 weekly.
- Re-run this spike when either signals a fix.
