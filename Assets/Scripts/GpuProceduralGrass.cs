using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(ProceduralRibbonWorld))]
public sealed class GpuProceduralGrass : MonoBehaviour
{
    [StructLayout(LayoutKind.Sequential)]
    private struct GrassInstance
    {
        public Vector4 positionRandom;
        public Vector4 parameters;
    }

    private sealed class GrassChunk : IDisposable
    {
        public GraphicsBuffer buffer;
        public MaterialPropertyBlock properties;
        public Bounds bounds;
        public int count;

        public void Dispose()
        {
            buffer?.Release();
            buffer = null;
        }
    }

    private static readonly int GrassInstancesId =
        Shader.PropertyToID("_GrassInstances");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int TipColorId = Shader.PropertyToID("_TipColor");
    private static readonly int DryColorId = Shader.PropertyToID("_DryColor");
    private static readonly int MinBladeHeightId =
        Shader.PropertyToID("_MinBladeHeight");
    private static readonly int MaxBladeHeightId =
        Shader.PropertyToID("_MaxBladeHeight");
    private static readonly int MinBladeWidthId =
        Shader.PropertyToID("_MinBladeWidth");
    private static readonly int MaxBladeWidthId =
        Shader.PropertyToID("_MaxBladeWidth");
    private static readonly int WindStrengthId =
        Shader.PropertyToID("_WindStrength");
    private static readonly int WindScaleId = Shader.PropertyToID("_WindScale");
    private static readonly int WindSpeedId = Shader.PropertyToID("_WindSpeed");
    private static readonly int RoadHalfWidthId =
        Shader.PropertyToID("_RoadHalfWidth");
    private static readonly int RoadClearanceId =
        Shader.PropertyToID("_RoadClearance");
    private static readonly int RoadEdgeFadeId =
        Shader.PropertyToID("_RoadEdgeFade");
    private static readonly int InteractorPositionId =
        Shader.PropertyToID("_GrassInteractorPosition");
    private static readonly int InteractionRadiusId =
        Shader.PropertyToID("_GrassInteractionRadius");
    private static readonly int InteractionStrengthId =
        Shader.PropertyToID("_GrassInteractionStrength");
    private static readonly int FadeStartId = Shader.PropertyToID("_FadeStart");
    private static readonly int FadeEndId = Shader.PropertyToID("_FadeEnd");

    [Header("Density")]
    [Tooltip("Metrekare başına hedef çim kökü sayısı. Görüntüyü ve GPU maliyetini en çok etkileyen ayardır.")]
    [Min(0.1f)] public float bladesPerSquareMeter = 4.5f;
    [Tooltip("Çimleri yol boyunca kaç culling parçasına böler. Fazlası daha iyi culling, daha çok draw call demektir.")]
    [Range(4, 40)] public int longitudinalChunks = 20;
    [Tooltip("Yoğunluk ve dünya boyutu ne olursa olsun üretilebilecek en yüksek blade sayısı.")]
    [Min(1000)] public int maximumBladeCount = 360000;
    [Tooltip("Terrain'in dış sınırında çim bırakılmayacak boş kenar mesafesi.")]
    [Min(0f)] public float outerMargin = 1f;

    [Header("Blade Shape")]
    [Tooltip("Rastgele üretilecek en kısa çim boyu.")]
    [Min(0.05f)] public float minBladeHeight = 0.55f;
    [Tooltip("Rastgele üretilecek en uzun çim boyu.")]
    [Min(0.05f)] public float maxBladeHeight = 1.15f;
    [Tooltip("En ince çim yaprağının genişliği.")]
    [Min(0.005f)] public float minBladeWidth = 0.045f;
    [Tooltip("En kalın çim yaprağının genişliği.")]
    [Min(0.005f)] public float maxBladeWidth = 0.085f;

    [Header("Color")]
    [Tooltip("Çim yapraklarının köke yakın koyu rengi.")]
    public Color baseColor = new(0.055f, 0.19f, 0.025f);
    [Tooltip("Çim yapraklarının uç rengi.")]
    public Color tipColor = new(0.39f, 0.69f, 0.12f);
    [Tooltip("Bazı yapraklarda rastgele kullanılan kuru/sarı renk.")]
    public Color dryColor = new(0.68f, 0.53f, 0.12f);

    [Header("Road Mask")]
    [Tooltip("Yolun fiziksel genişliğine ek olarak çimsiz bırakılacak omuz mesafesi.")]
    [Min(0f)] public float roadClearance = 0.25f;
    [Tooltip("Yol kenarında çim yoğunluğunun sıfırdan normale geçtiği yumuşak bölgenin genişliği.")]
    [Min(0.01f)] public float roadEdgeFade = 1.4f;

    [Header("Wind")]
    [Tooltip("Rüzgârın çimleri ne kadar yana yatıracağı.")]
    [Range(0f, 1f)] public float windStrength = 0.18f;
    [Tooltip("Rüzgâr dalgalarının dünya üzerindeki boyutu. Küçük değer geniş ve yumuşak dalgalar üretir.")]
    [Min(0.001f)] public float windScale = 0.16f;
    [Tooltip("Rüzgâr animasyonunun hareket hızı.")]
    [Min(0f)] public float windSpeed = 1.4f;

