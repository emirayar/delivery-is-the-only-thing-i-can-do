using UnityEngine;

[DisallowMultipleComponent]
public sealed class DeliveryGameLoop : MonoBehaviour
{
    private enum LoopState
    {
        WaitingForGame,
        Delivering,
        Ending,
        Finished
    }

    [Header("Game")]
    [Tooltip("Total time before the world ends, in minutes.")]
    [Range(5f, 30f)] public float gameDurationMinutes = 18f;

    [Tooltip("Number of parking deliveries required to finish the run.")]
    [Range(1, 20)] public int deliveryCount = 8;

    [Tooltip("First usable position along the generated road.")]
    [Range(0.02f, 0.4f)] public float firstDeliveryT = 0.11f;

    [Tooltip("Last usable position along the generated road.")]
    [Range(0.5f, 0.98f)] public float lastDeliveryT = 0.88f;

    [Header("Parking")]
    [Tooltip("Width of the delivery parking marker in metres.")]
    [Range(2f, 6f)] public float parkingWidth = 3.8f;

    [Tooltip("Length of the delivery parking marker in metres.")]
    [Range(4f, 10f)] public float parkingLength = 7.2f;

    [Tooltip("Maximum vehicle speed that counts as parked.")]
    [Range(0.2f, 5f)] public float parkingSpeedKph = 1.3f;

    [Tooltip("How long the vehicle must remain parked to deliver.")]
    [Range(0.25f, 4f)] public float deliveryHoldSeconds = 1.35f;

    [Tooltip("Material asset used by the parking outline and floating beacon. "
           + "Keeping an asset reference prevents its shader from being stripped in builds.")]
    [SerializeField] private Material markerMaterialTemplate;

    [Header("Ending")]
    [Tooltip("Duration of the final camera rise.")]
    [Range(3f, 15f)] public float endingCinematicSeconds = 8f;

    [Tooltip("How high the camera rises above the vehicle.")]
    [Range(20f, 120f)] public float endingCameraHeight = 62f;

    private RoadSpline road;
    private ArcadeDeliveryVan van;
    private Rigidbody vanBody;
    private DeliveryFollowCamera followCamera;
    private Camera sceneCamera;
    private LoopState state;
    private float remainingSeconds;
    private int completedDeliveries;
    private float parkingProgress;
    private GameObject markerRoot;
    private Transform markerBeacon;
    private Material markerMaterial;
    private AudioSource feedbackAudio;
    private AudioClip deliveryChime;
    private Vector3 markerPosition;
    private Quaternion markerRotation;
    private Vector3 cinematicStartPosition;
    private Quaternion cinematicStartRotation;
    private Vector3 cinematicEndPosition;
    private Quaternion cinematicEndRotation;
    private float endingElapsed;
    private bool won;
    private GUIStyle timerStyle;
    private GUIStyle counterStyle;
    private GUIStyle messageStyle;
    private GUIStyle smallStyle;

    public bool HasEnded => state == LoopState.Ending || state == LoopState.Finished;
    public int CompletedDeliveries => completedDeliveries;
    public int TotalDeliveries => deliveryCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureGameLoopExists()
    {
        if (FindAnyObjectByType<DeliveryGameLoop>() != null)
            return;
        RoadSpline spline = FindAnyObjectByType<RoadSpline>();
        if (spline != null)
            spline.gameObject.AddComponent<DeliveryGameLoop>();
    }

    private void Awake()
    {
        road = GetComponent<RoadSpline>();
        if (road == null)
            road = FindAnyObjectByType<RoadSpline>();
        ResolveSceneReferences();
        remainingSeconds = gameDurationMinutes * 60f;
        state = LoopState.WaitingForGame;
        CreateFeedbackAudio();
    }

    private void Update()
    {
        if (state == LoopState.WaitingForGame)
        {
            ResolveSceneReferences();
            if (MainMenuController.IsGameplayActive
                && van != null
                && van.enabled
                && vanBody != null
                && !vanBody.isKinematic)
                BeginDeliveries();
            return;
        }

        if (state == LoopState.Delivering)
        {
            remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
            AnimateMarker();
            UpdateParking();
            if (remainingSeconds <= 0f)
                BeginEnding(false);
            return;
        }

        if (state == LoopState.Ending)
            UpdateEndingCinematic();
    }

