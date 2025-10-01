# BlendShare NDMF Extension

This Unity package bridges [BlendShare](https://github.com/Tr1turbo/BlendShare) blendshape data into the [NDMF](https://github.com/bdunderscore/ndmf) build pipeline without mutating the source FBX meshes. It works whether or not AvatarOptimizer is installed.

## Requirements

- Unity 2022.3 LTS
- `nadena.dev.ndmf` **1.9.4**
- `com.triturbo.blendshare` **0.0.11**

These dependency versions are pinned in `package.json`.

AvatarOptimizer remains fully compatible but is no longer required.

## Package Contents

- `Runtime/AddBlendShapeByBlendShare.cs` — component that stores the BlendShare-to-renderer mapping.
- `Editor/AddBlendShapeByBlendShareProcessor.cs` — NDMF plugin that duplicates meshes, applies BlendShare data, and writes the result back during the Optimizing phase.
- `Editor/AddBlendShapeByBlendShareEditor.cs` — inspector UI with a reorderable list, hash checking toggle, and duplicate name policy selection.
- `Tests~/Editor/BlendShareTestUtilities.cs` — menu utilities to build a manual smoke-test scene from the current selection.
- `Tests~/Manual/BlendSharePipelineTestGuide.md` — recommended validation steps covering hash mismatches and duplicate handling.

## Quick Start

1. Install the dependencies above via VPM or UPM.
2. Add the `AddBlendShapeByBlendShare` component to the avatar root.
3. Use the inspector list to map each target `SkinnedMeshRenderer` to the corresponding `BlendShapeDataSO`.
4. Keep **Enforce Hash** enabled to detect topology drift; switch **On Conflict** to *Skip* if you want to leave existing blendshapes untouched.
5. Run your NDMF build (e.g., AvatarOptimizer or another NDMF-powered pipeline); new meshes receive a `_BlendShare` suffix and inherit the original blendshape weights.

## Testing

- **Populate From Children** in the inspector seeds mappings for every child renderer.
- **BlendShare ▶ Test ▶ Attach Component To Selected Renderer** builds a quick test harness around the selected renderer.
- Follow the manual walkthrough in `Tests~/Manual/BlendSharePipelineTestGuide.md` to verify hash validation, duplicate handling, and log output.

Issues and feature requests are welcome via the repository issue tracker.
