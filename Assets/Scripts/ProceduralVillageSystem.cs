using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(ProceduralRibbonWorld))]
public sealed class ProceduralVillageSystem : MonoBehaviour
{
    private struct GrassExclusion
    {
        public Vector2 center;
        public Vector2 right;
        public Vector2 forward;
        public Vector2 halfSize;

        public bool Contains(Vector3 point, float padding)
        {
            Vector2 delta = new(point.x - center.x, point.z - center.y);
            return Mathf.Abs(Vector2.Dot(delta, right))
                       <= halfSize.x + padding
                && Mathf.Abs(Vector2.Dot(delta, forward))
                       <= halfSize.y + padding;
        }
    }

    [Header("Village Layout")]
    [Tooltip("Aynı seed aynı ev, ahır ve çatı dağılımını üretir.")]
    public int villageSeed = 42057;
    [Tooltip("İki komşu çiftlik parseli arasındaki en kısa yol mesafesi.")]
    [Min(8f)] public float minimumLotSpacing = 20f;
    [Tooltip("İki komşu çiftlik parseli arasındaki en uzun yol mesafesi.")]
    [Min(8f)] public float maximumLotSpacing = 34f;
    [Tooltip("Üretilen parsel noktalarının bina içermesi ihtimali. Küçük boşluklar köy siluetini doğal tutar.")]
    [Range(0f, 1f)] public float lotOccupancy = 0.88f;
    [Tooltip("Yolun başlangıç ve sonunda yapı üretilmeden bırakılan mesafe.")]
    [Min(0f)] public float roadEndMargin = 45f;

    [Header("Main Houses")]
    [Tooltip("Ana evin asfalt kenarından en yakın geri çekilme mesafesi.")]
    [Min(1f)] public float minimumHouseSetback = 7f;
    [Tooltip("Ana evin asfalt kenarından en uzak geri çekilme mesafesi.")]
    [Min(1f)] public float maximumHouseSetback = 13f;
    [Tooltip("Ana evlerin minimum taban genişliği.")]
    [Min(3f)] public float minimumHouseWidth = 7f;
    [Tooltip("Ana evlerin maksimum taban genişliği.")]
    [Min(3f)] public float maximumHouseWidth = 11f;
    [Tooltip("Ana evlerin minimum yol dışına doğru derinliği.")]
    [Min(4f)] public float minimumHouseDepth = 10f;
    [Tooltip("Ana evlerin maksimum yol dışına doğru derinliği.")]
    [Min(4f)] public float maximumHouseDepth = 17f;
    [Tooltip("Bir evin uzun ekseninin yola dik olma ihtimali. Kalan evler yola paralel yerleşir.")]
    [Range(0f, 1f)] public float perpendicularHouseChance = 0.68f;

    [Header("Farm Buildings")]
    [Tooltip("Bir ev parselinin arkasında büyük bir ahır veya depo bulunma ihtimali.")]
    [Range(0f, 1f)] public float barnChance = 0.76f;
    [Tooltip("Ahırların asfalt kenarından başlayabileceği en yakın mesafe.")]
    [Min(5f)] public float minimumBarnSetback = 24f;
    [Tooltip("Ahırların asfalt kenarından başlayabileceği en uzak mesafe.")]
    [Min(5f)] public float maximumBarnSetback = 42f;
    [Tooltip("Ana yapı veya ahıra ek küçük bir kulübe üretilme ihtimali.")]
    [Range(0f, 1f)] public float shedChance = 0.34f;

    [Header("Building Asset Kit")]
    [Tooltip("Açıkken basit prosedürel kutu evler yerine atanmış model havuzunu kullanır.")]
    public bool useBuildingPrefabs = true;
    [Tooltip("Yol boyunca rastgele seçilecek ev modelleri. Boşsa eski prosedürel evler kullanılır.")]
    public GameObject[] buildingPrefabs;
    [Tooltip("Turkuaz çatılı ev varyasyonları. Renk ağırlığı bu havuz ile kiremit havuzu arasında uygulanır.")]
    public GameObject[] turquoiseBuildingPrefabs;
    [Tooltip("Kiremit çatılı ev varyasyonları.")]
    public GameObject[] terracottaBuildingPrefabs;
    [Tooltip("Evlerin turkuaz çatılı havuzdan seçilme ihtimali. Kalanlar kiremit çatılı olur.")]
    [Range(0f, 1f)] public float turquoiseRoofChance = 0.68f;
    [Tooltip("Kenney'nin kiremit renk paleti. Kiremit seçilen evlerin atlası bununla değiştirilir.")]
    public Texture2D terracottaPaletteTexture;
    [Tooltip("FBX import ölçeğinden bağımsız otomatik boyutlandırmada izin verilen en küçük düzeltme çarpanı. Normalde değiştirmene gerek yok.")]
    [Min(0.001f)] public float minimumPrefabScale = 0.01f;
    [Tooltip("FBX import ölçeğinden bağımsız otomatik boyutlandırmada izin verilen en büyük düzeltme çarpanı. Farklı paket ölçeklerini desteklemek için geniş bırakılır.")]
    [Min(0.01f)] public float maximumPrefabScale = 100f;
    [Tooltip("Hesaplanan parsel boyuna göre tüm evleri birlikte büyütüp küçültür. 1 gerçek hedef ayak izidir.")]
    [Range(0.5f, 2f)] public float prefabFootprintMultiplier = 1f;
    [Tooltip("Geniş verandası olan modellerin araç yanında fazla küçük kalmasını önleyen minimum dünya yüksekliği.")]
    [Min(2.5f)] public float minimumPrefabWorldHeight = 4.2f;
    [Tooltip("Evin tabanının dalgalı terrain içine girmemesi için bırakılan temel yüksekliği.")]
    [Min(0.05f)] public float buildingFoundationClearance = 0.18f;
    [Tooltip("Evlerin yola kusursuz hizalanmaması için eklenen maksimum rastgele dönüş.")]
    [Range(0f, 20f)] public float prefabYawJitter = 5f;

