using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class AudioPrototypeSetup
{
    private const string SetupKey = "GmtkAudioPrototypeSetupV2";

    static AudioPrototypeSetup()
    {
        EditorApplication.delayCall += TryConfigureOpenScene;
    }

    [MenuItem("Tools/GMTK/Configure Vehicle and Radio Audio")]
    public static void ConfigureFromMenu()
    {
        EditorPrefs.DeleteKey(SetupKey);
        TryConfigureOpenScene();
    }

    private static void TryConfigureOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorPrefs.GetBool(SetupKey, false))
            return;

        ArcadeDeliveryVan van = Object.FindFirstObjectByType<ArcadeDeliveryVan>();
        RadioMusicController radio = Object.FindFirstObjectByType<RadioMusicController>();
        if (van == null || radio == null)
            return;

        VehicleEngineAudio engine = van.GetComponent<VehicleEngineAudio>();
        if (engine == null)
            engine = Undo.AddComponent<VehicleEngineAudio>(van.gameObject);
        engine.engineLoopClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Resources/Audio/Vehicle/Engine_Loop_CC0.wav");
        engine.engineStartClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Resources/Audio/Vehicle/Engine_Start_CC0.wav");
        EditorUtility.SetDirty(engine);

        AudioClip[] tracks = new[]
        {
            "Staring_At_Reflections_CC0.mp3",
            "Suspended_CC0.mp3",
            "Sweet_Talk_CC0.mp3",
            "Bugmintide_CC0.mp3",
            "Europa_Ice_Extended_CC0.mp3",
            "Unease_CC0.mp3"
        }
        .Select(file => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/Radio/" + file))
        .Where(clip => clip != null)
        .ToArray();

        AudioSource source = radio.audioSource;
        if (source == null)
            source = radio.GetComponent<AudioSource>();
        if (source == null)
            source = Undo.AddComponent<AudioSource>(radio.gameObject);
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        radio.audioSource = source;
        radio.playlist = tracks;
        radio.playOnStart = true;
        radio.loopPlaylist = true;
        radio.shufflePlaylist = true;
        EditorUtility.SetDirty(source);
        EditorUtility.SetDirty(radio);

        EditorSceneManager.MarkSceneDirty(van.gameObject.scene);
        EditorSceneManager.SaveScene(van.gameObject.scene);
        EditorPrefs.SetBool(SetupKey, true);
        Debug.Log($"Configured vehicle audio and {tracks.Length} CC0 radio tracks.");
    }
}
