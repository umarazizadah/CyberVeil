# CyberVeil Performance Optimization Case Study

## Overview

CyberVeil is a third-person action game built in Unity 6 with URP, Shader Graph, Visual Effect Graph, AI Navigation, the Input System, animated characters, enemy waves, and layered combat feedback.

Performance work focused on raising real Windows-player performance without changing the intended image, combat timing, effects, animation, or scene composition. The target was a stable minimum of 30 FPS across the three playable levels.

This document separates direct measurements from estimates. Results can vary with hardware, resolution, camera position, active enemies, and build configuration.

## Test environment

- Unity: **6000.0.26f1**
- Render pipeline: **Universal Render Pipeline 17**
- Test platform: **Windows**
- Test GPU: **AMD Radeon (TM) Graphics**
- Standalone probe resolution: **1544 × 775**
- Scenes: `CyberVeil_Level1`, `CyberVeil_Level2`, and `CyberVeil_Level3`
- Standalone sampling: 8-second warm-up followed by a 12-second measurement window
- VSync disabled with no forced frame-rate cap

The standalone scene probe sampled relatively quiet starting views. Combat-heavy waves still require separate worst-case captures.

## Initial problems

The investigation identified several independent costs:

1. The original player mesh contained approximately **915,000 triangles**.
2. Levels contained hundreds of repeated environment renderers and high draw-call counts.
3. Enemy projectiles were repeatedly instantiated and destroyed.
4. Reused particle systems did not explicitly clear all emitted particles before reuse.
5. Editor Play Mode and Development Build tooling added overhead not present in a production build.

## Optimization 1: player mesh

The player model at `Assets/Art/Characters/PLAYER/MAINPLAYER.fbx` was reduced from approximately **915,000 triangles** to **249,998 triangles**, a reduction of roughly **73%**.

The optimized asset retained the original path and GUID. Validation covered:

- Armature, bone naming, skinning, and deformation
- Idle, movement, sprint, dash, attack, and damage states
- Weapon placement
- Material slots and shader compatibility
- Damage and dissolve effects
- Scene references in Levels 1–3

This primarily reduces vertex processing, skinning, memory bandwidth, and shadow-pass geometry. Its isolated GPU-frame contribution was not captured with a standalone GPU profiler, so no unsupported millisecond claim is made here.

## Optimization 2: carefully audited GPU instancing

GPU instancing was enabled on two compatible URP/Lit materials:

- `Assets/Materials/112ca.mat`
- `Assets/Art/Environment/rocks 1/rockTex.mat`

These materials were shared by **141 repeated rock renderers** across Levels 1–3.

| Scene | Draws | Batches before | Batches after | SetPass before | SetPass after |
| --- | ---: | ---: | ---: | ---: | ---: |
| Level 1 | 567 | 548 | 548 | 139 | 139 |
| Level 2 | 730 | 664 | 664 | 136 | 135 |
| Level 3 | 901 | 826 | 823 | 150 | 146 |

The improvement was intentionally modest because many other environment objects were already static-batched or already used instancing.

Synchronized image comparisons produced mean full-frame absolute differences between **0.002 and 0.038 values out of 255**. Remaining differences were consistent with time-dependent particles rather than a material or lighting regression.

Candidates were excluded when they already used batching, used skinned rendering, relied on per-object dissolve or damage state, used unproven custom shaders, or had genuinely different properties. No materials were merged merely because they looked similar.

## Optimization 3: projectile and particle pooling

A scene-local, prefab-keyed runtime pool was added for enemy projectiles. Projectile pools prewarm four instances and expand safely if demand exceeds the prewarmed capacity.

Reset behavior covers transforms, rigidbodies, colliders, renderers, particles, trails, animators, audio, coroutines, damage, ownership, timers, hit state, and double-return prevention. The existing particle manager was also updated to clear particles before playback and return.

| Operation over 100 cycles | Time |
| --- | ---: |
| Instantiate and immediate destruction | 14.03 ms |
| Pooled reuse | 3.71 ms |

The pooled path was approximately **73.5% faster** in this isolated test. A 200-cycle reset/reuse stress test completed without Console errors.

Generic attack wrappers were not pooled because a representative lightweight wrapper measured slower when pooled. Full enemies and healing crystals were excluded because destruction participates in wave progression and rewards.

## Optimization 4: production build configuration

Standalone builds were configured for IL2CPP, the Release compiler configuration, Minimal managed stripping, and incremental garbage collection. Development Build, Script Debugging, Deep Profiling, and Profiler autoconnection are disabled for production.

Before the Windows IL2CPP module was installed, equivalent Mono Development and non-development Mono players measured the cost of development instrumentation.

| Scene | Development FPS | Development frame time | Release FPS | Release frame time |
| --- | ---: | ---: | ---: | ---: |
| Level 1 | 28.73 | 34.80 ms | 29.32 | 34.11 ms |
| Level 2 | 27.25 | 36.69 ms | 27.56 | 36.28 ms |
| Level 3 | 25.96 | 38.53 ms | 26.71 | 37.44 ms |

The non-development Mono player improved average frame time by approximately **1–3%** in these samples.

| Metric | Development Mono | Release Mono |
| --- | ---: | ---: |
| Input-ready window | 1,034 ms | 458 ms |
| Working memory | 664.2 MB | 532.5 MB |
| Build folder size | 521.6 MB | 467.0 MB |

No garbage collection occurred during the 12-second per-level idle samples.

## Final IL2CPP result

The Windows IL2CPP module was installed after the initial comparison, and the resulting game was manually reported to run substantially better.

An exact IL2CPP FPS claim is intentionally not recorded yet. Before publishing one, repeat the same resolution, camera position, warm-up, scene sequence, and combat scenario, including a representative enemy wave. The verified Mono release results above remain the current reproducible comparison baseline.

## Outcome and lessons

The work produced four independently reversible improvements: a lighter player mesh, safe instancing for verified environment materials, faster projectile reuse with reliable VFX reset, and a production-oriented IL2CPP configuration.

The central lesson was to measure candidates individually. Several plausible changes produced only small gains, while generic attack pooling was rejected because measurement showed it was not beneficial.

## Further work

1. Capture repeatable IL2CPP measurements for Levels 1–3 and a representative full wave.
2. Use a GPU frame capture to identify the most expensive passes in Level 3.
3. Evaluate terrain instancing and occlusion culling separately.
4. Measure shadows, transparency, particles, and post-processing during worst-case combat.
5. Add a repeatable performance-test scene or build-only benchmarking harness.
