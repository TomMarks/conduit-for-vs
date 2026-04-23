# BRAND.md — Conduit design tokens

> Single source of truth. The webview (`src/ClaudeCode.Extension.Webview`) generates its CSS variables from this file. The Remote UI XAML (`src/ClaudeCode.Extension.UI`) generates its `SolidColorBrush` resources from this file. Do not redefine colors anywhere else.

## Identity

| | |
|---|---|
| Name | **Conduit** |
| Tagline | Agentic coding inside Visual Studio |
| Marketplace title | Conduit for Claude Code |
| Icon concept | B — signal waveform with a single warm signal pulse |

## Color tokens

### Surfaces

| Token | Hex | Usage |
|---|---|---|
| `bg` | `#0E1116` | Tool window root background |
| `surface` | `#161B22` | Cards, message backgrounds |
| `elevated` | `#1F252E` | Hover, focused message, popovers |
| `border` | `#30363D` | All dividers and outlines |
| `border-subtle` | `#21262D` | Inline rules, separators |

### Text

| Token | Hex | Usage |
|---|---|---|
| `text-primary` | `#E6EDF3` | Default body |
| `text-secondary` | `#8B949E` | Labels, muted captions |
| `text-tertiary` | `#6E7681` | Timestamps, hints |
| `text-on-accent` | `#0E1116` | Text on `accent-primary` fills |

### Accents

| Token | Hex | Usage |
|---|---|---|
| `accent-primary` | `#2DD4BF` | Brand color — primary buttons, focus rings, brand glyph stroke |
| `accent-primary-hover` | `#5EEAD4` | Primary button hover |
| `accent-primary-muted` | `#134E4A` | Subtle fills (selection, badge bg with `accent-primary` text) |
| `accent-signal` | `#FB7185` | Warm pop — active session dot, tool-call attention, brand glyph node |
| `accent-signal-muted` | `#7F1D1D` | (rarely) signal fill backgrounds |

### Semantic

| Token | Hex | Usage |
|---|---|---|
| `success` | `#3FB950` | Approved edits, completed tasks |
| `warning` | `#D29922` | Plan-mode pending, throttle warnings |
| `danger` | `#F85149` | Errors, rejected edits, rate-limit |
| `info` | `#58A6FF` | Informational toasts |

### Diff colors (chat inline diffs)

| Token | Hex | Usage |
|---|---|---|
| `diff-add-bg` | `#0F3923` | Added line background |
| `diff-add-text` | `#7EE787` | Added line text |
| `diff-del-bg` | `#3D1414` | Removed line background |
| `diff-del-text` | `#FFA198` | Removed line text |

## Typography

| Role | Family | Notes |
|---|---|---|
| UI | `Segoe UI Variable, Segoe UI, system-ui, sans-serif` | Ships with Windows 11 / VS2026; no fetch |
| Code | `'Cascadia Code', 'Consolas', ui-monospace, monospace` | VS default; supports ligatures |
| Numeric tabular | as UI, `font-variant-numeric: tabular-nums` | For token counts, line numbers |

Sizes: 13px UI default · 12px secondary · 14px chat input · 13px code in chat (slightly down-tuned for density).

## Spacing

4 / 8 / 12 / 16 / 24 / 32 px. Avoid arbitrary values.

## Radii

| Token | Value | Usage |
|---|---|---|
| `radius-sm` | 4px | Buttons, inputs |
| `radius-md` | 6px | Cards, message bubbles |
| `radius-lg` | 10px | Modals, the chat composer |

## Motion

- All transitions: `120ms ease-out`. No spring physics, no stagger choreography.
- Streaming text: append-only, no per-token fade. Cursor blinks at `1Hz`.
- New message: 80ms opacity 0 → 1, no translate.

## Iconography

- **Primary mark**: signal waveform — flat midline that spikes once into a node, asymmetric (the spike sits ~70% across).
  - Stroke: `accent-primary` at 2.5px on a 48-unit grid.
  - Node: `accent-signal` filled circle, radius 2.5.
  - On 16px chrome icons: simplify to 3 segments, no node.
- **Secondary glyphs**: monochrome strokes from `text-secondary`, 1.5px on a 16-unit grid. Avoid filled icons except for state indicators.

## Theming integration

The user has VS theme switching. Conduit ships **dark-only at v1** (deliberate scope cut — most Claude Code users run dark). For light-theme parity in v2:
- Define a parallel light token set in this file.
- Webview reads `prefers-color-scheme` plus an explicit setting `conduit.theme: auto | dark | light`.
- Remote UI XAML brushes bind through a `ThemeService` that subscribes to `IVsUIShell5.SubscribeToColorThemeUpdates`-equivalent OOP API.

## Generation

Two generators consume this file at build time (Phase 1 deliverable):

1. `tools/generate-webview-tokens.ts` → `src/ClaudeCode.Extension.Webview/src/tokens.css`
2. `tools/generate-xaml-tokens.ps1` → `src/ClaudeCode.Extension.UI/Themes/ConduitBrushes.xaml`

If you change a token, **change it here**, then `dotnet build` regenerates both. There is no other place to edit a color.
