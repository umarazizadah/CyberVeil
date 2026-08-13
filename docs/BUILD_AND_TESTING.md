# Building and Testing CyberVeil

## Supported environment

Use **Unity 6000.0.26f1**. Opening the project in a different version can reserialize scenes, materials, render-pipeline assets, and project settings.

The project uses Universal Render Pipeline 17, Shader Graph, Visual Effect Graph, AI Navigation, the Unity Input System, and selected legacy-input integrations. Unity Package Manager restores dependencies from `Packages/manifest.json` and `Packages/packages-lock.json`.

## Required build modules

For a production Windows player, install:

- Windows Build Support (IL2CPP)
- A compatible Windows C++ compiler toolchain supplied by the Unity module or Visual Studio

Unity Hub modules are installed per computer and are not stored in Git.

## Authoritative scene order

The enabled sequence in `ProjectSettings/EditorBuildSettings.asset` is:

1. `Assets/Scenes/CyberVeil_HomeScreen.unity`
2. `Assets/Scenes/CyberVeil_Level1.unity`
3. `Assets/Scenes/CyberVeil_Level2.unity`
4. `Assets/Scenes/CyberVeil_Level3.unity`

Disabled sample, playground, backup, or typo-named scenes are not production sources.

## Recommended build variants

| Build | Backend | Development | Profiler | Purpose |
| --- | --- | --- | --- | --- |
| Fast local test | Mono | On | Off | Quick standalone gameplay checks |
| Profile build | Mono | On | Autoconnect on | CPU, rendering, memory, and GC diagnosis |
| IL2CPP validation | IL2CPP | On | Optional | Find IL2CPP, stripping, or native-only problems |
| Production release | IL2CPP | Off | Off | Final performance testing and distribution |

Deep Profiling should remain disabled for ordinary FPS measurement because its instrumentation significantly changes script cost.

## Windows production build

1. Open **File → Build Profiles**.
2. Select **Windows**.
3. Confirm the four authoritative scenes are enabled and ordered correctly.
4. Open **Player Settings → Other Settings → Configuration**.
5. Set **Scripting Backend** to **IL2CPP**.
6. Use the **Release** IL2CPP compiler configuration.
7. Use **Minimal** managed stripping unless a more aggressive level has been validated.
8. Disable Development Build, Script Debugging, Autoconnect Profiler, and Deep Profiling Support.
9. Build into a new, empty output folder.

Example:

```text
Builds/CyberVeil_Windows_IL2CPP_Release
```

Do not place Mono and IL2CPP players in the same directory. Unity rejects a mixed-backend incremental build.

## Development and profiling folders

Keep build types separate:

```text
Builds/CyberVeil_Windows_Mono_Development
Builds/CyberVeil_Windows_Mono_Profile
Builds/CyberVeil_Windows_IL2CPP_Development
Builds/CyberVeil_Windows_IL2CPP_Release
```

Enable Profiler autoconnection only when the Profiler will be attached. A Development player can otherwise wait for a connection during startup.

## Pre-build checks

- Confirm Unity has finished compiling.
- Clear the Console, reload the active scene, and check for new errors.
- Confirm the current Git branch and worktree.
- Review changes to `ProjectSettings/`, render-pipeline assets, scenes, prefabs, and `.meta` files.
- Confirm no missing scripts or broken prefab references.
- Confirm the build scene list and string-based scene names agree.
- Build into a fresh folder.

## Manual release checklist

### Startup and navigation

- Game launches without exceptions.
- Home screen renders correctly.
- Mouse and keyboard navigation work.
- Settings open, apply, and close.
- Starting the game loads Level 1.

### Player

- Camera-relative movement, sprint, and dash
- Three-hit light combo and charged heavy attack
- Attack limiting and recovery
- Veil Surge
- Damage, knockback, death, and restart
- Weapon visibility and placement

### Combat and enemies

- Patrol and chase transitions
- Melee, charge, leap, shield, and projectile attacks
- Projectile collision, ownership, damage, and lifetime
- Repeated projectile reuse without stale trails or hit state
- Enemy death and wave accounting
- Multiple consecutive waves

### Presentation

- Player and enemy animation
- Damage flashes and dissolve effects
- Particles and trails
- Hit stop and screen shake
- Combat audio and footsteps
- Health, curse, ability, prompt, tutorial, and upgrade UI
- Lighting, shadows, post-processing, and terrain details

### Progression

- Healing crystal interaction
- Trial curse application and clearing
- Upgrade selection and stat persistence
- Portal activation
- Level 1 → Level 2 → Level 3
- Death and restart in every level
- Application quit

## Performance-test protocol

1. Use the same computer, power mode, resolution, quality settings, and graphics API.
2. Close unrelated CPU- or GPU-heavy applications.
3. Build candidates from the same content revision.
4. Warm each scene before recording.
5. Use the same camera position and gameplay sequence.
6. Record average FPS, frame time, 1% low, worst frame, CPU main thread, render thread, GPU time, memory, allocations, GC, batches, SetPass calls, and triangles where available.
7. Test both quiet starting views and a representative full enemy wave.
8. Repeat captures to distinguish real improvements from run-to-run noise.

Development Profiler measurements are useful for attribution. Confirm the final FPS result in a non-development IL2CPP player.

## Automated testing status

The repository currently has no project-owned automated test suite.

Good candidates for future tests include attack-selection cooldowns, stat calculations, damage and health rules, pool reset behavior, and scene progression ordering. Scene-dependent combat, animation, VFX, UI, and progression still require Play Mode or standalone validation.

## Generated files

Do not commit:

- `Library/`
- `Temp/`
- `Logs/`
- `Obj/`
- `UserSettings/`
- `Builds/`
- Player executables
- Profiler or memory captures
- Temporary screenshots and approval captures

Unity assets under `Assets/` must remain paired with their `.meta` files. Preserve GUIDs when moving or replacing assets.

## Known warnings

Some builds have reported warnings for LPW grass assets expecting the `Nature/Soft Occlusion` shader. Treat warnings as pre-existing only after confirming that a clean baseline produces the same messages.

Do not dismiss new shader, missing-reference, or stripping warnings without testing the affected feature in the release player.
