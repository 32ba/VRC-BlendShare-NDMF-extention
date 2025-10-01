# BlendShare Metadata Requirements

This document summarizes the metadata required to safely apply a BlendShare `BlendShapeDataSO` asset to a Unity mesh within the NDMF pipeline.

## Stored Fields inside BlendShapeDataSO

| Field | Purpose |
| --- | --- |
| Blendshape vertex offsets (per shape) | Delta vectors that re-create each blendshape in the original vertex order. |
| Original FBX object & mesh reference (GUID only) | Ensures the mesh GUID matches the source FBX without embedding the asset itself. |
| Unity vertex count & hash | Guards against topology drift before applying blendshapes. |
| Deformer ID | Identifies the blendshape deformer group that should be replaced or updated. |
| Compare method (Name / Index / Custom) | Controls how BlendShare matches source blendshapes to the target mesh. |

## Implications for This Project

- **Topology Verification**: Always verify that the vertex count and stored hash are identical before calling `BlendShapeAppender.CreateBlendShapesMesh`, otherwise abort with a descriptive error.
- **Mesh Selection**: Store the intended SkinnedMeshRenderer identifier alongside the `BlendShapeDataSO` reference so the processor can locate the right mesh instance.
- **Deformer Handling**: Respect the `Deformer ID` so subsequent BlendShare operations remain incremental and predictable.
- **Custom Extraction Awareness**: When the data asset was created via the "Custom" compare mode, expect only a subset of shapes; report missing names rather than silently skipping them.

## References

- BlendShare documentation, "BlendShapes Data Asset" section (retrieved 2025-10-01).
