using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(RoadSpline))]
public sealed class ProceduralRibbonWorld : MonoBehaviour
{
    [Header("Road-Driven World")]
    [Tooltip("Açıkken terrain çözünürlüğünü sabit segment sayısından değil yolun gerçek metre uzunluğundan hesaplar.")]
    public bool deriveResolutionFromRoad = true;
    [Tooltip("Yol boyunca iki terrain vertex satırı arasındaki hedef metre. Küçük değer daha pürüzsüz ve daha maliyetlidir.")]
    [Min(0.5f)] public float metersPerLengthSegment = 3f;
    [Tooltip("Terrain genişliği boyunca iki vertex arasındaki hedef metre.")]
    [Min(0.5f)] public float metersPerWidthSegment = 4f;

    [Header("Resolution")]
    [Tooltip("Yol boyunca terrain örnek sayısı. Artırmak virajları yumuşatır fakat mesh maliyetini yükseltir.")]
    [Min(8)] public int lengthSegments = 160;
    [Tooltip("Yolun bir kenarından diğerine terrain örnek sayısı. Artırmak engebeleri yumuşatır.")]
    [Min(4)] public int widthSegments = 40;
    [Tooltip("Yol merkezinden terrain'in her iki yana uzandığı mesafe. Toplam genişlik bunun iki katıdır.")]
    [Min(10f)] public float halfWidth = 80f;

    [Header("Terrain")]
    [Tooltip("Aynı değer aynı arazi engebelerini üretir.")]
    public int seed = 72526;
    [Tooltip("Terrain gürültüsünün dünya üzerindeki ölçeği. Küçük değerler geniş tepeler, büyük değerler sık tümsekler üretir.")]
    [Min(0.001f)] public float noiseScale = 0.025f;
    [Tooltip("Terrain tepeleri ile çukurları arasındaki dikey yükseklik miktarı.")]
    [Min(0f)] public float heightAmplitude = 4f;
    [Tooltip("Üst üste kullanılan noise katmanı sayısı. Yükseldikçe ayrıntı ve üretim maliyeti artar.")]
    [Range(1, 5)] public int noiseOctaves = 3;
    [Tooltip("Her yeni noise katmanının bir öncekine göre gücü.")]
    [Range(0f, 1f)] public float persistence = 0.5f;

    [Header("Valley Profile")]
    [Tooltip("Açıkken yol vadi tabanında kalır ve terrain iki yana doğru kademeli yükselir.")]
    public bool generateValley = true;
    [Tooltip("Yol kenarından sonra yükselmenin başlamayacağı yaklaşık düz taban mesafesi. Ev şeridinin daha rahat oturmasını sağlar.")]
    [Min(0f)] public float valleyFloorWidth = 10f;
    [Tooltip("Terrain'in dışa doğru her 100 metresinde kazanacağı yaklaşık yükseklik. Dünya genişledikçe vadi yüksekliği de doğal biçimde ölçeklenir.")]
    [Min(0f)] public float valleyRisePer100Meters = 12f;
    [Tooltip("Vadi yamacının profilini belirler. 1 doğrusal; daha yüksek değer yol yakınını düz, dış kenarları daha eğimli yapar.")]
    [Range(1f, 3f)] public float valleyCurve = 1.45f;
    [Tooltip("Yamaç yüksekliğinin yol boyunca ne kadar değişeceği. 0 her yerde aynı kesit; 0.3 yaklaşık yüzde 30 tepe-vadi farkı üretir.")]
    [Range(0f, 0.65f)] public float valleyLongitudinalVariation = 0.3f;
    [Tooltip("Yolun başından sonuna kaç geniş elevation dalgası sığacağını belirler.")]
    [Min(0.1f)] public float valleyVariationFrequency = 2.4f;
    [Tooltip("Asfalt kenarından itibaren ev ve bahçeler için neredeyse düz tutulacak şeridin genişliği.")]
    [Min(5f)] public float settlementFlatWidth = 48f;
    [Tooltip("Ev şeridinde terrain noise yüksekliğinin ne kadarının korunacağı. 0 tamamen düz, 0.05 çok hafif dalgalıdır.")]
    [Range(0f, 0.25f)] public float settlementHeightInfluence = 0.04f;