    private void BeginDeliveries()
    {
        remainingSeconds = gameDurationMinutes * 60f;
        completedDeliveries = 0;
        parkingProgress = 0f;
        state = LoopState.Delivering;
        SpawnCurrentMarker();
    }

    private void UpdateParking()
    {
        if (van == null || markerRoot == null)
            return;

        Vector3 local = Quaternion.Inverse(markerRotation)
                      * (van.transform.position - markerPosition);
        bool inside = Mathf.Abs(local.x) <= parkingWidth * 0.58f
                   && Mathf.Abs(local.z) <= parkingLength * 0.58f
                   && Mathf.Abs(local.y) <= 3f;
        bool stopped = van.SpeedKph <= parkingSpeedKph;

        if (inside && stopped)
        {
            parkingProgress += Time.deltaTime;
            if (parkingProgress >= deliveryHoldSeconds)
                CompleteDelivery();
        }
        else
        {
            parkingProgress = Mathf.MoveTowards(
                parkingProgress,
                0f,
                Time.deltaTime * 1.8f);
        }
    }

    private void CompleteDelivery()
    {
        completedDeliveries++;
        parkingProgress = 0f;
        if (feedbackAudio != null && deliveryChime != null)
            feedbackAudio.PlayOneShot(deliveryChime, 0.55f);
        DestroyMarker();

        if (completedDeliveries >= deliveryCount)
        {
            BeginEnding(true);
            return;
        }
        SpawnCurrentMarker();
    }

    private void SpawnCurrentMarker()
    {
        if (road == null)
            return;

        float normalizedIndex = deliveryCount <= 1
            ? 0.5f
            : completedDeliveries / (float)(deliveryCount - 1);
        float t = Mathf.Lerp(firstDeliveryT, lastDeliveryT, normalizedIndex);
        road.GetFrame(t, out Vector3 center, out Vector3 forward, out Vector3 right);
        float side = completedDeliveries % 2 == 0 ? 1f : -1f;
        float laneOffset = Mathf.Min(
            road.roadWidth * 0.25f,
            Mathf.Max(1.7f, road.roadWidth * 0.5f - parkingWidth * 0.55f));
        markerPosition = center + right * laneOffset * side + Vector3.up * 0.11f;
        markerRotation = Quaternion.LookRotation(forward, Vector3.up);

        markerRoot = new GameObject($"Delivery Stop {completedDeliveries + 1}");
        markerRoot.transform.SetPositionAndRotation(markerPosition, markerRotation);
        EnsureMarkerMaterial();
        if (markerMaterial == null)
        {
            Debug.LogError("Delivery marker could not be created: marker material is missing.");
            DestroyMarker();
            return;
        }

        const float border = 0.16f;
        CreateMarkerBar("Left", new Vector3(-parkingWidth * 0.5f, 0f, 0f), new Vector3(border, 0.055f, parkingLength));
        CreateMarkerBar("Right", new Vector3(parkingWidth * 0.5f, 0f, 0f), new Vector3(border, 0.055f, parkingLength));
        CreateMarkerBar("Front", new Vector3(0f, 0f, parkingLength * 0.5f), new Vector3(parkingWidth, 0.055f, border));
        CreateMarkerBar("Back", new Vector3(0f, 0f, -parkingLength * 0.5f), new Vector3(parkingWidth, 0.055f, border));

        GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beacon.name = "Floating Delivery Indicator";
        beacon.transform.SetParent(markerRoot.transform, false);
        beacon.transform.localPosition = new Vector3(0f, 2.7f, 0f);
        beacon.transform.localRotation = Quaternion.Euler(0f, 45f, 45f);
        beacon.transform.localScale = Vector3.one * 0.58f;
        Renderer beaconRenderer = beacon.GetComponent<Renderer>();
        beaconRenderer.sharedMaterial = markerMaterial;
        Collider beaconCollider = beacon.GetComponent<Collider>();
        if (beaconCollider != null)
            Destroy(beaconCollider);
        markerBeacon = beacon.transform;
    }

