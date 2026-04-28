# SPIKE-007 — Workspace file enumeration for @-mentions

> Status: **closed**  •  Date: 2026-04-27  •  SDK: Microsoft.VisualStudio.Extensibility 17.14  •  Owner: project plan

## Question

How do we enumerate workspace files efficiently in an OOP extension to power `@file` autocomplete in the chat input? Is the solution root path accessible without VsBridge?

## Finding

**Full file enumeration is available via the Project Query API. Solution root and active document are also accessible. No VsBridge needed.**

| Capability | Available in OOP? | API |
|---|---|---|
| Enumerate all files in solution projects | ✅ | `WorkspacesExtensibility.QueryProjectsAsync()` |
| Get solution root path | ✅ | `WorkspacesExtensibility.QuerySolutionAsync()` → `solution.Path` |
| Get active/focused document path | ✅ | `IClientContext.GetActiveTextViewAsync()` |
| `IVsHierarchy` / DTE file enumeration | ❌ | VSSDK in-proc only — ruled out |

## Project Query API pattern

```csharp
// Enumerate all .cs files across all projects in the solution
WorkspacesExtensibility workspace = this.Extensibility.Workspaces();

IQueryResults<IFileSnapshot> files = await workspace.QueryProjectsAsync(
    project => project
        .Get(p => p.Files)
        .Where(f => f.Extension == ".cs")
        .With(f => new { f.Path, f.FileName }),
    cancellationToken);
```

For the full solution root path:

```csharp
IQueryResults<ISolutionSnapshot> solution = await workspace.QuerySolutionAsync(
    s => s.With(s => new { s.Path }),
    cancellationToken);
var solutionDir = Path.GetDirectoryName(solution.First().Path);
```

For the active document (single-file quick insert):

```csharp
// In a command or tool window handler with IClientContext
using ITextViewSnapshot textView = await context.GetActiveTextViewAsync(ct);
// textView.FilePath → absolute path of the focused editor document
```

## Recommended implementation for Phase 4.1

The `@` trigger in the chat `TextBox` cannot use `IClientContext` (that's only available in command handlers). Instead:

1. **Index on solution open** — register a background service that calls `QueryProjectsAsync` once when the solution loads and maintains an in-memory `List<string>` of relative file paths. Update incrementally on file-add/remove events.

2. **`@` detection in ViewModel** — watch `InputText` for a `@` character; when found, filter the file index and show a popup/dropdown via a `ShowFilePicker` bool DataMember + `ObservableList<FileEntry>`.

3. **Insertion** — on selection, replace the `@...` token in `InputText` with the chosen relative path.

4. **File system walk fallback** — if the project query index is empty (no solution open), walk `solutionDir` with `Directory.EnumerateFiles("**/*.cs")`. Fast and requires no VS API.

## Implications for Phase 4

1. **No VsBridge needed** — `WorkspacesExtensibility` covers all enumeration needs from OOP.
2. **`VisualStudioExtensibility` already flows into `ConduitToolWindowViewModel`** (wired in Phase 3.2) — the workspace API is available via `this.extensibility.Workspaces()` without further plumbing.
3. **File index service** — lightweight; store as a field on the ViewModel or inject as a scoped service registered in `ConduitExtension.InitializeServices`.
4. **`@` autocomplete UI** — a `ListBox` popup above the input bar, toggled by `ShowFilePicker`; no custom controls required (standard WPF `ListBox` + `DataTrigger`).

## Exit criteria (all passed 2026-04-27)

| # | Criterion | Result |
|---|---|---|
| 1 | Confirmed `QueryProjectsAsync` enumerates project files in OOP | ✅ |
| 2 | Confirmed solution root path accessible via `QuerySolutionAsync` | ✅ |
| 3 | Confirmed active document path accessible via `GetActiveTextViewAsync` | ✅ |
| 4 | Implementation approach (index on open + `@` trigger + popup) specified | ✅ |
| 5 | VsBridge not required; project stays on net8 | ✅ |
