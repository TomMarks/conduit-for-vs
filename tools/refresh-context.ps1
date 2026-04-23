<#
.SYNOPSIS
    Refresh research context from upstream sources into docs/context/.

.DESCRIPTION
    Implements PROJECT_PLAN §1.2. Walks the source list, fetches each via
    Claude Code in headless mode, writes a dated snapshot, and appends a
    rolling delta entry to docs/context/CHANGELOG.md.

    Requires the `claude` CLI on PATH and a logged-in account.

.EXAMPLE
    pwsh tools/refresh-context.ps1
    pwsh tools/refresh-context.ps1 -Since 2026-04-01

.NOTES
    Sources defined inline below. Add to $sources to track another upstream.
#>

[CmdletBinding()]
param(
    [string] $Since = (Get-Date).AddDays(-7).ToString("yyyy-MM-dd"),
    [string] $OutDir = (Join-Path $PSScriptRoot ".." "docs" "context")
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command claude -ErrorAction SilentlyContinue)) {
    throw "Claude Code CLI not found on PATH. Install: https://docs.claude.com/en/docs/claude-code/setup"
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$sources = @(
    @{ Slug = "vsx-sdk-docs";        Url = "https://learn.microsoft.com/en-us/visualstudio/extensibility/visualstudio.extensibility/" }
    @{ Slug = "vsx-github";          Url = "https://github.com/microsoft/VSExtensibility" }
    @{ Slug = "vsx-issue-544";       Url = "https://github.com/microsoft/VSExtensibility/issues/544" }
    @{ Slug = "vsx-devblog";         Url = "https://devblogs.microsoft.com/visualstudio/tag/visualstudio-extensibility/" }
    @{ Slug = "vsx-remote-ui";       Url = "https://learn.microsoft.com/en-us/visualstudio/extensibility/visualstudio.extensibility/inside-the-sdk/remote-ui" }
    @{ Slug = "claude-vscode-docs";  Url = "https://code.claude.com/docs/en/vs-code" }
    @{ Slug = "claude-headless-cli"; Url = "https://code.claude.com/docs/en/headless" }
    @{ Slug = "claude-releases";     Url = "https://github.com/anthropics/claude-code/releases" }
)

$today = (Get-Date).ToString("yyyy-MM-dd")
$changelog = Join-Path $OutDir "CHANGELOG.md"
$entry = "## $today`n`n"

foreach ($src in $sources) {
    $outFile = Join-Path $OutDir "$($src.Slug)-$today.md"
    Write-Host "→ $($src.Slug)" -ForegroundColor Cyan

    $prompt = @"
Visit $($src.Url). Summarize what changed since $Since that's relevant to a
.NET developer building a Visual Studio 2026 extension that wraps the Claude
Code CLI. Focus on: API additions/breakage, new platform limitations, runtime
version requirements, and CLI flag changes. Output strictly markdown — no
preamble, no closing remark.
"@

    $result = $prompt | claude -p --bare --output-format text 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Failed: $($src.Slug). Skipping."
        continue
    }

    $result | Set-Content -Path $outFile -Encoding UTF8
    $entry += "- ``$($src.Slug)`` → [snapshot](./$($src.Slug)-$today.md)`n"
}

$entry += "`n"
Add-Content -Path $changelog -Value $entry -Encoding UTF8

Write-Host "Done. New entry appended to $changelog" -ForegroundColor Green
