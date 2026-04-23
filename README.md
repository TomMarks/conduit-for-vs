# Conduit

> Agentic coding inside Visual Studio. A VS2026 extension that brings Claude Code's agent loop, plan mode, inline diffs, and MCP integration into a native chat surface — without leaving the IDE.

**Status**: Phase 0 — scaffold smoke test.
**Target**: Visual Studio 2026 (Community/Pro/Enterprise).
**Model**: VisualStudio.Extensibility (out-of-process), .NET 8 LTS pinned.

---

## Repository layout

```
Conduit/
├── Conduit.slnx                                  ← VS2026 solution
├── Directory.Build.props                         ← shared MSBuild defaults
├── Directory.Packages.props                      ← centralized package versions
├── global.json                                   ← .NET 8 SDK pin
├── .editorconfig
├── .gitignore  /  .gitattributes
├── NuGet.config
│
├── src/
│   ├── ClaudeCode.Extension/                     ← VSIX entry, tool window, commands
│   │   ├── ConduitExtension.cs                   ← Extension metadata + DI composition
│   │   ├── Commands/OpenConduitCommand.cs        ← View → Other Windows → Conduit
│   │   ├── ToolWindows/
│   │   │   ├── ConduitToolWindow.cs              ← ToolWindow declaration
│   │   │   ├── ConduitToolWindowContent.cs       ← RemoteUserControl
│   │   │   ├── ConduitToolWindowContent.xaml     ← single-file XAML, MVVM, no code-behind
│   │   │   └── ConduitToolWindowViewModel.cs     ← [DataContract] view model
│   │   └── string-resources.json                 ← localizable strings
│   └── ClaudeCode.Core/                          ← domain primitives (SessionId, etc.)
│
├── tools/
│   ├── refresh-context.ps1                       ← snapshot upstream docs (PROJECT_PLAN §1.2)
│   ├── generate-webview-tokens.ts                ← Phase 1 — BRAND.md → webview tokens.css
│   └── generate-xaml-tokens.ps1                  ← Phase 1 — BRAND.md → ConduitBrushes.xaml
│
├── docs/
│   ├── PROJECT_PLAN.md                           ← architecture, phases, parity matrix
│   ├── BRAND.md                                  ← design tokens (single source of truth)
│   └── spikes/
│       └── SPIKE-000-tfm.md                      ← TFM verification (closed)
│
└── .github/workflows/build.yml                   ← CI
```

Projects from the architecture diagram that aren't yet present (`ClaudeCode.Cli`, `ClaudeCode.Editor`, `ClaudeCode.Mcp`, `ClaudeCode.Extension.UI`, `ClaudeCode.Extension.Webview`, `ClaudeCode.VsBridge`) are added in their respective phases — see `docs/PROJECT_PLAN.md`.

---

## Prerequisites

| | Version | Notes |
|---|---|---|
| Visual Studio 2026 | Community, Pro, or Enterprise | Dec 2025 update or newer |
| Workload: **Visual Studio extension development** | — | Installer → Modify → Workloads |
| .NET 8 SDK | 8.0.413 or newer | `winget install Microsoft.DotNet.SDK.8` |
| (Optional) Mads Kristensen's **Extensibility Essentials 2022** | latest | Useful for VSSDK-side scaffolding only — Phase 0 doesn't need it |

> **Why .NET 8 and not .NET 10?** See `docs/spikes/SPIKE-000-tfm.md`. The VisualStudio.Extensibility SDK doesn't yet host on .NET 10 ([VSExtensibility#544](https://github.com/microsoft/VSExtensibility/issues/544)). When that issue closes we flip the TFM in one place.

---

## Phase 0 — get a tool window on screen

1. **Clone**.
   ```pwsh
   git clone <repo-url> conduit
   cd conduit
   ```

2. **Verify the .NET SDK pin resolves**.
   ```pwsh
   dotnet --version
   # → 8.0.413  (or whatever rollForward latestFeature gives you)
   ```

3. **Restore + build from CLI** (sanity check before opening VS).
   ```pwsh
   dotnet restore Conduit.slnx
   dotnet build Conduit.slnx -c Debug
   ```
   First build pulls the `Microsoft.VisualStudio.Extensibility.*` packages. Pinned version is in `Directory.Packages.props` — bump deliberately, not casually.

4. **Open the solution in VS2026**.
   ```
   File → Open → Project/Solution → Conduit.slnx
   ```

5. **Set `ClaudeCode.Extension` as the startup project**, then `F5`.
   The Experimental Instance launches with Conduit hot-loaded.

6. **Open the tool window**.
   - `View → Other Windows → Conduit: Open chat window`, **or**
   - `Ctrl+Q`, type "Conduit", **or**
   - default keybinding `Ctrl+Alt+C, Ctrl+Alt+C`.

   You should see a deep-slate panel with the Conduit signal-waveform mark, the tagline, and the Phase 0 placeholder text.

**Phase 0 exit**: ✅ that panel renders.

---

## Hot-loading

OOP extensions hot-load — when you rebuild in the main IDE, the Experimental Instance picks up the new assembly without a restart. This is one of the largest UX wins of the new model. Don't introduce static state that requires a restart, or you'll lose it.

---

## What Phase 0 deliberately doesn't include

- WebView2-hosted chat surface → **Phase 1**
- Claude CLI subprocess → **Phase 2**
- Auth / provider selection → **Phase 3**
- @-mentions / inline diffs → **Phase 4**
- Plan mode / permissions / auto-accept → **Phase 5**
- Multi-session / tabs / history → **Phase 6**
- MCP / slash commands / plugins → **Phase 7**
- Status bar, theming generators, accessibility → **Phases 8–9**
- Marketplace packaging → **Phase 10**

See `docs/PROJECT_PLAN.md` §4 for full phase definitions, exit criteria, and risk spikes.

---

## Conventions

- **Branding**: `docs/BRAND.md` is the single source of truth for colors and typography. In Phase 0 the XAML inlines hex values; from Phase 1 they're sourced from generated brush dictionaries.
- **Centralized package management**: every NuGet version lives in `Directory.Packages.props`. Project files reference packages without versions.
- **TFM**: `net8.0-windows10.0.22621.0` for Windows-surface projects, `net8.0` for pure libraries. Do not bump without re-running `SPIKE-000`.
- **Code style**: `Nullable=enable`, file-scoped namespaces, `TreatWarningsAsErrors=true` outside test projects. See `.editorconfig`.

## License

TBD.
