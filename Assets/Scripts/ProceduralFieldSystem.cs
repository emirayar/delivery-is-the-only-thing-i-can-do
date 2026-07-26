using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(ProceduralRibbonWorld))]
public sealed class ProceduralFieldSystem : MonoBehaviour
{
    [Serializable]
    private struct FieldParcel
    {
        public float tMin;
        public float tMax;
        public float innerOffset;
        public float outerOffset;
        public float startSkewT;
        public float endSkewT;
        public float startCurveT;
        public float endCurveT;
        public float startSecondCurveT;
        public float endSecondCurveT;
        public float boundsMinT;
        public float boundsMaxT;
        public int cropType;

        public bool Contains(float t, float lateral)
        {
            float minLateral = Mathf.Min(innerOffset, outerOffset);
            float maxLateral = Mathf.Max(innerOffset, outerOffset);
            if (lateral < minLateral || lateral > maxLateral)
                return false;

            float depth01 = Mathf.InverseLerp(
                Mathf.Abs(innerOffset),
                Mathf.Abs(outerOffset),
                Mathf.Abs(lateral));
            float broadCurve = Mathf.Sin(depth01 * Mathf.PI);
            float sCurve = Mathf.Sin(depth01 * Mathf.PI * 2f);
            float shapedMin = tMin
                            + startSkewT * depth01
                            + startCurveT * broadCurve
                            + startSecondCurveT * sCurve;
            float shapedMax = tMax
                            + endSkewT * depth01
                            + endCurveT * broadCurve
                            + endSecondCurveT * sCurve;
            return t >= Mathf.Min(shapedMin, shapedMax)
                && t <= Mathf.Max(shapedMin, shapedMax);
        }
    }

    [Header("Layout")]
    [Tooltip("Aynı seed aynı tarla parsellerini ve ürün türlerini üretir.")]
    public int fieldSeed = 192837;
    [Tooltip("Bir parselin yol boyunca kaplayabileceği en kısa metre.")]
    [Min(5f)] public float minimumParcelLength = 35f;
    [Tooltip("Bir parselin yol boyunca kaplayabileceği en uzun metre.")]
    [Min(5f)] public float maximumParcelLength = 90f;
    [Tooltip("Asfalt kenarı ile yapılaşma şeridi arasındaki çimli banket/hendek mesafesi.")]
    [Min(0f)] public float roadsideGap = 3.5f;
    [Tooltip("Yol kenarında evler, bahçeler ve giriş yolları için boş bırakılacak şeridin derinliği.")]
    [Min(0f)] public float buildingZoneDepth = 22f;
    [Tooltip("Terrain'in dış kenarında tarla üretilmeden bırakılan mesafe.")]
    [Min(0f)] public float outerMargin = 3f;
    [Tooltip("Komşu tarlalar arasında çim olarak bırakılan sınır şeridinin genişliği.")]
    [Min(0f)] public float boundaryWidth = 1.5f;
    [Tooltip("Parsel sınırının yol kenarından dış kenara giderken yol boyunca kayabileceği maksimum metre. Yüksek değer daha açılı/trapez tarlalar üretir.")]
    [Min(0f)] public float maximumBoundarySkew = 16f;
    [Tooltip("Parsel sınırlarına verilen hafif organik eğriliğin metre cinsinden gücü.")]
    [Min(0f)] public float boundaryWaviness = 3f;
    [Tooltip("Tarla dışarı uzadıkça her 100 metrede eklenecek sınır eğriliği. Çok derin tarlalarda fotoğraftaki uzun kavisleri görünür kılar.")]
    [Min(0f)] public float boundaryCurvePer100Meters = 4f;
    [Tooltip("Bir parselin sürülmüş kahverengi tarla olma ihtimali.")]
    [Range(0f, 1f)] public float fallowFieldChance = 0.06f;
    [Tooltip("Toprak olmayan bir parselin sarı/hasat edilmiş tarla olma ihtimali.")]
    [Range(0f, 1f)] public float yellowFieldChance = 0.18f;
    [Tooltip("Toprak olmayan bir parselin açık yeşil ekin olma ihtimali. Kalan parseller koyu yeşil olur.")]
    [Range(0f, 1f)] public float lightGreenFieldChance = 0.28f;