    [Header("Paths And Fenced Gardens")]
    [Tooltip("Yol kenarından ev girişine uzatılacak Kenney patika/giriş modelleri.")]
    public GameObject[] drivewayPrefabs;
    [Tooltip("Ev giriş patikasının metre cinsinden genişliği.")]
    [Range(0.8f, 3f)] public float drivewayWidth = 1.6f;
    [Tooltip("Patikanın terrain kıvrımlarını takip etmek için bölüneceği en uzun parça. Küçük değer zemine daha sıkı oturur.")]
    [Range(1f, 4f)] public float drivewayMaximumSegmentLength = 1.8f;
    [Tooltip("Bahçe çevresinde kullanılacak çit modeli.")]
    public GameObject fencePrefab;
    [Tooltip("Bir ev parselinin çitli bahçeye sahip olma ihtimali.")]
    [Range(0f, 1f)] public float fencedGardenChance = 0.72f;
    [Tooltip("Bahçe girişinde patika için bırakılacak çit açıklığı.")]
    [Min(1.5f)] public float fenceGateWidth = 3.2f;
    [Tooltip("Çitli bahçelerin içinde kullanılacak küçük ağaç modelleri.")]
    public GameObject[] gardenTreePrefabs;
    [Tooltip("Çitli bir bahçede ağaç bulunma ihtimali.")]
    [Range(0f, 1f)] public float gardenTreeChance = 0.82f;
    [Tooltip("Çitli bahçelerde üretilecek minimum ve maksimum ağaç sayısı.")]
    public Vector2Int gardenTreesPerLot = new(2, 3);
    [Tooltip("Komşu evlerin arasındaki boşlukta ek ağaç kümesi oluşma ihtimali.")]
    [Range(0f, 1f)] public float interHouseTreeChance = 0.9f;
    [Tooltip("Her dolu parselin evler arası sınırında üretilecek minimum ve maksimum ağaç sayısı.")]
    public Vector2Int interHouseTreesPerLot = new(1, 2);
    [Tooltip("Evlerin yol tarafındaki parsel sınırında seyrek bir ağaç-çit sırası oluşma ihtimali.")]
    [Range(0f, 1f)] public float roadsideTreeLineChance = 0.74f;
    [Tooltip("Her yol tarafı parsel sınırında üretilecek normal ağaç sayısı.")]
    public Vector2Int roadsideTreesPerBoundary = new(1, 1);
    [Tooltip("Köy siluetine nadir uzun vurgular eklemek için kullanılacak büyük ağaç modelleri.")]
    public GameObject[] tallTreePrefabs;
    [Tooltip("Bir parsel sınırında normal ağaçlara ek olarak uzun bir ağaç oluşma ihtimali.")]
    [Range(0f, 1f)] public float rareTallTreeChance = 0.08f;
    [Tooltip("Nadir uzun ağaçların hedef dünya yüksekliği.")]
    public Vector2 tallTreeHeightRange = new(10f, 16f);
    [Tooltip("Bahçe ağaçlarının hedef dünya yüksekliği.")]
    public Vector2 gardenTreeHeightRange = new(3.5f, 6.5f);

    [Header("Palette")]
    [Tooltip("Quibli duvar preset'i. Boşsa Quibli/Stylized Lit shader'ı ile geçici materyal oluşturulur.")]
    public Material wallMaterialTemplate;
    [Tooltip("Quibli çatı preset'i. Boşsa Quibli/Stylized Lit shader'ı ile geçici materyal oluşturulur.")]
    public Material roofMaterialTemplate;
    public Color warmWallColor = new(0.72f, 0.63f, 0.48f);
    public Color lightWallColor = new(0.82f, 0.82f, 0.76f);
    public Color redRoofColor = new(0.55f, 0.085f, 0.045f);
    public Color grayRoofColor = new(0.29f, 0.29f, 0.27f);
    public Color darkRoofColor = new(0.105f, 0.12f, 0.115f);

    private readonly List<Material> runtimeMaterials = new();
    private readonly Dictionary<Material, Material>
        terracottaMaterialVariants = new();
    private readonly List<GrassExclusion> grassExclusions = new();
    private const float GrassExclusionCellSize = 24f;
    private readonly Dictionary<Vector2Int, List<int>> grassExclusionGrid =
        new();
    private ProceduralRibbonWorld world;
    private Mesh villageMesh;
    private MeshFilter villageFilter;
    private MeshRenderer villageRenderer;
    private Material runtimeWallTemplate;
    private Material runtimeRoofTemplate;
    private Transform prefabRoot;
    private bool regenerateQueued;

    public int GeneratedBuildingCount { get; private set; }

    private void OnEnable()
    {
        regenerateQueued = true;
    }

    private void OnValidate()
    {
        minimumLotSpacing = Mathf.Max(8f, minimumLotSpacing);
        maximumLotSpacing = Mathf.Max(minimumLotSpacing, maximumLotSpacing);
        maximumHouseSetback = Mathf.Max(
            minimumHouseSetback,
            maximumHouseSetback);
        maximumHouseWidth = Mathf.Max(minimumHouseWidth, maximumHouseWidth);
        maximumHouseDepth = Mathf.Max(minimumHouseDepth, maximumHouseDepth);
        maximumBarnSetback = Mathf.Max(
            minimumBarnSetback,
            maximumBarnSetback);
        maximumPrefabScale = Mathf.Max(
            minimumPrefabScale,
            maximumPrefabScale);
        fenceGateWidth = Mathf.Max(1.5f, fenceGateWidth);
        drivewayMaximumSegmentLength = Mathf.Clamp(
            drivewayMaximumSegmentLength,
            1f,
            4f);
        minimumPrefabWorldHeight = Mathf.Max(2.5f, minimumPrefabWorldHeight);
        buildingFoundationClearance = Mathf.Max(
            0.05f,
            buildingFoundationClearance);
        gardenTreesPerLot = new Vector2Int(
            Mathf.Max(0, Mathf.Min(gardenTreesPerLot.x, gardenTreesPerLot.y)),
            Mathf.Max(0, Mathf.Max(gardenTreesPerLot.x, gardenTreesPerLot.y)));
        interHouseTreesPerLot = new Vector2Int(
            Mathf.Max(0, Mathf.Min(
                interHouseTreesPerLot.x,
                interHouseTreesPerLot.y)),
            Mathf.Max(0, Mathf.Max(
                interHouseTreesPerLot.x,
                interHouseTreesPerLot.y)));
        roadsideTreesPerBoundary = new Vector2Int(
            Mathf.Max(0, Mathf.Min(
                roadsideTreesPerBoundary.x,
                roadsideTreesPerBoundary.y)),
            Mathf.Max(0, Mathf.Max(
                roadsideTreesPerBoundary.x,
                roadsideTreesPerBoundary.y)));
        tallTreeHeightRange = new Vector2(
            Mathf.Max(2f, Mathf.Min(
                tallTreeHeightRange.x,
                tallTreeHeightRange.y)),
            Mathf.Max(2f, Mathf.Max(
                tallTreeHeightRange.x,
                tallTreeHeightRange.y)));
        gardenTreeHeightRange = new Vector2(
            Mathf.Max(0.5f, Mathf.Min(
                gardenTreeHeightRange.x,
                gardenTreeHeightRange.y)),
            Mathf.Max(0.5f, Mathf.Max(
                gardenTreeHeightRange.x,
                gardenTreeHeightRange.y)));

        if (isActiveAndEnabled)
            regenerateQueued = true;
    }

