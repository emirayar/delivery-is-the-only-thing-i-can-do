using System;
using UnityEngine;

[ExecuteAlways]
public sealed class RoadSpline : MonoBehaviour
{
    [SerializeField] private Vector3[] controlPoints =
    {
        new(0f, 0f, 0f),
        new(60f, 0f, 8f),
        new(130f, 0f, -10f),
        new(210f, 0f, 12f),
        new(300f, 0f, -5f),
        new(390f, 0f, 6f)
    };

    [Tooltip("Yol mesh'inin metre cinsinden toplam genişliği.")]
    [Min(1f)] public float roadWidth = 6f;
    [Tooltip("Terrain'in yol yüksekliğine kaç metrede yumuşakça yaklaşacağını belirler.")]
    [Min(0f)] public float roadFalloff = 8f;

    [Header("Procedural Generation")]
    [Tooltip("Üretilecek yolun başlangıçtan sona yaklaşık uzunluğu (metre).")]
    [Min(20f)] public float generatedLength = 500f;
    [Tooltip("Yolu şekillendiren kontrol noktası sayısı. Fazlası daha ayrıntılı kıvrımlar üretir.")]
    [Range(2, 32)] public int generatedPointCount = 8;
    [Tooltip("Yolun düz eksenden sağa ve sola sapabileceği en yüksek mesafe.")]
    [Min(0f)] public float maximumLateralBend = 28f;
    [Tooltip("Yol boyunca kıvrımların ne sıklıkta değişeceğini belirler.")]
    [Min(0.1f)] public float bendFrequency = 1.7f;
    [Tooltip("Yolun başlangıç yüksekliğine göre ulaşacağı yaklaşık gerçek tepe/vadi yüksekliği (metre). Elevation eğrisi bu değere normalize edilir.")]
    [Min(0f)] public float maximumElevationChange = 10f;
    [Tooltip("Yol boyunca tepe ve vadilerin ne sıklıkta değişeceğini belirler. Düşük değer daha uzun, yumuşak rampalar üretir.")]
    [Min(0.1f)] public float elevationFrequency = 1.15f;
    [Tooltip("İki kontrol noktası arasında izin verilen en yüksek yol eğimi. 0.08 yaklaşık yüzde 8 eğimdir.")]
    [Range(0.01f, 0.25f)] public float maximumGrade = 0.08f;
    [Tooltip("Aynı değer aynı yol şeklini üretir. Farklı yollar için seed'i değiştir.")]
    public int generationSeed = 72526;

    public int PointCount => controlPoints?.Length ?? 0;
    public ReadOnlySpan<Vector3> ControlPoints => controlPoints;

    public Vector3 Evaluate(float t)
    {
        if (PointCount == 0)
            return transform.position;
        if (PointCount == 1)
            return transform.TransformPoint(controlPoints[0]);

        t = Mathf.Clamp01(t);
        float scaled = t * (PointCount - 1);
        int segment = Mathf.Min(Mathf.FloorToInt(scaled), PointCount - 2);
        float localT = scaled - segment;

        Vector3 p0 = controlPoints[Mathf.Max(segment - 1, 0)];
        Vector3 p1 = controlPoints[segment];
        Vector3 p2 = controlPoints[segment + 1];
        Vector3 p3 = controlPoints[Mathf.Min(segment + 2, PointCount - 1)];

        return transform.TransformPoint(CatmullRom(p0, p1, p2, p3, localT));
    }

    public Vector3 EvaluateTangent(float t)
    {
        const float epsilon = 0.001f;
        Vector3 tangent = Evaluate(Mathf.Min(1f, t + epsilon))
                        - Evaluate(Mathf.Max(0f, t - epsilon));
        return tangent.sqrMagnitude > 0.0001f ? tangent.normalized : transform.forward;
    }

    public void GetFrame(float t, out Vector3 center, out Vector3 forward, out Vector3 right)
    {
        center = Evaluate(t);
        forward = EvaluateTangent(t);

        Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f)
            flatForward = transform.forward;

