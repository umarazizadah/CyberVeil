# Third-Party Software and Assets

CyberVeil contains Unity packages and imported art, animation, audio, shader, and visual-effect assets that may be governed by licenses separate from the project-authored source code.

This file is an inventory aid, not a substitute for original license text or purchase records. Inclusion in this repository does not grant rights beyond those provided by each asset's owner.

## Unity packages

Package identifiers and exact versions are recorded in:

- `Packages/manifest.json`
- `Packages/packages-lock.json`

Major dependencies include:

- Unity Universal Render Pipeline
- Unity Shader Graph and Visual Effect Graph
- Unity Input System and AI Navigation
- Unity UI and Timeline
- Unity Test Framework and performance-analysis packages
- Unity Toon Shader preview package
- MCP for Unity by Coplay

Unity packages are governed by their respective Unity or publisher licenses. The package manifest is the authoritative dependency list.

## Imported collections requiring license verification

Source- or publisher-named directories include:

- `Assets/Art/Characters/MonsterPack3D`
- `Assets/Art/Characters/minotaur1`
- `Assets/Art/Environment/DungeonModularPack`
- `Assets/Art/Environment/GraveyardProps`
- `Assets/Vfx/Particles/COMICOMI`
- `Assets/Vfx/Particles/Free Slash VFX`
- `Assets/Vfx/Particles/Hovl Studio`
- `Assets/Vfx/Particles/Matthew Guz`
- `Assets/Vfx/Particles/NamuFX`
- `Assets/Vfx/Particles/UnityTechnologies`
- `Assets/Vfx/Shaders/ShaderGraph_Dissolve/Lana Studio`
- `Assets/Vfx/Shaders/SimpleToon`

Additional textures, fonts, models, animations, music, sound effects, and UI assets may also originate from third parties even when their directory does not identify the publisher.

## Release audit checklist

Before distributing the game or changing the repository's public license:

1. Locate the license, receipt, download page, or publisher terms for every imported collection.
2. Record the asset name, publisher, source URL, version, license, and required attribution.
3. Confirm the license permits use in a compiled commercial game.
4. Confirm whether raw source assets may be redistributed in a public repository.
5. Preserve required copyright and attribution notices.
6. Remove demo or raw content that is unnecessary and cannot be redistributed.
7. Confirm fonts, music, sound effects, and generated assets separately.

## Project-authored content

The repository does not currently declare a project-wide license. Unless one is added, no blanket permission should be inferred for reuse of CyberVeil's project-authored code or assets.

For licensing questions, contact the repository owner.
