using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    private static readonly Color Ink = new(0.075f, 0.105f, 0.085f, 0.96f);
    private static readonly Color Paper = new(0.94f, 0.925f, 0.85f, 1f);
    private static readonly Color Muted = new(0.66f, 0.72f, 0.62f, 1f);
    private static readonly Color Accent = new(0.55f, 0.69f, 0.44f, 1f);
    private static readonly Color ButtonIdle = new(0.12f, 0.18f, 0.135f, 0.96f);
    private static readonly Color ButtonHover = new(0.21f, 0.3f, 0.21f, 1f);

    private static MainMenuController instance;
    private Font font;
    private Canvas canvas;
    private GameObject homePanel;
    private GameObject settingsPanel;
    private GameObject audioSettingsPanel;
    private GameObject displaySettingsPanel;
    private GameObject controlsSettingsPanel;
    private GameObject creditsPanel;
    private Text playButtonText;
    private Text masterVolumeValue;
    private Text masterMuteValue;
    private Text musicVolumeValue;
    private Text musicMuteValue;
    private Text sensitivityValue;
    private Text fovValue;
    private Text fullscreenValue;
    private Dropdown qualityDropdown;
    private Dropdown resolutionDropdown;
    private Resolution[] availableResolutions;
    private CanvasGroup scenicFade;
    private DeliveryFollowCamera followCamera;
    private ArcadeDeliveryVan van;
    private Rigidbody vanBody;
    private MainMenuCameraDirector cameraDirector;
    private bool gameplayStarted;
    private bool menuVisible = true;
    private ulong configuredSceneHandle = ulong.MaxValue;

    public static bool IsGameplayActive => instance != null
                                        && instance.gameplayStarted
                                        && !instance.menuVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;
        var root = new GameObject("Main Menu Runtime");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<MainMenuController>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        font = Font.CreateDynamicFontFromOSFont(
            new[] { "Bahnschrift SemiCondensed", "Bahnschrift", "Segoe UI Semibold", "Arial" },
            32);
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();
        BuildInterface();
        cameraDirector = gameObject.AddComponent<MainMenuCameraDirector>();
        SceneManager.sceneLoaded += HandleSceneLoaded;
        GameSettings.Apply();
    }

    private void Start()
    {
        StartCoroutine(ConfigureCurrentScene());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            return;

        if (menuVisible)
        {
            if (settingsPanel.activeSelf || creditsPanel.activeSelf)
                ShowHome();
            return;
        }

        if (gameplayStarted)
            OpenPauseMenu();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ConfigureCurrentScene());
    }

    private IEnumerator ConfigureCurrentScene()
    {
        yield return null;
        RoadSpline road = FindAnyObjectByType<RoadSpline>();
        followCamera = FindAnyObjectByType<DeliveryFollowCamera>();
        van = FindAnyObjectByType<ArcadeDeliveryVan>();
        if (road == null || followCamera == null || van == null)
            yield break;
        ulong sceneHandle = road.gameObject.scene.handle.GetRawData();
        if (configuredSceneHandle == sceneHandle)
            yield break;
        configuredSceneHandle = sceneHandle;

        vanBody = van.GetComponent<Rigidbody>();
        GameSettings.Apply(followCamera);
        FreezeGameplay();
        followCamera.SetMenuPresentation(true);
        Camera sceneCamera = followCamera.GetComponent<Camera>();
        cameraDirector.Begin(sceneCamera, road, van.transform, scenicFade);
        canvas.enabled = true;
        menuVisible = true;
        ShowHome();
    }

    private void FreezeGameplay()
    {
        if (followCamera != null)
            followCamera.enabled = false;
        if (van != null)
        {
            van.showPrototypeHud = false;
            van.enabled = false;
        }
        if (vanBody != null)
        {
            if (!vanBody.isKinematic)
            {
                vanBody.linearVelocity = Vector3.zero;
                vanBody.angularVelocity = Vector3.zero;
            }
            vanBody.useGravity = false;
            vanBody.isKinematic = true;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void BeginGame()
    {
        cameraDirector.Stop();
        canvas.enabled = false;
        menuVisible = false;
        Time.timeScale = 1f;
        if (vanBody != null)
        {
            vanBody.isKinematic = false;
            vanBody.useGravity = true;
            vanBody.WakeUp();
        }
        if (van != null)
        {
            van.showPrototypeHud = true;
            van.enabled = true;
        }
        if (followCamera != null)
        {
            GameSettings.Apply(followCamera);
            followCamera.SetMenuPresentation(false);
            followCamera.enabled = true;
            followCamera.SnapToTarget();
        }
        gameplayStarted = true;
    }

    private void OpenPauseMenu()
    {
        Time.timeScale = 0f;
        FreezeGameplay();
        followCamera.SetMenuPresentation(true);
        RoadSpline road = FindAnyObjectByType<RoadSpline>();
        cameraDirector.Begin(
            followCamera.GetComponent<Camera>(),
            road,
            van != null ? van.transform : null,
            scenicFade);
        playButtonText.text = "RESUME";
        canvas.enabled = true;
        menuVisible = true;
        ShowHome();
    }

    private void ShowHome()
    {
        homePanel.SetActive(true);
        settingsPanel.SetActive(false);
        creditsPanel.SetActive(false);
        if (playButtonText != null)
            playButtonText.text = gameplayStarted ? "RESUME" : "PLAY";
    }

    private void ShowSettings()
    {
        homePanel.SetActive(false);
        settingsPanel.SetActive(true);
        creditsPanel.SetActive(false);
        ShowAudioSettings();
        RefreshSettingLabels();
    }

    private void ShowAudioSettings()
    {
        audioSettingsPanel.SetActive(true);
        displaySettingsPanel.SetActive(false);
        controlsSettingsPanel.SetActive(false);
    }

    private void ShowDisplaySettings()
    {
        audioSettingsPanel.SetActive(false);
        displaySettingsPanel.SetActive(true);
        controlsSettingsPanel.SetActive(false);
    }

    private void ShowControlsSettings()
    {
        audioSettingsPanel.SetActive(false);
        displaySettingsPanel.SetActive(false);
        controlsSettingsPanel.SetActive(true);
    }

    private void ShowCredits()
    {
        homePanel.SetActive(false);
        settingsPanel.SetActive(false);
        creditsPanel.SetActive(true);
    }

    private void BuildInterface()
    {
        var canvasObject = new GameObject("Main Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image wash = CreateImage(canvas.transform, "Cinematic Wash", new Color(0.03f, 0.055f, 0.035f, 0.14f));
        Stretch(wash.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        wash.raycastTarget = false;

        Image fadeImage = CreateImage(canvas.transform, "Scenic Fade", Color.black);
        Stretch(fadeImage.rectTransform, new Vector2(0.37f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
        fadeImage.raycastTarget = false;
        scenicFade = fadeImage.gameObject.AddComponent<CanvasGroup>();
        scenicFade.alpha = 1f;

        Image left = CreateImage(canvas.transform, "Left Panel", Ink);
        Stretch(left.rectTransform, Vector2.zero, new Vector2(0.37f, 1f), Vector2.zero, Vector2.zero);

        Image accent = CreateImage(left.transform, "Accent", Accent);
        Stretch(accent.rectTransform, new Vector2(0f, 0f), new Vector2(0.012f, 1f), Vector2.zero, Vector2.zero);

        Text eyebrow = CreateText(left.transform, "GMTK GAME JAM  •  COUNT DOWN", 22, Accent, FontStyle.Bold, TextAnchor.MiddleLeft);
        Place(eyebrow.rectTransform, new Vector2(0.11f, 0.85f), new Vector2(0.9f, 0.91f));
        Text title = CreateText(left.transform, "DELIVERY IS THE ONLY\nTHING I CAN DO", 47, Paper, FontStyle.Normal, TextAnchor.MiddleLeft);
        title.lineSpacing = 0.82f;
        Place(title.rectTransform, new Vector2(0.11f, 0.7f), new Vector2(0.94f, 0.86f));

        homePanel = CreatePanel(left.transform, "Home");
        Place(homePanel.GetComponent<RectTransform>(), new Vector2(0.11f, 0.17f), new Vector2(0.9f, 0.61f));
        playButtonText = CreateMenuButton(homePanel.transform, "PLAY", 0.78f, BeginGame);
        CreateMenuButton(homePanel.transform, "SETTINGS", 0.52f, ShowSettings);
        CreateMenuButton(homePanel.transform, "CREDITS", 0.26f, ShowCredits);
        CreateMenuButton(homePanel.transform, "QUIT", 0f, QuitGame);

        settingsPanel = CreatePanel(left.transform, "Settings");
        Place(settingsPanel.GetComponent<RectTransform>(), new Vector2(0.11f, 0.09f), new Vector2(0.9f, 0.68f));
        CreateSectionTitle(settingsPanel.transform, "SETTINGS");
        CreateTopBackButton(settingsPanel.transform);

        CreateSettingsTab(settingsPanel.transform, "AUDIO", 0f, ShowAudioSettings);
        CreateSettingsTab(settingsPanel.transform, "DISPLAY", 0.34f, ShowDisplaySettings);
        CreateSettingsTab(settingsPanel.transform, "CONTROLS", 0.68f, ShowControlsSettings);

        audioSettingsPanel = CreatePanel(settingsPanel.transform, "Audio Settings");
        Place(audioSettingsPanel.GetComponent<RectTransform>(), new Vector2(0f, 0.02f), new Vector2(1f, 0.7f));
        masterVolumeValue = CreateAudioSetting(audioSettingsPanel.transform, "MASTER VOLUME", 0.58f, GameSettings.MasterVolume, ToggleMasterMute, out masterMuteValue, value =>
        {
            GameSettings.MasterVolume = value;
            masterVolumeValue.text = $"{Mathf.RoundToInt(value * 100f)}%";
        });
        musicVolumeValue = CreateAudioSetting(audioSettingsPanel.transform, "MUSIC VOLUME", 0.2f, GameSettings.MusicVolume, ToggleMusicMute, out musicMuteValue, value =>
        {
            GameSettings.MusicVolume = value;
            musicVolumeValue.text = $"{Mathf.RoundToInt(value * 100f)}%";
        });

        controlsSettingsPanel = CreatePanel(settingsPanel.transform, "Control Settings");
        Place(controlsSettingsPanel.GetComponent<RectTransform>(), new Vector2(0f, 0.02f), new Vector2(1f, 0.7f));
        sensitivityValue = CreateSettingSlider(controlsSettingsPanel.transform, "MOUSE SENSITIVITY", 0.58f, 0.02f, 0.3f, GameSettings.MouseSensitivity, value =>
        {
            GameSettings.MouseSensitivity = value;
            sensitivityValue.text = value.ToString("0.00");
            if (followCamera != null)
                followCamera.ApplyUserPreferences(value, GameSettings.FieldOfView);
        });
        fovValue = CreateSettingSlider(controlsSettingsPanel.transform, "FIELD OF VIEW", 0.2f, 50f, 85f, GameSettings.FieldOfView, value =>
        {
            GameSettings.FieldOfView = value;
            fovValue.text = Mathf.RoundToInt(value) + "°";
            if (followCamera != null)
                followCamera.ApplyUserPreferences(GameSettings.MouseSensitivity, value);
        });

        displaySettingsPanel = CreatePanel(settingsPanel.transform, "Display Settings");
        Place(displaySettingsPanel.GetComponent<RectTransform>(), new Vector2(0f, 0.02f), new Vector2(1f, 0.7f));
        qualityDropdown = CreateDropdown(
            displaySettingsPanel.transform,
            "QUALITY",
            0.62f,
            GameSettings.QualityNames,
            GameSettings.QualityLevel,
            SetQuality);
        availableResolutions = GameSettings.GetAvailableResolutions();
        string[] resolutionOptions = new string[availableResolutions.Length];
        for (int i = 0; i < availableResolutions.Length; i++)
            resolutionOptions[i] = $"{availableResolutions[i].width} × {availableResolutions[i].height}";
        resolutionDropdown = CreateDropdown(
            displaySettingsPanel.transform,
            "RESOLUTION",
            0.29f,
            resolutionOptions,
            GameSettings.GetResolutionIndex(availableResolutions),
            SetResolution);
        CreateSmallButton(displaySettingsPanel.transform, "FULLSCREEN", -0.04f, ToggleFullscreen, out fullscreenValue);

        creditsPanel = CreatePanel(left.transform, "Credits");
        Place(creditsPanel.GetComponent<RectTransform>(), new Vector2(0.11f, 0.12f), new Vector2(0.91f, 0.68f));
        CreateSectionTitle(creditsPanel.transform, "CREDITS");
        string credits =
            "FREE DELIVERY TRUCK  —  Miguel Vega / CC BY 4.0\n" +
            "CAR RADIO  —  Jakers_H / CC BY 3.0\n" +
            "DASHBOARD & STEERING  —  i7270 / CGTrader RF\n\n" +
            "ENGINE AUDIO  —  domasx2 + looneybits / CC0\n" +
            "RADIO MUSIC  —  Cosmo Myzrail Gorynych + pickentcode / CC0\n\n" +
            "CAR KIT + CITY KIT SUBURBAN  —  Kenney / CC0\n" +
            "STYLIZED NATURE MEGAKIT  —  Quaternius / CC0\n" +
            "QUIBLI ANIME SHADERS  —  Dustyroom / Unity Asset Store\n\n" +
            "DESIGN & DEVELOPMENT  —  Emir Ayar\n" +
            "GMTK Game Jam 2026";
        Text creditText = CreateText(creditsPanel.transform, credits, 20, Muted, FontStyle.Normal, TextAnchor.UpperLeft);
        creditText.horizontalOverflow = HorizontalWrapMode.Wrap;
        creditText.verticalOverflow = VerticalWrapMode.Overflow;
        Place(creditText.rectTransform, new Vector2(0f, 0.12f), new Vector2(1f, 0.82f));
        CreateBackButton(creditsPanel.transform);

        Text liveLabel = CreateText(canvas.transform, "●  LIVE WORLD", 18, Paper, FontStyle.Bold, TextAnchor.MiddleRight);
        Place(liveLabel.rectTransform, new Vector2(0.72f, 0.91f), new Vector2(0.96f, 0.97f));
        Text hint = CreateText(canvas.transform, "ESC  MENU", 16, new Color(1f, 1f, 1f, 0.72f), FontStyle.Bold, TextAnchor.MiddleRight);
        Place(hint.rectTransform, new Vector2(0.78f, 0.035f), new Vector2(0.96f, 0.085f));

        ShowHome();
    }

    private void RefreshSettingLabels()
    {
        masterVolumeValue.text = $"{Mathf.RoundToInt(GameSettings.MasterVolume * 100f)}%";
        masterMuteValue.text = GameSettings.MasterMuted ? "UNMUTE" : "MUTE";
        musicVolumeValue.text = $"{Mathf.RoundToInt(GameSettings.MusicVolume * 100f)}%";
        musicMuteValue.text = GameSettings.MusicMuted ? "UNMUTE" : "MUTE";
        sensitivityValue.text = GameSettings.MouseSensitivity.ToString("0.00");
        fovValue.text = Mathf.RoundToInt(GameSettings.FieldOfView) + "°";
        fullscreenValue.text = GameSettings.Fullscreen ? "ON" : "OFF";
        if (qualityDropdown != null)
            qualityDropdown.SetValueWithoutNotify(GameSettings.QualityLevel);
        if (resolutionDropdown != null)
            resolutionDropdown.SetValueWithoutNotify(GameSettings.GetResolutionIndex(availableResolutions));
    }

    private void ToggleMasterMute()
    {
        GameSettings.MasterMuted = !GameSettings.MasterMuted;
        RefreshSettingLabels();
    }

    private void ToggleMusicMute()
    {
        GameSettings.MusicMuted = !GameSettings.MusicMuted;
        RefreshSettingLabels();
    }

    private void ToggleFullscreen()
    {
        GameSettings.Fullscreen = !GameSettings.Fullscreen;
        RefreshSettingLabels();
    }

    private void SetQuality(int level)
    {
        GameSettings.QualityLevel = level;
        RefreshSettingLabels();
    }

    private void SetResolution(int index)
    {
        if (availableResolutions == null
            || index < 0
            || index >= availableResolutions.Length)
        {
            return;
        }
        Resolution resolution = availableResolutions[index];
        GameSettings.SetResolution(resolution.width, resolution.height);
        RefreshSettingLabels();
    }

    private void QuitGame()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private GameObject CreatePanel(Transform parent, string name)
    {
        var panel = new GameObject(name, typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        return panel;
    }

    private void CreateSectionTitle(Transform parent, string label)
    {
        Text text = CreateText(parent, label, 34, Paper, FontStyle.Bold, TextAnchor.MiddleLeft);
        Place(text.rectTransform, new Vector2(0f, 0.85f), new Vector2(1f, 1f));
    }

    private Text CreateMenuButton(Transform parent, string label, float y, UnityEngine.Events.UnityAction action)
    {
        Button button = CreateButton(parent, label, 25, action);
        Place(button.GetComponent<RectTransform>(), new Vector2(0f, y), new Vector2(1f, y + 0.2f));
        return button.GetComponentInChildren<Text>();
    }

    private void CreateSmallButton(Transform parent, string label, float y, UnityEngine.Events.UnityAction action, out Text value)
    {
        Text caption = CreateText(parent, label, 17, Muted, FontStyle.Bold, TextAnchor.MiddleLeft);
        Place(caption.rectTransform, new Vector2(0f, y + 0.11f), new Vector2(0.52f, y + 0.23f));
        Button button = CreateButton(parent, "", 18, action);
        Place(button.GetComponent<RectTransform>(), new Vector2(0.55f, y + 0.1f), new Vector2(1f, y + 0.24f));
        value = button.GetComponentInChildren<Text>();
    }

    private void CreateSettingsTab(
        Transform parent,
        string label,
        float x,
        UnityEngine.Events.UnityAction action)
    {
        Button tab = CreateButton(parent, label, 15, action);
        Place(tab.GetComponent<RectTransform>(), new Vector2(x, 0.72f), new Vector2(x + 0.32f, 0.82f));
        tab.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
    }

    private Dropdown CreateDropdown(
        Transform parent,
        string label,
        float y,
        string[] options,
        int selected,
        UnityEngine.Events.UnityAction<int> changed)
    {
        Text caption = CreateText(parent, label, 16, Muted, FontStyle.Normal, TextAnchor.MiddleLeft);
        Place(caption.rectTransform, new Vector2(0f, y + 0.18f), new Vector2(1f, y + 0.31f));

        Image background = CreateImage(parent, label + " Dropdown", Color.white);
        Place(background.rectTransform, new Vector2(0f, y), new Vector2(1f, y + 0.17f));
        Dropdown dropdown = background.gameObject.AddComponent<Dropdown>();
        dropdown.targetGraphic = background;
        ColorBlock colors = dropdown.colors;
        colors.normalColor = ButtonIdle;
        colors.highlightedColor = ButtonHover;
        colors.pressedColor = Accent;
        colors.selectedColor = ButtonHover;
        dropdown.colors = colors;

        Text selectedLabel = CreateText(background.transform, "", 18, Paper, FontStyle.Normal, TextAnchor.MiddleLeft);
        Stretch(selectedLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(22f, 0f), new Vector2(-54f, 0f));
        Text arrow = CreateText(background.transform, "▼", 16, Accent, FontStyle.Normal, TextAnchor.MiddleCenter);
        Place(arrow.rectTransform, new Vector2(0.88f, 0f), Vector2.one);

        Image template = CreateImage(background.transform, "Template", new Color(0.07f, 0.105f, 0.08f, 0.99f));
        RectTransform templateRect = template.rectTransform;
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.anchoredPosition = new Vector2(0f, -6f);
        templateRect.sizeDelta = new Vector2(0f, 240f);
        ScrollRect scroll = template.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        Image viewport = CreateImage(template.transform, "Viewport", Color.clear);
        Stretch(viewport.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(viewport.transform, false);
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 38f);

        Image item = CreateImage(content, "Item", Color.white);
        RectTransform itemRect = item.rectTransform;
        itemRect.anchorMin = new Vector2(0f, 0.5f);
        itemRect.anchorMax = new Vector2(1f, 0.5f);
        itemRect.sizeDelta = new Vector2(0f, 42f);
        Toggle toggle = item.gameObject.AddComponent<Toggle>();
        ColorBlock itemColors = toggle.colors;
        itemColors.normalColor = ButtonIdle;
        itemColors.highlightedColor = ButtonHover;
        itemColors.selectedColor = ButtonHover;
        toggle.colors = itemColors;

        Image check = CreateImage(item.transform, "Checkmark", Accent);
        Stretch(check.rectTransform, new Vector2(0f, 0f), new Vector2(0.018f, 1f), Vector2.zero, Vector2.zero);
        toggle.targetGraphic = item;
        toggle.graphic = check;
        Text itemLabel = CreateText(item.transform, "Option", 16, Paper, FontStyle.Normal, TextAnchor.MiddleLeft);
        Stretch(itemLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-10f, 0f));

        scroll.viewport = viewport.rectTransform;
        scroll.content = content;
        dropdown.template = templateRect;
        dropdown.captionText = selectedLabel;
        dropdown.itemText = itemLabel;
        dropdown.ClearOptions();
        var dropdownOptions = new System.Collections.Generic.List<Dropdown.OptionData>();
        foreach (string option in options)
            dropdownOptions.Add(new Dropdown.OptionData(option));
        dropdown.AddOptions(dropdownOptions);
        dropdown.SetValueWithoutNotify(Mathf.Clamp(selected, 0, Mathf.Max(0, options.Length - 1)));
        dropdown.RefreshShownValue();
        dropdown.onValueChanged.AddListener(changed);
        template.gameObject.SetActive(false);
        return dropdown;
    }

    private Text CreateAudioSetting(
        Transform parent,
        string label,
        float y,
        float current,
        UnityEngine.Events.UnityAction muteAction,
        out Text muteValue,
        UnityEngine.Events.UnityAction<float> changed)
    {
        Text value = CreateSettingSlider(parent, label, y, 0f, 1f, current, changed);
        Place(value.rectTransform, new Vector2(0.54f, y + 0.13f), new Vector2(0.7f, y + 0.25f));
        Button mute = CreateButton(parent, "MUTE", 14, muteAction);
        Place(mute.GetComponent<RectTransform>(), new Vector2(0.73f, y + 0.13f), new Vector2(1f, y + 0.245f));
        muteValue = mute.GetComponentInChildren<Text>();
        muteValue.alignment = TextAnchor.MiddleCenter;
        return value;
    }

    private void CreateHalfButton(
        Transform parent,
        string label,
        float y,
        bool left,
        UnityEngine.Events.UnityAction action,
        out Text value)
    {
        float minX = left ? 0f : 0.52f;
        float maxX = left ? 0.48f : 1f;
        Text caption = CreateText(parent, label, 15, Muted, FontStyle.Normal, TextAnchor.MiddleLeft);
        Place(caption.rectTransform, new Vector2(minX, y + 0.12f), new Vector2(maxX, y + 0.22f));
        Button button = CreateButton(parent, "", 16, action);
        Place(button.GetComponent<RectTransform>(), new Vector2(minX, y), new Vector2(maxX, y + 0.115f));
        value = button.GetComponentInChildren<Text>();
        value.alignment = TextAnchor.MiddleCenter;
    }

    private void CreateTopBackButton(Transform parent)
    {
        Button back = CreateButton(parent, "BACK", 14, ShowHome);
        Place(back.GetComponent<RectTransform>(), new Vector2(0.72f, 0.88f), new Vector2(1f, 0.98f));
        back.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
    }

    private Text CreateSettingSlider(
        Transform parent,
        string label,
        float y,
        float min,
        float max,
        float current,
        UnityEngine.Events.UnityAction<float> changed)
    {
        Text caption = CreateText(parent, label, 17, Muted, FontStyle.Bold, TextAnchor.MiddleLeft);
        Place(caption.rectTransform, new Vector2(0f, y + 0.13f), new Vector2(0.7f, y + 0.25f));
        Text value = CreateText(parent, "", 18, Paper, FontStyle.Bold, TextAnchor.MiddleRight);
        Place(value.rectTransform, new Vector2(0.72f, y + 0.13f), new Vector2(1f, y + 0.25f));

        Image background = CreateImage(parent, label + " Slider", new Color(1f, 1f, 1f, 0.14f));
        Place(background.rectTransform, new Vector2(0f, y + 0.05f), new Vector2(1f, y + 0.09f));
        Slider slider = background.gameObject.AddComponent<Slider>();
        Image fill = CreateImage(background.transform, "Fill", Accent);
        Stretch(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image handle = CreateImage(background.transform, "Handle", Paper);
        RectTransform handleRect = handle.rectTransform;
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.sizeDelta = new Vector2(18f, 28f);
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = current;
        slider.onValueChanged.AddListener(changed);
        return value;
    }

    private void CreateBackButton(Transform parent)
    {
        Button back = CreateButton(parent, "←  BACK", 18, ShowHome);
        Place(back.GetComponent<RectTransform>(), new Vector2(0f, -0.29f), new Vector2(0.43f, -0.14f));
    }

    private Button CreateButton(Transform parent, string label, int fontSize, UnityEngine.Events.UnityAction action)
    {
        Image image = CreateImage(parent, label + " Button", Color.white);
        Button button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = ButtonIdle;
        colors.highlightedColor = ButtonHover;
        colors.pressedColor = Accent;
        colors.selectedColor = ButtonHover;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(action);
        Text text = CreateText(image.transform, label, fontSize, Paper, FontStyle.Bold, TextAnchor.MiddleLeft);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(26f, 0f), new Vector2(-20f, 0f));
        return button;
    }

    private Image CreateImage(Transform parent, string name, Color color)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private Text CreateText(Transform parent, string value, int size, Color color, FontStyle style, TextAnchor alignment)
    {
        var gameObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        gameObject.transform.SetParent(parent, false);
        Text text = gameObject.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;
        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        DontDestroyOnLoad(eventSystem);
    }
}
