using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EndTimesSkySetup
{
    private const string SetupKey = "GmtkEndTimesSkySetupV2";
    private const string MaterialPath = "Assets/Settings/EndTimesSkybox.mat";

    static EndTimesSkySetup()
    {
        EditorApplication.delayCall += ConfigureOpenScene;
    }

    [MenuItem("Tools/GMTK/Configure End Times Sky")]
    public static void ConfigureFromMenu()
    {
        EditorPrefs.DeleteKey(SetupKey);
        ConfigureOpenScene();
    }

    private static void ConfigureOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorPrefs.GetBool(SetupKey, false))
            return;

        Shader shader = Shader.Find("GMTK/End Times Procedural Sky");
        if (shader == null)
            return;

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "End Times Skybox" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        Light directional = null;
        foreach (Light candidate in Object.FindObjectsByType<Light>())
        {
            if (candidate.type == LightType.Directional)
            {
                directional = candidate;
                break;
            }
        }
        if (directional == null)
            return;

        EndTimesSkyController controller = directional.GetComponent<EndTimesSkyController>();
        if (controller == null)
            controller = Undo.AddComponent<EndTimesSkyController>(directional.gameObject);
        if (controller.originalSkybox == null && RenderSettings.skybox != material)
            controller.originalSkybox = RenderSettings.skybox;
        controller.skyboxMaterial = material;
        controller.sunLight = directional;
        controller.useEndTimesSky = true;
        controller.ApplySky();

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(material);
        EditorSceneManager.MarkSceneDirty(directional.gameObject.scene);
        EditorSceneManager.SaveScene(directional.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorPrefs.SetBool(SetupKey, true);
        Debug.Log("Configured the custom End Times procedural skybox.");
    }
}
