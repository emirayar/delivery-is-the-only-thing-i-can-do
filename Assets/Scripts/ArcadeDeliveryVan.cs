using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class ArcadeDeliveryVan : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Yol üzerinde mi çimde mi olduğumuzu hesaplamak için kullanılan spline.")]
    public RoadSpline roadSpline;

    [Header("Driving")]
    [Tooltip("Yoldaki en yüksek ileri hız (km/saat).")]
    [Min(5f)] public float roadTopSpeedKph = 58f;
    [Tooltip("Çimdeki en yüksek ileri hız (km/saat).")]
    [Min(5f)] public float grassTopSpeedKph = 28f;
    [Tooltip("Motorun aracı ne kadar hızlı ivmelendirdiği.")]
    [Min(1f)] public float acceleration = 16f;
    [Tooltip("Geri vitesin ulaşabileceği en yüksek hız (km/saat).")]
    [Min(2f)] public float reverseTopSpeedKph = 18f;
    [Tooltip("Space veya ters yönde gaz verildiğinde uygulanan fren gücü.")]
    [Min(1f)] public float brakeStrength = 26f;
    [Tooltip("Gaz bırakıldığında aracın saniyede ne kadar yavaşlayacağı. Küçük değer daha uzun süzülme sağlar.")]
    [Min(0f)] public float coastingDeceleration = 2.5f;

    [Header("Handling")]
    [Tooltip("Düşük ve orta hızda saniyedeki maksimum dönüş açısı.")]
    [Min(5f)] public float steeringDegreesPerSecond = 105f;
    [Tooltip("Yan kaymayı azaltan arcade yol tutuşu. Çok yükseltmek aracı ray üzerinde hissettirebilir.")]
    [Min(0f)] public float lateralGrip = 7.5f;
    [Tooltip("Hız arttıkça aracı yere bastıran kuvvet.")]
    [Min(0f)] public float downforce = 0.035f;
    [Tooltip("Yol kenarından ne kadar uzakta hâlâ asfalt üzerinde sayılacağı.")]
    [Min(0f)] public float roadDetectionPadding = 0.4f;

    [Header("Grounding")]
    [Tooltip("Aracın merkezinden aşağı gönderilen zemin ışınının uzunluğu. Spawn yüksekliğinden büyük olmalıdır.")]
    [Min(0.2f)] public float groundCheckDistance = 2.25f;
    [Tooltip("Zemin kontrolünde kullanılacak fizik katmanları.")]
    public LayerMask groundLayers = ~0;
    [Tooltip("Aracın terrain yüzeyinin üzerinde tutulacağı yükseklik.")]
    [Min(0.01f)] public float rideHeight = 0.08f;

    [Header("Prototype UI")]
    [Tooltip("Sol üstte hız, zemin ve kontrolleri gösteren geçici arayüz.")]
    public bool showPrototypeHud = true;

    private Rigidbody body;
    private CapsuleCollider bodyCollider;
    private PhysicsMaterial runtimeBodyMaterial;
    private float throttleInput;
    private float steeringInput;
    private bool brakeInput;
    private bool grounded;
    private bool onRoad;
    private float currentDriveSpeed;
    private Vector3 respawnPosition;
    private Quaternion respawnRotation;
    private readonly RaycastHit[] groundHits = new RaycastHit[16];

    public float SpeedKph => Mathf.Abs(currentDriveSpeed) * 3.6f;
    public bool IsOnRoad => onRoad;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<CapsuleCollider>();
        body.isKinematic = true;
        body.useGravity = false;
        runtimeBodyMaterial = new PhysicsMaterial("Van Zero Friction")
        {
            hideFlags = HideFlags.HideAndDontSave,
            staticFriction = 0f,
            dynamicFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        bodyCollider.material = runtimeBodyMaterial;
        respawnPosition = transform.position;
        respawnRotation = transform.rotation;
    }

    private void OnDestroy()
    {
        if (runtimeBodyMaterial != null)
            Destroy(runtimeBodyMaterial);
    }

    private void Update()
    {
        ReadInput();

        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;
        if ((keyboard != null && keyboard.rKey.wasPressedThisFrame)
            || (gamepad != null && gamepad.selectButton.wasPressedThisFrame))
        {
            ResetVan();
        }
    }

    private void FixedUpdate()
    {
        grounded = TryFindGround(body.position, out RaycastHit currentGround);

        if (roadSpline != null)
        {
            Vector3 closestRoadPoint =
                roadSpline.FindClosestPointXZ(transform.position, out _);
            Vector2 vanXZ = new(transform.position.x, transform.position.z);
            Vector2 roadXZ = new(closestRoadPoint.x, closestRoadPoint.z);
            onRoad = Vector2.Distance(vanXZ, roadXZ)
                  <= roadSpline.roadWidth * 0.5f + roadDetectionPadding;
        }
        else
        {
            onRoad = false;
        }

        if (!grounded)
            return;

        float topSpeed = (onRoad ? roadTopSpeedKph : grassTopSpeedKph) / 3.6f;
        float reverseTopSpeed = reverseTopSpeedKph / 3.6f;
        bool changingDirection = Mathf.Abs(currentDriveSpeed) > 0.8f
                              && Mathf.Sign(throttleInput) != Mathf.Sign(currentDriveSpeed);

        if (brakeInput || changingDirection)
        {
            currentDriveSpeed = Mathf.MoveTowards(
                currentDriveSpeed,
                0f,
                brakeStrength * Time.fixedDeltaTime);
        }
        else if (Mathf.Abs(throttleInput) > 0.01f)
        {
            float targetSpeed = throttleInput > 0f
                ? throttleInput * topSpeed
                : throttleInput * reverseTopSpeed;
            float surfaceAcceleration = onRoad ? 1f : 0.72f;
            currentDriveSpeed = Mathf.MoveTowards(
                currentDriveSpeed,
                targetSpeed,
                acceleration * surfaceAcceleration * Time.fixedDeltaTime);
        }
        else
        {
            currentDriveSpeed = Mathf.MoveTowards(
                currentDriveSpeed,
                0f,
                coastingDeceleration * Time.fixedDeltaTime);
        }

        Quaternion nextRotation = CalculateSteering(currentDriveSpeed);
        Vector3 nextForward = Vector3.ProjectOnPlane(
            nextRotation * Vector3.forward,
            Vector3.up).normalized;
        Vector3 nextPosition = body.position
                             + nextForward
                             * currentDriveSpeed
                             * Time.fixedDeltaTime;

        if (TryFindGround(nextPosition, out RaycastHit nextGround))
            nextPosition.y = nextGround.point.y + rideHeight;
        else
            nextPosition.y = currentGround.point.y + rideHeight;

        body.MoveRotation(nextRotation);
        body.MovePosition(nextPosition);
    }

    private void ReadInput()
    {
        throttleInput = 0f;
        steeringInput = 0f;
        brakeInput = false;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                throttleInput += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                throttleInput -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                steeringInput += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                steeringInput -= 1f;
            brakeInput = keyboard.spaceKey.isPressed;
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            float gamepadThrottle = gamepad.rightTrigger.ReadValue()
                                  - gamepad.leftTrigger.ReadValue();
            if (Mathf.Abs(gamepadThrottle) > Mathf.Abs(throttleInput))
                throttleInput = gamepadThrottle;

            float gamepadSteering = gamepad.leftStick.x.ReadValue();
            if (Mathf.Abs(gamepadSteering) > Mathf.Abs(steeringInput))
                steeringInput = gamepadSteering;

            brakeInput |= gamepad.buttonSouth.isPressed;
        }
    }

    private Quaternion CalculateSteering(float forwardSpeed)
    {
        float speedMagnitude = Mathf.Abs(forwardSpeed);
        if (speedMagnitude < 0.15f || Mathf.Abs(steeringInput) < 0.01f)
            return body.rotation;

        float direction = forwardSpeed >= 0f ? 1f : -1f;
        float lowSpeedAuthority = Mathf.InverseLerp(0.15f, 3.5f, speedMagnitude);
        float highSpeedReduction = Mathf.Lerp(
            1f,
            0.38f,
            Mathf.InverseLerp(8f, roadTopSpeedKph / 3.6f, speedMagnitude));
        float yaw = steeringInput
                  * direction
                  * steeringDegreesPerSecond
                  * lowSpeedAuthority
                  * highSpeedReduction
                  * Time.fixedDeltaTime;

        return body.rotation * Quaternion.Euler(0f, yaw, 0f);
    }

    private bool TryFindGround(Vector3 position, out RaycastHit bestHit)
    {
        Vector3 origin = position + Vector3.up * 6f;
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            groundHits,
            Mathf.Max(12f, groundCheckDistance + 6f),
            groundLayers,
            QueryTriggerInteraction.Ignore);

        bestHit = default;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider == null
                || hit.rigidbody == body
                || hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                bestHit = hit;
            }
        }

        return bestDistance < float.MaxValue;
    }

    [ContextMenu("Reset Van")]
    public void ResetVan()
    {
        if (body == null)
            body = GetComponent<Rigidbody>();

        currentDriveSpeed = 0f;
        body.position = respawnPosition;
        body.rotation = respawnRotation;
    }

    private void OnGUI()
    {
        if (!showPrototypeHud || body == null)
            return;

        GUILayout.BeginArea(new Rect(18f, 18f, 250f, 138f), GUI.skin.box);
        GUILayout.Label($"HIZ  {SpeedKph:0} km/h");
        GUILayout.Label(
            $"{(grounded ? "YERDE" : "HAVADA")}  •  "
            + (onRoad ? "Yol" : "Çim"));
        GUILayout.Label($"Gaz {throttleInput:0.0}  •  Direksiyon {steeringInput:0.0}");
        GUILayout.Label("Kinematik terrain takibi");
        GUILayout.Label("WASD / Oklar: Sürüş");
        GUILayout.Label("Space: Fren    R: Sıfırla");
        GUILayout.EndArea();
    }
}
