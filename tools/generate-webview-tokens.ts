// generate-webview-tokens.ts
//
// Phase 1 deliverable. Not yet wired — the webview project doesn't exist until Phase 1.
//
// Reads docs/BRAND.md, parses the color/typography/spacing tables, emits:
//   src/ClaudeCode.Extension.Webview/src/tokens.css
//
// Contract:
//   - Idempotent. Same input → byte-identical output.
//   - Fails the build if a token is referenced in CSS but missing from BRAND.md.
//   - Fails the build if BRAND.md changed but the generator output didn't (CI guardrail).
//
// Wire-up plan:
//   1. Phase 1 creates src/ClaudeCode.Extension.Webview with a Vite config.
//   2. package.json adds "prebuild": "ts-node ../../tools/generate-webview-tokens.ts".
//   3. Vite then bundles tokens.css alongside the chat surface.
//
// Implementation sketch:
//   const md = await readFile("docs/BRAND.md", "utf8");
//   const colors = parseColorTables(md);  // walks the markdown tables under "## Color tokens"
//   const css = renderCssVariables(colors);
//   await writeFileIfChanged("src/ClaudeCode.Extension.Webview/src/tokens.css", css);
//
// DO NOT add token values here. BRAND.md is the source of truth.

throw new Error("generate-webview-tokens.ts is a Phase 1 deliverable — not yet implemented.");