    private void CreateMarkerBar(string name, Vector3 localPosition, Vector3 scale)
    {
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = name;
        bar.transform.SetParent(markerRoot.transform, false);
        bar.transform.localPosition = localPosition;
        bar.transform.localScale = scale;
        bar.GetComponent<Renderer>().sharedMaterial = markerMaterial;
        Collider collider = bar.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
    }

    private void AnimateMarker()
    {
        if (markerRoot == null)
            return;
        float pulse = 0.72f + Mathf.Sin(Time.time * 4f) * 0.18f;
        float progress01 = Mathf.Clamp01(parkingProgress / deliveryHoldSeconds);
        Color color = Color.Lerp(
            new Color(0.82f, 0.94f, 0.32f),
            new Color(0.32f, 1f, 0.62f),
            progress01);
        color *= 1.25f + pulse * 0.35f;
        if (markerMaterial != null)
        {
            markerMaterial.SetColor("_BaseColor", color);
            markerMaterial.color = color;
        }
        if (markerBeacon != null)
        {
            markerBeacon.localPosition = new Vector3(
                0f,
                2.7f + Mathf.Sin(Time.time * 2.7f) * 0.22f,
                0f);
            markerBeacon.Rotate(Vector3.up, 70f * Time.deltaTime, Space.World);
        }
    }

