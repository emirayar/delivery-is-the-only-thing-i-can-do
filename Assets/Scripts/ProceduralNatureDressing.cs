using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(ProceduralRibbonWorld))]
public sealed class ProceduralNatureDressing : MonoBehaviour
{
    [Header("Asset Pools")]
    [Tooltip("Vadi yamaçlarında kullanılacak büyük Quaternius ağaç modelleri.")]
    public GameObject[] treePrefabs;
    [Tooltip("Ev şeridi ile tarlalar arasına serpiştirilecek çalı ve büyük bitki modelleri.")]
    public GameObject[] bushPrefabs;
    [Tooltip("Yamaç ve tarla kenarlarında seyrek kullanılacak kaya modelleri.")]
    public GameObject[] rockPrefabs;

    [Header("Distribution")]
    [Tooltip("Aynı seed aynı doğa yerleşimini üretir.")]
    public int natureSeed = 91827;
    [Tooltip("Yol boyunca iki doğa kümesi arasındaki yaklaşık mesafe.")]
    [Min(8f)] public float clusterSpacing = 28f;
    [Tooltip("Her örnek noktasında ağaç üretilme ihtimali.")]
    [Range(0f, 1f)] public float treeChance = 0.72f;
    [Tooltip("Her örnek noktasında çalı üretilme ihtimali.")]
    [Range(0f, 1f)] public float bushChance = 0.58f;
    [Tooltip("Her örnek noktasında kaya üretilme ihtimali.")]
    [Range(0f, 1f)] public float rockChance = 0.16f;
    [Tooltip("Yolun başlangıç ve sonunda dekor üretilmeden bırakılan mesafe.")]
    [Min(0f)] public float roadEndMargin = 24f;

    [Header("Scale")]
    [Tooltip("Ağaçların hedef dünya yüksekliği aralığı.")]
    public Vector2 treeHeightRange = new(7f, 14f);
    [Tooltip("Çalıların hedef dünya yüksekliği aralığı.")]
    public Vector2 bushHeightRange = new(0.8f, 1.8f);
    [Tooltip("Kayaların hedef dünya yüksekliği aralığı.")]
    public Vector2 rockHeightRange = new(0.55f, 1.5f);
    [Tooltip("Objelerin zemine gömülmesini engelleyen küçük dikey pay.")]
    [Min(0f)] public float groundClearance = 0.03f;

    private ProceduralRibbonWorld world;
    private Transform generatedRoot;
    private bool regenerateQueued;

    public int GeneratedObjectCount { get; private set; }

    private void OnEnable()
    {
        regenerateQueued = true;
    }

    private void OnValidate()
    {
        clusterSpacing = Mathf.Max(8f, clusterSpacing);
        treeHeightRange = SortPositive(treeHeightRange);
        bushHeightRange = SortPositive(bushHeightRange);
        rockHeightRange = SortPositive(rockHeightRange);

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

    [ContextMenu("Regenerate Nature Dressing")]
    public void Generate()
    {
        world = GetComponent<ProceduralRibbonWorld>();
        ClearGenerated();
        GeneratedObjectCount = 0;
        if (world == null
            || world.Spline == null
            || world.Spline.PointCount < 2)
        {
            return;
        }

        float roadLength = Mathf.Max(
            1f,
            world.Spline.ApproximateLength(256));
        var random = new System.Random(natureSeed);
        float cursor = roadEndMargin + NextRange(random, 0f, clusterSpacing);

        while (cursor < roadLength - roadEndMargin)
        {
            float t = Mathf.Clamp01(cursor / roadLength);
            PlaceCluster(t, -1f, random);
            PlaceCluster(t, 1f, random);
            cursor += NextRange(
                random,
                clusterSpacing * 0.7f,
                clusterSpacing * 1.35f);
        }
    }

    private void PlaceCluster(
        float t,
        float side,
        System.Random random)
    {
        float roadHalfWidth = world.Spline.roadWidth * 0.5f;
        float outerLimit = Mathf.Max(
            roadHalfWidth + 20f,
            world.halfWidth - 7f);

        if (NextFloat(random) < treeChance)
        {
            float minimum = Mathf.Min(roadHalfWidth + 40f, outerLimit);
            float lateral = side * NextRange(random, minimum, outerLimit);
            PlaceRandom(
                treePrefabs,
                t + NextRange(random, -0.004f, 0.004f),
                lateral,
                treeHeightRange,
                random,
                5f);
        }

        if (NextFloat(random) < bushChance)
        {
            float minimum = roadHalfWidth + 16f;
            float maximum = Mathf.Min(
                roadHalfWidth + 36f,
                outerLimit);
            float lateral = side * NextRange(random, minimum, maximum);
            PlaceRandom(
                bushPrefabs,
                t + NextRange(random, -0.006f, 0.006f),
                lateral,
                bushHeightRange,
                random,
                12f);
        }

        if (NextFloat(random) < rockChance)
        {
            float minimum = roadHalfWidth + 28f;
            float lateral = side * NextRange(random, minimum, outerLimit);
            PlaceRandom(
                rockPrefabs,
                t + NextRange(random, -0.008f, 0.008f),
                lateral,
                rockHeightRange,
                random,
                18f);
        }
    }

    private void PlaceRandom(
        GameObject[] prefabs,
        float t,
        float lateral,
        Vector2 heightRange,
        System.Random random,
        float tiltLimit)
    {
        GameObject prefab = Pick(prefabs, random);
        if (prefab == null)
            return;

        world.SampleSurface(
            Mathf.Clamp01(t),
            lateral,
            out Vector3 surface,
            out Vector3 normal);
        Quaternion slope = Quaternion.FromToRotation(Vector3.up, normal);
        Quaternion yaw = Quaternion.Euler(
            NextRange(random, -tiltLimit, tiltLimit),
            NextRange(random, 0f, 360f),
            NextRange(random, -tiltLimit, tiltLimit));
        GameObject instance = Instantiate(
            prefab,
            surface,
            slope * yaw,
            generatedRoot);
        instance.name = prefab.name;
        instance.hideFlags = HideFlags.DontSave;

        if (!TryGetBounds(instance, out Bounds bounds))
            return;

        float targetHeight = NextRange(
            random,
            heightRange.x,
            heightRange.y);
        float scale = targetHeight / Mathf.Max(0.01f, bounds.size.y);
        instance.transform.localScale *= scale;

        if (TryGetBounds(instance, out bounds))
        {
            instance.transform.position += Vector3.up
                * (surface.y + groundClearance - bounds.min.y);
        }

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
        GeneratedObjectCount++;
    }

    private void ClearGenerated()
    {
        Transform existing = transform.Find("Generated Nature Dressing");
        if (existing != null)
            DestroySafely(existing.gameObject);

        var rootObject = new GameObject("Generated Nature Dressing")
        {
            hideFlags = HideFlags.DontSave
        };
        generatedRoot = rootObject.transform;
        generatedRoot.SetParent(transform, false);
    }

    private static GameObject Pick(
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

    private static bool TryGetBounds(
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

    private static Vector2 SortPositive(Vector2 value)
    {
        float minimum = Mathf.Max(0.05f, Mathf.Min(value.x, value.y));
        float maximum = Mathf.Max(minimum, Mathf.Max(value.x, value.y));
        return new Vector2(minimum, maximum);
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
        if (maximum <= minimum)
            return minimum;
        return Mathf.Lerp(minimum, maximum, NextFloat(random));
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
}
