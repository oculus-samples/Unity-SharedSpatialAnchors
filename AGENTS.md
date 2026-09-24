# Agent Instructions — Unity Shared Spatial Anchors

A Unity sample demonstrating the multiplayer-oriented Shared Spatial Anchors APIs (`OVRSpatialAnchor`). Covers both the legacy user-based sharing flow (via Photon PUN2) and the newer Group Sharing + Colocation Discovery flow (PUN-free).

## Source-of-truth files (read these first, do not duplicate their contents in this file)

For setup, build steps, SDK versions, and project layout, read:

- `README.md` — sample overview, supported devices, setup table
- `Documentation/Setup.md` — step-by-step setup
- `Documentation/Glossary.md` — precise terms ("saved anchor", "serialized anchor", "colocated", etc.); prefer quoting verbatim
- `ProjectSettings/ProjectVersion.txt` — Unity editor version
- `Packages/manifest.json` — Unity package versions
- `.gitattributes` — Git LFS configuration
- `LICENSE` — license terms

## Quest / Horizon-specific notes

- Every key API callsite is preceded by a `// KEY API CALL` comment. Use `git grep -nFA1 '// KEY API CALL' -- '*.cs'` to enumerate them — agents should rely on these markers when locating relevant SDK calls rather than searching by API name.
- In the `ColocationSessionGroups` scene, the hardcoded Group UUID defaults to `Application.buildGuid`, so two devices on different builds will silently fail to colocate. When debugging "nothing shows up", confirm both headsets are running the exact same APK before chasing code bugs.
- Shared anchor features work best on Standalone APK builds. Quest Link is technically supported but the multiplayer/colocation aspects are intrinsically limited in tethered setups — do not assume PC Link parity when reproducing bugs.
- This repo is PUN2-only on the legacy path (no Photon Voice) — do not confuse with `oculus-samples/Unity-SharedSpaces`.

# Meta Quest tooling

This is a Meta Quest / Horizon OS sample. The bespoke intro above is the source of truth for what this project is and how it's built — use it (and the files it points at) instead of restating facts from memory.

When the user asks anything about Quest device behavior, build / deploy / debug / capture flows, on-device performance, or Horizon OS APIs, reach for these tools instead of generic Unity answers:

- **`hzdb`** — Quest-aware ADB wrapper (device list, install / launch / stop, logs, screenshots, Perfetto traces, on-device docs search). Already wired up as an MCP server via `.mcp.json`, `.vscode/mcp.json`, and `.cursor/mcp.json`. Also runnable directly: `npx -y @meta-quest/hzdb <subcommand>`.
- **Meta Quest Agentic Tools** — the full skill set, including Unity-specific skills: <https://github.com/meta-quest/agentic-tools>. Install per your client (Claude Code: `/plugin install meta-vr@meta-quest`; Gemini CLI: `gemini extensions install https://github.com/meta-quest/agentic-tools`; Cursor / VS Code: install the **Meta Horizon** extension from the Marketplace).

A few behavior expectations:

- **Read this repo's files first.** Before answering anything project-specific, read `README.md` and whichever source-of-truth files the intro above points at. Don't restate their contents in chat — quote or link instead.
- **Use `hzdb` for device-side work.** Anything that touches an attached Quest (install, launch, logs, screenshot, capture, manifest inspection) goes through `hzdb`, not raw `adb`.
- **Check live Horizon OS docs before answering API questions.** `hzdb docs search "..."` queries the live docs; training data on Horizon OS APIs goes stale fast.
- **Don't fabricate SDK / engine versions.** If a version isn't visible in this repo's files, say so rather than guessing.
