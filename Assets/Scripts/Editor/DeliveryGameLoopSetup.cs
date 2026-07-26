using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class DeliveryGameLoopSetup
{
    private const string SetupKey = "GmtkDeliveryGameLoopSetupV1";

    static DeliveryGameLoopSetup()
    {
        EditorApplication.delayCall += ConfigureOpenScene;
    }

    [MenuItem("Tools/GMTK/Configure Delivery Game Loop")]
    public static void ConfigureFromMenu()
    {
        EditorPrefs.DeleteKey(SetupKey);
        ConfigureOpenScene();
    }

    private static void ConfigureOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorPrefs.GetBool(SetupKey, false))
            return;
        RoadSpline road = Object.FindAnyObjectByType<RoadSpline>();
        if (road == null)
            return;
        DeliveryGameLoop loop = road.GetComponent<DeliveryGameLoop>();
        if (loop == null)
            loop = Undo.AddComponent<DeliveryGameLoop>(road.gameObject);
        EditorUtility.SetDirty(loop);
        EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
        EditorSceneManager.SaveScene(road.gameObject.scene);
        EditorPrefs.SetBool(SetupKey, true);
        Debug.Log("Configured the timed delivery game loop.");
    }
}
