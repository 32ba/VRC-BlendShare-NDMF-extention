# BlendShare Pipeline Setup Sample

This sample guides you through preparing a local avatar scene that can be used to verify the BlendShare + NDMF pipeline.

1. Install the package dependencies listed in `package.json` (NDMF 1.9.4, BlendShare 0.0.11).
2. Import an avatar FBX that already matches the vertex layout expected by the BlendShapeDataSO you plan to apply.
3. Create an instance of the `AddBlendShapeByBlendShare` component and assign your `SkinnedMeshRenderer`.
4. Reference a `BlendShapeDataSO` asset from BlendShare and verify that the mesh duplicate receives the expected blendshapes during the NDMF build step.

Update this sample once concrete prefabs/pipeline assets are added to the repository.