    [Header("Interaction")]
    [Tooltip("Yaklaştığında çimleri bükecek oyuncu, araç veya başka bir Transform.")]
    public Transform interactor;
    [Tooltip("Interactor'ın çimleri etkilemeye başladığı yarıçap.")]
    [Min(0.1f)] public float interactionRadius = 2.2f;
    [Tooltip("Interactor'ın çimleri ne kadar güçlü yana yatıracağı.")]
    [Min(0f)] public float interactionStrength = 1.15f;

    [Header("Distance")]
    [Tooltip("Bu kamera mesafesinden sonra çim yoğunluğu azalmaya başlar.")]
    [Min(1f)] public float fadeStart = 75f;
    [Tooltip("Bu kamera mesafesinde çimler tamamen kaybolur.")]
    [Min(2f)] public float fadeEnd = 115f;

    [Header("Rendering")]
    [Tooltip("GPU procedural grass shader'ını kullanan material. Boşsa sistem otomatik oluşturur.")]
    public Material grassMaterial;
    [Tooltip("Çim gölgelerini açar. Görüntüyü iyileştirebilir fakat GPU maliyetini ciddi artırabilir.")]
    public bool castShadows;

    private readonly List<GrassChunk> chunks = new();
    private ProceduralRibbonWorld world;
    private ProceduralFieldSystem fields;
    private int generatedSignature;
    private bool ownsMaterial;

    public int VisibleBladeCapacity { get; private set; }
    public int DrawCallCount => chunks.Count;

    private void OnEnable()
    {
        world = GetComponent<ProceduralRibbonWorld>();
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        Rebuild();
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        ReleaseChunks();
    }

    private void OnDestroy()
    {
        ReleaseChunks();
        if (ownsMaterial && grassMaterial != null)
            DestroySafely(grassMaterial);
    }

    private void OnValidate()
    {
        bladesPerSquareMeter = Mathf.Max(0.1f, bladesPerSquareMeter);
        longitudinalChunks = Mathf.Clamp(longitudinalChunks, 4, 40);
        maximumBladeCount = Mathf.Max(1000, maximumBladeCount);
        maxBladeHeight = Mathf.Max(minBladeHeight, maxBladeHeight);
        maxBladeWidth = Mathf.Max(minBladeWidth, maxBladeWidth);
        fadeEnd = Mathf.Max(fadeStart + 1f, fadeEnd);

        if (isActiveAndEnabled)
            Rebuild();
    }

    [ContextMenu("Rebuild GPU Grass")]
    public void Rebuild()
    {
        world = GetComponent<ProceduralRibbonWorld>();
        fields = GetComponent<ProceduralFieldSystem>();
        ReleaseChunks();
        EnsureMaterial();

        if (world == null || world.Spline == null || grassMaterial == null)
            return;

        float approximateLength = world.Spline.ApproximateLength(128);
        float usableWidth = Mathf.Max(0f, world.halfWidth * 2f - outerMargin * 2f);
        int totalCount = Mathf.Min(
            maximumBladeCount,
            Mathf.RoundToInt(approximateLength * usableWidth * bladesPerSquareMeter));

        if (totalCount <= 0)
            return;

        var random = new System.Random(world.seed ^ 0x6C8E9CF5);
        int remainder = totalCount % longitudinalChunks;

        for (int chunkIndex = 0; chunkIndex < longitudinalChunks; chunkIndex++)
        {
            int count = totalCount / longitudinalChunks
                      + (chunkIndex < remainder ? 1 : 0);
            if (count <= 0)
                continue;

            float tMin = chunkIndex / (float)longitudinalChunks;
            float tMax = (chunkIndex + 1f) / longitudinalChunks;
            GrassChunk chunk = BuildChunk(random, count, tMin, tMax);
            if (chunk != null)
            {
                chunks.Add(chunk);
                VisibleBladeCapacity += chunk.count;
            }
        }

        generatedSignature = CalculateSignature();
    }