    [Header("Rendering")]
    [Tooltip("Terrain yüzeyinde kullanılacak material. Boş bırakılırsa geçici URP material oluşturulur.")]
    public Material terrainMaterial;
    [Tooltip("Tarla parselleri dışında kalan yol kenarı ve boş terrain alanlarının temiz toon rengi.")]
    public Color nonFieldTerrainColor = new(0.12f, 0.42f, 0.18f);
    [Tooltip("Yol yüzeyinde kullanılacak material. Boş bırakılırsa geçici URP material oluşturulur.")]
    public Material roadMaterial;
    [Tooltip("Araç ve oyuncunun üzerinde hareket edebilmesi için terrain collider üretir.")]
    public bool addTerrainCollider = true;
    [Tooltip("Terrain'in asfaltın içinden görünmemesi için yol yatağının terrain yüzeyinden ne kadar aşağı alınacağı.")]
    [Min(0.01f)] public float roadBedDepth = 0.12f;
    [Tooltip("Yol yatağındaki yükseklik farkının asfalt kenarı dışında kaç metrede terrain'e karışacağı.")]
    [Min(0.1f)] public float roadBedShoulder = 1.5f;
    [Tooltip("Terrain yatağı aşağı alındığında aracın asfaltın kendi fizik yüzeyinde gitmesini sağlar.")]
    public bool addRoadCollider = true;
    [Tooltip("Z-fighting'i önlemek için asfaltın spline merkezinin üzerinde tutulduğu küçük yükseklik.")]
    [Min(0.01f)] public float roadSurfaceClearance = 0.08f;
    [Tooltip("Inspector veya spline tutamaçları değiştiğinde dünyayı anında yeniden üretir.")]
    public bool liveEdit = true;

    private RoadSpline spline;
    private Mesh terrainMesh;
    private Mesh roadMesh;
    private MeshFilter terrainFilter;
    private MeshFilter roadFilter;
    private MeshRenderer terrainRenderer;
    private readonly List<Material> fieldMaterials = new();
    private Material fieldVariantTemplate;
    private Material runtimeRoadMaterial;
    private Material runtimeRoadTemplate;
    private bool regenerateQueued;

    public RoadSpline Spline => spline != null ? spline : spline = GetComponent<RoadSpline>();
    public float GeneratedRoadLength { get; private set; }
    public int GeneratedVertexCount =>
        (lengthSegments + 1) * (widthSegments + 1);

    private void OnEnable()
    {
        EnsureChildren();
        Regenerate();
    }

    private void OnDestroy()
    {
        foreach (Material material in fieldMaterials)
            DestroySafely(material);
        fieldMaterials.Clear();
        DestroySafely(runtimeRoadMaterial);
    }

    private void OnValidate()
    {
        lengthSegments = Mathf.Max(8, lengthSegments);
        widthSegments = Mathf.Max(4, widthSegments);
        halfWidth = Mathf.Max(10f, halfWidth);
        metersPerLengthSegment = Mathf.Max(0.5f, metersPerLengthSegment);
        metersPerWidthSegment = Mathf.Max(0.5f, metersPerWidthSegment);
        valleyFloorWidth = Mathf.Max(0f, valleyFloorWidth);
        valleyRisePer100Meters = Mathf.Max(0f, valleyRisePer100Meters);
        valleyCurve = Mathf.Clamp(valleyCurve, 1f, 3f);
        valleyLongitudinalVariation = Mathf.Clamp(
            valleyLongitudinalVariation,
            0f,
            0.65f);
        valleyVariationFrequency = Mathf.Max(
            0.1f,
            valleyVariationFrequency);
        roadBedDepth = Mathf.Max(0.01f, roadBedDepth);
        roadBedShoulder = Mathf.Max(0.1f, roadBedShoulder);
        roadSurfaceClearance = Mathf.Max(0.01f, roadSurfaceClearance);
        settlementFlatWidth = Mathf.Max(5f, settlementFlatWidth);

        if (liveEdit && isActiveAndEnabled)
            regenerateQueued = true;
    }

