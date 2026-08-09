# CyberVeil Agent Guide

## Purpose

This file defines durable repository expectations for coding agents working on CyberVeil. Follow the user's current request first; use these rules for project context, implementation choices, validation, and handoff.

## Project identity

- CyberVeil is a cyber-medieval third-person hack-and-slash game built in Unity.
- Preserve its core identity: responsive melee combat, deliberate heavy attacks, Veil Surge, enemy waves, trial curses, portal upgrades, neon corruption, and readable combat feedback.
- Prefer additions that deepen the existing combat, encounter, progression, and presentation systems over unrelated generic mechanics.
- Treat game feel and clarity as functional requirements. Player actions and dangerous enemy attacks should have readable animation, audio, VFX, camera, UI, and timing feedback.
- Do not silently decide major balance, progression, narrative, control, or visual-direction questions when the intended design cannot be inferred. Present the concrete tradeoff and ask.

## Supported environment

- Use Unity `6000.0.26f1`, as recorded in `ProjectSettings/ProjectVersion.txt`.
- The project uses Universal Render Pipeline 17, Shader Graph, Visual Effect Graph, AI Navigation, the Unity Input System, and selected legacy-input integrations.
- Do not upgrade Unity, packages, render pipelines, input systems, or project-wide settings unless the task explicitly requires it.
- Treat `Packages/manifest.json`, `Packages/packages-lock.json`, and `ProjectSettings/` changes as project-wide changes. Inspect and explain them separately from gameplay work.

## Authoritative scenes

The enabled build flow in `ProjectSettings/EditorBuildSettings.asset` is:

1. `Assets/Scenes/CyberVeil_HomeScreen.unity`
2. `Assets/Scenes/CyberVeil_Level1.unity`
3. `Assets/Scenes/CyberVeil_Level2.unity`
4. `Assets/Scenes/CyberVeil_Level3.unity`

- Treat enabled build scenes as authoritative unless the user names another scene.
- Do not use scene backups, typo-named scenes, screenshots, or approval captures as runtime sources without explicit confirmation.
- When changing scene flow, verify both the build settings and string-based scene references in scripts.

## Repository organization

- `Assets/Art/`: models, animations, materials, shaders, textures, and UI art.
- `Assets/Audio/`: music and sound effects.
- `Assets/Scenes/`: the home screen and playable levels.
- `Assets/Scripts/Audio/`: audio feedback components and audio-facing interfaces.
- `Assets/Scripts/Combat/`: damage, health, knockback, and combat coordination.
- `Assets/Scripts/Core/`: shared interfaces and character-state infrastructure.
- `Assets/Scripts/Enemies/`: enemy state machines, attacks, projectiles, and responses.
- `Assets/Scripts/Player Scripts/`: movement, attacks, abilities, interaction, and progression.
- `Assets/Scripts/Systems/`: waves, curses, scene flow, camera, audio, tutorials, and feedback systems.
- `Assets/Scripts/UI/`: menus, HUD, dialogue, prompts, settings, and archive UI.
- `Assets/Scripts/VFX/`: particles, dissolves, and visual damage feedback.
- `Assets/Scripts/World/`: NPCs, crystals, portals, upgrades, and world interactions.

Place new code with the system it owns. Do not reorganize unrelated files solely to make a task look cleaner.

## C# and architecture

- Use the existing `CyberVeil.*` namespace that matches the script's owning system.
- Prefer focused components with one clear responsibility over expanding large manager classes.
- Reuse established interfaces and systems before adding parallel implementations. Relevant examples include damage, knockback, interaction, enemy attacks, sound, visual response, scene progression, hit stop, and screen shake.
- For new Inspector configuration, prefer `[SerializeField] private` fields unless another component genuinely needs public access. Preserve existing serialized field names when possible so scene and prefab data remains connected.
- Avoid changing public APIs, enum ordering, serialized types, or `ScriptableObject` schemas without checking all consumers and serialized instances.
- If adding a `SoundType`, update every scene or prefab that serializes `SoundManager` lists and verify the enum-to-array indexing remains valid.
- Preserve the input approach already used by the subsystem. Do not migrate unrelated controls between the Input System and legacy input as part of a focused change.
- Avoid broad singleton, persistence, or scene-loading changes without checking `DontDestroyOnLoad` behavior and duplicate-instance handling across all enabled scenes.
- Keep per-frame code allocation-light. Avoid repeated object searches, component lookups, string construction, or collection creation in `Update`-family methods when references or cached values will work.

