# Procedural Ribbon World Prototype

This prototype deliberately generates a narrow world around one road instead of
building a full open world. The constraint keeps the GMTK jam scope small while
preserving the appearance of a long rural landscape.

## Run it

1. Open `Assets/Scenes/SampleScene.unity`.
2. Choose `GMTK > Build Procedural World Prototype`.
3. Select `RoadSpline`.
4. Set `Generated Length`, point count, bend, elevation, grade, and seed.
5. Press `Generate Road From Settings`, or move the orange handles manually.
6. Use `New Terrain Seed` in the inspector to test terrain variations.

The setup command is safe to run more than once. It reuses the existing
`RoadSpline` and does not create duplicate generator components.

## Components

- `RoadSpline` evaluates a Catmull-Rom curve through editable control points.
- `ProceduralRibbonWorld` samples that curve to create a terrain ribbon and a
  separate road mesh. Terrain length and segment count are derived from the
  spline's measured length, so extending the road generates more world instead
  of stretching a fixed mesh. Terrain noise fades to zero near the road.
- `GpuProceduralGrass` stores only compact root data in `GraphicsBuffer`
  chunks. The GPU expands every root into tapered crossed blades using
  `SV_VertexID`; there are no blade meshes, transforms, or GameObjects.
- `GpuProceduralGrass.shader` performs road exclusion, distance-density LOD,
  color variation, wind, lighting, and interactor bending on the GPU.
- The road and grass share the ribbon's lateral-distance coordinate. Editing
  road width changes the grass exclusion mask without rebuilding blade meshes.

## Performance levers

- `lengthSegments` and `widthSegments` control terrain CPU/mesh cost.
- `bladesPerSquareMeter` and `maximumBladeCount` control GPU vertex cost.
- `longitudinalChunks` controls culling granularity and draw calls.
- Grass batches never exceed Unity's 1023-instance draw limit.
- Terrain has one collider; grass has no colliders or scripts per instance.

For the jam, start near the defaults. Profile before increasing grass density.
The next useful optimization would be distance-based grass density, not a more
complex terrain system.
