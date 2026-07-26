using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(RoadSpline))]
public sealed class RoadSplineEditor : Editor
{
    private SerializedProperty controlPoints;

    [InitializeOnLoadMethod]
    private static void UpgradeElevationGeneration()
    {
        string key = "GMTK.NormalizedRoadElevation.v1."
                   + Hash128.Compute(Application.dataPath);
        if (EditorPrefs.GetBool(key, false))
            return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            RoadSpline spline = Object.FindAnyObjectByType<RoadSpline>();
            if (spline == null)
                return;

            spline.GenerateControlPoints();
            RegenerateWorldAndGrass(
                spline,
                spline.GetComponent<ProceduralRibbonWorld>());
            EditorUtility.SetDirty(spline);
            EditorSceneManager.MarkSceneDirty(spline.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            EditorPrefs.SetBool(key, true);
            Debug.Log(
                "Road elevation regenerated with normalized height.",
                spline);
        };
    }

    private void OnEnable()
    {
        controlPoints = serializedObject.FindProperty("controlPoints");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        RoadSpline spline = (RoadSpline)target;
        ProceduralRibbonWorld world = spline.GetComponent<ProceduralRibbonWorld>();

        if (GUILayout.Button(new GUIContent(
            "Generate Road From Settings",
            "Length, Point Count, Bend, Elevation, Grade ve Seed ayarlarıyla kontrol noktalarını baştan üretir.")))
        {
            Undo.RecordObject(spline, "Generate road control points");
            spline.GenerateControlPoints();
            RegenerateWorldAndGrass(spline, world);
            EditorUtility.SetDirty(spline);
        }

        if (GUILayout.Button(new GUIContent(
            "Random Seed And Generate",
            "Yeni seed seçer; aynı uzunluk, kıvrım ve elevation ayarlarıyla farklı bir yol üretir.")))
        {
            Undo.RecordObject(spline, "Randomize and generate road");
            spline.generationSeed = Random.Range(
                -1_000_000_000,
                1_000_000_000);
            spline.GenerateControlPoints();
            RegenerateWorldAndGrass(spline, world);
            EditorUtility.SetDirty(spline);
        }

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(world == null))
        {
            if (GUILayout.Button(new GUIContent(
                "Regenerate World",
                "Mevcut spline'ı değiştirmeden terrain, yol mesh'i ve çimleri yeniden kurar.")))
                RegenerateWorldAndGrass(spline, world);

            if (GUILayout.Button(new GUIContent(
                "New Terrain Seed",
                "Yol şeklini korur; yalnızca terrain engebeleri için yeni bir seed seçer.")))
            {
                Undo.RecordObject(world, "Change terrain seed");
                world.seed = Random.Range(-1_000_000_000, 1_000_000_000);
                RegenerateWorldAndGrass(spline, world);

                EditorUtility.SetDirty(world);
            }
        }
    }

    private void OnSceneGUI()
    {
        serializedObject.Update();
        RoadSpline spline = (RoadSpline)target;

        for (int i = 0; i < controlPoints.arraySize; i++)
        {
            SerializedProperty point = controlPoints.GetArrayElementAtIndex(i);
            Vector3 worldPosition = spline.transform.TransformPoint(point.vector3Value);

            EditorGUI.BeginChangeCheck();
            float handleSize = HandleUtility.GetHandleSize(worldPosition) * 0.08f;
            Handles.color = new Color(1f, 0.45f, 0.08f);
            Handles.SphereHandleCap(
                0,
                worldPosition,
                Quaternion.identity,
                handleSize,
                EventType.Repaint);
            Vector3 moved = Handles.PositionHandle(worldPosition, Quaternion.identity);

            if (!EditorGUI.EndChangeCheck())
                continue;

            Undo.RecordObject(spline, "Move road control point");
            point.vector3Value = spline.transform.InverseTransformPoint(moved);
            serializedObject.ApplyModifiedProperties();

            ProceduralRibbonWorld world =
                spline.GetComponent<ProceduralRibbonWorld>();
            if (world != null && world.liveEdit)
            {
                RegenerateWorldAndGrass(spline, world);
            }
        }
    }

    private static void RegenerateWorldAndGrass(
        RoadSpline spline,
        ProceduralRibbonWorld world)
    {
        ProceduralFieldSystem fields =
            spline.GetComponent<ProceduralFieldSystem>();
        if (fields != null)
        {
            fields.Generate();
            EditorUtility.SetDirty(fields);
        }

        if (world != null)
        {
            world.Regenerate();
            EditorUtility.SetDirty(world);
        }

        GpuProceduralGrass grass = spline.GetComponent<GpuProceduralGrass>();
        if (grass != null)
            grass.Rebuild();
    }
}
