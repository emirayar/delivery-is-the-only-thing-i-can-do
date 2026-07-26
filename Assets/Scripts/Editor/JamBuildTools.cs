using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class JamBuildTools
{
    private const string ProductName = "Delivery Is The Only Thing I Can Do";
    private const string AutoBuildKey = "GmtkJamWindowsReleaseBuiltV1";
    private const string AutoWebBuildKey = "GmtkJamWebReleaseBuiltV1";
    private const string BuildFolder = "Builds/Windows";
    private const string ExecutablePath = BuildFolder + "/Delivery Is The Only Thing I Can Do.exe";
    private const string ZipPath = "Builds/Delivery-Is-The-Only-Thing-I-Can-Do-Windows.zip";
    private const string WebBuildFolder = "Builds/WebGL";
    private const string WebZipPath = "Builds/Delivery-Is-The-Only-Thing-I-Can-Do-WebGL.zip";

    static JamBuildTools()
    {
        EditorApplication.delayCall += ConfigureAndBuildOnce;
        EditorApplication.update += ConfigureAndBuildWebOnce;
    }

    [MenuItem("Tools/GMTK/Build WebGL Release + ZIP")]
    public static void BuildWebRelease()
    {
        ConfigureReleaseSettings();
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.dataCaching = true;

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0)
        {
            Debug.LogError("WebGL release build cancelled: no enabled scenes in Build Settings.");
            return;
        }

        Directory.CreateDirectory(WebBuildFolder);
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = WebBuildFolder,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"WebGL release build failed: {report.summary.result}");
            return;
        }

        if (File.Exists(WebZipPath))
            File.Delete(WebZipPath);
        ZipFile.CreateFromDirectory(
            WebBuildFolder,
            WebZipPath,
            System.IO.Compression.CompressionLevel.Optimal,
            false);
        Debug.Log(
            $"WEBGL RELEASE READY: {WebZipPath} "
            + $"({report.summary.totalSize / (1024f * 1024f):0.0} MB build)");
        EditorUtility.RevealInFinder(WebZipPath);
    }

    [MenuItem("Tools/GMTK/Configure Release Player Settings")]
    public static void ConfigureReleaseSettings()
    {
        PlayerSettings.companyName = "Emir Ayar";
        PlayerSettings.productName = ProductName;
        PlayerSettings.bundleVersion = "1.0.0";
        PlayerSettings.SetApplicationIdentifier(
            NamedBuildTarget.Standalone,
            "com.emirayar.deliveryistheonlythingicando");
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.allowFullscreenSwitch = true;
        PlayerSettings.runInBackground = false;
        PlayerSettings.forceSingleInstance = true;
        PlayerSettings.usePlayerLog = true;
        PlayerSettings.SetScriptingBackend(
            NamedBuildTarget.Standalone,
            ScriptingImplementation.Mono2x);
        PlayerSettings.SetManagedStrippingLevel(
            NamedBuildTarget.Standalone,
            ManagedStrippingLevel.Minimal);

        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.connectProfiler = false;
        EditorUserBuildSettings.allowDebugging = false;
        EditorUserBuildSettings.buildWithDeepProfilingSupport = false;
        AssetDatabase.SaveAssets();
        Debug.Log("Configured Windows jam release player settings.");
    }

    [MenuItem("Tools/GMTK/Build Windows Release + ZIP")]
    public static void BuildWindowsRelease()
    {
        ConfigureReleaseSettings();
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0)
        {
            Debug.LogError("Windows release build cancelled: no enabled scenes in Build Settings.");
            return;
        }

        Directory.CreateDirectory(BuildFolder);
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = ExecutablePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"Windows release build failed: {report.summary.result}");
            return;
        }

        // Burst can emit symbol data beside the player. It is useful for debugging,
        // but should not be included in the downloadable jam release.
        string burstDebugFolder = Path.Combine(
            BuildFolder,
            ProductName + "_BurstDebugInformation_DoNotShip");
        if (Directory.Exists(burstDebugFolder))
            Directory.Delete(burstDebugFolder, true);

        if (File.Exists(ZipPath))
            File.Delete(ZipPath);
        ZipFile.CreateFromDirectory(
            BuildFolder,
            ZipPath,
            System.IO.Compression.CompressionLevel.Optimal,
            false);
        Debug.Log(
            $"WINDOWS RELEASE READY: {ZipPath} "
            + $"({report.summary.totalSize / (1024f * 1024f):0.0} MB build)");
        EditorUtility.RevealInFinder(ZipPath);
    }

    private static void ConfigureAndBuildOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode
            || EditorPrefs.GetBool(AutoBuildKey, false))
        {
            return;
        }
        EditorPrefs.SetBool(AutoBuildKey, true);
        BuildWindowsRelease();
    }

    private static void ConfigureAndBuildWebOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }
        EditorApplication.update -= ConfigureAndBuildWebOnce;
        if (EditorPrefs.GetBool(AutoWebBuildKey, false))
            return;

        EditorPrefs.SetBool(AutoWebBuildKey, true);
        BuildWebRelease();
    }
}
