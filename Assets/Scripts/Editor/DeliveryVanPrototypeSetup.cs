using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DeliveryVanPrototypeSetup
{
    private const string KenneyDeliveryPath =
        "Assets/ThirdPartyAssets/KenneyCarKit/Models/delivery.fbx";
    private const string CarRadioPath =
        "Assets/ThirdPartyAssets/CarRadio/CarRadio.fbx";
    private const string ImportedDashboardPath =
        "Assets/ThirdPartyAssets/CarRadio/"
        + "uploads_files_977855_Dashboard_wsteering.fbx";
    private const string FreeDeliveryTruckFolder =
        "Assets/ThirdPartyAssets/FreeDeliveryTruck";
    private const string FreeDeliveryTruckPath =
        "Assets/ThirdPartyAssets/FreeDeliveryTruck/Imported/source/FreeDeliveryTruck.fbx";

    [InitializeOnLoadMethod]
    private static void ScheduleFreeDeliveryTruckVisual()
    {
        string key = "GMTK.FreeDeliveryTruck.v1."
                   + Hash128.Compute(Application.dataPath);
        if (EditorPrefs.GetBool(key, false))
            return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                    FreeDeliveryTruckPath) == null)
                return;
            if (Object.FindAnyObjectByType<ArcadeDeliveryVan>() == null)
                return;

            InstallFreeDeliveryTruckVisual();
            EditorPrefs.SetBool(key, true);
        };
    }

    [InitializeOnLoadMethod]
    private static void ScheduleKenneyPlayerVisual()
    {
        string key = "GMTK.KenneyPlayerDelivery.v1."
                   + Hash128.Compute(Application.dataPath);
        if (EditorPrefs.GetBool(key, false))
            return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            ArcadeDeliveryVan van =
                Object.FindAnyObjectByType<ArcadeDeliveryVan>();
            if (van == null)
                return;

            if (!InstallKenneyDeliveryVisual(van))
                return;

            ConfigureBodyCollider(van.gameObject);
            ConfigureCamera(van);
            EditorSceneManager.MarkSceneDirty(van.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            EditorPrefs.SetBool(key, true);
            Debug.Log("Kenney delivery truck installed as player visual.", van);
        };
    }

    [InitializeOnLoadMethod]
    private static void ScheduleCockpitBuild()
    {
        string key = "GMTK.DeliveryVanCockpit.v18."
                   + Hash128.Compute(Application.dataPath);
        if (EditorPrefs.GetBool(key, false))
            return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            ArcadeDeliveryVan van =
                Object.FindAnyObjectByType<ArcadeDeliveryVan>();
            if (van == null)
                return;

            BuildCockpit(van);
            ConfigureCamera(van);
            EditorSceneManager.MarkSceneDirty(van.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            EditorPrefs.SetBool(key, true);
            Debug.Log(
                "Delivery van cockpit and first-person view ready. Press C.",
                van);
        };
    }

    [InitializeOnLoadMethod]
    private static void ScheduleColliderFix()
    {
        string key = "GMTK.DeliveryVanCollider.v3."
                   + Hash128.Compute(Application.dataPath);
        if (EditorPrefs.GetBool(key, false))
            return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            ArcadeDeliveryVan van =
                Object.FindAnyObjectByType<ArcadeDeliveryVan>();
            if (van == null)
                return;

            ConfigureBodyCollider(van.gameObject);
            EditorUtility.SetDirty(van.gameObject);
            EditorSceneManager.MarkSceneDirty(van.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            EditorPrefs.SetBool(key, true);
            Debug.Log(
                "Delivery van collider upgraded to a rounded capsule.",
                van);
        };
    }

    [InitializeOnLoadMethod]
    private static void ScheduleGroundingFix()
    {
        string key = "GMTK.DeliveryVanPhysicsRig.v4."
                   + Hash128.Compute(Application.dataPath);
        if (EditorPrefs.GetBool(key, false))
            return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            ArcadeDeliveryVan van =
                Object.FindAnyObjectByType<ArcadeDeliveryVan>();
            RoadSpline road = Object.FindAnyObjectByType<RoadSpline>();
            if (van == null || road == null)
                return;

            road.GetFrame(
                0.035f,
                out Vector3 roadCenter,
                out Vector3 roadForward,
                out _);
            Vector3 flatForward = Vector3.ProjectOnPlane(
                roadForward,
                Vector3.up);
            if (flatForward.sqrMagnitude < 0.001f)
                flatForward = Vector3.forward;

            Undo.RecordObject(van.transform, "Fix delivery van grounding");
            Undo.RecordObject(van, "Fix delivery van grounding");
            van.transform.SetPositionAndRotation(
                roadCenter + Vector3.up * 0.28f,
                Quaternion.LookRotation(flatForward.normalized, Vector3.up));
            van.vehicleMass = 1180f;
            van.wheelRadius = 0.32f;
            van.suspensionDistance = 0.24f;
            van.suspensionSpring = 32000f;
            van.suspensionDamper = 4300f;
            van.maximumMotorTorque = 1500f;
            van.serviceBrakeTorque = 3100f;
            van.centerOfMass = new Vector3(0f, -0.48f, 0.08f);
            EditorUtility.SetDirty(van);
            EditorSceneManager.MarkSceneDirty(van.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            EditorPrefs.SetBool(key, true);
            Debug.Log("Delivery van grounding fixed.", van);
        };
    }

    [InitializeOnLoadMethod]
    private static void ScheduleFirstPrototypeBuild()
    {
        string key = GetPrototypeBuildKey();
        if (EditorPrefs.GetBool(key, false))
            return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (Object.FindAnyObjectByType<ArcadeDeliveryVan>() != null)
            {
                EditorPrefs.SetBool(key, true);
                return;
            }
            if (Object.FindAnyObjectByType<RoadSpline>() == null)
                return;

            BuildVan();
            EditorPrefs.SetBool(key, true);
            EditorSceneManager.SaveOpenScenes();
        };
    }

    [MenuItem("GMTK/Build Drivable Delivery Van %#v")]
    public static void BuildVan()
    {
        RoadSpline road = Object.FindAnyObjectByType<RoadSpline>();
        if (road == null)
        {
            Debug.LogError(
                "RoadSpline bulunamadı. Önce procedural world prototipini kur.");
            return;
        }

        ArcadeDeliveryVan van = Object.FindAnyObjectByType<ArcadeDeliveryVan>();
        if (van == null)
            van = CreateVan(road);

        ConfigureCamera(van);

        GpuProceduralGrass grass =
            road.GetComponent<GpuProceduralGrass>();
        if (grass != null)
        {
            grass.interactor = van.transform;
            EditorUtility.SetDirty(grass);
        }

        EditorUtility.SetDirty(van);
        EditorSceneManager.MarkSceneDirty(van.gameObject.scene);
        Selection.activeGameObject = van.gameObject;
        Debug.Log(
            "Drivable delivery van ready. Press Play and use WASD.",
            van);
    }

    [MenuItem("GMTK/Build Delivery Van Cockpit")]
    public static void BuildSelectedVanCockpit()
    {
        ArcadeDeliveryVan van =
            Object.FindAnyObjectByType<ArcadeDeliveryVan>();
        if (van == null)
        {
            Debug.LogError("Delivery Van bulunamadi.");
            return;
        }

        BuildCockpit(van);
        ConfigureCamera(van);
        EditorSceneManager.MarkSceneDirty(van.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = van.transform
            .Find("Cockpit Interior")?.gameObject;
    }

    [MenuItem("GMTK/Install Kenney Player Truck")]
    public static void InstallSelectedKenneyTruck()
    {
        ArcadeDeliveryVan van =
            Object.FindAnyObjectByType<ArcadeDeliveryVan>();
        if (van == null || !InstallKenneyDeliveryVisual(van))
            return;

        ConfigureBodyCollider(van.gameObject);
        ConfigureCamera(van);
        EditorSceneManager.MarkSceneDirty(van.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = van.transform
            .Find("Kenney Player Exterior")?.gameObject;
    }

    [MenuItem("GMTK/Install Free Delivery Truck Visual")]
    public static void InstallFreeDeliveryTruckVisual()
    {
        ArcadeDeliveryVan van =
            Object.FindAnyObjectByType<ArcadeDeliveryVan>();
        if (van == null)
        {
            Debug.LogError("Delivery Van bulunamadi.");
            return;
        }

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
            FreeDeliveryTruckPath);
        if (source == null)
        {
            Debug.LogError(
                "Free Delivery Truck model dosyasi bulunamadi. "
                + $"FBX/OBJ dosyasini {FreeDeliveryTruckFolder} klasorune koy.");
            return;
        }

        Transform previous = van.transform.Find("Player Exterior");
        if (previous != null)
            Undo.DestroyObjectImmediate(previous.gameObject);

        GameObject visual = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (visual == null)
            return;

        Undo.RegisterCreatedObjectUndo(visual, "Install Free Delivery Truck");
        visual.name = "Player Exterior";
        visual.transform.SetParent(van.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        NormalizeVehicleVisual(visual.transform, 1.86f, 3.75f);
        ApplyFreeDeliveryTruckMaterials(visual.transform);

        Transform kenney = van.transform.Find("Kenney Player Exterior");
        if (kenney != null)
        {
            Undo.RecordObject(kenney.gameObject, "Deactivate Kenney truck");
            kenney.gameObject.SetActive(false);
            EditorUtility.SetDirty(kenney.gameObject);
        }

        SetPrototypeRendererEnabled(van.transform, "Van Body", false);
        SetPrototypeRendererEnabled(van.transform, "Windshield", false);
        ConfigureBodyCollider(van.gameObject);
        ConfigureCamera(van);
        EditorUtility.SetDirty(visual);
        EditorSceneManager.MarkSceneDirty(van.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = visual;
        Debug.Log(
            "Free Delivery Truck aktif edildi; Kenney kamyon pasif olarak korundu.",
            visual);
    }

    [MenuItem("GMTK/Vehicle Visual/Use Free Delivery Truck")]
    public static void UseFreeDeliveryTruckVisual()
    {
        SetVehicleVisualChoice(false);
    }

    [MenuItem("GMTK/Vehicle Visual/Use Kenney Delivery Truck")]
    public static void UseKenneyDeliveryTruckVisual()
    {
        SetVehicleVisualChoice(true);
    }

    private static void SetVehicleVisualChoice(bool useKenney)
    {
        ArcadeDeliveryVan van =
            Object.FindAnyObjectByType<ArcadeDeliveryVan>();
        if (van == null)
            return;

        Transform kenney = van.transform.Find("Kenney Player Exterior");
        Transform freeTruck = van.transform.Find("Player Exterior");
        if (kenney != null)
        {
            Undo.RecordObject(kenney.gameObject, "Switch vehicle visual");
            kenney.gameObject.SetActive(useKenney);
            EditorUtility.SetDirty(kenney.gameObject);
        }
        if (freeTruck != null)
        {
            Undo.RecordObject(freeTruck.gameObject, "Switch vehicle visual");
            freeTruck.gameObject.SetActive(!useKenney);
            EditorUtility.SetDirty(freeTruck.gameObject);
        }

        EditorSceneManager.MarkSceneDirty(van.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = useKenney
            ? kenney?.gameObject
            : freeTruck?.gameObject;
    }

    private static ArcadeDeliveryVan CreateVan(RoadSpline road)
    {
        var root = new GameObject("Delivery Van");
        Undo.RegisterCreatedObjectUndo(root, "Create delivery van");
        root.tag = "Player";

        road.GetFrame(
            0.035f,
            out Vector3 roadCenter,
            out Vector3 roadForward,
            out _);
        Vector3 flatForward = Vector3.ProjectOnPlane(roadForward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.forward;
        root.transform.SetPositionAndRotation(
            roadCenter + Vector3.up * 0.08f,
            Quaternion.LookRotation(flatForward.normalized, Vector3.up));

        Rigidbody rigidbody = root.AddComponent<Rigidbody>();
        rigidbody.mass = 1180f;
        rigidbody.linearDamping = 0.035f;
        rigidbody.angularDamping = 0.55f;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbody.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;
        rigidbody.constraints = RigidbodyConstraints.None;
        rigidbody.centerOfMass = new Vector3(0f, -0.48f, 0.08f);

        ConfigureBodyCollider(root);

        ArcadeDeliveryVan van = root.AddComponent<ArcadeDeliveryVan>();
        van.roadSpline = road;

        CreateVisuals(root.transform);
        return van;
    }

    private static void ConfigureBodyCollider(GameObject root)
    {
        BoxCollider box = root.GetComponent<BoxCollider>();
        if (box != null)
        {
            Undo.RecordObject(box, "Disable snagging van collider");
            box.enabled = false;
            EditorUtility.SetDirty(box);
        }

        CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = Undo.AddComponent<CapsuleCollider>(root);

        Undo.RecordObject(capsule, "Configure rounded van collider");
        capsule.direction = 2;
        capsule.center = new Vector3(0f, 0.82f, 0f);
        capsule.radius = 0.68f;
        capsule.height = 3.35f;
        capsule.contactOffset = 0.015f;
        EditorUtility.SetDirty(capsule);
    }

    private static void CreateVisuals(Transform root)
    {
        Material bodyMaterial = GetOrCreateMaterial(
            "Assets/Settings/PrototypeVanBody.mat",
            new Color(0.91f, 0.28f, 0.11f));
        Material windowMaterial = GetOrCreateMaterial(
            "Assets/Settings/PrototypeVanWindows.mat",
            new Color(0.08f, 0.22f, 0.29f));
        Material wheelMaterial = GetOrCreateMaterial(
            "Assets/Settings/PrototypeVanWheels.mat",
            new Color(0.035f, 0.035f, 0.035f));

        CreatePrimitiveChild(
            root,
            PrimitiveType.Cube,
            "Van Body",
            new Vector3(0f, 0.68f, -0.15f),
            new Vector3(1.72f, 1.12f, 3.1f),
            Quaternion.identity,
            bodyMaterial);
        CreatePrimitiveChild(
            root,
            PrimitiveType.Cube,
            "Windshield",
            new Vector3(0f, 1.18f, 1.42f),
            new Vector3(1.48f, 0.58f, 0.06f),
            Quaternion.Euler(-11f, 0f, 0f),
            windowMaterial);

        Vector3[] wheelPositions =
        {
            new(-0.92f, 0.38f, 1.05f),
            new(0.92f, 0.38f, 1.05f),
            new(-0.92f, 0.38f, -1.05f),
            new(0.92f, 0.38f, -1.05f)
        };
        foreach (Vector3 position in wheelPositions)
        {
            CreatePrimitiveChild(
                root,
                PrimitiveType.Cylinder,
                "Wheel",
                position,
                new Vector3(0.34f, 0.16f, 0.34f),
                Quaternion.Euler(0f, 0f, 90f),
                wheelMaterial);
        }
    }

    private static bool InstallKenneyDeliveryVisual(ArcadeDeliveryVan van)
    {
        Transform existing = van.transform.Find("Kenney Player Exterior");
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
            KenneyDeliveryPath);
        if (source == null)
        {
            Debug.LogError(
                $"Kenney delivery model bulunamadi: {KenneyDeliveryPath}");
            return false;
        }

        GameObject visual = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (visual == null)
            return false;

        Undo.RegisterCreatedObjectUndo(visual, "Install Kenney player truck");
        visual.name = "Kenney Player Exterior";
        visual.transform.SetParent(van.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one * 1.06f;

        SetPrototypeRendererEnabled(van.transform, "Van Body", false);
        SetPrototypeRendererEnabled(van.transform, "Windshield", false);
        for (int i = 0; i < van.transform.childCount; i++)
        {
            Transform child = van.transform.GetChild(i);
            if (!child.name.StartsWith("Wheel"))
                continue;
            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null)
            {
                Undo.RecordObject(renderer, "Hide prototype wheel");
                renderer.enabled = false;
                EditorUtility.SetDirty(renderer);
            }
        }

        EditorUtility.SetDirty(visual);
        return true;
    }

    private static void SetPrototypeRendererEnabled(
        Transform root,
        string childName,
        bool enabled)
    {
        Renderer renderer = root.Find(childName)?.GetComponent<Renderer>();
        if (renderer == null)
            return;
        Undo.RecordObject(renderer, "Toggle prototype vehicle visual");
        renderer.enabled = enabled;
        EditorUtility.SetDirty(renderer);
    }

    private static void BuildCockpit(ArcadeDeliveryVan van)
    {
        Transform existing = van.transform.Find("Cockpit Interior");
        Vector3 radioPosition = new Vector3(-0.004f, 0.916f, 0.953f);
        Quaternion radioRotation = Quaternion.Euler(73.9f, 0f, 0f);
        Vector3 radioScale = Vector3.one * 10f;
        Transform existingRadio = existing?.Find(
            "Imported Dashboard + Radio/Car Radio");
        if (existingRadio != null)
        {
            radioPosition = existingRadio.localPosition;
            radioRotation = existingRadio.localRotation;
            radioScale = existingRadio.localScale;
        }
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        Material dashboardMaterial = GetOrCreateMaterial(
            "Assets/Settings/CockpitDashboard.mat",
            new Color(0.055f, 0.105f, 0.12f));
        Material trimMaterial = GetOrCreateMaterial(
            "Assets/Settings/CockpitTrim.mat",
            new Color(0.025f, 0.035f, 0.04f));
        Material gaugeMaterial = GetOrCreateMaterial(
            "Assets/Settings/CockpitGauge.mat",
            new Color(0.76f, 0.88f, 0.82f));
        Material needleMaterial = GetOrCreateMaterial(
            "Assets/Settings/CockpitNeedle.mat",
            new Color(0.95f, 0.25f, 0.11f));
        Material accentMaterial = GetOrCreateMaterial(
            "Assets/Settings/CockpitAccent.mat",
            new Color(0.12f, 0.42f, 0.45f));
        Material upholsteryMaterial = GetOrCreateMaterial(
            "Assets/Settings/CockpitUpholstery.mat",
            new Color(0.075f, 0.16f, 0.19f));
        Material metalMaterial = GetOrCreateMaterial(
            "Assets/Settings/CockpitMetal.mat",
            new Color(0.36f, 0.43f, 0.45f));
        Material screenMaterial = GetOrCreateMaterial(
            "Assets/Settings/CockpitScreen.mat",
            new Color(0.055f, 0.36f, 0.46f));
        Material mirrorMaterial = GetOrCreateMaterial(
            "Assets/Settings/CockpitMirror.mat",
            new Color(0.48f, 0.72f, 0.78f));
        Material glassMaterial = GetOrCreateTransparentMaterial(
            "Assets/Settings/CockpitGlass.mat",
            new Color(0.34f, 0.72f, 0.82f, 0.22f));

        var cockpitRoot = new GameObject("Cockpit Interior");
        Undo.RegisterCreatedObjectUndo(
            cockpitRoot,
            "Build delivery van cockpit");
        cockpitRoot.transform.SetParent(van.transform, false);

        var handmadeDashboard = new GameObject("Handmade Dashboard");
        handmadeDashboard.transform.SetParent(cockpitRoot.transform, false);
        bool hasImportedDashboard = AssetDatabase.LoadAssetAtPath<GameObject>(
            ImportedDashboardPath) != null;

        CreatePrimitiveChild(
            handmadeDashboard.transform,
            PrimitiveType.Cube,
            "Dashboard Base",
            new Vector3(0f, 0.88f, 1.06f),
            new Vector3(1.46f, 0.18f, 0.34f),
            Quaternion.Euler(-5f, 0f, 0f),
            dashboardMaterial);
        CreatePrimitiveChild(
            handmadeDashboard.transform,
            PrimitiveType.Cube,
            "Dashboard Accent",
            new Vector3(0.25f, 0.98f, 0.87f),
            new Vector3(0.62f, 0.035f, 0.035f),
            Quaternion.identity,
            accentMaterial);
        CreatePrimitiveChild(
            handmadeDashboard.transform,
            PrimitiveType.Cube,
            "Center Console",
            new Vector3(0.2f, 0.68f, 0.68f),
            new Vector3(0.28f, 0.38f, 0.62f),
            Quaternion.Euler(-8f, 0f, 0f),
            dashboardMaterial);
        CreatePrimitiveChild(
            cockpitRoot.transform,
            PrimitiveType.Cube,
            "Cockpit Floor",
            new Vector3(0f, 0.47f, 0.18f),
            new Vector3(1.42f, 0.12f, 1.55f),
            Quaternion.identity,
            trimMaterial);

        CreateCabinShell(
            cockpitRoot.transform,
            dashboardMaterial,
            trimMaterial,
            upholsteryMaterial,
            glassMaterial);
        CreateDoorDetails(
            cockpitRoot.transform,
            trimMaterial,
            accentMaterial,
            mirrorMaterial);
        CreateCabinGapFillers(
            cockpitRoot.transform,
            dashboardMaterial,
            trimMaterial,
            accentMaterial);
        CreateCenterConsoleDetails(
            handmadeDashboard.transform,
            trimMaterial,
            metalMaterial,
            screenMaterial,
            accentMaterial);
        CreatePassengerDashboardDetails(
            handmadeDashboard.transform,
            dashboardMaterial,
            trimMaterial,
            accentMaterial);
        CreatePrimitiveChild(
            cockpitRoot.transform,
            PrimitiveType.Cube,
            "Left Door Interior",
            new Vector3(-0.76f, 0.91f, 0.4f),
            new Vector3(0.08f, 0.54f, 1.18f),
            Quaternion.identity,
            dashboardMaterial);
        CreatePrimitiveChild(
            cockpitRoot.transform,
            PrimitiveType.Cube,
            "Right Door Interior",
            new Vector3(0.76f, 0.91f, 0.4f),
            new Vector3(0.08f, 0.54f, 1.18f),
            Quaternion.identity,
            dashboardMaterial);

        Transform steering = CreateSteeringWheel(
            handmadeDashboard.transform,
            trimMaterial);
        Transform needle = CreateGaugeCluster(
            handmadeDashboard.transform,
            gaugeMaterial,
            needleMaterial,
            trimMaterial);

        CreatePrimitiveChild(
            cockpitRoot.transform,
            PrimitiveType.Cube,
            "Left Windshield Pillar",
            new Vector3(-0.72f, 1.4f, 1.22f),
            new Vector3(0.045f, 0.72f, 0.045f),
            Quaternion.Euler(-10f, 0f, -5f),
            trimMaterial);
        CreatePrimitiveChild(
            cockpitRoot.transform,
            PrimitiveType.Cube,
            "Right Windshield Pillar",
            new Vector3(0.72f, 1.4f, 1.22f),
            new Vector3(0.045f, 0.72f, 0.045f),
            Quaternion.Euler(-10f, 0f, 5f),
            trimMaterial);
        CreatePrimitiveChild(
            cockpitRoot.transform,
            PrimitiveType.Cube,
            "Rear View Mirror",
            new Vector3(0f, 1.62f, 1.27f),
            new Vector3(0.24f, 0.07f, 0.035f),
            Quaternion.identity,
            trimMaterial);
        CreatePrimitiveChild(
            cockpitRoot.transform,
            PrimitiveType.Cube,
            "Windshield Header",
            new Vector3(0f, 1.72f, 1.24f),
            new Vector3(1.42f, 0.055f, 0.045f),
            Quaternion.identity,
            trimMaterial);
        CreatePrimitiveChild(
            cockpitRoot.transform,
            PrimitiveType.Cube,
            "Mirror Stalk",
            new Vector3(0f, 1.67f, 1.27f),
            new Vector3(0.035f, 0.11f, 0.035f),
            Quaternion.identity,
            trimMaterial);

        _ = steering;
        _ = needle;

        if (hasImportedDashboard)
        {
            handmadeDashboard.SetActive(false);
            InstallImportedDashboard(
                cockpitRoot.transform,
                radioPosition,
                radioRotation,
                radioScale);
        }
        else
        {
            InstallCarRadio(
                handmadeDashboard.transform,
                radioPosition,
                radioRotation,
                radioScale);
            Debug.LogWarning(
                $"Dashboard modeli bulunamadi: {ImportedDashboardPath}");
        }
    }

    private static void InstallImportedDashboard(
        Transform cockpitRoot,
        Vector3 radioPosition,
        Quaternion radioRotation,
        Vector3 radioScale)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
            ImportedDashboardPath);
        if (source == null)
            return;

        var group = new GameObject("Imported Dashboard + Radio");
        group.transform.SetParent(cockpitRoot, false);

        GameObject dashboard =
            PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (dashboard == null)
            return;

        Undo.RegisterCreatedObjectUndo(
            dashboard,
            "Install imported cockpit dashboard");
        dashboard.name = "Steering Wheel and Dashboard";
        dashboard.transform.SetParent(group.transform, false);
        dashboard.transform.localPosition = Vector3.zero;
        dashboard.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        dashboard.transform.localScale = Vector3.one;
        FitImportedDashboard(dashboard.transform, group.transform);

        InstallCarRadio(
            group.transform,
            radioPosition,
            radioRotation,
            radioScale);
    }

    private static void FitImportedDashboard(
        Transform dashboard,
        Transform cockpitSpace)
    {
        Renderer[] renderers =
            dashboard.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float localWidth = Mathf.Abs(
            cockpitSpace.InverseTransformVector(bounds.size).x);
        if (localWidth < 0.0001f)
            return;

        dashboard.localScale *= 1.42f / localWidth;

        renderers = dashboard.GetComponentsInChildren<Renderer>(true);
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 center = cockpitSpace.InverseTransformPoint(bounds.center);
        Vector3 minimum = cockpitSpace.InverseTransformPoint(bounds.min);
        dashboard.localPosition += new Vector3(
            -center.x,
            0.76f - minimum.y,
            0.91f - center.z);
    }

    private static void InstallCarRadio(
        Transform cockpitRoot,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
            CarRadioPath);
        if (source == null)
        {
            Debug.LogWarning($"Car radio modeli bulunamadi: {CarRadioPath}");
            return;
        }

        GameObject radio = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (radio == null)
            return;

        Undo.RegisterCreatedObjectUndo(radio, "Install cockpit radio");
        radio.name = "Car Radio";
        radio.transform.SetParent(cockpitRoot, false);
        radio.transform.localPosition = localPosition;
        radio.transform.localRotation = localRotation;
        radio.transform.localScale = localScale;

        AudioSource audioSource = radio.GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = radio.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.55f;

        RadioMusicController controller =
            radio.GetComponent<RadioMusicController>();
        if (controller == null)
            controller = radio.AddComponent<RadioMusicController>();
        controller.audioSource = audioSource;
        CreateRadioInteractionHotspots(radio.transform, controller);
        EditorUtility.SetDirty(radio);
    }

    private static void CreateRadioInteractionHotspots(
        Transform radio,
        RadioMusicController controller)
    {
        CreateRadioHotspot(
            radio,
            "Power Button Hotspot",
            new Vector3(-0.0055f, -0.0002f, -0.00325f),
            RadioButtonInteractable.ButtonAction.TogglePlayPause,
            controller);
        CreateRadioHotspot(
            radio,
            "Previous Track Hotspot",
            new Vector3(0.0044f, -0.0002f, -0.00325f),
            RadioButtonInteractable.ButtonAction.PreviousTrack,
            controller);
        CreateRadioHotspot(
            radio,
            "Next Track Hotspot",
            new Vector3(0.0062f, -0.0002f, -0.00325f),
            RadioButtonInteractable.ButtonAction.NextTrack,
            controller);
    }

    private static void CreateRadioHotspot(
        Transform parent,
        string objectName,
        Vector3 localPosition,
        RadioButtonInteractable.ButtonAction action,
        RadioMusicController controller)
    {
        var hotspot = new GameObject(objectName);
        hotspot.transform.SetParent(parent, false);
        hotspot.transform.localPosition = localPosition;
        hotspot.transform.localRotation = Quaternion.identity;
        hotspot.transform.localScale = Vector3.one;
        BoxCollider collider = hotspot.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(0.0017f, 0.0025f, 0.0012f);
        RadioButtonInteractable button =
            hotspot.AddComponent<RadioButtonInteractable>();
        button.action = action;
        button.radio = controller;
    }

    private static void NormalizeVehicleVisual(
        Transform visual,
        float targetWidth,
        float targetLength)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float width = Mathf.Min(bounds.size.x, bounds.size.z);
        float length = Mathf.Max(bounds.size.x, bounds.size.z);
        if (width < 0.001f || length < 0.001f)
            return;

        float scale = Mathf.Min(targetWidth / width, targetLength / length);
        visual.localScale *= scale;

        renderers = visual.GetComponentsInChildren<Renderer>(true);
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        Vector3 localCenter = visual.parent.InverseTransformPoint(bounds.center);
        float localBottom = visual.parent.InverseTransformPoint(bounds.min).y;
        visual.localPosition += new Vector3(
            -localCenter.x,
            0.08f - localBottom,
            -localCenter.z);
    }

    private static void ApplyFreeDeliveryTruckMaterials(Transform visual)
    {
        Material body = GetOrCreateMaterial(
            "Assets/Settings/FreeTruckBody.mat",
            new Color(0.82f, 0.88f, 0.71f));
        Material dark = GetOrCreateMaterial(
            "Assets/Settings/FreeTruckDark.mat",
            new Color(0.055f, 0.09f, 0.1f));
        Material metal = GetOrCreateMaterial(
            "Assets/Settings/FreeTruckMetal.mat",
            new Color(0.46f, 0.54f, 0.53f));
        Material window = GetOrCreateMaterial(
            "Assets/Settings/FreeTruckWindow.mat",
            new Color(0.28f, 0.66f, 0.78f));
        Material orange = GetOrCreateMaterial(
            "Assets/Settings/FreeTruckOrangeLight.mat",
            new Color(1f, 0.48f, 0.08f));
        Material red = GetOrCreateMaterial(
            "Assets/Settings/FreeTruckRedLight.mat",
            new Color(0.9f, 0.08f, 0.06f));
        Material cream = GetOrCreateMaterial(
            "Assets/Settings/FreeTruckCream.mat",
            new Color(1f, 0.92f, 0.72f));

        foreach (Renderer renderer in
                 visual.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                string materialName = materials[i] != null
                    ? materials[i].name.ToLowerInvariant()
                    : string.Empty;
                if (materialName.Contains("chasis"))
                    materials[i] = body;
                else if (materialName.Contains("window"))
                    materials[i] = window;
                else if (materialName.Contains("orange"))
                    materials[i] = orange;
                else if (materialName.Contains("red_light"))
                    materials[i] = red;
                else if (materialName.Contains("lights_white")
                         || materialName.Contains("white"))
                    materials[i] = cream;
                else if (materialName.Contains("tire")
                         || materialName.Contains("black")
                         || materialName.Contains("metal_dark"))
                    materials[i] = dark;
                else if (materialName.Contains("metal"))
                    materials[i] = metal;
            }
            renderer.sharedMaterials = materials;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void CreateCabinGapFillers(
        Transform parent,
        Material panel,
        Material trim,
        Material accent)
    {
        CreatePrimitiveChild(
            parent,
            PrimitiveType.Cube,
            "Lower Firewall Trim",
            new Vector3(0f, 0.68f, 1.17f),
            new Vector3(1.38f, 0.4f, 0.1f),
            Quaternion.Euler(-4f, 0f, 0f),
            panel);
        CreatePrimitiveChild(
            parent,
            PrimitiveType.Cube,
            "Windshield Lower Seal",
            new Vector3(0f, 1.13f, 1.24f),
            new Vector3(1.42f, 0.075f, 0.09f),
            Quaternion.Euler(-8f, 0f, 0f),
            trim);
        CreatePrimitiveChild(
            parent,
            PrimitiveType.Cube,
            "Front Roof Liner",
            new Vector3(0f, 1.81f, 1.2f),
            new Vector3(1.46f, 0.075f, 0.38f),
            Quaternion.Euler(-32f, 0f, 0f),
            panel);

        foreach (float side in new[] { -1f, 1f })
        {
            string sideName = side < 0f ? "Left" : "Right";
            CreatePrimitiveChild(
                parent,
                PrimitiveType.Cube,
                $"{sideName} Dashboard End Cap",
                new Vector3(side * 0.68f, 0.91f, 1.02f),
                new Vector3(0.14f, 0.38f, 0.34f),
                Quaternion.Euler(-4f, 0f, side * 2f),
                panel);
            CreatePrimitiveChild(
                parent,
                PrimitiveType.Cube,
                $"{sideName} Footwell Liner",
                new Vector3(side * 0.4f, 0.545f, 0.72f),
                new Vector3(0.52f, 0.025f, 0.72f),
                Quaternion.identity,
                accent);
            CreatePrimitiveChild(
                parent,
                PrimitiveType.Cube,
                $"{sideName} Door Sill Cover",
                new Vector3(side * 0.7f, 0.59f, 0.28f),
                new Vector3(0.1f, 0.16f, 1.35f),
                Quaternion.identity,
                trim);
            CreatePrimitiveChild(
                parent,
                PrimitiveType.Cube,
                $"{sideName} Upper Door Rail",
                new Vector3(side * 0.735f, 1.76f, 0.48f),
                new Vector3(0.085f, 0.1f, 1.34f),
                Quaternion.identity,
                trim);
            CreatePrimitiveChild(
                parent,
                PrimitiveType.Cube,
                $"{sideName} Rear Door Pillar",
                new Vector3(side * 0.735f, 1.34f, -0.18f),
                new Vector3(0.09f, 0.82f, 0.1f),
                Quaternion.identity,
                trim);
            CreatePrimitiveChild(
                parent,
                PrimitiveType.Cube,
                $"{sideName} Front Door Jamb",
                new Vector3(side * 0.735f, 1.18f, 1.08f),
                new Vector3(0.1f, 0.72f, 0.11f),
                Quaternion.Euler(-8f, 0f, 0f),
                trim);
            CreatePrimitiveChild(
                parent,
                PrimitiveType.Cube,
                $"{sideName} Lower Dash Closure",
                new Vector3(side * 0.48f, 0.72f, 0.91f),
                new Vector3(0.48f, 0.26f, 0.18f),
                Quaternion.Euler(-3f, 0f, 0f),
                panel);
        }

        CreatePrimitiveChild(
            parent,
            PrimitiveType.Cube,
            "Center Floor Tunnel",
            new Vector3(0f, 0.58f, 0.28f),
            new Vector3(0.3f, 0.18f, 1.08f),
            Quaternion.identity,
            panel);
        CreatePrimitiveChild(
            parent,
            PrimitiveType.Cube,
            "Center Tunnel Accent",
            new Vector3(0f, 0.68f, 0.3f),
            new Vector3(0.24f, 0.025f, 0.82f),
            Quaternion.identity,
            accent);
    }

    private static void CreateCabinShell(
        Transform parent,
        Material panelMaterial,
        Material trimMaterial,
        Material upholsteryMaterial,
        Material glassMaterial)
    {
        CreateGlassPanel(
            parent,
            "Front Windshield Glass",
            new Vector3(0f, 1.42f, 1.285f),
            new Vector3(1.32f, 0.56f, 0.012f),
            Quaternion.Euler(-10f, 0f, 0f),
            glassMaterial);
        CreateGlassPanel(
            parent,
            "Left Door Glass",
            new Vector3(-0.745f, 1.39f, 0.55f),
            new Vector3(0.012f, 0.47f, 0.82f),
            Quaternion.identity,
            glassMaterial);
        CreateGlassPanel(
            parent,
            "Right Door Glass",
            new Vector3(0.745f, 1.39f, 0.55f),
            new Vector3(0.012f, 0.47f, 0.82f),
            Quaternion.identity,
            glassMaterial);

        CreatePrimitiveChild(
            parent,
            PrimitiveType.Cube,
            "Cabin Ceiling",
            new Vector3(0f, 1.91f, 0.35f),
            new Vector3(1.42f, 0.04f, 1.5f),
            Quaternion.identity,
            panelMaterial);
        CreatePrimitiveChild(
            parent,
            PrimitiveType.Cube,
            "Left Sun Visor",
            new Vector3(-0.38f, 1.68f, 1.08f),
            new Vector3(0.43f, 0.035f, 0.18f),
            Quaternion.Euler(8f, 0f, 0f),
            upholsteryMaterial);
        CreatePrimitiveChild(
            parent,
            PrimitiveType.Cube,
            "Right Sun Visor",
            new Vector3(0.38f, 1.68f, 1.08f),
            new Vector3(0.43f, 0.035f, 0.18f),
            Quaternion.Euler(8f, 0f, 0f),
            upholsteryMaterial);

        CreateSeat(parent, "Driver", -0.38f, upholsteryMaterial, trimMaterial);
        CreateSeat(parent, "Passenger", 0.38f, upholsteryMaterial, trimMaterial);

        CreatePrimitiveChild(
            parent,
            PrimitiveType.Cube,
            "Rear Bulkhead Lower",
            new Vector3(0f, 0.74f, -0.69f),
            new Vector3(1.42f, 0.55f, 0.055f),
            Quaternion.identity,
            panelMaterial);
        CreatePrimitiveChild(
            parent,
            PrimitiveType.Cube,
            "Rear Bulkhead Upper",
            new Vector3(0f, 1.61f, -0.69f),
            new Vector3(1.42f, 0.27f, 0.055f),
            Quaternion.identity,
            panelMaterial);
        CreatePrimitiveChild(
            parent,
            PrimitiveType.Cube,
            "Rear Bulkhead Left",
            new Vector3(-0.59f, 1.25f, -0.69f),
            new Vector3(0.24f, 0.5f, 0.055f),
            Quaternion.identity,
            panelMaterial);
        CreatePrimitiveChild(
            parent,
            PrimitiveType.Cube,
            "Rear Bulkhead Right",
            new Vector3(0.59f, 1.25f, -0.69f),
            new Vector3(0.24f, 0.5f, 0.055f),
            Quaternion.identity,
            panelMaterial);
        CreateGlassPanel(
            parent,
            "Rear Window Glass",
            new Vector3(0f, 1.3f, -0.705f),
            new Vector3(0.94f, 0.42f, 0.012f),
            Quaternion.identity,
            glassMaterial);
    }

    private static void CreateSeat(
        Transform parent,
        string prefix,
        float x,
        Material upholstery,
        Material trim)
    {
        CreatePrimitiveChild(
            parent,
            PrimitiveType.Cube,
            $"{prefix} Seat Cushion",
            new Vector3(x, 0.62f, -0.18f),
            new Vector3(0.48f, 0.14f, 0.5f),
            Quaternion.Euler(-4f, 0f, 0f),
            upholstery);
        CreatePrimitiveChild(
            parent,
            PrimitiveType.Cube,
            $"{prefix} Seat Back",
            new Vector3(x, 0.92f, -0.46f),
            new Vector3(0.48f, 0.64f, 0.13f),
            Quaternion.Euler(-8f, 0f, 0f),
            upholstery);
        CreatePrimitiveChild(
            parent,
            PrimitiveType.Cube,
            $"{prefix} Headrest",
            new Vector3(x, 1.31f, -0.5f),
            new Vector3(0.3f, 0.22f, 0.13f),
            Quaternion.Euler(-5f, 0f, 0f),
            trim);
    }

    private static void CreateDoorDetails(
        Transform parent,
        Material trim,
        Material accent,
        Material mirror)
    {
        foreach (float side in new[] { -1f, 1f })
        {
            string sideName = side < 0f ? "Left" : "Right";
            CreatePrimitiveChild(
                parent,
                PrimitiveType.Cube,
                $"{sideName} Window Sill",
                new Vector3(side * 0.71f, 1.14f, 0.55f),
                new Vector3(0.045f, 0.075f, 0.85f),
                Quaternion.identity,
                trim);
            CreatePrimitiveChild(
                parent,
                PrimitiveType.Cube,
                $"{sideName} Door Armrest",
                new Vector3(side * 0.705f, 0.97f, 0.35f),
                new Vector3(0.055f, 0.1f, 0.42f),
                Quaternion.identity,
                accent);
            CreatePrimitiveChild(
                parent,
                PrimitiveType.Cube,
                $"{sideName} Door Handle",
                new Vector3(side * 0.67f, 1.08f, 0.3f),
                new Vector3(0.06f, 0.045f, 0.18f),
                Quaternion.identity,
                trim);
            CreatePrimitiveChild(
                parent,
                PrimitiveType.Cube,
                $"{sideName} Window Switch",
                new Vector3(side * 0.67f, 1.04f, 0.48f),
                new Vector3(0.065f, 0.025f, 0.055f),
                Quaternion.identity,
                trim);
            CreatePrimitiveChild(
                parent,
                PrimitiveType.Cube,
                $"{sideName} Mirror Housing",
                new Vector3(side * 0.86f, 1.39f, 1.02f),
                new Vector3(0.12f, 0.12f, 0.09f),
                Quaternion.Euler(0f, side * -12f, 0f),
                trim);
            CreatePrimitiveChild(
                parent,
                PrimitiveType.Cube,
                $"{sideName} Mirror Glass",
                new Vector3(side * 0.88f, 1.39f, 0.96f),
                new Vector3(0.1f, 0.085f, 0.012f),
                Quaternion.Euler(0f, side * -12f, 0f),
                mirror);
        }
    }

    private static void CreateCenterConsoleDetails(
        Transform parent,
        Material trim,
        Material metal,
        Material screen,
        Material accent)
    {
        bool hasExternalRadio = AssetDatabase.LoadAssetAtPath<GameObject>(
            CarRadioPath) != null;
        if (!hasExternalRadio)
        {
            CreatePrimitiveChild(
                parent,
                PrimitiveType.Cube,
                "Radio Screen",
                new Vector3(0.22f, 0.93f, 0.345f),
                new Vector3(0.25f, 0.115f, 0.025f),
                Quaternion.Euler(-5f, 0f, 0f),
                screen);
            for (int i = 0; i < 4; i++)
            {
                CreatePrimitiveChild(
                    parent,
                    PrimitiveType.Cube,
                    $"Radio Button {i + 1}",
                    new Vector3(0.13f + i * 0.06f, 0.845f, 0.325f),
                    new Vector3(0.035f, 0.025f, 0.018f),
                    Quaternion.identity,
                    accent);
            }
        }
        foreach (float x in new[] { 0.08f, 0.36f })
        {
            CreatePrimitiveChild(
                parent,
                PrimitiveType.Cube,
                "Air Vent",
                new Vector3(x, 1.045f, 0.88f),
                new Vector3(0.16f, 0.075f, 0.025f),
                Quaternion.identity,
                trim);
            for (int i = -1; i <= 1; i++)
            {
                CreatePrimitiveChild(
                    parent,
                    PrimitiveType.Cube,
                    "Vent Slat",
                    new Vector3(x + i * 0.045f, 1.045f, 0.86f),
                    new Vector3(0.012f, 0.055f, 0.012f),
                    Quaternion.identity,
                    metal);
            }
        }

        CreatePrimitiveChild(
            parent,
            PrimitiveType.Cube,
            "Gear Lever",
            new Vector3(0.2f, 0.69f, 0.32f),
            new Vector3(0.035f, 0.28f, 0.035f),
            Quaternion.Euler(-18f, 0f, 0f),
            metal);
        CreatePrimitiveChild(
            parent,
            PrimitiveType.Sphere,
            "Gear Knob",
            new Vector3(0.2f, 0.84f, 0.27f),
            Vector3.one * 0.09f,
            Quaternion.identity,
            trim);
        foreach (float x in new[] { 0.1f, 0.3f })
        {
            CreatePrimitiveChild(
                parent,
                PrimitiveType.Cylinder,
                "Cup Holder",
                new Vector3(x, 0.57f, 0.02f),
                new Vector3(0.075f, 0.012f, 0.075f),
                Quaternion.identity,
                trim);
        }
    }

    private static void CreatePassengerDashboardDetails(
        Transform parent,
        Material panel,
        Material trim,
        Material accent)
    {
        CreatePrimitiveChild(
            parent,
            PrimitiveType.Cube,
            "Glove Box",
            new Vector3(0.48f, 0.86f, 0.86f),
            new Vector3(0.42f, 0.16f, 0.025f),
            Quaternion.identity,
            panel);
        CreatePrimitiveChild(
            parent,
            PrimitiveType.Cube,
            "Glove Box Handle",
            new Vector3(0.48f, 0.89f, 0.84f),
            new Vector3(0.13f, 0.025f, 0.018f),
            Quaternion.identity,
            trim);
        CreatePrimitiveChild(
            parent,
            PrimitiveType.Cube,
            "Dashboard Parcel Mat",
            new Vector3(0.49f, 1.005f, 1.02f),
            new Vector3(0.4f, 0.018f, 0.17f),
            Quaternion.identity,
            accent);
    }

    private static GameObject CreateGlassPanel(
        Transform parent,
        string objectName,
        Vector3 localPosition,
        Vector3 localScale,
        Quaternion localRotation,
        Material material)
    {
        GameObject panel = CreatePrimitiveChild(
            parent,
            PrimitiveType.Cube,
            objectName,
            localPosition,
            localScale,
            localRotation,
            material);
        Renderer renderer = panel.GetComponent<Renderer>();
        renderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return panel;
    }

    private static Transform CreateSteeringWheel(
        Transform parent,
        Material material)
    {
        var root = new GameObject("Steering Wheel Pivot");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(-0.38f, 1.11f, 0.87f);
        root.transform.localRotation = Quaternion.Euler(-13f, 0f, 0f);

        const int segmentCount = 14;
        const float radius = 0.19f;
        for (int i = 0; i < segmentCount; i++)
        {
            float angle = i * 360f / segmentCount;
            float radians = angle * Mathf.Deg2Rad;
            CreatePrimitiveChild(
                root.transform,
                PrimitiveType.Cube,
                $"Rim {i:00}",
                new Vector3(
                    Mathf.Cos(radians) * radius,
                    Mathf.Sin(radians) * radius,
                    0f),
                new Vector3(0.105f, 0.038f, 0.038f),
                Quaternion.Euler(0f, 0f, angle + 90f),
                material);
        }

        CreatePrimitiveChild(
            root.transform,
            PrimitiveType.Cylinder,
            "Steering Hub",
            Vector3.zero,
            new Vector3(0.075f, 0.03f, 0.075f),
            Quaternion.Euler(90f, 0f, 0f),
            material);
        for (int i = 0; i < 3; i++)
        {
            float angle = 90f + i * 120f;
            float radians = angle * Mathf.Deg2Rad;
            CreatePrimitiveChild(
                root.transform,
                PrimitiveType.Cube,
                $"Steering Spoke {i + 1}",
                new Vector3(
                    Mathf.Cos(radians) * radius * 0.48f,
                    Mathf.Sin(radians) * radius * 0.48f,
                    0f),
                new Vector3(radius * 0.82f, 0.026f, 0.028f),
                Quaternion.Euler(0f, 0f, angle),
                material);
        }
        return root.transform;
    }

    private static Transform CreateGaugeCluster(
        Transform parent,
        Material faceMaterial,
        Material needleMaterial,
        Material trimMaterial)
    {
        CreatePrimitiveChild(
            parent,
            PrimitiveType.Cube,
            "Gauge Hood",
            new Vector3(-0.38f, 1.105f, 1.045f),
            new Vector3(0.42f, 0.25f, 0.075f),
            Quaternion.identity,
            trimMaterial);
        CreatePrimitiveChild(
            parent,
            PrimitiveType.Cylinder,
            "Speedometer Face",
            new Vector3(-0.38f, 1.105f, 0.995f),
            new Vector3(0.105f, 0.014f, 0.105f),
            Quaternion.Euler(90f, 0f, 0f),
            faceMaterial);

        var needlePivot = new GameObject("Speed Needle Pivot");
        needlePivot.transform.SetParent(parent, false);
        needlePivot.transform.localPosition =
            new Vector3(-0.38f, 1.105f, 0.972f);
        CreatePrimitiveChild(
            needlePivot.transform,
            PrimitiveType.Cube,
            "Speed Needle",
            new Vector3(0f, 0.065f, 0f),
            new Vector3(0.018f, 0.13f, 0.018f),
            Quaternion.identity,
            needleMaterial);
        return needlePivot.transform;
    }

    private static void ConfigureCamera(ArcadeDeliveryVan van)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create follow camera");
        }

        DeliveryFollowCamera follow =
            mainCamera.GetComponent<DeliveryFollowCamera>();
        if (follow == null)
            follow = Undo.AddComponent<DeliveryFollowCamera>(mainCamera.gameObject);
        follow.target = van.transform;
        follow.startInFirstPerson = true;
        follow.firstPersonOffset = new Vector3(-0.36f, 1.36f, -0.35f);
        follow.SnapToTarget();
        EditorUtility.SetDirty(follow);
    }

    private static GameObject CreatePrimitiveChild(
        Transform parent,
        PrimitiveType primitiveType,
        string objectName,
        Vector3 localPosition,
        Vector3 localScale,
        Quaternion localRotation,
        Material material)
    {
        GameObject child = GameObject.CreatePrimitive(primitiveType);
        child.name = objectName;
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = localRotation;
        child.transform.localScale = localScale;

        if (child.TryGetComponent(out Collider primitiveCollider))
            Object.DestroyImmediate(primitiveCollider);
        child.GetComponent<Renderer>().sharedMaterial = material;
        return child;
    }

    private static Material GetOrCreateMaterial(string path, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        material = new Material(shader)
        {
            name = System.IO.Path.GetFileNameWithoutExtension(path),
            color = color
        };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Material GetOrCreateTransparentMaterial(
        string path,
        Color color)
    {
        Material material = GetOrCreateMaterial(path, color);
        material.color = color;
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", 5f);
        material.SetFloat("_DstBlend", 10f);
        material.SetFloat("_ZWrite", 0f);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static string GetPrototypeBuildKey()
    {
        return "GMTK.DeliveryVanPrototype.v1."
             + Hash128.Compute(Application.dataPath);
    }
}