    private void Update()
    {
        if (!regenerateQueued || !isActiveAndEnabled)
            return;
        regenerateQueued = false;
        Generate();
    }

    private void OnDestroy()
    {
        foreach (Material material in runtimeMaterials)
            DestroySafely(material);
        runtimeMaterials.Clear();
        foreach (Material material in terracottaMaterialVariants.Values)
            DestroySafely(material);
        terracottaMaterialVariants.Clear();
    }

    [ContextMenu("Regenerate Village")]
    public void Generate()
    {
        world = GetComponent<ProceduralRibbonWorld>();
        if (world == null || world.Spline == null || world.Spline.PointCount < 2)
            return;

        EnsureRenderer();
        grassExclusions.Clear();
        grassExclusionGrid.Clear();
        bool prefabMode = HasBuildingPrefabs();
        ClearPrefabBuildings();
        villageRenderer.enabled = !prefabMode;

        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new[]
        {
            new List<int>(),
            new List<int>(),
            new List<int>(),
            new List<int>(),
            new List<int>()
        };

        GeneratedBuildingCount = 0;
        float roadLength = Mathf.Max(
            1f,
            world.Spline.ApproximateLength(256));
        BuildSide(
            -1f,
            roadLength,
            new System.Random(villageSeed ^ 0x2A17B69D),
            vertices,
            uv,
            triangles);
        BuildSide(
            1f,
            roadLength,
            new System.Random(villageSeed ^ 0x51D3E42B),
            vertices,
            uv,
            triangles);

        if (prefabMode)
        {
            villageFilter.sharedMesh = null;
            return;
        }

        if (villageMesh == null)
        {
            villageMesh = new Mesh
            {
                name = "Procedural Sułoszowa Village",
                hideFlags = HideFlags.DontSave
            };
        }
        else
        {
            villageMesh.Clear();
        }

        villageMesh.indexFormat = vertices.Count > 65535
            ? IndexFormat.UInt32
            : IndexFormat.UInt16;
        villageMesh.SetVertices(vertices);
        villageMesh.SetUVs(0, uv);
        villageMesh.subMeshCount = triangles.Length;
        for (int i = 0; i < triangles.Length; i++)
            villageMesh.SetTriangles(triangles[i], i);
        villageMesh.RecalculateNormals();
        villageMesh.RecalculateBounds();
        villageFilter.sharedMesh = villageMesh;
        UpdateMaterials();
    }

    private void BuildSide(
        float side,
        float roadLength,
        System.Random random,
        List<Vector3> vertices,
        List<Vector2> uv,
        List<int>[] triangles)
    {
        float cursor = roadEndMargin + NextRange(random, 0f, 12f);
        float roadHalfWidth = world.Spline.roadWidth * 0.5f;

        while (cursor < roadLength - roadEndMargin)
        {
            float lotSpacing = NextRange(
                random,
                minimumLotSpacing,
                maximumLotSpacing);
            cursor += lotSpacing;
            if (cursor >= roadLength - roadEndMargin)
                break;
            if (NextFloat(random) > lotOccupancy)
                continue;

            float t = Mathf.Clamp01(cursor / roadLength);
            float houseSetback = NextRange(
                random,
                minimumHouseSetback,
                maximumHouseSetback);
            float houseLateral = side * (roadHalfWidth + houseSetback);
            float houseWidth = NextRange(
                random,
                minimumHouseWidth,
                maximumHouseWidth);
            float houseDepth = NextRange(
                random,
                minimumHouseDepth,
                maximumHouseDepth);
            float houseHeight = NextRange(random, 4.8f, 7.6f);
            bool perpendicular =
                NextFloat(random) < perpendicularHouseChance;

            world.SampleSurface(
                t,
                houseLateral,
                out Vector3 houseSurface,
                out _);
            world.Spline.GetFrame(
                t,
                out _,
                out Vector3 roadForward,
                out Vector3 roadRight);
            Vector3 flatRoadForward = Vector3.ProjectOnPlane(
                roadForward,
                Vector3.up).normalized;
            Vector3 outward = roadRight * side;
            Quaternion houseRotation = Quaternion.LookRotation(
                perpendicular ? -outward : flatRoadForward,
                Vector3.up);

            AddBuilding(
                houseSurface,
                houseRotation,
                new Vector3(houseWidth, houseHeight, houseDepth),
                NextFloat(random) < 0.58f ? 1 : 0,
                SelectRoofMaterial(random),
                random,
                vertices,
                uv,
                triangles);

            if (HasBuildingPrefabs())
            {
                PlaceDriveway(
                    t,
                    side,
                    roadHalfWidth,
                    Mathf.Abs(houseLateral),
                    random);
                bool hasFence = fencePrefab != null
                             && NextFloat(random) < fencedGardenChance;
                if (hasFence)
                {
                    PlaceFencedGarden(
                        t,
                        side,
                        roadLength,
                        roadHalfWidth,
                        Mathf.Abs(houseLateral),
                        houseWidth,
                        lotSpacing,
                        random);
                }
                else if (NextFloat(random) < 0.72f)
                {
                    PlaceGardenTree(
                        Mathf.Clamp01(t + NextRange(
                            random,
                            -5f / roadLength,
                            5f / roadLength)),
                        side * (Mathf.Abs(houseLateral) + 4.5f),
                        random);
                }

                if (NextFloat(random) < interHouseTreeChance)
                {
                    int interHouseCount = random.Next(
                        interHouseTreesPerLot.x,
                        interHouseTreesPerLot.y + 1);
                    float boundaryOffset = lotSpacing * 0.38f / roadLength;
                    for (int treeIndex = 0;
                         treeIndex < interHouseCount;
                         treeIndex++)
                    {
                        float direction = treeIndex % 2 == 0 ? 1f : -1f;
                        float treeT = Mathf.Clamp01(
                            t + direction * boundaryOffset
                            + NextRange(random, -1.5f, 1.5f) / roadLength);
                        float treeLateral = side * NextRange(
                            random,
                            Mathf.Abs(houseLateral) + 1.5f,
                            Mathf.Abs(houseLateral) + 5f);
                        PlaceGardenTree(treeT, treeLateral, random);
                    }
                }

                if (NextFloat(random) < roadsideTreeLineChance)
                {
                    float boundaryT = Mathf.Clamp01(
                        t + lotSpacing * 0.4f / roadLength);
                    int roadsideCount = random.Next(
                        roadsideTreesPerBoundary.x,
                        roadsideTreesPerBoundary.y + 1);
                    for (int treeIndex = 0;
                         treeIndex < roadsideCount;
                         treeIndex++)
                    {
                        float treeT = Mathf.Clamp01(
                            boundaryT + NextRange(random, -1.8f, 1.8f)
                            / roadLength);
                        float treeLateral = side * (
                            roadHalfWidth + NextRange(random, 7f, 10.5f));
                        PlaceGardenTree(treeT, treeLateral, random);
                    }

                    if (NextFloat(random) < rareTallTreeChance)
                    {
                        float tallLateral = side * (
                            roadHalfWidth + NextRange(random, 8.5f, 12.5f));
                        PlaceTree(
                            tallTreePrefabs,
                            boundaryT,
                            tallLateral,
                            tallTreeHeightRange,
                            random);
                    }
                }
            }

            if (!HasBuildingPrefabs() && NextFloat(random) < barnChance)
            {
                float barnSetback = NextRange(
                    random,
                    minimumBarnSetback,
                    maximumBarnSetback);
                float barnLateral = side * (roadHalfWidth + barnSetback);
                float barnT = Mathf.Clamp01(
                    t + NextRange(random, -5f, 5f) / roadLength);
                world.SampleSurface(
                    barnT,
                    barnLateral,
                    out Vector3 barnSurface,
                    out _);
                float barnWidth = NextRange(random, 9f, 15f);
                float barnDepth = NextRange(random, 15f, 28f);
                float barnHeight = NextRange(random, 4.2f, 6.5f);
                Quaternion barnRotation = Quaternion.LookRotation(
                    outward,
                    Vector3.up);
                AddBuilding(
                    barnSurface,
                    barnRotation,
                    new Vector3(barnWidth, barnHeight, barnDepth),
                    NextFloat(random) < 0.3f ? 0 : 1,
                    NextFloat(random) < 0.72f ? 3 : 4,
                    random,
                    vertices,
                    uv,
                    triangles);
            }

            if (!HasBuildingPrefabs() && NextFloat(random) < shedChance)
            {
                float shedLateral = side * (
                    roadHalfWidth + NextRange(random, 17f, 32f));
                float shedT = Mathf.Clamp01(
                    t + NextRange(random, -7f, 7f) / roadLength);
                world.SampleSurface(
                    shedT,
                    shedLateral,
                    out Vector3 shedSurface,
                    out _);
                AddBuilding(
                    shedSurface,
                    Quaternion.LookRotation(outward, Vector3.up),
                    new Vector3(
                        NextRange(random, 4f, 7f),
                        NextRange(random, 2.8f, 4.3f),
                        NextRange(random, 6f, 11f)),
                    0,
                    NextFloat(random) < 0.5f ? 3 : 4,
                    random,
                    vertices,
                    uv,
                    triangles);
            }
        }
    }