    private GrassChunk BuildChunk(
        System.Random random,
        int count,
        float tMin,
        float tMax)
    {
        var instances = new List<GrassInstance>(count);
        Bounds bounds = default;
        int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
        float minOffset = -world.halfWidth + outerMargin;
        float maxOffset = world.halfWidth - outerMargin;

        for (int i = 0; i < count; i++)
        {
            int x = i % columns;
            int y = i / columns;
            float jitterX = NextFloat(random);
            float jitterY = NextFloat(random);
            float t = Mathf.Lerp(tMin, tMax, (y + jitterY) / rows);
            float lateral = Mathf.Lerp(
                minOffset,
                maxOffset,
                (x + jitterX) / columns);

            if (fields != null && fields.IsInsideField(t, lateral))
                continue;

            world.SampleSurface(t, lateral, out Vector3 position, out _);
            float randomValue = NextFloat(random);
            float dryVariation = Mathf.Pow(NextFloat(random), 5f);
            float lean = NextFloat(random) * 2f - 1f;

            instances.Add(new GrassInstance
            {
                positionRandom = new Vector4(
                    position.x,
                    position.y,
                    position.z,
                    randomValue),
                parameters = new Vector4(lateral, dryVariation, lean, t)
            });

            if (instances.Count == 1)
                bounds = new Bounds(position, Vector3.one * 2f);
            else
                bounds.Encapsulate(position);
        }

        if (instances.Count == 0)
            return null;

        bounds.Expand(new Vector3(4f, maxBladeHeight * 5f, 4f));
        var buffer = new GraphicsBuffer(
            GraphicsBuffer.Target.Structured,
            instances.Count,
            Marshal.SizeOf<GrassInstance>());
        buffer.SetData(instances);

        var properties = new MaterialPropertyBlock();
        properties.SetBuffer(GrassInstancesId, buffer);

        return new GrassChunk
        {
            buffer = buffer,
            properties = properties,
            bounds = bounds,
            count = instances.Count
        };
    }

    private void OnBeginCameraRendering(
        ScriptableRenderContext context,
        Camera renderingCamera)
    {
        if (!isActiveAndEnabled
            || renderingCamera == null
            || renderingCamera.cameraType == CameraType.Preview
            || renderingCamera.cameraType == CameraType.Reflection)
        {
            return;
        }

        EnsureMaterial();
        if (world == null)
            world = GetComponent<ProceduralRibbonWorld>();
        if (world == null || grassMaterial == null)
            return;

        if (generatedSignature != CalculateSignature() || chunks.Count == 0)
            Rebuild();

        UpdateMaterial();
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(renderingCamera);

        foreach (GrassChunk chunk in chunks)
        {
            if (!GeometryUtility.TestPlanesAABB(planes, chunk.bounds))
                continue;

            Graphics.DrawProcedural(
                grassMaterial,
                chunk.bounds,
                MeshTopology.Triangles,
                12,
                chunk.count,
                renderingCamera,
                chunk.properties,
                castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                true,
                gameObject.layer);
        }
    }

    private void UpdateMaterial()
    {
        grassMaterial.SetColor(BaseColorId, baseColor);
        grassMaterial.SetColor(TipColorId, tipColor);
        grassMaterial.SetColor(DryColorId, dryColor);
        grassMaterial.SetFloat(MinBladeHeightId, minBladeHeight);
        grassMaterial.SetFloat(MaxBladeHeightId, maxBladeHeight);
        grassMaterial.SetFloat(MinBladeWidthId, minBladeWidth);
        grassMaterial.SetFloat(MaxBladeWidthId, maxBladeWidth);
        grassMaterial.SetFloat(WindStrengthId, windStrength);
        grassMaterial.SetFloat(WindScaleId, windScale);
        grassMaterial.SetFloat(WindSpeedId, windSpeed);
        grassMaterial.SetFloat(
            RoadHalfWidthId,
            world.Spline.roadWidth * 0.5f);
        grassMaterial.SetFloat(RoadClearanceId, roadClearance);
        grassMaterial.SetFloat(RoadEdgeFadeId, roadEdgeFade);
        grassMaterial.SetFloat(FadeStartId, fadeStart);
        grassMaterial.SetFloat(FadeEndId, fadeEnd);

        Vector3 interactionPosition = interactor != null
            ? interactor.position
            : new Vector3(100000f, 100000f, 100000f);
        grassMaterial.SetVector(InteractorPositionId, interactionPosition);
        grassMaterial.SetFloat(InteractionRadiusId, interactionRadius);
        grassMaterial.SetFloat(InteractionStrengthId, interactionStrength);
    }

    private void EnsureMaterial()
    {
        if (grassMaterial != null)
            return;

        Shader shader = Shader.Find("GMTK/GPU Procedural Grass");
        if (shader == null)
            return;

        grassMaterial = new Material(shader)
        {
            name = "Generated GPU Procedural Grass",
            hideFlags = HideFlags.HideAndDontSave
        };
        ownsMaterial = true;
    }

    private int CalculateSignature()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (world != null ? world.seed : 0);
            hash = hash * 31 + Mathf.RoundToInt(bladesPerSquareMeter * 100f);
            hash = hash * 31 + longitudinalChunks;
            hash = hash * 31 + maximumBladeCount;
            hash = hash * 31 + Mathf.RoundToInt(outerMargin * 100f);
            hash = hash * 31 + (world != null
                ? Mathf.RoundToInt(world.halfWidth * 100f)
                : 0);
            hash = hash * 31 + (fields != null ? fields.LayoutVersion : 0);
            return hash;
        }
    }

    private void ReleaseChunks()
    {
        foreach (GrassChunk chunk in chunks)
            chunk.Dispose();
        chunks.Clear();
        VisibleBladeCapacity = 0;
    }

    private static float NextFloat(System.Random random)
    {
        return (float)random.NextDouble();
    }

    private static void DestroySafely(UnityEngine.Object target)
    {
        if (target == null)
            return;
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