    [Header("Terrain Colors")]
    [Tooltip("Birinci ekin çeşidinin terrain rengi.")]
    public Color cropColorA = new(0.25f, 0.48f, 0.075f);
    [Tooltip("İkinci ekin çeşidinin terrain rengi.")]
    public Color cropColorB = new(0.48f, 0.62f, 0.13f);
    [Tooltip("Olgun/sarı ekin terrain rengi.")]
    public Color cropColorC = new(0.72f, 0.57f, 0.13f);
    [Tooltip("Sürülmüş veya boş tarla terrain rengi.")]
    public Color soilColor = new(0.32f, 0.19f, 0.085f);

    private readonly List<FieldParcel> leftParcels = new();
    private readonly List<FieldParcel> rightParcels = new();
    private ProceduralRibbonWorld world;
    private bool regenerateQueued;

    public int ParcelCount => leftParcels.Count + rightParcels.Count;
    public int LayoutVersion { get; private set; }

    private void OnEnable()
    {
        Generate();
        DisableLegacyOverlay();
    }

    private void OnValidate()
    {
        minimumParcelLength = Mathf.Max(5f, minimumParcelLength);
        maximumParcelLength = Mathf.Max(
            minimumParcelLength,
            maximumParcelLength);
        roadsideGap = Mathf.Max(0f, roadsideGap);
        buildingZoneDepth = Mathf.Max(0f, buildingZoneDepth);
        outerMargin = Mathf.Max(0f, outerMargin);
        boundaryWidth = Mathf.Max(0f, boundaryWidth);
        maximumBoundarySkew = Mathf.Max(0f, maximumBoundarySkew);
        boundaryWaviness = Mathf.Max(0f, boundaryWaviness);
        boundaryCurvePer100Meters = Mathf.Max(
            0f,
            boundaryCurvePer100Meters);

        if (isActiveAndEnabled)
            regenerateQueued = true;
    }

    private void Update()
    {
        if (!regenerateQueued || !isActiveAndEnabled)
            return;
        regenerateQueued = false;
        Generate();
        world.Regenerate();

        GpuProceduralGrass grass = GetComponent<GpuProceduralGrass>();
        if (grass != null)
            grass.Rebuild();
    }

    [ContextMenu("Regenerate Field Layout")]
    public void Generate()
    {
        world = GetComponent<ProceduralRibbonWorld>();
        if (world == null || world.Spline == null || world.Spline.PointCount < 2)
            return;

        BuildLayout();
        DisableLegacyOverlay();
        LayoutVersion++;
    }

    public bool IsInsideField(float t, float lateral)
    {
        return TryGetCropType(t, lateral, out _);
    }

    public bool TryGetCropType(float t, float lateral, out int cropType)
    {
        List<FieldParcel> parcels = lateral < 0f ? leftParcels : rightParcels;
        for (int i = 0; i < parcels.Count; i++)
        {
            FieldParcel parcel = parcels[i];
            if (t < parcel.boundsMinT)
                break;
            if (t <= parcel.boundsMaxT && parcel.Contains(t, lateral))
            {
                cropType = parcel.cropType;
                return true;
            }
        }

        cropType = -1;
        return false;
    }

    public Color GetCropColor(int cropType)
    {
        return cropType switch
        {
            0 => cropColorA,
            1 => cropColorB,
            2 => cropColorC,
            _ => soilColor
        };
    }

    private void BuildLayout()
    {
        leftParcels.Clear();
        rightParcels.Clear();

        float roadLength = Mathf.Max(
            1f,
            world.Spline.ApproximateLength(256));
        float innerDistance = world.Spline.roadWidth * 0.5f
                            + roadsideGap
                            + buildingZoneDepth;
        float outerDistance = Mathf.Max(
            innerDistance + 1f,
            world.halfWidth - outerMargin);

        // If the reserved village strip consumes the whole ribbon there is no
        // valid field depth, so leave the terrain as grass.
        if (innerDistance >= world.halfWidth - outerMargin)
            return;

        BuildSide(
            leftParcels,
            roadLength,
            -innerDistance,
            -outerDistance,
            fieldSeed ^ 0x2C9277B5);
        BuildSide(
            rightParcels,
            roadLength,
            innerDistance,
            outerDistance,
            fieldSeed ^ 0x68E31DA4);
    }

