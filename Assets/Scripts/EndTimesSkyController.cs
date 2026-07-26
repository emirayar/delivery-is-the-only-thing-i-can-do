using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class EndTimesSkyController : MonoBehaviour
{
    [Header("Skybox")]
    [Tooltip("Turn the custom disaster sky on or off without deleting the original skybox.")]
    public bool useEndTimesSky = true;

    [Tooltip("Generated GMTK procedural skybox material.")]
    public Material skyboxMaterial;

    [Tooltip("Skybox that was active before the disaster sky was enabled.")]
    public Material originalSkybox;

    [Header("Sky Gradient")]
    [Tooltip("Color directly above the player.")]
    [ColorUsage(true, true)] public Color zenithColor = new(0.30f, 0.20f, 0.48f, 1f);
    [Tooltip("Main color between the horizon and the top of the sky.")]
    [ColorUsage(true, true)] public Color middleColor = new(0.58f, 0.25f, 0.45f, 1f);
    [Tooltip("Warm disaster color along the horizon.")]
    [ColorUsage(true, true)] public Color horizonColor = new(0.82f, 0.31f, 0.30f, 1f);
    [Tooltip("How gradually the horizon blends into the middle color.")]
    [Range(0.1f, 1.5f)] public float horizonSoftness = 0.65f;

    [Header("Sun")]
    [Tooltip("Directional light used to position the sun disc.")]
    public Light sunLight;
    [Tooltip("HDR color and brightness of the sun and its glow.")]
    [ColorUsage(true, true)] public Color sunColor = new(1.65f, 0.85f, 0.48f, 1f);
    [Tooltip("Visible radius of the sun disc.")]
    [Range(0.001f, 0.08f)] public float sunSize = 0.018f;
    [Tooltip("Strength of the wide glow around the sun.")]
    [Range(0f, 1.5f)] public float sunGlow = 0.55f;
    [Tooltip("Higher values make the sun glow tighter and more focused.")]
    [Range(1f, 32f)] public float sunGlowFalloff = 8f;

    [Header("Clouds")]
    [Tooltip("Dark underside color of the procedural clouds.")]
    [ColorUsage(true, true)] public Color cloudColor = new(0.30f, 0.17f, 0.38f, 1f);
    [Tooltip("Color used where clouds face the sun.")]
    [ColorUsage(true, true)] public Color cloudLightColor = new(0.74f, 0.39f, 0.51f, 1f);
    [Tooltip("Size and frequency of cloud shapes. Lower values create larger clouds.")]
    [Range(1f, 12f)] public float cloudScale = 4.4f;
    [Tooltip("Amount of the sky covered by clouds.")]
    [Range(0f, 1f)] public float cloudCoverage = 0.48f;
    [Tooltip("Softness of cloud edges.")]
    [Range(0.03f, 0.4f)] public float cloudSoftness = 0.15f;
    [Tooltip("How strongly clouds cover the sky gradient.")]
    [Range(0f, 1f)] public float cloudOpacity = 0.62f;
    [Tooltip("Speed of procedural cloud movement. Zero keeps clouds still.")]
    [Range(0f, 1f)] public float cloudSpeed = 0.035f;

    private Material runtimeMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureControllerExists()
    {
        if (FindAnyObjectByType<EndTimesSkyController>() != null)
            return;
        Light directional = null;
        foreach (Light candidate in FindObjectsByType<Light>())
        {
            if (candidate.type == LightType.Directional)
            {
                directional = candidate;
                break;
            }
        }
        GameObject host = directional != null
            ? directional.gameObject
            : new GameObject("End Times Sky");
        EndTimesSkyController controller = host.AddComponent<EndTimesSkyController>();
        controller.sunLight = directional;
    }

    private void OnEnable()
    {
        if (originalSkybox == null && RenderSettings.skybox != skyboxMaterial)
            originalSkybox = RenderSettings.skybox;
        ApplySky();
    }

    private void Update()
    {
        ApplySky();
    }

    private void OnValidate()
    {
        ApplySky();
    }

    private void OnDisable()
    {
        if (RenderSettings.skybox == ActiveMaterial)
            RenderSettings.skybox = originalSkybox;
    }

    [ContextMenu("Apply End Times Sky")]
    public void ApplySky()
    {
        if (!useEndTimesSky)
        {
            if (RenderSettings.skybox == ActiveMaterial)
                RenderSettings.skybox = originalSkybox;
            return;
        }

        Material material = GetOrCreateMaterial();
        if (material == null)
            return;

        Vector3 sunDirection = sunLight != null
            ? -sunLight.transform.forward
            : new Vector3(0f, 0.35f, 0.94f).normalized;
        material.SetColor("_ZenithColor", zenithColor);
        material.SetColor("_MiddleColor", middleColor);
        material.SetColor("_HorizonColor", horizonColor);
        material.SetFloat("_HorizonSoftness", horizonSoftness);
        material.SetColor("_SunColor", sunColor);
        material.SetVector("_SunDirection", new Vector4(sunDirection.x, sunDirection.y, sunDirection.z, 0f));
        material.SetFloat("_SunSize", sunSize);
        material.SetFloat("_SunGlow", sunGlow);
        material.SetFloat("_SunGlowFalloff", sunGlowFalloff);
        material.SetColor("_CloudColor", cloudColor);
        material.SetColor("_CloudLightColor", cloudLightColor);
        material.SetFloat("_CloudScale", cloudScale);
        material.SetFloat("_CloudCoverage", cloudCoverage);
        material.SetFloat("_CloudSoftness", cloudSoftness);
        material.SetFloat("_CloudOpacity", cloudOpacity);
        material.SetFloat("_CloudSpeed", cloudSpeed);
        RenderSettings.skybox = material;
    }

    [ContextMenu("Restore Original Sky")]
    public void RestoreOriginalSky()
    {
        useEndTimesSky = false;
        RenderSettings.skybox = originalSkybox;
    }

    private Material ActiveMaterial => skyboxMaterial != null ? skyboxMaterial : runtimeMaterial;

    private Material GetOrCreateMaterial()
    {
        if (skyboxMaterial != null)
            return skyboxMaterial;
        if (runtimeMaterial != null)
            return runtimeMaterial;
        Shader shader = Shader.Find("GMTK/End Times Procedural Sky");
        if (shader == null)
            return null;
        runtimeMaterial = new Material(shader)
        {
            name = "End Times Skybox (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };
        return runtimeMaterial;
    }
}