    private void BeginEnding(bool deliveriesComplete)
    {
        if (state == LoopState.Ending || state == LoopState.Finished)
            return;
        won = deliveriesComplete;
        state = LoopState.Ending;
        endingElapsed = 0f;
        DestroyMarker();
        ResolveSceneReferences();

        if (van != null)
            van.enabled = false;
        if (vanBody != null)
        {
            vanBody.linearVelocity = Vector3.zero;
            vanBody.angularVelocity = Vector3.zero;
            vanBody.isKinematic = true;
            vanBody.useGravity = false;
        }
        if (followCamera != null)
            followCamera.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (sceneCamera != null)
        {
            cinematicStartPosition = sceneCamera.transform.position;
            cinematicStartRotation = sceneCamera.transform.rotation;
            Vector3 anchor = van != null ? van.transform.position : cinematicStartPosition;
            cinematicEndPosition = anchor + Vector3.up * endingCameraHeight;
            Vector3 forward = Vector3.ProjectOnPlane(
                cinematicStartRotation * Vector3.forward,
                Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            Vector3 skyLook = (Vector3.up * 0.82f + forward * 0.36f).normalized;
            cinematicEndRotation = Quaternion.LookRotation(skyLook, forward);
        }
    }

    private void UpdateEndingCinematic()
    {
        endingElapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(endingElapsed / endingCinematicSeconds);
        float eased = t * t * (3f - 2f * t);
        if (sceneCamera != null)
        {
            sceneCamera.transform.position = Vector3.Lerp(
                cinematicStartPosition,
                cinematicEndPosition,
                eased);
            sceneCamera.transform.rotation = Quaternion.Slerp(
                cinematicStartRotation,
                cinematicEndRotation,
                eased);
        }
        if (t >= 1f)
            state = LoopState.Finished;
    }

    private void ResolveSceneReferences()
    {
        if (van == null)
            van = FindAnyObjectByType<ArcadeDeliveryVan>();
        if (van != null && vanBody == null)
            vanBody = van.GetComponent<Rigidbody>();
        if (followCamera == null)
            followCamera = FindAnyObjectByType<DeliveryFollowCamera>();
        if (followCamera != null)
            sceneCamera = followCamera.GetComponent<Camera>();
        if (sceneCamera == null)
            sceneCamera = Camera.main;
    }

    private void EnsureMarkerMaterial()
    {
        if (markerMaterial != null)
            return;

        if (markerMaterialTemplate == null)
        {
            Debug.LogError(
                "Delivery marker material template is not assigned. "
                + "Assign a material asset instead of resolving a shader at runtime.");
            return;
        }

        markerMaterial = new Material(markerMaterialTemplate)
        {
            name = "Delivery Marker (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private void CreateFeedbackAudio()
    {
        feedbackAudio = gameObject.GetComponent<AudioSource>();
        if (feedbackAudio == null)
            feedbackAudio = gameObject.AddComponent<AudioSource>();
        feedbackAudio.playOnAwake = false;
        feedbackAudio.spatialBlend = 0f;

        const int sampleRate = 44100;
        const float duration = 0.42f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float frequency = time < duration * 0.48f ? 659.25f : 880f;
            float envelope = Mathf.Sin(Mathf.PI * i / sampleCount);
            samples[i] = Mathf.Sin(time * frequency * Mathf.PI * 2f)
                       * envelope * 0.28f;
        }
        deliveryChime = AudioClip.Create(
            "Delivery Complete Chime",
            sampleCount,
            1,
            sampleRate,
            false);
        deliveryChime.SetData(samples, 0);
    }

    private void DestroyMarker()
    {
        if (markerRoot != null)
            Destroy(markerRoot);
        markerRoot = null;
        markerBeacon = null;
    }

    private void OnGUI()
    {
        if (state == LoopState.WaitingForGame)
            return;
        EnsureGuiStyles();
        GUI.depth = -80;

        if (state == LoopState.Delivering)
        {
            int minutes = Mathf.FloorToInt(remainingSeconds / 60f);
            int seconds = Mathf.FloorToInt(remainingSeconds % 60f);
            Rect panel = new(Screen.width * 0.5f - 230f, 18f, 460f, 80f);
            GUI.color = new Color(0.035f, 0.045f, 0.04f, 0.88f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x, panel.y + 8f, panel.width, 34f),
                $"THE WORLD ENDS IN  {minutes:00}:{seconds:00}", timerStyle);
            GUI.Label(new Rect(panel.x, panel.y + 43f, panel.width, 26f),
                $"DELIVERIES  {completedDeliveries} / {deliveryCount}"
                + (van != null && markerRoot != null
                    ? $"     |     NEXT STOP  {Vector3.Distance(van.transform.position, markerPosition):0} m"
                    : string.Empty),
                counterStyle);

            if (parkingProgress > 0.02f)
            {
                float progress = Mathf.Clamp01(parkingProgress / deliveryHoldSeconds);
                Rect progressBack = new(Screen.width * 0.5f - 150f, 108f, 300f, 22f);
                GUI.color = new Color(0f, 0f, 0f, 0.72f);
                GUI.DrawTexture(progressBack, Texture2D.whiteTexture);
                GUI.color = new Color(0.55f, 0.9f, 0.38f, 1f);
                GUI.DrawTexture(new Rect(progressBack.x + 3f, progressBack.y + 3f,
                    (progressBack.width - 6f) * progress, progressBack.height - 6f),
                    Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(progressBack, "DELIVERING...", smallStyle);
            }
            return;
        }

        float reveal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 3.5f, endingElapsed));
        GUI.color = new Color(0.035f, 0.018f, 0.045f, reveal * 0.38f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, reveal);
        string title = won ? "EVERY PACKAGE MADE IT." : "TIME RAN OUT.";
        string subtitle = won
            ? "The world still ended. You did your job anyway."
            : $"{completedDeliveries} of {deliveryCount} packages were delivered.";
        GUI.Label(new Rect(0f, Screen.height * 0.36f, Screen.width, 70f), title, messageStyle);
        GUI.Label(new Rect(0f, Screen.height * 0.36f + 72f, Screen.width, 40f), subtitle, counterStyle);
        GUI.color = Color.white;
    }

    private void EnsureGuiStyles()
    {
        if (timerStyle != null)
            return;
        timerStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 27,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.95f, 0.91f, 0.76f) }
        };
        counterStyle = new GUIStyle(timerStyle)
        {
            fontSize = 18,
            fontStyle = FontStyle.Normal,
            normal = { textColor = new Color(0.72f, 0.82f, 0.67f) }
        };
        messageStyle = new GUIStyle(timerStyle)
        {
            fontSize = 42,
            normal = { textColor = new Color(0.97f, 0.91f, 0.78f) }
        };
        smallStyle = new GUIStyle(timerStyle)
        {
            fontSize = 13,
            normal = { textColor = Color.white }
        };
    }
}
