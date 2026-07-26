using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DeliveryVanPrototypeSetup
{
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
        string key = "GMTK.DeliveryVanGrounding.v2."
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
                roadCenter + Vector3.up * 0.08f,
                Quaternion.LookRotation(flatForward.normalized, Vector3.up));
            van.groundCheckDistance = 2.25f;
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
        rigidbody.mass = 850f;
        rigidbody.linearDamping = 0.35f;
        rigidbody.angularDamping = 4.5f;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rigidbody.constraints = RigidbodyConstraints.FreezeRotationX
                              | RigidbodyConstraints.FreezeRotationZ;
        rigidbody.centerOfMass = new Vector3(0f, -0.35f, 0f);

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
        capsule.center = new Vector3(0f, 0.78f, 0f);
        capsule.radius = 0.72f;
        capsule.height = 3.05f;
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

    private static string GetPrototypeBuildKey()
    {
        return "GMTK.DeliveryVanPrototype.v1."
             + Hash128.Compute(Application.dataPath);
    }
}