    private void AddBuilding(
        Vector3 surfacePosition,
        Quaternion rotation,
        Vector3 size,
        int wallMaterial,
        int roofMaterial,
        System.Random random,
        List<Vector3> vertices,
        List<Vector2> uv,
        List<int>[] triangles)
    {
        Vector3 right3 = rotation * Vector3.right;
        Vector3 forward3 = rotation * Vector3.forward;
        AddGrassExclusion(new GrassExclusion
        {
            center = new Vector2(surfacePosition.x, surfacePosition.z),
            right = new Vector2(right3.x, right3.z).normalized,
            forward = new Vector2(forward3.x, forward3.z).normalized,
            halfSize = new Vector2(size.x, size.z) * 0.5f
        });

        if (HasBuildingPrefabs())
        {
            PlaceBuildingPrefab(surfacePosition, rotation, size, random);
            GeneratedBuildingCount++;
            return;
        }

        float roofHeight = Mathf.Clamp(size.x * 0.34f, 1.2f, 3.2f);
        Vector3 bodyCenter = surfacePosition
                           + Vector3.up * (size.y * 0.5f + 0.08f);
        AddBox(
            bodyCenter,
            rotation,
            size,
            wallMaterial,
            vertices,
            uv,
            triangles);
        AddGableRoof(
            surfacePosition + Vector3.up * (size.y + 0.08f),
            rotation,
            size.x * 1.12f,
            size.z * 1.08f,
            roofHeight,
            roofMaterial,
            vertices,
            uv,
            triangles);
        GeneratedBuildingCount++;
    }

    public bool IsGrassBlocked(Vector3 worldPosition, float padding)
    {
        Vector2Int cell = GrassCell(new Vector2(
            worldPosition.x,
            worldPosition.z));
        if (!grassExclusionGrid.TryGetValue(cell, out List<int> indices))
            return false;

        for (int i = 0; i < indices.Count; i++)
        {
            if (grassExclusions[indices[i]].Contains(worldPosition, padding))
                return true;
        }
        return false;
    }