        right = Vector3.Cross(Vector3.up, flatForward.normalized);
    }

    public float ApproximateLength(int samples = 128)
    {
        if (PointCount < 2)
            return 0f;

        samples = Mathf.Max(8, samples);
        float length = 0f;
        Vector3 previous = Evaluate(0f);

        for (int i = 1; i <= samples; i++)
        {
            Vector3 current = Evaluate(i / (float)samples);
            length += Vector3.Distance(previous, current);
            previous = current;
        }

        return length;
    }

    public Vector3 FindClosestPointXZ(Vector3 worldPosition, out float closestT)
    {
        if (PointCount < 2)
        {
            closestT = 0f;
            return Evaluate(0f);
        }

        int coarseSamples = Mathf.Max(48, (PointCount - 1) * 32);
        float bestDistanceSquared = float.MaxValue;
        int bestIndex = 0;
        Vector2 target = new(worldPosition.x, worldPosition.z);

        for (int i = 0; i <= coarseSamples; i++)
        {
            float t = i / (float)coarseSamples;
            Vector3 sample3D = Evaluate(t);
            Vector2 sample = new(sample3D.x, sample3D.z);
            float distanceSquared = (sample - target).sqrMagnitude;
            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestIndex = i;
            }
        }

        float minT = Mathf.Max(0f, (bestIndex - 1f) / coarseSamples);
        float maxT = Mathf.Min(1f, (bestIndex + 1f) / coarseSamples);

        // Refine the winning interval. This keeps detection accurate even when
        // the generated road is several kilometres long.
        for (int iteration = 0; iteration < 10; iteration++)
        {
            float leftT = Mathf.Lerp(minT, maxT, 1f / 3f);
            float rightT = Mathf.Lerp(minT, maxT, 2f / 3f);
            Vector3 left3D = Evaluate(leftT);
            Vector3 right3D = Evaluate(rightT);
            float leftDistance = (
                new Vector2(left3D.x, left3D.z) - target).sqrMagnitude;
            float rightDistance = (
                new Vector2(right3D.x, right3D.z) - target).sqrMagnitude;

            if (leftDistance <= rightDistance)
                maxT = rightT;
            else
                minT = leftT;
        }

        closestT = (minT + maxT) * 0.5f;
        return Evaluate(closestT);
    }

    [ContextMenu("Generate Road Control Points")]
    public void GenerateControlPoints()
    {
        generatedLength = Mathf.Max(20f, generatedLength);
        generatedPointCount = Mathf.Clamp(generatedPointCount, 2, 32);
        maximumLateralBend = Mathf.Max(0f, maximumLateralBend);
        bendFrequency = Mathf.Max(0.1f, bendFrequency);
        maximumElevationChange = Mathf.Max(0f, maximumElevationChange);
        elevationFrequency = Mathf.Max(0.1f, elevationFrequency);
        maximumGrade = Mathf.Clamp(maximumGrade, 0.01f, 0.25f);

        controlPoints = new Vector3[generatedPointCount];
        float seedOffset = Mathf.Abs(generationSeed % 100000) * 0.01371f;

        for (int i = 0; i < generatedPointCount; i++)
        {
            float t = i / (float)(generatedPointCount - 1);
            float x = t * generatedLength;

            float envelope = Mathf.Sin(t * Mathf.PI);
            float broadNoise = Mathf.PerlinNoise(
                seedOffset,
                t * bendFrequency + 17.31f) * 2f - 1f;
            float detailNoise = Mathf.PerlinNoise(
                seedOffset + 43.17f,
                t * bendFrequency * 2.13f + 5.73f) * 2f - 1f;
            float z = (broadNoise + detailNoise * 0.28f)
                    * maximumLateralBend
                    * envelope;

            float elevationNoise = Mathf.PerlinNoise(
                seedOffset + 91.73f,
                t * elevationFrequency + 31.19f) * 2f - 1f;
            float elevationDetail = Mathf.PerlinNoise(
                seedOffset + 137.41f,
                t * elevationFrequency * 1.91f + 8.37f) * 2f - 1f;
            float y = (elevationNoise + elevationDetail * 0.18f)
                    * maximumElevationChange
                    * envelope;

            controlPoints[i] = new Vector3(x, y, z);
        }

        // Perlin output can be almost constant at low frequencies. Normalize
        // the generated profile so Maximum Elevation Change describes the
        // actual peak/valley height rather than only being a weak multiplier.
        float largestElevation = 0f;
        for (int i = 1; i < controlPoints.Length - 1; i++)
            largestElevation = Mathf.Max(
                largestElevation,
                Mathf.Abs(controlPoints[i].y));

        if (largestElevation > 0.0001f && maximumElevationChange > 0f)
        {
            float elevationScale = maximumElevationChange / largestElevation;
            for (int i = 1; i < controlPoints.Length - 1; i++)
                controlPoints[i].y *= elevationScale;
        }

        // Noise can occasionally place adjacent points too far apart vertically.
        // Clamp the change so generated roads remain comfortably driveable.
        for (int i = 1; i < controlPoints.Length; i++)
        {
            Vector2 previousFlat = new(controlPoints[i - 1].x, controlPoints[i - 1].z);
            Vector2 currentFlat = new(controlPoints[i].x, controlPoints[i].z);
            float horizontalDistance = Vector2.Distance(previousFlat, currentFlat);
            float allowedHeightChange = horizontalDistance * maximumGrade;
            controlPoints[i].y = Mathf.Clamp(
                controlPoints[i].y,
                controlPoints[i - 1].y - allowedHeightChange,
                controlPoints[i - 1].y + allowedHeightChange);
        }
    }

    private static Vector3 CatmullRom(
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector3 p3,
        float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1)
            + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private void OnDrawGizmos()
    {
        if (PointCount < 2)
            return;

        Gizmos.color = new Color(1f, 0.72f, 0.12f);
        Vector3 previous = Evaluate(0f);
        const int previewSegments = 100;

        for (int i = 1; i <= previewSegments; i++)
        {
            Vector3 current = Evaluate(i / (float)previewSegments);
            Gizmos.DrawLine(previous, current);
            previous = current;
        }

        Gizmos.color = new Color(1f, 0.35f, 0.1f);
        foreach (Vector3 point in controlPoints)
            Gizmos.DrawSphere(transform.TransformPoint(point), 0.7f);
    }
}
