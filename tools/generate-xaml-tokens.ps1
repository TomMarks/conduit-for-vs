<#
.SYNOPSIS
    Generates Remote UI XAML brush resources from docs/BRAND.md.

.DESCRIPTION
    Phase 1 deliverable. Reads the color tables in BRAND.md and emits:
        src/ClaudeCode.Extension.UI/Themes/ConduitBrushes.xaml

    Output is a ResourceDictionary of SolidColorBrush entries keyed by token
    name, importable from Remote UI XAML via:

        <ResourceDictionary Source="/Conduit.Extension.UI;component/Themes/ConduitBrushes.xaml" />

.NOTES
    Idempotent. Fails the build if BRAND.md changed but the generator output
    didn't (CI guardrail). DO NOT add token values here — BRAND.md is the
    source of truth.

    Wire-up plan:
      1. Phase 1 promotes ToolWindows/* into a separate Conduit.Extension.UI project.
      2. Conduit.Extension.UI.csproj adds a BeforeBuild target invoking this script.
      3. Phase 0's inline hex codes in ConduitToolWindowContent.xaml get replaced
         with {DynamicResource Conduit.Background} etc.
#>

[CmdletBinding()]
param(
    [string] $BrandFile = (Join-Path $PSScriptRoot ".." "docs" "BRAND.md"),
    [string] $OutputFile = (Join-Path $PSScriptRoot ".." "src" "ClaudeCode.Extension.UI" "Themes" "ConduitBrushes.xaml")
)

throw "generate-xaml-tokens.ps1 is a Phase 1 deliverable — not yet implemented."
