using UnityEngine;

// Kept only so existing scenes can migrate without a missing-script component.
// GmtkPrototypeSetup removes this component and adds GpuProceduralGrass.
[AddComponentMenu("")]
public sealed class InteractiveGrassRenderer : MonoBehaviour
{
    private void OnEnable()
    {
        enabled = false;
        Debug.LogWarning(
            "InteractiveGrassRenderer is retired. Run "
            + "GMTK > Build Procedural World Prototype to migrate to "
            + "GpuProceduralGrass.",
            this);
    }
}
