# BlendShare NDMF Extension

This Unity package bridges [BlendShare](https://github.com/Tr1turbo/BlendShare) blendshape data into the [NDMF](https://github.com/bdunderscore/ndmf) build pipeline without mutating the source FBX meshes. It duplicates meshes during the NDMF Transforming phase, applies the BlendShare shapes, and preserves the original assets in the project.

## Requirements

- Unity 2022.3 LTS
- `nadena.dev.ndmf` **1.9.4**
- `com.triturbo.blendshare` **0.0.11**

These versions are pinned in `package.json`. AvatarOptimizer remains fully compatible but is not required.

## Package Contents

- `Runtime/BlendShareRendererMapping.cs` – component that stores the BlendShare asset reference, mesh override, duplicate policy, and animatable blendshape definitions per renderer.
- `Runtime/BlendShareBlendShapeDefinition.cs` – serializable definition that exposes an animatable `Weight` property for additional BlendShare keys.
- `Editor/AddBlendShapeByBlendShareWizard.cs` – tooling to attach mappings to renderers with hash checks and duplicate handling options.
- `Editor/BlendShareRendererMappingEditor.cs` ? custom inspector with a BlendShape Definitions list and quick actions to populate entries from the source asset.
- `Editor/AddBlendShapeByBlendShareProcessor.cs` ? NDMF plugin that appends BlendShare shapes, applies default weights, and remaps recorded definition curves to `SkinnedMeshRenderer` blendshape curves.
- `Editor/BlendSharePreviewFilter.cs` ? NDMF preview integration that mirrors generated meshes and applies definition weights while preview mode is active.

## Quick Start

1. Install the dependencies above via VPM or UPM.
2. Open the avatar in the Unity Editor and launch **BlendShare ▸ Mapping Wizard** to bind a `BlendShareRendererMapping` to each target `SkinnedMeshRenderer`.
3. In the inspector for the newly added mapping component, use the **BlendShape Definitions** section to add entries for the BlendShare keys you intend to animate. The **Add All From Asset** button seeds definitions for every available key.
4. With a definition selected in the Animation window, record keyframes on the `Weight` property. The component keeps the values animatable in edit mode.
5. Build the avatar through an NDMF-powered pipeline (AvatarOptimizer or another NDMF build). During build, the processor duplicates the mesh, appends BlendShare shapes, converts the recorded `Weight` curves into `blendShape.{Name}` curves on the renderer, and removes the intermediate property from the clips.

## Animation Workflow

1. Configure definitions as described above.
2. In the Animation window, expand the mapping component and add keys on `BlendShareBlendShapeDefinition.Weight`.
3. Switch to the NDMF Preview window (if available) to verify that generated meshes respond to definition weights in real time.
4. After building, inspect the generated animation clips: the original definition bindings are removed and equivalent `blendShape` curves appear on the destination `SkinnedMeshRenderer`.

## Validation Tips

- Confirm that the generated mesh gains a `_BlendShare` suffix in the build output and that blendshape weights persist at their default values.
- Inspect one of the converted animation clips to ensure the original `Weight` track is gone and `blendShape.<Name>` curves exist with matching key data.
- If the Animation window does not list your BlendShare keys, verify that the mapping component still references the correct `BlendShapeDataSO` and that the mesh name matches the BlendShare asset entry.
- Vertex hash mismatch warnings indicate topology changes between the recorded mesh and the BlendShare asset; either disable the hash check or regenerate the asset if the mesh was edited intentionally.

Issues and feature requests are welcome via the repository issue tracker.
