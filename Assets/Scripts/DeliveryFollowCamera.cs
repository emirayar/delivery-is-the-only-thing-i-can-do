using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public sealed class DeliveryFollowCamera : MonoBehaviour
{
    [Tooltip("Kameranın takip edeceği araç.")]
    public Transform target;
    [Tooltip("Kameranın aracın arkasında kalacağı mesafe.")]
    [Min(1f)] public float followDistance = 7.5f;
    [Tooltip("Kameranın aracın üzerinde kalacağı yükseklik.")]
    [Min(0.5f)] public float followHeight = 4.1f;
    [Tooltip("Kameranın baktığı noktanın araçtan ne kadar ileride olacağı.")]
    [Min(0f)] public float lookAhead = 3.2f;
    [Tooltip("Kamera konumunun hedefe yaklaşma süresi. Küçük değer daha sıkı takip eder.")]
    [Min(0.01f)] public float positionSmoothTime = 0.18f;
    [Tooltip("Kamera dönüşünün yumuşaklık hızı.")]
    [Min(0.1f)] public float rotationSharpness = 9f;
    [Tooltip("Araç hızlandığında görüş alanına eklenecek derece.")]
    [Min(0f)] public float speedFovIncrease = 7f;
    [Tooltip("Maksimum hizda takip mesafesine eklenecek metre. Hiz hissini artirir.")]
    [Min(0f)] public float speedDistanceIncrease = 1.8f;
    [Tooltip("Kameranin aracin burnu yerine gercek hareket yonune ne kadar bakacagi.")]
    [Range(0f, 1f)] public float velocityLookInfluence = 0.35f;

    [Header("Collision")]
    [Tooltip("Kameranin ev, agac ve citlerin icinden gecmesini onleyen sanal kure yaricapi.")]
    [Range(0.05f, 0.75f)] public float collisionRadius = 0.28f;
    [Tooltip("Kamerayi carptigi yuzeyden bu kadar onde tutar.")]
    [Range(0.02f, 0.75f)] public float collisionPadding = 0.18f;
    [Tooltip("Kamera carpismasinda kontrol edilecek katmanlar.")]
    public LayerMask collisionLayers = ~0;

    [Header("First Person")]
    [Tooltip("Oyun basladiginda surucu kamerasini kullanir. C ile gorunum degistirilebilir.")]
    public bool startInFirstPerson;
    [Tooltip("Surucu kamerasinin araca gore konumu. X negatifse direksiyon soldadir.")]
    public Vector3 firstPersonOffset = new(-0.36f, 1.36f, -0.35f);
    [Tooltip("First-person gorus acisi.")]
    [Range(45f, 90f)] public float firstPersonFieldOfView = 67f;
    [Tooltip("First-person fare ve third-person sag tik bakis hassasiyeti.")]
    [Range(0.02f, 0.5f)] public float mouseLookSensitivity = 0.11f;
    [Tooltip("Third-person sag tik birakilinca kameranin arkaya donme hizi.")]
    [Min(0f)] public float lookRecenteringSpeed = 1.8f;
    [Tooltip("Surucu kamerasinin yatay bakis limiti.")]
    [Range(30f, 120f)] public float maximumLookYaw = 82f;
    [Tooltip("First-person kameranin yukari bakabilecegi maksimum aci.")]
    [Range(10f, 89f)] public float maximumLookUp = 70f;
    [Tooltip("First-person kameranin asagi bakabilecegi maksimum aci. Ayaklara kadar bakmak icin 80-89 kullanilabilir.")]
    [Range(10f, 89f)] public float maximumLookDown = 85f;
    [Tooltip("Third-person kameranin sag tikla yatay orbit limiti.")]
    [Range(45f, 180f)] public float thirdPersonMaximumYaw = 115f;
    [Tooltip("Third-person kameranin yukari/asagi orbit limiti.")]
    [Range(10f, 50f)] public float thirdPersonMaximumPitch = 28f;
    [Tooltip("First-person gorunumunde ekran ortasindaki nokta imleci gosterir.")]
    public bool showFirstPersonDot = true;

    private Vector3 positionVelocity;
    private Camera attachedCamera;
    private ArcadeDeliveryVan van;
    private Rigidbody targetBody;
    private float baseFieldOfView;
    private float baseNearClipPlane;
    private bool firstPersonMode;
    private float firstPersonYaw;
    private float firstPersonPitch;
    private float thirdPersonYaw;
    private float thirdPersonPitch;
    private int ignoredLockedMouseFrames;
    private Renderer prototypeBodyRenderer;
    private Renderer prototypeWindshieldRenderer;
    private Renderer[] kenneyExteriorRenderers;
    private Renderer[] cockpitRenderers;
    private readonly RaycastHit[] collisionHits = new RaycastHit[16];

    public bool IsFirstPerson => firstPersonMode;

    public void ApplyUserPreferences(float sensitivity, float fieldOfView)
    {
        mouseLookSensitivity = Mathf.Clamp(sensitivity, 0.02f, 0.5f);
        firstPersonFieldOfView = Mathf.Clamp(fieldOfView, 45f, 90f);
        baseFieldOfView = firstPersonFieldOfView;
        if (attachedCamera == null)
            attachedCamera = GetComponent<Camera>();
        if (attachedCamera != null)
            attachedCamera.fieldOfView = firstPersonFieldOfView;
    }

    public void SetMenuPresentation(bool active)
    {
        ResolveVan();
        if (!active)
        {
            ApplyViewVisibility();
            return;
        }

        if (prototypeBodyRenderer != null)
            prototypeBodyRenderer.enabled = false;
        if (prototypeWindshieldRenderer != null)
            prototypeWindshieldRenderer.enabled = false;
        if (kenneyExteriorRenderers != null)
        {
            foreach (Renderer exteriorRenderer in kenneyExteriorRenderers)
            {
                if (exteriorRenderer != null)
                    exteriorRenderer.enabled = true;
            }
        }
        if (cockpitRenderers != null)
        {
            foreach (Renderer cockpitRenderer in cockpitRenderers)
            {
                if (cockpitRenderer != null)
                    cockpitRenderer.enabled = false;
            }
        }
    }

    private void Awake()
    {
        attachedCamera = GetComponent<Camera>();
        baseFieldOfView = attachedCamera.fieldOfView;
        baseNearClipPlane = attachedCamera.nearClipPlane;
        firstPersonMode = startInFirstPerson;
        ResolveVan();
        ApplyViewVisibility();
        ApplyCursorState();
        SnapToTarget();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            ApplyCursorState();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            ResolveVan();
            if (target == null)
                return;
        }

        ReadCameraInput();
        if (firstPersonMode)
        {
            UpdateFirstPersonCamera();
            return;
        }

        if (attachedCamera != null)
            attachedCamera.nearClipPlane = baseNearClipPlane;

        float speed01 = van != null
            ? Mathf.InverseLerp(0f, van.roadTopSpeedKph, van.SpeedKph)
            : 0f;
        Vector3 flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.forward;
        flatForward.Normalize();

        if (targetBody != null)
        {
            Vector3 velocityDirection = Vector3.ProjectOnPlane(
                targetBody.linearVelocity,
                Vector3.up);
            if (velocityDirection.sqrMagnitude > 4f)
            {
                velocityDirection.Normalize();
                if (Vector3.Dot(velocityDirection, flatForward) < 0f)
                    velocityDirection = -velocityDirection;
                flatForward = Vector3.Slerp(
                    flatForward,
                    velocityDirection,
                    velocityLookInfluence * speed01).normalized;
            }
        }

        float currentFollowDistance = followDistance
                                    + speedDistanceIncrease * speed01;
        Vector3 orbitForward = Quaternion.AngleAxis(
            thirdPersonYaw,
            Vector3.up) * flatForward;
        float orbitHeight = followHeight
                          + Mathf.Sin(thirdPersonPitch * Mathf.Deg2Rad)
                          * 3.2f;
        float orbitDistance = currentFollowDistance
                            * Mathf.Cos(thirdPersonPitch * Mathf.Deg2Rad);
        Vector3 desiredPosition = target.position
                                - orbitForward * orbitDistance
                                + Vector3.up * orbitHeight;
        Vector3 lookTarget = target.position
                           + flatForward * lookAhead
                           + Vector3.up * 0.75f;
        desiredPosition = ResolveCameraCollision(lookTarget, desiredPosition);
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime);

        Quaternion desiredRotation = Quaternion.LookRotation(
            lookTarget - transform.position,
            Vector3.up);
        float rotationBlend = 1f - Mathf.Exp(
            -rotationSharpness * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationBlend);

        if (attachedCamera != null)
        {
            attachedCamera.fieldOfView = Mathf.Lerp(
                attachedCamera.fieldOfView,
                baseFieldOfView + speedFovIncrease * speed01,
                rotationBlend);
        }
    }

    [ContextMenu("Snap To Target")]
    public void SnapToTarget()
    {
        if (target == null)
            return;

        Vector3 flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.forward;
        flatForward.Normalize();

        transform.position = target.position
                           - flatForward * followDistance
                           + Vector3.up * followHeight;
        transform.LookAt(
            target.position + flatForward * lookAhead + Vector3.up * 0.75f);
        positionVelocity = Vector3.zero;
    }

    private void ReadCameraInput()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        if ((keyboard != null && keyboard.cKey.wasPressedThisFrame)
            || (gamepad != null
                && gamepad.rightStickButton.wasPressedThisFrame))
        {
            firstPersonMode = !firstPersonMode;
            firstPersonYaw = 0f;
            firstPersonPitch = 0f;
            positionVelocity = Vector3.zero;
            if (!firstPersonMode)
                SnapToTarget();
            ApplyViewVisibility();
            ApplyCursorState();
        }

        Mouse mouse = Mouse.current;
        if (firstPersonMode)
        {
            if (mouse != null)
            {
                if (ignoredLockedMouseFrames > 0)
                {
                    ignoredLockedMouseFrames--;
                }
                else
                {
                    Vector2 delta = Vector2.ClampMagnitude(
                        mouse.delta.ReadValue(),
                        90f);
                    firstPersonYaw += delta.x * mouseLookSensitivity;
                    firstPersonPitch -= delta.y * mouseLookSensitivity;
                }
            }

            if (gamepad != null)
            {
                Vector2 stick = gamepad.rightStick.ReadValue();
                if (stick.sqrMagnitude > 0.02f)
                {
                    firstPersonYaw += stick.x * 105f * Time.unscaledDeltaTime;
                    firstPersonPitch -= stick.y * 80f * Time.unscaledDeltaTime;
                }
            }

            firstPersonYaw = Mathf.Clamp(
                firstPersonYaw,
                -maximumLookYaw,
                maximumLookYaw);
            firstPersonPitch = Mathf.Clamp(
                firstPersonPitch,
                -maximumLookUp,
                maximumLookDown);
            return;
        }

        bool orbiting = false;
        if (mouse != null && mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            thirdPersonYaw += delta.x * mouseLookSensitivity;
            thirdPersonPitch -= delta.y * mouseLookSensitivity;
            orbiting = true;
        }
        if (gamepad != null)
        {
            Vector2 stick = gamepad.rightStick.ReadValue();
            if (stick.sqrMagnitude > 0.02f)
            {
                thirdPersonYaw += stick.x * 105f * Time.unscaledDeltaTime;
                thirdPersonPitch -= stick.y * 80f * Time.unscaledDeltaTime;
                orbiting = true;
            }
        }
        thirdPersonYaw = Mathf.Clamp(
            thirdPersonYaw,
            -thirdPersonMaximumYaw,
            thirdPersonMaximumYaw);
        thirdPersonPitch = Mathf.Clamp(
            thirdPersonPitch,
            -thirdPersonMaximumPitch,
            thirdPersonMaximumPitch);
        if (!orbiting && lookRecenteringSpeed > 0f)
        {
            float blend = 1f - Mathf.Exp(
                -lookRecenteringSpeed * Time.unscaledDeltaTime);
            thirdPersonYaw = Mathf.Lerp(thirdPersonYaw, 0f, blend);
            thirdPersonPitch = Mathf.Lerp(thirdPersonPitch, 0f, blend);
        }
    }

    private void UpdateFirstPersonCamera()
    {
        // A follow-camera spring creates visible acceleration lag inside the
        // cabin. The driver's head must remain rigidly attached to the van.
        transform.position = target.TransformPoint(firstPersonOffset);
        positionVelocity = Vector3.zero;

        Vector3 levelForward = Vector3.ProjectOnPlane(
            target.forward,
            Vector3.up).normalized;
        if (levelForward.sqrMagnitude < 0.001f)
            levelForward = Vector3.forward;
        Quaternion vehicleHeading = Quaternion.LookRotation(
            levelForward,
            Vector3.up);
        Quaternion headLook = Quaternion.Euler(
            firstPersonPitch,
            firstPersonYaw,
            0f);
        transform.rotation = vehicleHeading * headLook;

        if (attachedCamera != null)
        {
            attachedCamera.nearClipPlane = 0.035f;
            attachedCamera.fieldOfView = Mathf.Lerp(
                attachedCamera.fieldOfView,
                firstPersonFieldOfView,
                1f - Mathf.Exp(-12f * Time.deltaTime));
        }


        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            Ray interactionRay = new Ray(
                transform.position,
                transform.forward);
            if (Physics.Raycast(
                    interactionRay,
                    out RaycastHit hit,
                    1.8f,
                    ~0,
                    QueryTriggerInteraction.Collide))
            {
                RadioButtonInteractable button =
                    hit.collider.GetComponent<RadioButtonInteractable>();
                if (button != null)
                    button.Activate();
            }
        }
    }

    private void ResolveVan()
    {
        if (target != null)
            van = target.GetComponent<ArcadeDeliveryVan>();
        else
        {
            van = FindAnyObjectByType<ArcadeDeliveryVan>();
            if (van != null)
                target = van.transform;
        }

        targetBody = target != null
            ? target.GetComponent<Rigidbody>()
            : null;
        prototypeBodyRenderer = target != null
            ? target.Find("Van Body")?.GetComponent<Renderer>()
            : null;
        prototypeWindshieldRenderer = target != null
            ? target.Find("Windshield")?.GetComponent<Renderer>()
            : null;
        Transform playerExterior = target != null
            ? target.Find("Player Exterior")
            : null;
        if (playerExterior == null && target != null)
            playerExterior = target.Find("Kenney Player Exterior");
        kenneyExteriorRenderers = playerExterior != null
            ? playerExterior.GetComponentsInChildren<Renderer>(true)
            : null;
        Transform cockpit = target != null
            ? target.Find("Cockpit Interior")
            : null;
        cockpitRenderers = cockpit != null
            ? cockpit.GetComponentsInChildren<Renderer>(true)
            : null;
    }

    private void ApplyViewVisibility()
    {
        bool hasKenneyExterior = kenneyExteriorRenderers != null
                              && kenneyExteriorRenderers.Length > 0;
        if (prototypeBodyRenderer != null)
            prototypeBodyRenderer.enabled = !hasKenneyExterior
                                           && !firstPersonMode;
        if (prototypeWindshieldRenderer != null)
            prototypeWindshieldRenderer.enabled = !hasKenneyExterior
                                                 && !firstPersonMode;
        if (kenneyExteriorRenderers == null)
            return;
        foreach (Renderer exteriorRenderer in kenneyExteriorRenderers)
        {
            if (exteriorRenderer != null)
                exteriorRenderer.enabled = !firstPersonMode;
        }
        if (cockpitRenderers == null)
            return;
        foreach (Renderer cockpitRenderer in cockpitRenderers)
        {
            if (cockpitRenderer != null)
                cockpitRenderer.enabled = firstPersonMode;
        }
    }

    private void ApplyCursorState()
    {
        if (firstPersonMode)
            ignoredLockedMouseFrames = 3;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            return;
        if (isActiveAndEnabled && MainMenuController.IsGameplayActive)
            ApplyCursorState();
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnGUI()
    {
        if (!firstPersonMode || !showFirstPersonDot)
            return;

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(
            new Rect(centerX - 4f, centerY - 4f, 8f, 8f),
            Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(
            new Rect(centerX - 2f, centerY - 2f, 4f, 4f),
            Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private Vector3 ResolveCameraCollision(
        Vector3 lookTarget,
        Vector3 desiredPosition)
    {
        Vector3 offset = desiredPosition - lookTarget;
        float distance = offset.magnitude;
        if (distance < 0.01f)
            return desiredPosition;

        Vector3 direction = offset / distance;
        int hitCount = Physics.SphereCastNonAlloc(
            lookTarget,
            collisionRadius,
            direction,
            collisionHits,
            distance,
            collisionLayers,
            QueryTriggerInteraction.Ignore);
        float nearestDistance = distance;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = collisionHits[i].collider;
            if (hitCollider == null
                || (target != null
                    && hitCollider.transform.IsChildOf(target)))
            {
                continue;
            }

            nearestDistance = Mathf.Min(
                nearestDistance,
                collisionHits[i].distance);
        }

        return lookTarget
             + direction * Mathf.Max(0.5f, nearestDistance - collisionPadding);
    }
}