## Unity asset safety

- Prefer the connected Unity MCP for inspecting and changing scenes, prefabs, components, materials, animations, and project settings when it is available.
- Before using Unity MCP, confirm the connected instance, editor readiness, active scene, play-mode state, and compilation state.
- Prefer Unity serialization through the Editor over hand-editing large `.unity`, `.prefab`, `.controller`, `.mat`, or `.asset` YAML files.
- Hand-edit Unity YAML only when the change is small, structurally understood, and safer than opening/resaving the asset. Review the exact diff and validate afterward.
- Every Unity asset must remain paired with its `.meta` file. Preserve GUIDs during moves and renames, include the asset and `.meta` together, and never create duplicate GUIDs.
- Before committing moved assets or scenes, search GUID references and confirm all relationships resolve.
- Do not delete, regenerate, or replace a `.meta` file merely to fix an import problem.
- Do not modify third-party assets or package contents unless the requested fix requires it. Prefer wrappers, derived materials, prefabs, or project-owned scripts.
- Treat large automatic changes to font atlases, render-pipeline settings, lighting data, navigation data, and imported assets as suspicious until their semantic purpose is verified.

## Existing work and scope control

- Assume a dirty worktree contains user work. Inspect it before editing and preserve unrelated modifications and untracked files.
- Never use destructive cleanup commands, hard resets, broad checkout/revert operations, or mass deletion to simplify the workspace.
- Do not overwrite or normalize Unity assets just to reduce diff size.
- Make the smallest coherent change that fully solves the request. Do not bundle opportunistic refactors, balance changes, package upgrades, or asset reorganizations.
- If a deletion, missing asset, GUID mismatch, or scene replacement is ambiguous, stop and ask before continuing.

## Validation

- After C# changes, wait for Unity compilation to finish and inspect the Console for new compile errors and exceptions.
- When available, use Unity MCP script validation for changed scripts and scene validation for missing scripts or broken prefab references.
- Distinguish pre-existing console, shader, terrain, or third-party warnings from regressions introduced by the task.
- For scene, prefab, UI, input, combat, or progression changes, exercise the relevant flow in Play Mode when feasible.
- Check transitions between affected scenes, not only the scene where the change begins.
- Verify important interaction paths with both keyboard/mouse and any controller navigation affected by the change.
- There is currently no project-owned automated test suite. Add focused tests when a system is practically testable outside scene timing; otherwise provide explicit manual verification evidence.
- Do not claim a build, test, playtest, or visual result that was not actually run or observed. State any validation limitation in the handoff.

## Generated and local-only files

- Never commit Unity-generated folders such as `Library/`, `Temp/`, `Logs/`, `Obj/`, `UserSettings/`, memory captures, or IDE-generated files.
- Never commit player builds or build outputs unless the user explicitly requests a distributable artifact.
- Treat backup scenes, temporary exports, approval screenshots, debug captures, and generated reports as local-only unless the user explicitly asks to version them.
- Do not assume an untracked editor utility or prototype script is production code. Confirm it is referenced by a saved scene, prefab, asset, or runtime code path before including it in a feature commit.

## Git workflow

- Do not stage, commit, push, pull, merge, rebase, or modify remotes unless the user asks for that Git operation.
- Before committing, inspect the branch, tracking branch, status, diff statistics, actual diffs, untracked files, and Unity GUID relationships.
- Group commits by behavior and dependency, not merely by folder. Keep related scripts, scenes, prefabs, materials, animation, audio, UI, and `.meta` files together.
- Stage explicit paths. Do not use broad staging in a dirty worktree. Use patch staging only when a text or Unity YAML file contains safely separable changes.
- Use concise conventional commit messages that describe the actual result.
- Before each commit, run `git diff --cached --check`, review `git diff --cached --stat` and the staged diff, and verify no secret, cache, build output, backup, or unrelated file is staged.
- Do not amend, rewrite history, force-push, or automatically resolve remote divergence without explicit permission.

## Communication and handoff

- Lead with the result, then summarize the important implementation and validation evidence.
- Reference specific scenes, scripts, prefabs, assets, components, or settings when explaining findings.
- Be constructively critical about gameplay and technical risks, but separate verified facts from design hypotheses.
- For recommendations, prioritize changes that fit CyberVeil's existing identity and rank meaningful tradeoffs such as player impact, effort, and implementation risk.
- Report all intentionally untouched or uncommitted files when the task involves repository organization or Git publication.
