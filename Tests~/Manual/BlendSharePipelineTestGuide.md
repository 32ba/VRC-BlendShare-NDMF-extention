# BlendShare Pipeline Test Guide

This manual walkthrough verifies that the BlendShare → NDMF bridge functions correctly inside the Unity Editor.

## Prerequisites

- Unity 2022.3 or compatible LTS version.
- Packages installed via VPM or UPM:
  - NDMF 1.9.4
    - BlendShare 0.0.11
  - This package (`net.32ba.blendshare-ndmf-extension`).
- A BlendShare `BlendShapeDataSO` asset created from a reference mesh.

## Steps

1. Open the avatar scene you wish to validate and select a `SkinnedMeshRenderer` that matches the BlendShare data.
2. Use the menu **BlendShare ▶ Test ▶ Attach Component To Selected Renderer** to add the `AddBlendShapeByBlendShare` component to the avatar root.
3. In the component inspector:
   - Confirm that the selected renderer was added to the mapping list.
   - Assign the expected `BlendShapeDataSO` asset.
   - Optionally override the mesh name if it differs from the renderer’s shared mesh name.
   - Leave `Enforce Hash` enabled for the first validation.
4. Run your NDMF build (AvatarOptimizer or any other NDMF-driven pipeline) to trigger the processors.
5. Inspect the duplicated mesh in the built avatar:
   - The new mesh name contains the `_BlendShare` suffix.
   - All BlendShare blendshape names appear with weights preserved from the source renderer.
6. Change the duplicate policy to **Skip** and rebuild to confirm that conflicting blendshape names are logged and the mesh remains unchanged.
7. Temporarily disable `Enforce Hash`, modify the source mesh (e.g., reorder vertices), and observe the logged warning to confirm mismatch detection.

Document any deviations or issues discovered during the walkthrough and file them in the repository issue tracker.
