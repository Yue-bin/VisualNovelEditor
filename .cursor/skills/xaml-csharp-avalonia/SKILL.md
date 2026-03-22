---
name: xaml-csharp-avalonia
description: Builds, reviews, migrates, and optimizes Avalonia apps with modern XAML/C#, compiled bindings, AOT-friendly patterns, Fluent/pro design tokens, and platform/bootstrap guidance. Use for Avalonia .axaml, AppBuilder/lifetime, bindings, styles/themes, controls, performance, trimming/NativeAOT, HTML/WPF/WinForms/WinUI-to-Avalonia migration, or Avalonia 12 migration; load upstream reference docs from this skill’s upstream folder when present.
---

# XAML / C# for Avalonia (upstream: wieslawsoltes/xaml-csharp-development-skill-for-avalonia)

Produce reflection-minimized, maintainable Avalonia apps for desktop, browser, and mobile.

## Upstream reference root

Full curated docs and generated API indexes ship in the GitHub repo, not in this stub. After a one-time clone, read files under:

` .cursor/skills/xaml-csharp-avalonia/upstream/ `

(relative to the workspace root containing `.cursor`).

**Bootstrap (run once from workspace root):**

```bash
git clone https://github.com/wieslawsoltes/xaml-csharp-development-skill-for-avalonia.git .cursor/skills/xaml-csharp-avalonia/upstream
```

If `upstream/` is missing, clone before deep work, or open files from  
https://github.com/wieslawsoltes/xaml-csharp-development-skill-for-avalonia  
(raw URLs work for single-file reads).

**Pinned versions (per upstream README):** default guidance targets Avalonia **11.3.12**; Avalonia **12** lane uses **12.0.0-rc1** artifacts and `references/68-avalonia-12-migration-guide.md` — only apply 12-specific APIs when the task explicitly targets that lane.

**Start here after clone:**

- `upstream/references/compendium.md` — table of contents / task navigation  
- `upstream/references/00-api-map.md` — API map  
- `upstream/references/api-index-generated.md` — broad signature index (11.x pin)

## Workflow (load only what the task needs)

1. **Scope & lifetime** — `upstream/references/00-api-map.md`, `01-architecture-and-lifetimes.md` (pick `IClassicDesktopStyleApplicationLifetime` vs `ISingleViewApplicationLifetime`; optional `IActivatableLifetime`).

2. **Bootstrap & build** — `05-platforms-and-bootstrap.md`, `06-msbuild-aot-and-tooling.md`, `41-xaml-compiler-and-build-pipeline.md`, storage/clipboard/launcher/screens refs (`29`–`33`), `42`/`44` for runtime XAML.

3. **Bindings & commands** — `02-bindings-xaml-aot.md`, `45`–`47`, `49`–`50`, `03-reactive-threading.md`, `24-commands-hotkeys-and-gestures.md`. Prefer compiled bindings + `x:DataType`.

4. **Views & input** — `11`, `38`–`40`, `51`, `18`–`19`, `34`, `57`–`58`.

5. **Styles, themes, design** — `04`, `16`–`17`, `28`, `43`, `35`, `66` + `professional-design/`, `67` + `fluent-design/`.

6. **Controls & shell** — `10`, `13`, `36`, `48`, `52`–`56`, `25`.

7. **Layout & items** — `30`, `20`–`21`, `57`.

8. **Animation & rendering** — `12`, `14`–`15`, `37`, `59`, `61` (interop) only if needed.

9. **Validation & a11y** — `22`–`23`, `60`.

10. **Tests & perf** — `26`–`27`, `08`, `07`, `09` for examples.

11. **Migrations** — `62`+`html-to-avalonia/`, `63`+`winforms-to-avalonia/`, `64`+`wpf-to-avalonia/`, `65`+`winui-to-avalonia/`.

12. **Avalonia 12** — `68`, `69`, `api-index-12.0.0-rc1-generated.md`; cross-check official docs linked from `68`.

## Execution rules

- Prefer compiled XAML and compiled bindings in production; default `x:DataType` where applicable.  
- XAML-first; code-only UI only if the user asks.  
- Treat trim/AOT-unfriendly APIs as explicit tradeoffs (`RequiresUnreferencedCode` / `RequiresDynamicCode`).  
- Use `AppBuilder.With(...)` and platform option types instead of ad-hoc globals.  
- Separate startup wiring, views, view-model/reactive state, and styling/resources.

## Regenerating API indexes (optional)

See `upstream/README.md` and `upstream/scripts/generate_api_index.py` when upgrading Avalonia or verifying API drift.