    private void BuildSide(
        List<FieldParcel> parcels,
        float roadLength,
        float innerOffset,
        float outerOffset,
        int seed)
    {
        var random = new System.Random(seed);
        float cursor = 0f;
        float halfBoundary = boundaryWidth * 0.5f;
        float parcelDepth = Mathf.Abs(outerOffset - innerOffset);
        float scaledCurveStrength = boundaryWaviness
                                  + parcelDepth
                                  * boundaryCurvePer100Meters
                                  / 100f;

        while (cursor < roadLength - 0.01f)
        {
            float parcelLength = Mathf.Lerp(
                minimumParcelLength,
                maximumParcelLength,
                NextFloat(random));
            float end = Mathf.Min(roadLength, cursor + parcelLength);
            float visibleStart = Mathf.Min(end, cursor + halfBoundary);
            float visibleEnd = Mathf.Max(visibleStart, end - halfBoundary);

            if (visibleEnd - visibleStart > 1f)
            {
                bool fallow = NextFloat(random) < fallowFieldChance;
                float visibleLength = visibleEnd - visibleStart;
                float safeSkew = Mathf.Min(
                    maximumBoundarySkew,
                    Mathf.Max(0f, (visibleLength - 5f) * 0.35f));
                float startSkewMeters = Mathf.Lerp(
                    -safeSkew,
                    safeSkew,
                    NextFloat(random));
                float endSkewMeters = Mathf.Lerp(
                    -safeSkew,
                    safeSkew,
                    NextFloat(random));
                float startCurveMeters = Mathf.Lerp(
                    -scaledCurveStrength,
                    scaledCurveStrength,
                    NextFloat(random));
                float endCurveMeters = Mathf.Lerp(
                    -scaledCurveStrength,
                    scaledCurveStrength,
                    NextFloat(random));
                float startSecondCurveMeters = Mathf.Lerp(
                    -scaledCurveStrength * 0.38f,
                    scaledCurveStrength * 0.38f,
                    NextFloat(random));
                float endSecondCurveMeters = Mathf.Lerp(
                    -scaledCurveStrength * 0.38f,
                    scaledCurveStrength * 0.38f,
                    NextFloat(random));
                float startSkewT = startSkewMeters / roadLength;
                float endSkewT = endSkewMeters / roadLength;
                float startCurveT = startCurveMeters / roadLength;
                float endCurveT = endCurveMeters / roadLength;
                float startSecondCurveT =
                    startSecondCurveMeters / roadLength;
                float endSecondCurveT =
                    endSecondCurveMeters / roadLength;
                float tMin = visibleStart / roadLength;
                float tMax = visibleEnd / roadLength;
                float cropRoll = NextFloat(random);
                int cropType = fallow
                    ? 3
                    : cropRoll < yellowFieldChance
                        ? 2
                        : cropRoll < yellowFieldChance
                                   + lightGreenFieldChance
                            ? 1
                            : 0;
                parcels.Add(new FieldParcel
                {
                    tMin = tMin,
                    tMax = tMax,
                    innerOffset = innerOffset,
                    outerOffset = outerOffset,
                    startSkewT = startSkewT,
                    endSkewT = endSkewT,
                    startCurveT = startCurveT,
                    endCurveT = endCurveT,
                    startSecondCurveT = startSecondCurveT,
                    endSecondCurveT = endSecondCurveT,
                    boundsMinT = Mathf.Clamp01(
                        tMin
                        + Mathf.Min(0f, startSkewT)
                        - Mathf.Abs(startCurveT)
                        - Mathf.Abs(startSecondCurveT)),
                    boundsMaxT = Mathf.Clamp01(
                        tMax
                        + Mathf.Max(0f, endSkewT)
                        + Mathf.Abs(endCurveT)
                        + Mathf.Abs(endSecondCurveT)),
                    cropType = cropType
                });
            }

            cursor = end;
        }
    }

    private void DisableLegacyOverlay()
    {
        Transform legacy = transform.Find("Generated Fields");
        if (legacy != null && legacy.gameObject.activeSelf)
            legacy.gameObject.SetActive(false);
    }

    private static float NextFloat(System.Random random)
    {
        return (float)random.NextDouble();
    }
}
