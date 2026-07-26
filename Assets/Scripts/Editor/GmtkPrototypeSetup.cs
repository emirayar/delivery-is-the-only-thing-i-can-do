using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class GmtkPrototypeSetup
{
    [InitializeOnLoadMethod]
    private static void ScheduleSelectedAssetKitsMigration()
    {
        string key = "GMTK.SelectedAssetKits.v8."
                   + Hash128.Compute(Application.dataPath);
        if (EditorPrefs.GetBool(key, false))
            return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            ProceduralRibbonWorld world =
                Object.FindAnyObjectByType<ProceduralRibbonWorld>();
            if (world == null)
                return;

            ProceduralVillageSystem village =
                world.GetComponent<ProceduralVillageSystem>();
            if (village == null)
                village = Undo.AddComponent<ProceduralVillageSystem>(
                    world.gameObject);
            ProceduralNatureDressing nature =
                world.GetComponent<ProceduralNatureDressing>();
            if (nature == null)
                nature = Undo.AddComponent<ProceduralNatureDressing>(
                    world.gameObject);

            Undo.RecordObject(village, "Use Kenney suburban kit");
            Undo.RecordObject(nature, "Use Quaternius nature kit");
            village.useBuildingPrefabs = true;
            village.buildingPrefabs = LoadModels(
                "Assets/ThirdPartyAssets/KenneyCityKitSuburban/Models/",
                "building-type-a.fbx",
                "building-type-b.fbx",
                "building-type-c.fbx",
                "building-type-d.fbx",
                "building-type-e.fbx",
                "building-type-f.fbx",
                "building-type-g.fbx",
                "building-type-h.fbx",
                "building-type-i.fbx",
                "building-type-j.fbx",
                "building-type-k.fbx",
                "building-type-l.fbx",
                "building-type-m.fbx",
                "building-type-n.fbx",
                "building-type-o.fbx",
                "building-type-p.fbx",
                "building-type-q.fbx",
                "building-type-r.fbx",
                "building-type-s.fbx",
                "building-type-t.fbx",
                "building-type-u.fbx");
            village.turquoiseBuildingPrefabs = LoadModels(
                "Assets/ThirdPartyAssets/KenneyCityKitSuburban/Models/",
                "building-type-a.fbx",
                "building-type-c.fbx",
                "building-type-e.fbx",
                "building-type-g.fbx",
                "building-type-i.fbx",
                "building-type-k.fbx",
                "building-type-m.fbx",
                "building-type-o.fbx",
                "building-type-q.fbx",
                "building-type-s.fbx",
                "building-type-u.fbx");
            village.terracottaBuildingPrefabs = LoadModels(
                "Assets/ThirdPartyAssets/KenneyCityKitSuburban/Models/",
                "building-type-b.fbx",
                "building-type-d.fbx",
                "building-type-f.fbx",
                "building-type-h.fbx",
                "building-type-j.fbx",
                "building-type-l.fbx",
                "building-type-n.fbx",
                "building-type-p.fbx",
                "building-type-r.fbx",
                "building-type-t.fbx");
            village.turquoiseRoofChance = 0.68f;
            village.terracottaPaletteTexture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/ThirdPartyAssets/KenneyCityKitSuburban/"
                    + "Textures/variation-b.png");
            village.minimumPrefabScale = 0.01f;
            village.maximumPrefabScale = 100f;
            village.prefabFootprintMultiplier = 0.95f;
            village.minimumPrefabWorldHeight = 4.2f;
            village.buildingFoundationClearance = 0.18f;
            village.prefabYawJitter = 4f;
            village.minimumHouseWidth = 6.5f;
            village.maximumHouseWidth = 9.5f;
            village.minimumHouseSetback = 14f;
            village.maximumHouseSetback = 20f;
            village.drivewayPrefabs = LoadModels(
                "Assets/ThirdPartyAssets/KenneyCityKitSuburban/Models/",
                "path-long.fbx",
                "path-stones-long.fbx",
                "path-stones-messy.fbx");
            village.drivewayWidth = 1.6f;
            village.fencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ThirdPartyAssets/KenneyCityKitSuburban/Models/"
                + "fence.fbx");
            village.fencedGardenChance = 0.72f;
            village.fenceGateWidth = 3.2f;
            village.gardenTreePrefabs = LoadModels(
                "Assets/ThirdPartyAssets/QuaterniusStylizedNature/Models/",
                "CommonTree_1.fbx",
                "CommonTree_2.fbx",
                "CommonTree_3.fbx",
                "CommonTree_4.fbx");
            village.gardenTreeChance = 0.96f;
            village.gardenTreesPerLot = new Vector2Int(2, 3);
            village.gardenTreeHeightRange = new Vector2(3.5f, 6.5f);

            world.roadBedDepth = 0.24f;
            world.roadBedShoulder = 2f;
            world.roadSurfaceClearance = 0.1f;
            world.addRoadCollider = true;
            world.settlementFlatWidth = 48f;
            world.settlementHeightInfluence = 0.035f;

            nature.treePrefabs = LoadModels(
                "Assets/ThirdPartyAssets/QuaterniusStylizedNature/Models/",
                "CommonTree_1.fbx",
                "CommonTree_2.fbx",
                "CommonTree_3.fbx",
                "CommonTree_4.fbx",
                "CommonTree_5.fbx",
                "Pine_1.fbx",
                "Pine_2.fbx",
                "Pine_3.fbx");
            nature.bushPrefabs = LoadModels(
                "Assets/ThirdPartyAssets/QuaterniusStylizedNature/Models/",
                "Bush_Common.fbx",
                "Bush_Common_Flowers.fbx",
                "Plant_1_Big.fbx",
                "Plant_7_Big.fbx");
            nature.rockPrefabs = LoadModels(
                "Assets/ThirdPartyAssets/QuaterniusStylizedNature/Models/",
                "Rock_Medium_1.fbx",
                "Rock_Medium_2.fbx",
                "Rock_Medium_3.fbx");
            nature.clusterSpacing = 30f;
            nature.treeChance = 0.68f;
            nature.bushChance = 0.52f;
            nature.rockChance = 0.12f;

            world.Regenerate();
            GpuProceduralGrass grass =
                world.GetComponent<GpuProceduralGrass>();
            if (grass != null)
                grass.Rebuild();

            EditorUtility.SetDirty(village);
            EditorUtility.SetDirty(nature);
            EditorUtility.SetDirty(world);
            EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            EditorPrefs.SetBool(key, true);
            SceneView.RepaintAll();
            Debug.Log(
                "Kenney suburban buildings and Quaternius nature dressing "
                + "were connected to the procedural world.",
                world);
        };
    }

    private static GameObject[] LoadModels(
        string folder,
        params string[] fileNames)
    {
        var models = new GameObject[fileNames.Length];
        for (int i = 0; i < fileNames.Length; i++)
        {
            models[i] = AssetDatabase.LoadAssetAtPath<GameObject>(
                folder + fileNames[i]);
        }
        return models;
    }

    [InitializeOnLoadMethod]
    private static void ScheduleQuibliStyleMigration()
    {
        string key = "GMTK.QuibliAnimeStyle.v13."
                   + Hash128.Compute(Application.dataPath);
        if (EditorPrefs.GetBool(key, false))
            return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            RoadSpline spline = Object.FindAnyObjectByType<RoadSpline>();
            ProceduralRibbonWorld world =
                Object.FindAnyObjectByType<ProceduralRibbonWorld>();
            ProceduralFieldSystem fields =
                Object.FindAnyObjectByType<ProceduralFieldSystem>();
            ProceduralVillageSystem village =
                Object.FindAnyObjectByType<ProceduralVillageSystem>();
            Shader quibliShader = Shader.Find("Quibli/Stylized Lit");
            if (spline == null
                || world == null
                || fields == null
                || village == null
                || quibliShader == null)
            {
                return;
            }

            Material ground = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/ThirdPartyAssets/Quibli/Demos/Nature/Materials/"
                + "NatureScene_Ground.mat");
            Material road = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/ThirdPartyAssets/Quibli/Demos/City/Materials/"
                + "City_Road_1_1.mat");
            Material wall = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/ThirdPartyAssets/Quibli/Demos/City/Materials/"
                + "City_Building_Wall_1.mat");
            Material roof = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/ThirdPartyAssets/Quibli/Demos/City/Materials/"
                + "City_Building_Roof_1.mat");
            if (ground == null || road == null || wall == null || roof == null)
                return;

            Undo.RecordObject(spline, "Widen village road");
            Undo.RecordObject(world, "Apply Quibli world materials");
            Undo.RecordObject(fields, "Apply vivid field palette");
            Undo.RecordObject(village, "Apply Quibli village materials");
            spline.roadWidth = 11f;
            world.terrainMaterial = ground;
            world.roadMaterial = road;
            world.nonFieldTerrainColor =
                new Color(0.075f, 0.30f, 0.11f);
            village.wallMaterialTemplate = wall;
            village.roofMaterialTemplate = roof;
            fields.cropColorA = new Color(0.07f, 0.34f, 0.12f);
            fields.cropColorB = new Color(0.18f, 0.42f, 0.10f);
            fields.cropColorC = new Color(0.50f, 0.34f, 0.055f);
            fields.soilColor = new Color(0.30f, 0.12f, 0.035f);
            village.warmWallColor =
                new Color(0.58f, 0.30f, 0.12f);
            village.lightWallColor =
                new Color(0.55f, 0.68f, 0.76f);
            village.redRoofColor =
                new Color(0.48f, 0.035f, 0.025f);
            village.grayRoofColor =
                new Color(0.09f, 0.23f, 0.42f);
            village.darkRoofColor =
                new Color(0.035f, 0.065f, 0.14f);

            ConfigureVividCameraStyle();

            ApplyQuibliShader(
                "Assets/Settings/PrototypeVanBody.mat",
                quibliShader);
            ApplyQuibliShader(
                "Assets/Settings/PrototypeVanWindows.mat",
                quibliShader);
            ApplyQuibliShader(
                "Assets/Settings/PrototypeVanWheels.mat",
                quibliShader);

            fields.Generate();
            world.Regenerate();
            GpuProceduralGrass grass =
                spline.GetComponent<GpuProceduralGrass>();
            if (grass != null)
                grass.Rebuild();

            EditorUtility.SetDirty(spline);
            EditorUtility.SetDirty(world);
            EditorUtility.SetDirty(fields);
            EditorUtility.SetDirty(village);
            EditorSceneManager.MarkSceneDirty(spline.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            EditorPrefs.SetBool(key, true);
            SceneView.RepaintAll();
            Debug.Log(
                "Road widened and Quibli anime materials applied.",
                world);
        };
    }

    private static void ConfigureVividCameraStyle()
    {
        Camera camera = Object.FindAnyObjectByType<Camera>();
        if (camera != null
            && camera.TryGetComponent(
                out UniversalAdditionalCameraData cameraData))
        {
            Undo.RecordObject(cameraData, "Apply Quibli camera style");
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing =
                AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality =
                AntialiasingQuality.High;
            cameraData.stopNaN = true;
            EditorUtility.SetDirty(cameraData);
        }

        Volume volume = Object.FindAnyObjectByType<Volume>();
        VolumeProfile profile = volume != null
            ? volume.sharedProfile
            : null;
        if (profile == null)
            return;

        Undo.RecordObject(profile, "Apply vivid Quibli grading");
        profile.components.RemoveAll(component => component == null);
        if (!profile.TryGet(out ColorAdjustments color))
        {
            color = profile.Add<ColorAdjustments>(true);
            AssetDatabase.AddObjectToAsset(color, profile);
        }
        color.active = true;
        color.postExposure.Override(0.02f);
        color.contrast.Override(12f);
        color.saturation.Override(6f);
        color.hueShift.Override(-2f);
        color.colorFilter.Override(
            new Color(0.96f, 1f, 1.08f, 1f));

        if (!profile.TryGet(out Bloom bloom))
            bloom = profile.Add<Bloom>(true);
        bloom.active = true;
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(0.42f);
        bloom.scatter.Override(0.65f);
        bloom.tint.Override(
            new Color(0.55f, 0.94f, 1f, 1f));

        EditorUtility.SetDirty(profile);
    }

    private static void ApplyQuibliShader(string path, Shader shader)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
            return;

        Undo.RecordObject(material, "Apply Quibli anime shader");
        Color color = material.HasProperty("_BaseColor")
            ? material.GetColor("_BaseColor")
            : material.color;
        material.shader = shader;
        material.SetTexture("_BaseMap", Texture2D.whiteTexture);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_SelfShadingSize", 0.48f);
        material.SetFloat("_LightContribution", 0.75f);
        material.SetFloat("_OverrideLightAttenuation", 1f);
        material.SetColor(
            "_ShadowColor",
            Color.Lerp(color, new Color(0.10f, 0.14f, 0.22f, 1f), 0.55f));
        material.SetFloat("_RimEnabled", 1f);
        material.SetColor(
            "_FlatRimColor",
            new Color(0.55f, 0.68f, 0.78f, 1f));
        material.SetFloat("_FlatRimSize", 0.36f);
        material.SetFloat("_FlatRimEdgeSmoothness", 0.2f);
        material.EnableKeyword("DR_LIGHT_ATTENUATION");
        material.EnableKeyword("DR_RIM_ON");
        EditorUtility.SetDirty(material);
    }

    [InitializeOnLoadMethod]
    private static void ScheduleSuloszowaCalibration()
    {
        string key = "GMTK.SuloszowaCalibration.v6."
                   + Hash128.Compute(Application.dataPath);
        if (EditorPrefs.GetBool(key, false))
            return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            RoadSpline spline = Object.FindAnyObjectByType<RoadSpline>();
            ProceduralRibbonWorld world =
                Object.FindAnyObjectByType<ProceduralRibbonWorld>();
            ProceduralFieldSystem fields =
                Object.FindAnyObjectByType<ProceduralFieldSystem>();
            if (spline == null || world == null || fields == null)
                return;

            ProceduralVillageSystem village =
                spline.GetComponent<ProceduralVillageSystem>();
            if (village == null)
                village = Undo.AddComponent<ProceduralVillageSystem>(
                    spline.gameObject);

            Undo.RecordObject(spline, "Calibrate Sułoszowa road");
            Undo.RecordObject(world, "Calibrate Sułoszowa terrain");
            Undo.RecordObject(fields, "Calibrate Sułoszowa fields");
            spline.roadWidth = 8.5f;
            spline.roadFalloff = 5f;

            fields.minimumParcelLength = 22f;
            fields.maximumParcelLength = 58f;
            fields.roadsideGap = 2.5f;
            fields.buildingZoneDepth = 50f;
            fields.maximumBoundarySkew = 18f;
            fields.boundaryCurvePer100Meters = 3.7f;
            fields.fallowFieldChance = 0.06f;
            fields.yellowFieldChance = 0.18f;
            fields.lightGreenFieldChance = 0.28f;
            fields.cropColorA = new Color(0.12f, 0.36f, 0.06f);
            fields.cropColorB = new Color(0.32f, 0.52f, 0.11f);
            fields.cropColorC = new Color(0.62f, 0.52f, 0.18f);
            fields.soilColor = new Color(0.30f, 0.22f, 0.12f);

            world.valleyFloorWidth = 55f;
            world.valleyRisePer100Meters = 6.5f;
            world.valleyCurve = 1.35f;
            world.valleyLongitudinalVariation = 0.22f;
            world.valleyVariationFrequency = 1.8f;
            world.heightAmplitude = 5.5f;

            fields.Generate();
            world.Regenerate();

            GpuProceduralGrass grass =
                spline.GetComponent<GpuProceduralGrass>();
            if (grass != null)
                grass.Rebuild();

            EditorUtility.SetDirty(spline);
            EditorUtility.SetDirty(world);
            EditorUtility.SetDirty(fields);
            EditorUtility.SetDirty(village);
            EditorSceneManager.MarkSceneDirty(spline.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            EditorPrefs.SetBool(key, true);
            SceneView.RepaintAll();
            Debug.Log(
                "Sułoszowa reference calibration and village generated.",
                village);
        };
    }

    [InitializeOnLoadMethod]
    private static void ScheduleCurvedFieldsAndRollingValley()
    {
        string key = "GMTK.CurvedFieldsAndRollingValley.v5."
                   + Hash128.Compute(Application.dataPath);
        if (EditorPrefs.GetBool(key, false))
            return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            ProceduralFieldSystem fields =
                Object.FindAnyObjectByType<ProceduralFieldSystem>();
            ProceduralRibbonWorld world =
                Object.FindAnyObjectByType<ProceduralRibbonWorld>();
            if (fields == null || world == null)
                return;

            Undo.RecordObject(fields, "Curve deep field boundaries");
            Undo.RecordObject(world, "Add rolling valley elevation");
            fields.boundaryCurvePer100Meters = 4f;
            world.valleyLongitudinalVariation = 0.3f;
            world.valleyVariationFrequency = 2.4f;

            fields.Generate();
            world.Regenerate();
            GpuProceduralGrass grass =
                world.GetComponent<GpuProceduralGrass>();
            if (grass != null)
                grass.Rebuild();

            EditorUtility.SetDirty(fields);
            EditorUtility.SetDirty(world);
            EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            EditorPrefs.SetBool(key, true);
            SceneView.RepaintAll();
            Debug.Log(
                "Deep field curves and rolling valley elevation generated.",
                world);
        };
    }

    [InitializeOnLoadMethod]
    private static void ScheduleValleyAndOrganicFields()
    {
        string key = "GMTK.ValleyAndOrganicFields.v4."
                   + Hash128.Compute(Application.dataPath);
        if (EditorPrefs.GetBool(key, false))
            return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            ProceduralFieldSystem fields =
                Object.FindAnyObjectByType<ProceduralFieldSystem>();
            ProceduralRibbonWorld world =
                Object.FindAnyObjectByType<ProceduralRibbonWorld>();
            if (fields == null || world == null)
                return;

            Undo.RecordObject(fields, "Make field boundaries organic");
            Undo.RecordObject(world, "Generate valley profile");
            fields.maximumBoundarySkew = Mathf.Max(
                fields.maximumBoundarySkew,
                16f);
            fields.boundaryWaviness = Mathf.Max(
                fields.boundaryWaviness,
                3f);
            world.generateValley = true;
            world.valleyFloorWidth = 10f;
            world.valleyRisePer100Meters = 12f;
            world.valleyCurve = 1.45f;

            fields.Generate();
            world.Regenerate();
            GpuProceduralGrass grass =
                world.GetComponent<GpuProceduralGrass>();
            if (grass != null)
                grass.Rebuild();

            EditorUtility.SetDirty(fields);
            EditorUtility.SetDirty(world);
            EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            EditorPrefs.SetBool(key, true);
            SceneView.RepaintAll();
            Debug.Log(
                "Organic field boundaries and valley profile generated.",
                world);
        };
    }

    [InitializeOnLoadMethod]
    private static void ScheduleTerrainFieldPaintUpgrade()
    {
        string key = "GMTK.TerrainFieldPaint.v3."
                   + Hash128.Compute(Application.dataPath);
        if (EditorPrefs.GetBool(key, false))
            return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            ProceduralFieldSystem fields =
                Object.FindAnyObjectByType<ProceduralFieldSystem>();
            if (fields == null)
                return;

            Undo.RecordObject(fields, "Paint fields into terrain");
            fields.Generate();

            ProceduralRibbonWorld world =
                fields.GetComponent<ProceduralRibbonWorld>();
            if (world != null)
                world.Regenerate();

            GpuProceduralGrass grass =
                fields.GetComponent<GpuProceduralGrass>();
            if (grass != null)
                grass.Rebuild();

            EditorUtility.SetDirty(fields);
            EditorSceneManager.MarkSceneDirty(fields.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            EditorPrefs.SetBool(key, true);
            SceneView.RepaintAll();
            Debug.Log(
                "Field overlay retired; parcels are now painted into terrain.",
                fields);
        };
    }

    [InitializeOnLoadMethod]
    private static void ScheduleFieldSystemMigration()
    {
        EditorApplication.delayCall += AddFieldSystemIfNeeded;
    }

    private static void AddFieldSystemIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        ProceduralRibbonWorld world =
            Object.FindAnyObjectByType<ProceduralRibbonWorld>();
        if (world == null
            || world.GetComponent<ProceduralFieldSystem>() != null)
        {
            return;
        }

        ProceduralFieldSystem fields =
            Undo.AddComponent<ProceduralFieldSystem>(world.gameObject);
        fields.Generate();
        world.Regenerate();

        GpuProceduralGrass grass =
            world.GetComponent<GpuProceduralGrass>();
        if (grass != null)
            grass.Rebuild();

        EditorUtility.SetDirty(fields);
        EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        SceneView.RepaintAll();
        Debug.Log(
            "Procedural field system added: parcels generated and grass masked.",
            fields);
    }

    [InitializeOnLoadMethod]
    private static void ScheduleLegacyGrassMigration()
    {
        EditorApplication.delayCall += MigrateLegacyGrass;
    }

    private static void MigrateLegacyGrass()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        InteractiveGrassRenderer[] legacyRenderers =
            Object.FindObjectsByType<InteractiveGrassRenderer>(
                FindObjectsInactive.Include);

        foreach (InteractiveGrassRenderer legacy in legacyRenderers)
        {
            GameObject owner = legacy.gameObject;
            Undo.DestroyObjectImmediate(legacy);

            GpuProceduralGrass grass = owner.GetComponent<GpuProceduralGrass>();
            if (grass == null)
                grass = Undo.AddComponent<GpuProceduralGrass>(owner);

            Transform interactor = GameObject.FindWithTag("Player")?.transform;
            if (interactor == null && Camera.main != null)
                interactor = Camera.main.transform;
            grass.interactor = interactor;
            grass.Rebuild();

            EditorUtility.SetDirty(grass);
            EditorSceneManager.MarkSceneDirty(owner.scene);
            Debug.Log(
                "Migrated retired InteractiveGrassRenderer to "
                + "GpuProceduralGrass.",
                owner);
        }

        if (legacyRenderers.Length > 0)
            SceneView.RepaintAll();
    }

    [MenuItem("GMTK/Build Procedural World Prototype")]
    public static void BuildPrototype()
    {
        RoadSpline spline = Object.FindAnyObjectByType<RoadSpline>();
        if (spline == null)
        {
            var roadObject = new GameObject("RoadSpline");
            spline = roadObject.AddComponent<RoadSpline>();
            Undo.RegisterCreatedObjectUndo(roadObject, "Create procedural world");
        }

        ProceduralRibbonWorld world = spline.GetComponent<ProceduralRibbonWorld>();
        if (world == null)
            world = Undo.AddComponent<ProceduralRibbonWorld>(spline.gameObject);

        InteractiveGrassRenderer legacyGrass =
            spline.GetComponent<InteractiveGrassRenderer>();
        if (legacyGrass != null)
            Undo.DestroyObjectImmediate(legacyGrass);

        GpuProceduralGrass grass = spline.GetComponent<GpuProceduralGrass>();
        if (grass == null)
            grass = Undo.AddComponent<GpuProceduralGrass>(spline.gameObject);

        ProceduralFieldSystem fields =
            spline.GetComponent<ProceduralFieldSystem>();
        if (fields == null)
            fields = Undo.AddComponent<ProceduralFieldSystem>(spline.gameObject);
        ProceduralVillageSystem village =
            spline.GetComponent<ProceduralVillageSystem>();
        if (village == null)
            village = Undo.AddComponent<ProceduralVillageSystem>(
                spline.gameObject);

        Transform interactor = GameObject.FindWithTag("Player")?.transform;
        if (interactor == null && Camera.main != null)
            interactor = Camera.main.transform;
        grass.interactor = interactor;

        fields.Generate();
        world.Regenerate();
        grass.Rebuild();

        EditorUtility.SetDirty(spline);
        EditorUtility.SetDirty(world);
        EditorUtility.SetDirty(fields);
        EditorUtility.SetDirty(village);
        EditorUtility.SetDirty(grass);
        EditorSceneManager.MarkSceneDirty(spline.gameObject.scene);
        Selection.activeGameObject = spline.gameObject;

        Debug.Log(
            "GMTK procedural prototype ready: terrain, road and interactive grass "
            + "were added to RoadSpline.");
    }
}
