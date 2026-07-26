using UnityEngine;
using System.Collections.Generic;

public static class GameSettings
{
    private const string MasterVolumeKey = "settings.masterVolume";
    private const string MasterMutedKey = "settings.masterMuted";
    private const string MusicVolumeKey = "settings.musicVolume";
    private const string MusicMutedKey = "settings.musicMuted";
    private const string MouseSensitivityKey = "settings.mouseSensitivity";
    private const string FieldOfViewKey = "settings.fieldOfView";
    private const string FullscreenKey = "settings.fullscreen";
    private const string QualityKey = "settings.quality";
    private const string ResolutionWidthKey = "settings.resolutionWidth";
    private const string ResolutionHeightKey = "settings.resolutionHeight";

    public static readonly string[] QualityNames = { "Low", "Medium", "High", "Ultra" };

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(MasterVolumeKey, 0.8f);
        set
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value));
            ApplyAudio();
        }
    }

    public static bool MasterMuted
    {
        get => PlayerPrefs.GetInt(MasterMutedKey, 0) != 0;
        set
        {
            PlayerPrefs.SetInt(MasterMutedKey, value ? 1 : 0);
            ApplyAudio();
        }
    }

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicVolumeKey, 0.55f);
        set
        {
            PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
            ApplyAudio();
        }
    }

    public static bool MusicMuted
    {
        get => PlayerPrefs.GetInt(MusicMutedKey, 0) != 0;
        set
        {
            PlayerPrefs.SetInt(MusicMutedKey, value ? 1 : 0);
            ApplyAudio();
        }
    }

    public static float MouseSensitivity
    {
        get => PlayerPrefs.GetFloat(MouseSensitivityKey, 0.11f);
        set => PlayerPrefs.SetFloat(MouseSensitivityKey, Mathf.Clamp(value, 0.02f, 0.5f));
    }

    public static float FieldOfView
    {
        get => PlayerPrefs.GetFloat(FieldOfViewKey, 67f);
        set => PlayerPrefs.SetFloat(FieldOfViewKey, Mathf.Clamp(value, 45f, 90f));
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
        set
        {
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
            ApplyResolution();
        }
    }

    public static int QualityLevel
    {
        get => Mathf.Clamp(
            PlayerPrefs.GetInt(QualityKey, 2),
            0,
            QualityNames.Length - 1);
        set
        {
            int level = Mathf.Clamp(value, 0, QualityNames.Length - 1);
            PlayerPrefs.SetInt(QualityKey, level);
            ApplyQuality(level);
        }
    }

    public static int ResolutionWidth => PlayerPrefs.GetInt(ResolutionWidthKey, Screen.currentResolution.width);
    public static int ResolutionHeight => PlayerPrefs.GetInt(ResolutionHeightKey, Screen.currentResolution.height);

    public static string ResolutionLabel => $"{ResolutionWidth} × {ResolutionHeight}";

    public static void CycleResolution(int direction = 1)
    {
        Resolution[] resolutions = GetAvailableResolutions();
        if (resolutions.Length == 0)
            return;

        int currentIndex = 0;
        long bestDifference = long.MaxValue;
        for (int i = 0; i < resolutions.Length; i++)
        {
            long difference = Mathf.Abs(resolutions[i].width - ResolutionWidth)
                            + Mathf.Abs(resolutions[i].height - ResolutionHeight);
            if (difference < bestDifference)
            {
                bestDifference = difference;
                currentIndex = i;
            }
        }
        currentIndex = (currentIndex + direction + resolutions.Length) % resolutions.Length;
        PlayerPrefs.SetInt(ResolutionWidthKey, resolutions[currentIndex].width);
        PlayerPrefs.SetInt(ResolutionHeightKey, resolutions[currentIndex].height);
        ApplyResolution();
    }

    public static void SetResolution(int width, int height)
    {
        PlayerPrefs.SetInt(ResolutionWidthKey, width);
        PlayerPrefs.SetInt(ResolutionHeightKey, height);
        ApplyResolution();
    }

    public static int GetResolutionIndex(Resolution[] resolutions)
    {
        if (resolutions == null || resolutions.Length == 0)
            return 0;
        int bestIndex = 0;
        long bestDifference = long.MaxValue;
        for (int i = 0; i < resolutions.Length; i++)
        {
            long difference = Mathf.Abs(resolutions[i].width - ResolutionWidth)
                            + Mathf.Abs(resolutions[i].height - ResolutionHeight);
            if (difference < bestDifference)
            {
                bestDifference = difference;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    public static Resolution[] GetAvailableResolutions()
    {
        var unique = new Dictionary<Vector2Int, Resolution>();
        foreach (Resolution resolution in Screen.resolutions)
        {
            if (resolution.width < 1024 || resolution.height < 576)
                continue;
            unique[new Vector2Int(resolution.width, resolution.height)] = resolution;
        }
        if (unique.Count == 0)
        {
            Resolution current = Screen.currentResolution;
            unique[new Vector2Int(current.width, current.height)] = current;
        }
        var result = new List<Resolution>(unique.Values);
        result.Sort((a, b) =>
        {
            int pixels = (a.width * a.height).CompareTo(b.width * b.height);
            return pixels != 0 ? pixels : a.width.CompareTo(b.width);
        });
        return result.ToArray();
    }

    public static void Apply(DeliveryFollowCamera camera = null)
    {
        ApplyAudio();
        ApplyResolution();
        ApplyQuality(QualityLevel);
        if (camera != null)
            camera.ApplyUserPreferences(MouseSensitivity, FieldOfView);
        PlayerPrefs.Save();
    }

    public static void ApplyAudio()
    {
        AudioListener.volume = MasterMuted ? 0f : MasterVolume;
        foreach (RadioMusicController radio in Object.FindObjectsByType<RadioMusicController>())
            radio.RefreshUserVolume();
    }

    private static void ApplyResolution()
    {
        Screen.SetResolution(ResolutionWidth, ResolutionHeight, Fullscreen);
    }

    private static void ApplyQuality(int level)
    {
        int baseLevel = level == 0 ? 0 : Mathf.Max(0, QualitySettings.names.Length - 1);
        if (QualitySettings.names.Length > 0)
            QualitySettings.SetQualityLevel(baseLevel, false);

        switch (level)
        {
            case 0:
                QualitySettings.shadows = ShadowQuality.Disable;
                QualitySettings.shadowDistance = 25f;
                QualitySettings.lodBias = 0.8f;
                QualitySettings.antiAliasing = 0;
                break;
            case 1:
                QualitySettings.shadows = ShadowQuality.HardOnly;
                QualitySettings.shadowResolution = ShadowResolution.Low;
                QualitySettings.shadowDistance = 50f;
                QualitySettings.lodBias = 1.2f;
                QualitySettings.antiAliasing = 0;
                break;
            case 2:
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowResolution = ShadowResolution.Medium;
                QualitySettings.shadowDistance = 85f;
                QualitySettings.shadowCascades = 2;
                QualitySettings.lodBias = 1.7f;
                QualitySettings.antiAliasing = 2;
                break;
            default:
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowResolution = ShadowResolution.High;
                QualitySettings.shadowDistance = 130f;
                QualitySettings.shadowCascades = 4;
                QualitySettings.lodBias = 2.2f;
                QualitySettings.antiAliasing = 4;
                break;
        }
    }
}