    private void AddGrassExclusion(GrassExclusion exclusion)
    {
        int index = grassExclusions.Count;
        grassExclusions.Add(exclusion);
        float radius = exclusion.halfSize.magnitude + 1f;
        Vector2Int minimum = GrassCell(exclusion.center - Vector2.one * radius);
        Vector2Int maximum = GrassCell(exclusion.center + Vector2.one * radius);
        for (int y = minimum.y; y <= maximum.y; y++)
        {
            for (int x = minimum.x; x <= maximum.x; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!grassExclusionGrid.TryGetValue(
                        cell,
                        out List<int> indices))
                {
                    indices = new List<int>();
                    grassExclusionGrid.Add(cell, indices);
                }
                indices.Add(index);
            }
        }
    }

    private static Vector2Int GrassCell(Vector2 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / GrassExclusionCellSize),
            Mathf.FloorToInt(position.y / GrassExclusionCellSize));
    }

    private bool HasBuildingPrefabs()
    {
        if (!useBuildingPrefabs)
            return false;

        return HasAny(buildingPrefabs)
            || HasAny(turquoiseBuildingPrefabs)
            || HasAny(terracottaBuildingPrefabs);
    }

    private void ClearPrefabBuildings()
    {
        Transform existing = transform.Find("Generated Building Prefabs");
        if (existing != null)
            DestroySafely(existing.gameObject);

        var rootObject = new GameObject("Generated Building Prefabs")
        {
            hideFlags = HideFlags.DontSave
        };
        prefabRoot = rootObject.transform;
        prefabRoot.SetParent(transform, false);
    }

    private void PlaceBuildingPrefab(
        Vector3 surfacePosition,
        Quaternion rotation,
        Vector3 targetSize,
        System.Random random)
    {
        bool useTerracotta = NextFloat(random) > turquoiseRoofChance;
        GameObject prefab = PickRandom(buildingPrefabs, random);
        if (prefab == null)
            prefab = PickRandom(
                useTerracotta
                    ? terracottaBuildingPrefabs
                    : turquoiseBuildingPrefabs,
                random);
        if (prefab == null)
            prefab = PickRandom(
                useTerracotta
                    ? terracottaBuildingPrefabs
                    : turquoiseBuildingPrefabs,
                random);
        if (prefab == null)
            return;
        float yaw = NextRange(random, -prefabYawJitter, prefabYawJitter);
        GameObject instance = Instantiate(
            prefab,
            surfacePosition,
            rotation * Quaternion.Euler(0f, yaw, 0f),
            prefabRoot);
        instance.name = prefab.name;
        instance.hideFlags = HideFlags.DontSave;
        if (useTerracotta && terracottaPaletteTexture != null)
            ApplyTerracottaPalette(instance);

        if (!TryGetRendererBounds(instance, out Bounds bounds))
            return;

        float sourceFootprint = Mathf.Max(
            0.01f,
            Mathf.Max(bounds.size.x, bounds.size.z));
        // The parcel depth includes garden/yard allowance. Scaling a suburban
        // house to that longer axis makes it tower over the road and vehicle,
        // so the road-facing building width is the authoritative dimension.
        float targetFootprint = targetSize.x;
        float scale = Mathf.Clamp(
            targetFootprint * prefabFootprintMultiplier / sourceFootprint,
            minimumPrefabScale,
            maximumPrefabScale);
        scale = Mathf.Max(
            scale,
            minimumPrefabWorldHeight / Mathf.Max(0.01f, bounds.size.y));
        instance.transform.localScale *= scale;

        if (TryGetRendererBounds(instance, out bounds))
        {
            instance.transform.position += Vector3.up
                * (surfacePosition.y + buildingFoundationClearance - bounds.min.y);
        }

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    private void ApplyTerracottaPalette(GameObject instance)
    {
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                if (source == null)
                    continue;
                if (!terracottaMaterialVariants.TryGetValue(
                    source,
                    out Material variant))
                {
                    variant = new Material(source)
                    {
                        name = source.name + " Terracotta",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    if (variant.HasProperty("_BaseMap"))
                        variant.SetTexture(
                            "_BaseMap",
                            terracottaPaletteTexture);
                    if (variant.HasProperty("_MainTex"))
                        variant.SetTexture(
                            "_MainTex",
                            terracottaPaletteTexture);
                    terracottaMaterialVariants.Add(source, variant);
                }
                materials[i] = variant;
            }
            renderer.sharedMaterials = materials;
        }
    }

    private void PlaceDriveway(
        float t,
        float side,
        float roadHalfWidth,
        float houseLateral,
        System.Random random)
    {
        GameObject prefab = PickRandom(drivewayPrefabs, random);
        if (prefab == null)
            return;

        float startLateral = side * (roadHalfWidth - 0.4f);
        float endLateral = side * Mathf.Max(
            roadHalfWidth + 1.5f,
            houseLateral - 2.2f);
        PlaceLinearPrefab(
            prefab,
            t,
            startLateral,
            t,
            endLateral,
            drivewayWidth,
            0.012f);
    }

    private void PlaceFencedGarden(
        float t,
        float side,
        float roadLength,
        float roadHalfWidth,
        float houseLateral,
        float houseWidth,
        float lotSpacing,
        System.Random random)
    {
        float halfAlong = Mathf.Clamp(lotSpacing * 0.29f, 5f, 8.5f);
        float halfT = halfAlong / roadLength;
        float halfGateT = fenceGateWidth * 0.5f / roadLength;
        float inner = side * (roadHalfWidth + 3.6f);
        float outer = side * (houseLateral + Mathf.Max(5.5f, houseWidth * 0.58f));

        PlaceFenceLine(
            fencePrefab,
            t - halfT,
            inner,
            t - halfGateT,
            inner,
            0.03f);
        PlaceFenceLine(
            fencePrefab,
            t + halfGateT,
            inner,
            t + halfT,
            inner,
            0.03f);
        PlaceFenceLine(
            fencePrefab,
            t - halfT,
            outer,
            t + halfT,
            outer,
            0.03f);
        PlaceFenceLine(
            fencePrefab,
            t - halfT,
            inner,
            t - halfT,
            outer,
            0.03f);
        PlaceFenceLine(
            fencePrefab,
            t + halfT,
            inner,
            t + halfT,
            outer,
            0.03f);

        if (NextFloat(random) < gardenTreeChance)
        {
            int treeCount = random.Next(
                gardenTreesPerLot.x,
                gardenTreesPerLot.y + 1);
            for (int i = 0; i < treeCount; i++)
            {
                float treeT = Mathf.Clamp01(
                    t + NextRange(random, -halfT * 0.7f, halfT * 0.7f));
                float treeLateral = side * NextRange(
                    random,
                    houseLateral + 2.8f,
                    Mathf.Abs(outer) - 1.2f);
                PlaceGardenTree(treeT, treeLateral, random);
            }
        }
    }

    private void PlaceFenceLine(
        GameObject prefab,
        float startT,
        float startLateral,
        float endT,
        float endLateral,
        float clearance)
    {
        world.SampleSurface(
            Mathf.Clamp01(startT),
            startLateral,
            out Vector3 start,
            out _);
        world.SampleSurface(
            Mathf.Clamp01(endT),
            endLateral,
            out Vector3 end,
            out _);
        float horizontalLength = Vector2.Distance(
            new Vector2(start.x, start.z),
            new Vector2(end.x, end.z));
        int segmentCount = Mathf.Max(1, Mathf.CeilToInt(horizontalLength / 2.6f));
        for (int i = 0; i < segmentCount; i++)
        {
            float a = i / (float)segmentCount;
            float b = (i + 1f) / segmentCount;
            PlaceFenceSegment(
                prefab,
                Mathf.Lerp(startT, endT, a),
                Mathf.Lerp(startLateral, endLateral, a),
                Mathf.Lerp(startT, endT, b),
                Mathf.Lerp(startLateral, endLateral, b),
                clearance);
        }
    }

    private void PlaceFenceSegment(
        GameObject prefab,
        float startT,
        float startLateral,
        float endT,
        float endLateral,
        float clearance)
    {
        world.SampleSurface(startT, startLateral, out Vector3 start, out _);
        world.SampleSurface(endT, endLateral, out Vector3 end, out _);
        Vector3 flatDirection = end - start;
        flatDirection.y = 0f;
        float length = flatDirection.magnitude;
        if (length < 0.2f)
            return;

        GameObject instance = Instantiate(
            prefab,
            (start + end) * 0.5f,
            Quaternion.LookRotation(flatDirection.normalized, Vector3.up)
                * Quaternion.Euler(0f, 90f, 0f),
            prefabRoot);
        instance.name = prefab.name;
        instance.hideFlags = HideFlags.DontSave;
        if (!TryGetRendererBounds(instance, out Bounds bounds))
            return;

        float sourceLength = ProjectedSize(
            bounds,
            flatDirection.normalized);
        float uniformScale = length / Mathf.Max(0.01f, sourceLength);
        instance.transform.localScale *= uniformScale;
        if (TryGetRendererBounds(instance, out bounds))
        {
            instance.transform.position += Vector3.up
                * (((start.y + end.y) * 0.5f) + clearance - bounds.min.y);
        }
        AddFenceBoxCollider(instance);
    }

    private static void AddFenceBoxCollider(GameObject instance)
    {
        MeshFilter[] filters = instance.GetComponentsInChildren<MeshFilter>();
        bool hasBounds = false;
        Bounds localBounds = default;
        foreach (MeshFilter filter in filters)
        {
            if (filter.sharedMesh == null)
                continue;
            Bounds meshBounds = filter.sharedMesh.bounds;
            Vector3 center = meshBounds.center;
            Vector3 extents = meshBounds.extents;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 meshCorner = center + Vector3.Scale(
                            extents,
                            new Vector3(x, y, z));
                        Vector3 localCorner = instance.transform.InverseTransformPoint(
                            filter.transform.TransformPoint(meshCorner));
                        if (!hasBounds)
                        {
                            localBounds = new Bounds(localCorner, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(localCorner);
                        }
                    }
                }
            }
        }
        if (!hasBounds)
            return;

        BoxCollider collider = instance.GetComponent<BoxCollider>();
        if (collider == null)
            collider = instance.AddComponent<BoxCollider>();
        collider.center = localBounds.center;
        collider.size = new Vector3(
            Mathf.Max(0.08f, localBounds.size.x),
            Mathf.Max(0.45f, localBounds.size.y),
            Mathf.Max(0.08f, localBounds.size.z));
        collider.isTrigger = false;
    }

    private void PlaceLinearPrefab(
        GameObject prefab,
        float startT,
        float startLateral,
        float endT,
        float endLateral,
        float targetWidth,
        float clearance)
    {
        if (prefab == null)
            return;

        SampleLinearSurfacePoint(
            startT,
            startLateral,
            clearance,
            out Vector3 fullStart);
        SampleLinearSurfacePoint(
            endT,
            endLateral,
            clearance,
            out Vector3 fullEnd);
        float horizontalLength = Vector2.Distance(
            new Vector2(fullStart.x, fullStart.z),
            new Vector2(fullEnd.x, fullEnd.z));
        if (horizontalLength < 0.3f)
            return;

        int segmentCount = Mathf.Max(
            1,
            Mathf.CeilToInt(
                horizontalLength
                / Mathf.Max(1f, drivewayMaximumSegmentLength)));
        for (int segment = 0; segment < segmentCount; segment++)
        {
            float a = segment / (float)segmentCount;
            float b = (segment + 1f) / segmentCount;
            SampleLinearSurfacePoint(
                Mathf.Lerp(startT, endT, a),
                Mathf.Lerp(startLateral, endLateral, a),
                clearance,
                out Vector3 start);
            SampleLinearSurfacePoint(
                Mathf.Lerp(startT, endT, b),
                Mathf.Lerp(startLateral, endLateral, b),
                clearance,
                out Vector3 end);
            PlaceLinearPrefabSegment(
                prefab,
                start,
                end,
                targetWidth,
                clearance,
                segment);
        }
    }

    private void SampleLinearSurfacePoint(
        float t,
        float lateral,
        float clearance,
        out Vector3 point)
    {
        t = Mathf.Clamp01(t);
        world.SampleSurface(t, lateral, out point, out _);
        float roadHalfWidth = world.Spline.roadWidth * 0.5f;
        if (Mathf.Abs(lateral) > roadHalfWidth + 0.75f)
            return;

        world.Spline.GetFrame(
            t,
            out Vector3 roadCenter,
            out _,
            out Vector3 roadRight);
        point = roadCenter + roadRight * lateral;
        // The final local-up correction adds clearance. Subtract it here so
        // the first path slab meets the asphalt instead of hovering above it.
        point.y = roadCenter.y + world.roadSurfaceClearance - clearance + 0.003f;
    }

    private void PlaceLinearPrefabSegment(
        GameObject prefab,
        Vector3 start,
        Vector3 end,
        float targetWidth,
        float clearance,
        int segmentIndex)
    {
        Vector3 direction = end - start;
        float length = direction.magnitude;
        if (length < 0.2f)
            return;

        GameObject instance = Instantiate(
            prefab,
            (start + end) * 0.5f,
            Quaternion.LookRotation(direction.normalized, Vector3.up),
            prefabRoot);
        instance.name = $"{prefab.name} {segmentIndex + 1}";
        instance.hideFlags = HideFlags.DontSave;
        if (!TryGetRendererBounds(instance, out Bounds bounds))
            return;

        float forwardSize = ProjectedSize(bounds, instance.transform.forward);
        float rightSize = ProjectedSize(bounds, instance.transform.right);
        if (rightSize > forwardSize * 1.2f)
        {
            instance.transform.Rotate(0f, 90f, 0f, Space.Self);
            TryGetRendererBounds(instance, out bounds);
            forwardSize = ProjectedSize(
                bounds,
                instance.transform.forward);
            rightSize = ProjectedSize(bounds, instance.transform.right);
        }

        Vector3 scale = instance.transform.localScale;
        scale.z *= length / Mathf.Max(0.01f, forwardSize);
        if (targetWidth > 0f)
            scale.x *= targetWidth / Mathf.Max(0.01f, rightSize);
        instance.transform.localScale = scale;

        if (TryGetLowestOffsetAlongUp(instance, out float lowestOffset))
        {
            // Renderer.bounds.min.y is a world AABB value. On a slope its
            // downhill corner pulls the whole path into the air. Measuring
            // along the prefab's own up axis keeps the pitched slab grounded.
            instance.transform.position += instance.transform.up
                * (clearance - lowestOffset);
        }

        AddMeshColliders(instance);
    }

    private static bool TryGetLowestOffsetAlongUp(
        GameObject instance,
        out float lowestOffset)
    {
        lowestOffset = float.PositiveInfinity;
        Vector3 origin = instance.transform.position;
        Vector3 up = instance.transform.up;
        bool found = false;
        foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null)
                continue;
            Bounds bounds = filter.sharedMesh.bounds;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 localCorner = center + Vector3.Scale(
                            extents,
                            new Vector3(x, y, z));
                        Vector3 worldCorner = filter.transform.TransformPoint(
                            localCorner);
                        lowestOffset = Mathf.Min(
                            lowestOffset,
                            Vector3.Dot(worldCorner - origin, up));
                        found = true;
                    }
                }
            }
        }
        return found;
    }

    private static void AddMeshColliders(GameObject instance)
    {
        foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null
                || filter.TryGetComponent(out MeshCollider _))
            {
                continue;
            }
            MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
        }
    }

    private void PlaceGardenTree(
        float t,
        float lateral,
        System.Random random)
    {
        PlaceTree(
            gardenTreePrefabs,
            t,
            lateral,
            gardenTreeHeightRange,
            random);
    }

    private void PlaceTree(
        GameObject[] prefabs,
        float t,
        float lateral,
        Vector2 heightRange,
        System.Random random)
    {
        GameObject prefab = PickRandom(prefabs, random);
        if (prefab == null)
            return;

        world.SampleSurface(t, lateral, out Vector3 surface, out Vector3 normal);
        GameObject instance = Instantiate(
            prefab,
            surface,
            Quaternion.FromToRotation(Vector3.up, normal)
                * Quaternion.Euler(0f, NextRange(random, 0f, 360f), 0f),
            prefabRoot);
        instance.name = prefab.name;
        instance.hideFlags = HideFlags.DontSave;
        if (!TryGetRendererBounds(instance, out Bounds bounds))
            return;

        float height = NextRange(
            random,
            heightRange.x,
            heightRange.y);
        instance.transform.localScale *= height
            / Mathf.Max(0.01f, bounds.size.y);
        if (TryGetRendererBounds(instance, out bounds))
        {
            instance.transform.position += Vector3.up
                * (surface.y + 0.04f - bounds.min.y);
        }
    }

    private static float ProjectedSize(Bounds bounds, Vector3 axis)
    {
        axis = new Vector3(
            Mathf.Abs(axis.x),
            Mathf.Abs(axis.y),
            Mathf.Abs(axis.z));
        return 2f * Vector3.Dot(bounds.extents, axis);
    }

    private static bool HasAny(GameObject[] prefabs)
    {
        if (prefabs == null)
            return false;
        foreach (GameObject prefab in prefabs)
        {
            if (prefab != null)
                return true;
        }
        return false;
    }

    private static GameObject PickRandom(
        GameObject[] prefabs,
        System.Random random)
    {
        if (prefabs == null)
            return null;
        var available = new List<GameObject>();
        foreach (GameObject prefab in prefabs)
        {
            if (prefab != null)
                available.Add(prefab);
        }
        return available.Count == 0
            ? null
            : available[random.Next(available.Count)];
    }

    private static bool TryGetRendererBounds(
        GameObject owner,
        out Bounds bounds)
    {
        Renderer[] renderers = owner.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return true;
    }

    private void AddBox(
        Vector3 center,
        Quaternion rotation,
        Vector3 size,
        int materialIndex,
        List<Vector3> vertices,
        List<Vector2> uv,
        List<int>[] triangles)
    {
        Vector3 h = size * 0.5f;
        AddQuad(center, rotation,
            new(-h.x, -h.y, -h.z), new(h.x, -h.y, -h.z),
            new(h.x, h.y, -h.z), new(-h.x, h.y, -h.z),
            Vector3.back, materialIndex, vertices, uv, triangles);
        AddQuad(center, rotation,
            new(h.x, -h.y, h.z), new(-h.x, -h.y, h.z),
            new(-h.x, h.y, h.z), new(h.x, h.y, h.z),
            Vector3.forward, materialIndex, vertices, uv, triangles);
        AddQuad(center, rotation,
            new(-h.x, -h.y, h.z), new(-h.x, -h.y, -h.z),
            new(-h.x, h.y, -h.z), new(-h.x, h.y, h.z),
            Vector3.left, materialIndex, vertices, uv, triangles);
        AddQuad(center, rotation,
            new(h.x, -h.y, -h.z), new(h.x, -h.y, h.z),
            new(h.x, h.y, h.z), new(h.x, h.y, -h.z),
            Vector3.right, materialIndex, vertices, uv, triangles);
        AddQuad(center, rotation,
            new(-h.x, h.y, -h.z), new(h.x, h.y, -h.z),
            new(h.x, h.y, h.z), new(-h.x, h.y, h.z),
            Vector3.up, materialIndex, vertices, uv, triangles);
    }

    private void AddGableRoof(
        Vector3 baseCenter,
        Quaternion rotation,
        float width,
        float depth,
        float height,
        int materialIndex,
        List<Vector3> vertices,
        List<Vector2> uv,
        List<int>[] triangles)
    {
        float x = width * 0.5f;
        float z = depth * 0.5f;
        Vector3 ridgeFront = new(0f, height, -z);
        Vector3 ridgeBack = new(0f, height, z);

        AddQuad(baseCenter, rotation,
            new(-x, 0f, -z), ridgeFront, ridgeBack, new(-x, 0f, z),
            new Vector3(-height, x, 0f).normalized,
            materialIndex, vertices, uv, triangles);
        AddQuad(baseCenter, rotation,
            ridgeFront, new(x, 0f, -z), new(x, 0f, z), ridgeBack,
            new Vector3(height, x, 0f).normalized,
            materialIndex, vertices, uv, triangles);
        AddTriangle(baseCenter, rotation,
            new(-x, 0f, -z), new(x, 0f, -z), ridgeFront,
            Vector3.back, materialIndex, vertices, uv, triangles);
        AddTriangle(baseCenter, rotation,
            new(x, 0f, z), new(-x, 0f, z), ridgeBack,
            Vector3.forward, materialIndex, vertices, uv, triangles);
    }

    private void AddQuad(
        Vector3 center,
        Quaternion rotation,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        Vector3 expectedNormal,
        int materialIndex,
        List<Vector3> vertices,
        List<Vector2> uv,
        List<int>[] triangles)
    {
        int start = vertices.Count;
        Vector3 wa = center + rotation * a;
        Vector3 wb = center + rotation * b;
        Vector3 wc = center + rotation * c;
        Vector3 wd = center + rotation * d;
        vertices.Add(transform.InverseTransformPoint(wa));
        vertices.Add(transform.InverseTransformPoint(wb));
        vertices.Add(transform.InverseTransformPoint(wc));
        vertices.Add(transform.InverseTransformPoint(wd));
        uv.Add(new(0f, 0f));
        uv.Add(new(1f, 0f));
        uv.Add(new(1f, 1f));
        uv.Add(new(0f, 1f));

        bool correctWinding = Vector3.Dot(
            Vector3.Cross(wb - wa, wc - wa),
            rotation * expectedNormal) >= 0f;
        List<int> target = triangles[materialIndex];
        if (correctWinding)
        {
            target.Add(start); target.Add(start + 1); target.Add(start + 2);
            target.Add(start); target.Add(start + 2); target.Add(start + 3);
        }
        else
        {
            target.Add(start); target.Add(start + 2); target.Add(start + 1);
            target.Add(start); target.Add(start + 3); target.Add(start + 2);
        }
    }

    private void AddTriangle(
        Vector3 center,
        Quaternion rotation,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 expectedNormal,
        int materialIndex,
        List<Vector3> vertices,
        List<Vector2> uv,
        List<int>[] triangles)
    {
        int start = vertices.Count;
        Vector3 wa = center + rotation * a;
        Vector3 wb = center + rotation * b;
        Vector3 wc = center + rotation * c;
        vertices.Add(transform.InverseTransformPoint(wa));
        vertices.Add(transform.InverseTransformPoint(wb));
        vertices.Add(transform.InverseTransformPoint(wc));
        uv.Add(new(0f, 0f));
        uv.Add(new(1f, 0f));
        uv.Add(new(0.5f, 1f));

        List<int> target = triangles[materialIndex];
        if (Vector3.Dot(
            Vector3.Cross(wb - wa, wc - wa),
            rotation * expectedNormal) >= 0f)
        {
            target.Add(start); target.Add(start + 1); target.Add(start + 2);
        }
        else
        {
            target.Add(start); target.Add(start + 2); target.Add(start + 1);
        }
    }

    private int SelectRoofMaterial(System.Random random)
    {
        float value = NextFloat(random);
        if (value < 0.34f)
            return 2;
        return value < 0.78f ? 3 : 4;
    }

    private void EnsureRenderer()
    {
        Transform child = transform.Find("Generated Village");
        if (child == null)
        {
            var childObject = new GameObject("Generated Village");
            child = childObject.transform;
            child.SetParent(transform, false);
        }

        if (!child.TryGetComponent(out villageFilter))
            villageFilter = child.gameObject.AddComponent<MeshFilter>();
        if (!child.TryGetComponent(out villageRenderer))
            villageRenderer = child.gameObject.AddComponent<MeshRenderer>();
    }

    private void UpdateMaterials()
    {
        Color[] colors =
        {
            warmWallColor,
            lightWallColor,
            redRoofColor,
            grayRoofColor,
            darkRoofColor
        };

        if (runtimeMaterials.Count > 0
            && (runtimeWallTemplate != wallMaterialTemplate
                || runtimeRoofTemplate != roofMaterialTemplate))
        {
            foreach (Material material in runtimeMaterials)
                DestroySafely(material);
            runtimeMaterials.Clear();
        }
        runtimeWallTemplate = wallMaterialTemplate;
        runtimeRoofTemplate = roofMaterialTemplate;

        while (runtimeMaterials.Count < colors.Length)
        {
            int index = runtimeMaterials.Count;
            Material template = index < 2
                ? wallMaterialTemplate
                : roofMaterialTemplate;
            Material material;
            if (template != null)
            {
                material = new Material(template);
            }
            else
            {
                Shader shader = Shader.Find("Quibli/Stylized Lit");
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                material = new Material(shader);
            }

            material.name = $"Generated Village Material {index + 1}";
            material.hideFlags = HideFlags.HideAndDontSave;
            runtimeMaterials.Add(material);
        }

        for (int i = 0; i < colors.Length; i++)
        {
            Material material = runtimeMaterials[i];
            if (material.shader != null
                && material.shader.name == "Quibli/Stylized Lit")
            {
                material.SetTexture("_BaseMap", null);
                material.SetFloat("_TextureImpact", 1f);
                material.SetFloat("_TextureBlendingMode", 0f);
                material.SetFloat("_SelfShadingSize", 0.4f);
                material.SetFloat("_LightContribution", 0.76f);
                material.SetFloat("_OverrideLightAttenuation", 1f);
                Color shadowColor = Color.Lerp(
                    colors[i],
                    new Color(0.12f, 0.20f, 0.36f),
                    0.38f);
                shadowColor.a = 0.7f;
                material.SetColor("_ShadowColor", shadowColor);
                material.EnableKeyword("DR_LIGHT_ATTENUATION");
                material.DisableKeyword("_TEXTUREBLENDINGMODE_ADD");
                material.EnableKeyword("_TEXTUREBLENDINGMODE_MULTIPLY");
            }

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", colors[i]);
            else
                material.color = colors[i];
        }
        villageRenderer.sharedMaterials = runtimeMaterials.ToArray();
    }

    private static float NextFloat(System.Random random)
    {
        return (float)random.NextDouble();
    }

    private static float NextRange(
        System.Random random,
        float minimum,
        float maximum)
    {
        return Mathf.Lerp(minimum, maximum, NextFloat(random));
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