    private void Update()
    {
        if (!regenerateQueued || !isActiveAndEnabled)
            return;
        regenerateQueued = false;
        Regenerate();
    }

    [ContextMenu("Regenerate World")]
    public void Regenerate()
    {
        if (Spline == null || Spline.PointCount < 2)
            return;

        UpdateRoadDrivenDimensions();
        ProceduralFieldSystem fields =
            GetComponent<ProceduralFieldSystem>();
        if (fields != null)
            fields.Generate();
        EnsureChildren();
        BuildTerrain();
        BuildRoad();

        ProceduralVillageSystem village =
            GetComponent<ProceduralVillageSystem>();
        if (village != null)
            village.Generate();

        ProceduralNatureDressing nature =
            GetComponent<ProceduralNatureDressing>();
        if (nature != null)
            nature.Generate();
    }

    public void SampleSurface(float t, float lateralOffset, out Vector3 position, out Vector3 normal)
    {
        Spline.GetFrame(t, out Vector3 center, out _, out Vector3 right);
        float roadHalfWidth = Spline.roadWidth * 0.5f;
        float absoluteLateral = Mathf.Abs(lateralOffset);
        float nearRoadBlend = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(
                roadHalfWidth,
                roadHalfWidth + Mathf.Min(5f, settlementFlatWidth),
                absoluteLateral));
        float outerBlend = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(
                roadHalfWidth + settlementFlatWidth,
                roadHalfWidth + settlementFlatWidth + Spline.roadFalloff,
                absoluteLateral));
        float flatten = Mathf.Lerp(
            settlementHeightInfluence * nearRoadBlend,
            1f,
            outerBlend);

        position = center + right * lateralOffset;
        position.y += SampleHeight(position.x, position.z) * flatten;
        if (generateValley)
        {
            float valleyStart = roadHalfWidth + valleyFloorWidth;
            float usableSlopeWidth = Mathf.Max(1f, halfWidth - valleyStart);
            float slope01 = Mathf.Clamp01(
                (Mathf.Abs(lateralOffset) - valleyStart)
                / usableSlopeWidth);
            float edgeRise = usableSlopeWidth
                           * valleyRisePer100Meters
                           / 100f;
            float sidePhase = lateralOffset < 0f ? 0.17f : 0.61f;
            float seedPhase = Mathf.Repeat(seed * 0.000173f, 1f);
            float broadWave = Mathf.Sin(
                (t * valleyVariationFrequency + seedPhase + sidePhase)
                * Mathf.PI
                * 2f);
            float organicWave = Mathf.PerlinNoise(
                seedPhase * 23.7f + sidePhase * 11.3f,
                t * valleyVariationFrequency * 1.37f + 7.19f) * 2f - 1f;
            float elevationVariation = broadWave * 0.68f
                                     + organicWave * 0.32f;
            float riseMultiplier = Mathf.Max(
                0.25f,
                1f + elevationVariation * valleyLongitudinalVariation);
            position.y += edgeRise
                        * Mathf.Pow(slope01, valleyCurve)
                        * riseMultiplier;
        }
        float bedBlend = 1f - Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(
                roadHalfWidth,
                roadHalfWidth + roadBedShoulder,
                Mathf.Abs(lateralOffset)));
        position.y -= roadBedDepth * bedBlend;
        normal = Vector3.up;
    }

    private void EnsureChildren()
    {
        spline = GetComponent<RoadSpline>();

        terrainFilter = EnsureMeshChild("Generated Terrain", out terrainRenderer);
        roadFilter = EnsureMeshChild("Generated Road", out MeshRenderer roadRenderer);

        if (terrainMaterial == null)
            terrainMaterial = CreateRuntimeMaterial(
                "Generated Terrain Material",
                new Color(0.15f, 0.34f, 0.08f));
        if (roadMaterial == null)
            roadMaterial = CreateRuntimeMaterial(
                "Generated Road Material",
                new Color(0.21f, 0.22f, 0.22f));

        terrainRenderer.sharedMaterial = terrainMaterial;
        if (runtimeRoadMaterial == null
            || runtimeRoadTemplate != roadMaterial)
        {
            DestroySafely(runtimeRoadMaterial);
            runtimeRoadTemplate = roadMaterial;
            runtimeRoadMaterial = new Material(roadMaterial)
            {
                name = "Generated Road Toon Material",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        if (runtimeRoadMaterial.HasProperty("_ShadowColor"))
        {
            runtimeRoadMaterial.SetColor(
                "_ShadowColor",
                new Color(0.10f, 0.16f, 0.27f, 0.55f));
            runtimeRoadMaterial.SetFloat(
                "_OverrideLightAttenuation",
                1f);
            runtimeRoadMaterial.EnableKeyword("DR_LIGHT_ATTENUATION");
        }
        roadRenderer.sharedMaterial = runtimeRoadMaterial;
    }

    private void UpdateRoadDrivenDimensions()
    {
        int samples = Mathf.Clamp(Spline.PointCount * 24, 64, 512);
        GeneratedRoadLength = Spline.ApproximateLength(samples);

        if (!deriveResolutionFromRoad)
            return;

        lengthSegments = Mathf.Clamp(
            Mathf.CeilToInt(GeneratedRoadLength / metersPerLengthSegment),
            8,
            2048);
        widthSegments = Mathf.Clamp(
            Mathf.CeilToInt((halfWidth * 2f) / metersPerWidthSegment),
            4,
            256);
    }

    private MeshFilter EnsureMeshChild(string childName, out MeshRenderer meshRenderer)
    {
        Transform child = transform.Find(childName);
        if (child == null)
        {
            var childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(transform, false);
        }

        if (!child.TryGetComponent(out MeshFilter meshFilter))
            meshFilter = child.gameObject.AddComponent<MeshFilter>();
        if (!child.TryGetComponent(out meshRenderer))
            meshRenderer = child.gameObject.AddComponent<MeshRenderer>();

        return meshFilter;
    }

    private void BuildTerrain()
    {
        int columns = widthSegments + 1;
        int rows = lengthSegments + 1;
        var vertices = new Vector3[columns * rows];
        var uv = new Vector2[vertices.Length];
        var trianglesBySurface = new[]
        {
            new List<int>(),
            new List<int>(),
            new List<int>(),
            new List<int>(),
            new List<int>()
        };
        ProceduralFieldSystem fields = GetComponent<ProceduralFieldSystem>();

        for (int row = 0; row < rows; row++)
        {
            float t = row / (float)lengthSegments;
            for (int column = 0; column < columns; column++)
            {
                float across = column / (float)widthSegments;
                float offset = Mathf.Lerp(-halfWidth, halfWidth, across);
                SampleSurface(t, offset, out Vector3 worldPosition, out _);
                int index = row * columns + column;
                vertices[index] = transform.InverseTransformPoint(worldPosition);
                uv[index] = new Vector2(across, t);
            }
        }

        for (int row = 0; row < lengthSegments; row++)
        {
            float centerT = (row + 0.5f) / lengthSegments;
            for (int column = 0; column < widthSegments; column++)
            {
                int a = row * columns + column;
                int b = a + columns;
                int c = b + 1;
                int d = a + 1;

                float centerAcross = (column + 0.5f) / widthSegments;
                float centerOffset = Mathf.Lerp(
                    -halfWidth,
                    halfWidth,
                    centerAcross);
                float roadHalfWidth = Spline.roadWidth * 0.5f;
                if (Mathf.Abs(centerOffset) < roadHalfWidth - 0.25f)
                    continue;
                int surfaceIndex = 0;
                if (fields != null
                    && fields.TryGetCropType(
                        centerT,
                        centerOffset,
                        out int cropType))
                {
                    surfaceIndex = cropType + 1;
                }

                List<int> triangles = trianglesBySurface[surfaceIndex];
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(d);
            }
        }

        ReplaceMesh(ref terrainMesh, terrainFilter, "Procedural Ribbon Terrain");
        terrainMesh.indexFormat = vertices.Length > 65535
            ? IndexFormat.UInt32
            : IndexFormat.UInt16;
        terrainMesh.vertices = vertices;
        terrainMesh.uv = uv;
        terrainMesh.subMeshCount = trianglesBySurface.Length;
        for (int i = 0; i < trianglesBySurface.Length; i++)
            terrainMesh.SetTriangles(trianglesBySurface[i], i);
        terrainMesh.RecalculateNormals();
        terrainMesh.RecalculateBounds();
        UpdateTerrainMaterials(fields);

        if (addTerrainCollider)
        {
            if (!terrainFilter.TryGetComponent(out MeshCollider meshCollider))
                meshCollider = terrainFilter.gameObject.AddComponent<MeshCollider>();
            meshCollider.enabled = true;
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = terrainMesh;
        }
        else if (terrainFilter.TryGetComponent(out MeshCollider existingCollider))
        {
            existingCollider.enabled = false;
        }
    }

    private void BuildRoad()
    {
        int rows = lengthSegments + 1;
        var vertices = new Vector3[rows * 2];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[lengthSegments * 6];
        float halfRoadWidth = Spline.roadWidth * 0.5f;

        for (int row = 0; row < rows; row++)
        {
            float t = row / (float)lengthSegments;
            Spline.GetFrame(t, out Vector3 center, out _, out Vector3 right);
            center += Vector3.up * roadSurfaceClearance;

            vertices[row * 2] = transform.InverseTransformPoint(center - right * halfRoadWidth);
            vertices[row * 2 + 1] = transform.InverseTransformPoint(center + right * halfRoadWidth);
            uv[row * 2] = new Vector2(0f, t * lengthSegments * 0.25f);
            uv[row * 2 + 1] = new Vector2(1f, t * lengthSegments * 0.25f);
        }

        int triangleIndex = 0;
        for (int row = 0; row < lengthSegments; row++)
        {
            int a = row * 2;
            int b = a + 2;
            int c = b + 1;
            int d = a + 1;
            triangles[triangleIndex++] = a;
            triangles[triangleIndex++] = b;
            triangles[triangleIndex++] = c;
            triangles[triangleIndex++] = a;
            triangles[triangleIndex++] = c;
            triangles[triangleIndex++] = d;
        }

        ReplaceMesh(ref roadMesh, roadFilter, "Procedural Road");
        roadMesh.vertices = vertices;
        roadMesh.uv = uv;
        roadMesh.triangles = triangles;
        roadMesh.RecalculateNormals();
        roadMesh.RecalculateBounds();

        if (addRoadCollider)
        {
            if (!roadFilter.TryGetComponent(out MeshCollider roadCollider))
                roadCollider = roadFilter.gameObject.AddComponent<MeshCollider>();
            roadCollider.enabled = true;
            roadCollider.sharedMesh = null;
            roadCollider.sharedMesh = roadMesh;
        }
        else if (roadFilter.TryGetComponent(
            out MeshCollider existingRoadCollider))
        {
            existingRoadCollider.enabled = false;
        }
    }

    private float SampleHeight(float x, float z)
    {
        float frequency = noiseScale;
        float amplitude = 1f;
        float total = 0f;
        float normalization = 0f;
        float seedX = seed * 0.173f;
        float seedZ = seed * 0.317f;

        for (int octave = 0; octave < noiseOctaves; octave++)
        {
            float noise = Mathf.PerlinNoise(
                (x + seedX) * frequency,
                (z + seedZ) * frequency);
            total += (noise * 2f - 1f) * amplitude;
            normalization += amplitude;
            frequency *= 2f;
            amplitude *= persistence;
        }

        return normalization > 0f ? total / normalization * heightAmplitude : 0f;
    }

    private void UpdateTerrainMaterials(ProceduralFieldSystem fields)
    {
        if (terrainRenderer == null)
            return;

        if (fields == null)
        {
            terrainRenderer.sharedMaterial = terrainMaterial;
            return;
        }

        if (fieldMaterials.Count > 0
            && fieldVariantTemplate != terrainMaterial)
        {
            foreach (Material material in fieldMaterials)
                DestroySafely(material);
            fieldMaterials.Clear();
        }
        fieldVariantTemplate = terrainMaterial;

        while (fieldMaterials.Count < 5)
        {
            Material variant = terrainMaterial != null
                ? new Material(terrainMaterial)
                : CreateRuntimeMaterial(
                    $"Generated Terrain Field {fieldMaterials.Count + 1}",
                    Color.white);
            variant.name =
                $"Generated Terrain Field {fieldMaterials.Count + 1}";
            variant.hideFlags = HideFlags.HideAndDontSave;
            fieldMaterials.Add(variant);
        }

        var materials = new Material[5];
        PrepareFlatToonMaterial(fieldMaterials[0], nonFieldTerrainColor);
        materials[0] = fieldMaterials[0];
        for (int i = 0; i < 4; i++)
        {
            PrepareFlatToonMaterial(
                fieldMaterials[i + 1],
                fields.GetCropColor(i));
            materials[i + 1] = fieldMaterials[i + 1];
        }

        terrainRenderer.sharedMaterials = materials;
    }

    private static Material CreateRuntimeMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Quibli/Stylized Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader)
        {
            name = materialName,
            color = color,
            hideFlags = HideFlags.HideAndDontSave
        };
        return material;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        else
            material.color = color;
    }

    private static void PrepareFlatToonMaterial(
        Material material,
        Color color)
    {
        if (material == null)
            return;

        if (material.shader != null
            && material.shader.name == "Quibli/Stylized Lit")
        {
            material.SetTexture("_BaseMap", null);
            material.SetTexture("_DetailMap", null);
            material.SetFloat("_TextureImpact", 1f);
            material.SetFloat("_DetailMapImpact", 0f);
            material.SetFloat("_TextureBlendingMode", 0f);
            material.SetFloat("_SelfShadingSize", 0.42f);
            material.SetFloat("_LightContribution", 0.72f);
            material.SetFloat("_OverrideLightAttenuation", 1f);
            Color shadowColor =
                Color.Lerp(
                    color,
                    new Color(0.10f, 0.20f, 0.36f),
                    0.35f);
            shadowColor.a = 0.65f;
            material.SetColor(
                "_ShadowColor",
                shadowColor);
            material.EnableKeyword("DR_LIGHT_ATTENUATION");
            material.DisableKeyword("_TEXTUREBLENDINGMODE_ADD");
            material.EnableKeyword("_TEXTUREBLENDINGMODE_MULTIPLY");
            material.DisableKeyword("_DETAILMAPBLENDINGMODE_ADD");
            material.DisableKeyword("_DETAILMAPBLENDINGMODE_MULTIPLY");
            material.DisableKeyword("_DETAILMAPBLENDINGMODE_INTERPOLATE");
        }

        SetMaterialColor(material, color);
    }

    private static void DestroySafely(Object target)
    {
        if (target == null)
            return;
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private static void ReplaceMesh(ref Mesh mesh, MeshFilter filter, string meshName)
    {
        if (mesh == null)
        {
            mesh = new Mesh
            {
                name = meshName,
                hideFlags = HideFlags.DontSave
            };
        }
        else
        {
            mesh.Clear();
        }

        filter.sharedMesh = mesh;
    }
}
