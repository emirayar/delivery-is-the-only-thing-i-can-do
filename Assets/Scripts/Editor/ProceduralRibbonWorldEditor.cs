using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ProceduralRibbonWorld))]
public sealed class ProceduralRibbonWorldEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ProceduralRibbonWorld world = (ProceduralRibbonWorld)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Generated World", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.FloatField(
                new GUIContent(
                    "Road Length (m)",
                    "Spline örneklenerek hesaplanan gerçek yol uzunluğu."),
                world.GeneratedRoadLength);
            EditorGUILayout.IntField(
                new GUIContent(
                    "Terrain Vertices",
                    "Yol uzunluğu ve corridor genişliğinden türetilen toplam terrain vertex sayısı."),
                world.GeneratedVertexCount);
        }

        EditorGUILayout.HelpBox(
            world.deriveResolutionFromRoad
                ? "Terrain uzunluğu ve çözünürlüğü RoadSpline'dan türetiliyor. "
                  + "Yolu yeniden ürettiğinde dünya da aynı metre yoğunluğuyla uzar."
                : "Road-driven resolution kapalı. Length Segments ve Width "
                  + "Segments değerleri elle kullanılıyor.",
            MessageType.Info);

        if (GUILayout.Button(new GUIContent(
            "Regenerate From Road",
            "Mevcut spline uzunluğunu yeniden ölçer; terrain, collider, yol ve grass sistemini günceller.")))
        {
            world.Regenerate();
            GpuProceduralGrass grass =
                world.GetComponent<GpuProceduralGrass>();
            if (grass != null)
                grass.Rebuild();

            EditorUtility.SetDirty(world);
            SceneView.RepaintAll();
        }
    }
}
