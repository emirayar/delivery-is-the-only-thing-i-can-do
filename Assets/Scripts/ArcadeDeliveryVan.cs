using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class ArcadeDeliveryVan : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Asfalt ve arazi hızlarını ayırmak için kullanılan yol spline'ı.")]
    public RoadSpline roadSpline;

    [Header("Powertrain")]
    [Tooltip("Asfalttaki yaklaşık maksimum hız (km/saat). Motor torku bu hıza yaklaşırken yumuşakça azalır.")]
    [Min(20f)] public float roadTopSpeedKph = 62f;
    [Tooltip("Arazi üzerindeki yaklaşık maksimum hız (km/saat).")]
    [Min(10f)] public float grassTopSpeedKph = 31f;
    [Tooltip("Tahrik tekerleklerine dağıtılan toplam düşük hız motor torku (Nm).")]
    [Min(100f)] public float maximumMotorTorque = 1500f;
    [Tooltip("Geri vitesteki yaklaşık maksimum hız (km/saat).")]
    [Min(5f)] public float reverseTopSpeedKph = 20f;
    [Tooltip("Normal fren sırasında uygulanan toplam fren torku (Nm).")]
    [Min(100f)] public float serviceBrakeTorque = 3100f;
    [Tooltip("Space ile arka tekerleklere uygulanan el freni torku (Nm).")]
    [Min(100f)] public float handbrakeTorque = 4200f;
    [Tooltip("Gaz bırakıldığında motor ve aktarma organlarının doğal yavaşlatma torku.")]
    [Min(0f)] public float engineBrakingTorque = 85f;
    [Tooltip("Açıkken dört tekerden çekiş kullanır. Kapalıyken tork arka tekerleklere gider.")]
    public bool allWheelDrive;

    [Header("Steering")]
    [Tooltip("Çok düşük hızdaki maksimum ön tekerlek dönüş açısı.")]
    [Range(15f, 45f)] public float maximumSteerAngle = 32f;
    [Tooltip("Maksimum hız civarında korunacak ön tekerlek dönüş açısı.")]
    [Range(5f, 20f)] public float highSpeedSteerAngle = 10f;
    [Tooltip("Direksiyonun hedef açıya yaklaşma hızı. Küçük değer ağır, büyük değer çevik hissettirir.")]
    [Min(1f)] public float steeringResponse = 8f;

    [Header("Suspension")]
    [Tooltip("Fizik tekerleğinin yarıçapı (metre).")]
    [Range(0.2f, 0.55f)] public float wheelRadius = 0.32f;
    [Tooltip("Tekerleğin aşağı-yukarı hareket edebileceği süspansiyon mesafesi.")]
    [Range(0.08f, 0.5f)] public float suspensionDistance = 0.24f;
    [Tooltip("Süspansiyon yay sertliği (N/m).")]
    [Min(5000f)] public float suspensionSpring = 32000f;
    [Tooltip("Süspansiyon salınımını söndüren damper kuvveti.")]
    [Min(500f)] public float suspensionDamper = 4300f;
    [Tooltip("Süspansiyonun dinlenme konumu. 0 tam uzamış, 1 tam sıkışmış.")]
    [Range(0.2f, 0.8f)] public float suspensionTarget = 0.5f;
    [Tooltip("Virajda gövde yatmasını azaltmak için aks başına anti-roll kuvveti.")]
    [Min(0f)] public float antiRollForce = 6500f;

    [Header("Tyre Grip")]
    [Tooltip("Asfalttaki ileri/geri lastik tutuş çarpanı.")]
    [Range(0.5f, 3f)] public float roadForwardGrip = 1.45f;
    [Tooltip("Asfalttaki yan lastik tutuş çarpanı.")]
    [Range(0.5f, 3f)] public float roadSidewaysGrip = 1.6f;
    [Tooltip("Çim ve tarladaki ileri/geri lastik tutuş çarpanı.")]
    [Range(0.2f, 2f)] public float grassForwardGrip = 0.72f;
    [Tooltip("Çim ve tarladaki yan lastik tutuş çarpanı.")]
    [Range(0.2f, 2f)] public float grassSidewaysGrip = 0.62f;
    [Tooltip("Yüksek hızda aracı yere bastıran aerodinamik kuvvet katsayısı.")]
    [Min(0f)] public float downforceCoefficient = 2.1f;
    [Tooltip("WheelCollider'a ek olarak yan kaymayı yumuşatan düşük seviyeli simcade desteği.")]
    [Min(0f)] public float lateralStability = 1.7f;

    [Header("Body")]
    [Tooltip("Aracın fizik kütlesi (kg).")]
    [Min(300f)] public float vehicleMass = 1180f;
    [Tooltip("Yerel koordinatta ağırlık merkezi. Negatif Y devrilme eğilimini azaltır.")]
    public Vector3 centerOfMass = new(0f, -0.48f, 0.08f);
    [Tooltip("Yoldan ne kadar uzakta hâlâ asfalt üzerinde sayılacağı.")]
    [Min(0f)] public float roadDetectionPadding = 0.45f;

    [Header("Prototype UI")]
    public bool showPrototypeHud = true;

    [Header("Recovery")]
    [Tooltip("How often a grounded, upright road position is saved for manual reset.")]
    [Min(0.25f)] public float checkpointInterval = 1.5f;

    private sealed class RuntimeWheel
    {
        public WheelCollider collider;
        public Transform visual;
        public Quaternion visualRotationOffset;
        public Transform exteriorVisual;
        public Quaternion exteriorRotationOffset;
        public bool front;
        public bool left;
        public bool grounded;
        public WheelHit hit;
    }

    private readonly List<RuntimeWheel> wheels = new(4);
    private Rigidbody body;
    private CapsuleCollider bodyCollider;
    private PhysicsMaterial runtimeBodyMaterial;
    private Transform wheelColliderRoot;
    private float throttleInput;
    private float steeringInput;
    private float smoothedSteering;
    private bool handbrakeInput;
    private bool grounded;
    private bool onRoad;
    private Vector3 respawnPosition;
    private Quaternion respawnRotation;
    private float checkpointTimer;
    private Transform cockpitSteeringWheel;
    private Transform cockpitSpeedNeedle;
    private Quaternion cockpitSteeringRestRotation;
    private Quaternion cockpitNeedleRestRotation;
    private Vector3 cockpitSteeringRotationAxis = Vector3.forward;

    public float SpeedKph => body != null
        ? body.linearVelocity.magnitude * 3.6f
        : 0f;
    public float ForwardSpeedKph => body != null
        ? Vector3.Dot(body.linearVelocity, transform.forward) * 3.6f
        : 0f;
    public float ThrottleInput => throttleInput;
    public bool IsOnRoad => onRoad;
    public bool IsGrounded => grounded;
    public float SteeringVisual01 => smoothedSteering;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<CapsuleCollider>();
        ConfigureBody();
        BuildWheelRig();
        ResolveCockpitVisuals();
        respawnPosition = transform.position;
        respawnRotation = transform.rotation;
    }

    private void ConfigureBody()
    {
        body.isKinematic = false;
        body.useGravity = true;
        body.mass = vehicleMass;
        body.centerOfMass = centerOfMass;
        body.linearDamping = 0.035f;
        body.angularDamping = 0.55f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.maxAngularVelocity = 12f;
        body.constraints = RigidbodyConstraints.None;

        bodyCollider.enabled = true;
        bodyCollider.direction = 2;
        bodyCollider.radius = 0.68f;
        bodyCollider.height = 3.35f;
        bodyCollider.center = new Vector3(0f, 0.82f, 0f);
        runtimeBodyMaterial = new PhysicsMaterial("Van Body Low Friction")
        {
            hideFlags = HideFlags.HideAndDontSave,
            staticFriction = 0.08f,
            dynamicFriction = 0.04f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        bodyCollider.material = runtimeBodyMaterial;
    }

    private void BuildWheelRig()
    {
        wheels.Clear();
        Transform existing = transform.Find("Runtime Wheel Colliders");
        if (existing != null)
        {
            foreach (Collider collider in existing.GetComponentsInChildren<Collider>())
                collider.enabled = false;
            Destroy(existing.gameObject);
        }

        var root = new GameObject("Runtime Wheel Colliders")
        {
            hideFlags = HideFlags.DontSave
        };
        wheelColliderRoot = root.transform;
        wheelColliderRoot.SetParent(transform, false);

        var visuals = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != wheelColliderRoot
                && child.name.StartsWith("Wheel"))
            {
                visuals.Add(child);
            }
        }
        visuals.Sort((a, b) =>
        {
            int frontOrder = b.localPosition.z.CompareTo(a.localPosition.z);
            return frontOrder != 0
                ? frontOrder
                : a.localPosition.x.CompareTo(b.localPosition.x);
        });

        var exteriorWheels = new List<Transform>();
        Transform kenneyExterior = transform.Find("Kenney Player Exterior");
        if (kenneyExterior != null)
        {
            foreach (Transform child in
                     kenneyExterior.GetComponentsInChildren<Transform>(true))
            {
                if (child != kenneyExterior
                    && child.name.StartsWith("wheel-"))
                {
                    exteriorWheels.Add(child);
                }
            }
            exteriorWheels.Sort((a, b) =>
            {
                Vector3 localA = transform.InverseTransformPoint(a.position);
                Vector3 localB = transform.InverseTransformPoint(b.position);
                int frontOrder = localB.z.CompareTo(localA.z);
                return frontOrder != 0
                    ? frontOrder
                    : localA.x.CompareTo(localB.x);
            });
        }

        for (int i = 0; i < visuals.Count; i++)
        {
            Transform visual = visuals[i];
            Transform exteriorWheel = exteriorWheels.Count == visuals.Count
                ? exteriorWheels[i]
                : null;
            if (exteriorWheel != null)
            {
                visual.localPosition = transform.InverseTransformPoint(
                    exteriorWheel.position);
            }
            visual.localScale = new Vector3(
                wheelRadius * 2f,
                0.18f,
                wheelRadius * 2f);
            bool front = visual.localPosition.z > 0f;
            bool left = visual.localPosition.x < 0f;
            var wheelObject = new GameObject(
                $"{(front ? "Front" : "Rear")} {(left ? "Left" : "Right")}");
            wheelObject.transform.SetParent(wheelColliderRoot, false);
            wheelObject.transform.localPosition = visual.localPosition;
            WheelCollider wheel = wheelObject.AddComponent<WheelCollider>();
            ConfigureWheel(wheel);
            wheels.Add(new RuntimeWheel
            {
                collider = wheel,
                visual = visual,
                visualRotationOffset = Quaternion.Inverse(transform.rotation)
                                     * visual.rotation,
                exteriorVisual = exteriorWheel,
                exteriorRotationOffset = exteriorWheel != null
                    ? Quaternion.Inverse(transform.rotation)
                      * exteriorWheel.rotation
                    : Quaternion.identity,
                front = front,
                left = left
            });
        }

        if (wheels.Count > 0)
        {
            wheels[0].collider.ConfigureVehicleSubsteps(5f, 12, 15);
            wheels[0].collider.ResetSprungMasses();
        }
    }

    private void ConfigureWheel(WheelCollider wheel)
    {
        wheel.mass = 28f;
        wheel.radius = wheelRadius;
        wheel.suspensionDistance = suspensionDistance;
        wheel.forceAppPointDistance = 0.08f;
        wheel.wheelDampingRate = 0.35f;
        wheel.suspensionExpansionLimited = true;
        JointSpring spring = wheel.suspensionSpring;
        spring.spring = suspensionSpring;
        spring.damper = suspensionDamper;
        spring.targetPosition = suspensionTarget;
        wheel.suspensionSpring = spring;
        SetWheelGrip(wheel, true);
    }

    private void OnDestroy()
    {
        if (runtimeBodyMaterial != null)
            Destroy(runtimeBodyMaterial);
    }

    private void Update()
    {
        ReadInput();
        UpdateWheelVisuals();
        UpdateCockpitVisuals();

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
        if (wheels.Count != 4)
            return;

        UpdateGroundState();
        UpdateRecoveryCheckpoint();
        ApplySteering();
        ApplyPowerAndBrakes();
        ApplyAntiRoll(wheels[0], wheels[1]);
        ApplyAntiRoll(wheels[2], wheels[3]);
        ApplyStabilityForces();
    }

    private void UpdateGroundState()
    {
        int groundedCount = 0;
        int roadWheelCount = 0;
        foreach (RuntimeWheel wheel in wheels)
        {
            wheel.grounded = wheel.collider.GetGroundHit(out wheel.hit);
            if (wheel.grounded)
            {
                groundedCount++;
                if (IsRoadCollider(wheel.hit.collider))
                    roadWheelCount++;
            }
        }
        grounded = groundedCount >= 2;

        if (roadSpline != null)
        {
            Vector3 closest = roadSpline.FindClosestPointXZ(
                transform.position,
                out _);
            float distance = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(closest.x, closest.z));
            onRoad = roadWheelCount >= 2
                  || distance <= roadSpline.roadWidth * 0.5f
                               + roadDetectionPadding;
        }
        else
        {
            onRoad = roadWheelCount >= 2;
        }

        foreach (RuntimeWheel wheel in wheels)
            SetWheelGrip(wheel.collider, onRoad);
    }

    private static bool IsRoadCollider(Collider collider)
    {
        if (collider == null)
            return false;
        string ownerName = collider.gameObject.name;
        return ownerName.Contains("Road")
            || ownerName.Contains("Path")
            || ownerName.Contains("Driveway");
    }

    private void SetWheelGrip(WheelCollider wheel, bool roadGrip)
    {
        WheelFrictionCurve forward = wheel.forwardFriction;
        forward.extremumSlip = roadGrip ? 0.35f : 0.55f;
        forward.extremumValue = 1f;
        forward.asymptoteSlip = roadGrip ? 0.8f : 1.15f;
        forward.asymptoteValue = roadGrip ? 0.72f : 0.55f;
        forward.stiffness = roadGrip ? roadForwardGrip : grassForwardGrip;
        wheel.forwardFriction = forward;

        WheelFrictionCurve sideways = wheel.sidewaysFriction;
        sideways.extremumSlip = roadGrip ? 0.28f : 0.5f;
        sideways.extremumValue = 1f;
        sideways.asymptoteSlip = roadGrip ? 0.65f : 1f;
        sideways.asymptoteValue = roadGrip ? 0.72f : 0.52f;
        sideways.stiffness = roadGrip ? roadSidewaysGrip : grassSidewaysGrip;
        wheel.sidewaysFriction = sideways;
    }

    private void ApplySteering()
    {
        float speed01 = Mathf.InverseLerp(0f, roadTopSpeedKph, SpeedKph);
        float availableAngle = Mathf.Lerp(
            maximumSteerAngle,
            highSpeedSteerAngle,
            speed01);
        smoothedSteering = Mathf.MoveTowards(
            smoothedSteering,
            steeringInput,
            steeringResponse * Time.fixedDeltaTime);
        float targetAngle = smoothedSteering * availableAngle;
        foreach (RuntimeWheel wheel in wheels)
            wheel.collider.steerAngle = wheel.front ? targetAngle : 0f;
    }

    private void ApplyPowerAndBrakes()
    {
        float signedSpeed = ForwardSpeedKph;
        float topSpeed = onRoad ? roadTopSpeedKph : grassTopSpeedKph;
        bool directionConflict = Mathf.Abs(signedSpeed) > 1.5f
                              && Mathf.Abs(throttleInput) > 0.05f
                              && Mathf.Sign(signedSpeed) != Mathf.Sign(throttleInput);
        bool serviceBraking = directionConflict;
        float requestedDirection = directionConflict ? 0f : throttleInput;
        float speedLimit = requestedDirection < 0f
            ? reverseTopSpeedKph
            : topSpeed;
        float speedRatio = Mathf.Clamp01(Mathf.Abs(signedSpeed) / speedLimit);
        float torqueCurve = 1f - Mathf.SmoothStep(0.55f, 1f, speedRatio);
        float terrainTorque = onRoad ? 1f : 0.72f;
        int drivenWheelCount = allWheelDrive ? 4 : 2;
        float wheelTorque = requestedDirection
                          * maximumMotorTorque
                          * torqueCurve
                          * terrainTorque
                          / drivenWheelCount;

        foreach (RuntimeWheel wheel in wheels)
        {
            bool driven = allWheelDrive || !wheel.front;
            wheel.collider.motorTorque = driven ? wheelTorque : 0f;

            float brake = 0f;
            if (serviceBraking)
                brake += serviceBrakeTorque * (wheel.front ? 0.65f : 0.35f) / 2f;
            else if (Mathf.Abs(throttleInput) < 0.03f)
                brake += engineBrakingTorque;
            if (handbrakeInput && !wheel.front)
                brake += handbrakeTorque * 0.5f;
            wheel.collider.brakeTorque = brake;
        }
    }

    private void ApplyAntiRoll(RuntimeWheel leftWheel, RuntimeWheel rightWheel)
    {
        float leftTravel = 1f;
        float rightTravel = 1f;
        bool leftGrounded = leftWheel.collider.GetGroundHit(out WheelHit leftHit);
        bool rightGrounded = rightWheel.collider.GetGroundHit(out WheelHit rightHit);
        if (leftGrounded)
        {
            leftTravel = (-leftWheel.collider.transform.InverseTransformPoint(
                leftHit.point).y - wheelRadius)
                / Mathf.Max(0.01f, suspensionDistance);
        }
        if (rightGrounded)
        {
            rightTravel = (-rightWheel.collider.transform.InverseTransformPoint(
                rightHit.point).y - wheelRadius)
                / Mathf.Max(0.01f, suspensionDistance);
        }

        float antiRoll = (leftTravel - rightTravel) * antiRollForce;
        if (leftGrounded)
        {
            body.AddForceAtPosition(
                leftWheel.collider.transform.up * -antiRoll,
                leftWheel.collider.transform.position);
        }
        if (rightGrounded)
        {
            body.AddForceAtPosition(
                rightWheel.collider.transform.up * antiRoll,
                rightWheel.collider.transform.position);
        }
    }

    private void ApplyStabilityForces()
    {
        if (!grounded)
            return;
        float speed = body.linearVelocity.magnitude;
        body.AddForce(
            -transform.up * speed * speed * downforceCoefficient,
            ForceMode.Force);

        float lateralSpeed = Vector3.Dot(
            body.linearVelocity,
            transform.right);
        float stability = onRoad
            ? lateralStability
            : lateralStability * 0.45f;
        body.AddForce(
            -transform.right * lateralSpeed * stability,
            ForceMode.Acceleration);
    }

    private void UpdateWheelVisuals()
    {
        foreach (RuntimeWheel wheel in wheels)
        {
            if (wheel.visual == null || wheel.collider == null)
                continue;
            wheel.collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
            wheel.visual.position = position;
            wheel.visual.rotation = rotation * wheel.visualRotationOffset;
            if (wheel.exteriorVisual != null)
            {
                wheel.exteriorVisual.position = position;
                wheel.exteriorVisual.rotation = rotation
                                              * wheel.exteriorRotationOffset;
            }
        }
    }

    private void ResolveCockpitVisuals()
    {
        Transform cockpit = transform.Find("Cockpit Interior");
        if (cockpit == null)
            return;

        cockpitSteeringWheel = null;
        cockpitSpeedNeedle = null;
        foreach (Transform child in
                 cockpit.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "Steering_wheel")
            {
                cockpitSteeringWheel = child;
                cockpitSteeringRotationAxis = Vector3.up;
            }
            else if (cockpitSteeringWheel == null
                     && child.name == "Steering Wheel Pivot")
            {
                cockpitSteeringWheel = child;
                cockpitSteeringRotationAxis = Vector3.forward;
            }
            if (child.name == "Speed Needle Pivot")
                cockpitSpeedNeedle = child;
        }
        if (cockpitSteeringWheel != null)
            cockpitSteeringRestRotation = cockpitSteeringWheel.localRotation;
        if (cockpitSpeedNeedle != null)
            cockpitNeedleRestRotation = cockpitSpeedNeedle.localRotation;
    }

    private void UpdateCockpitVisuals()
    {
        if (cockpitSteeringWheel != null)
        {
            cockpitSteeringWheel.localRotation = cockpitSteeringRestRotation
                * Quaternion.AngleAxis(
                    SteeringVisual01 * 125f,
                    cockpitSteeringRotationAxis);
        }

        if (cockpitSpeedNeedle != null)
        {
            float speed01 = Mathf.InverseLerp(
                0f,
                roadTopSpeedKph,
                SpeedKph);
            float angle = Mathf.Lerp(115f, -115f, speed01);
            cockpitSpeedNeedle.localRotation = cockpitNeedleRestRotation
                * Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }

    private void ReadInput()
    {
        throttleInput = 0f;
        steeringInput = 0f;
        handbrakeInput = false;

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
            handbrakeInput = keyboard.spaceKey.isPressed;
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
            handbrakeInput |= gamepad.buttonSouth.isPressed;
        }
    }

    [ContextMenu("Reset Van")]
    public void ResetVan()
    {
        body.position = respawnPosition + Vector3.up * 0.45f;
        body.rotation = respawnRotation;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        smoothedSteering = 0f;
        body.WakeUp();
    }

    private void UpdateRecoveryCheckpoint()
    {
        if (!grounded
            || !onRoad
            || Vector3.Dot(transform.up, Vector3.up) < 0.65f)
        {
            checkpointTimer = 0f;
            return;
        }

        checkpointTimer += Time.fixedDeltaTime;
        if (checkpointTimer < checkpointInterval)
            return;
        checkpointTimer = 0f;
        respawnPosition = body.position;
        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (flatForward.sqrMagnitude > 0.001f)
            respawnRotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
    }

    private void OnGUI()
    {
        if (!showPrototypeHud || body == null)
            return;

        GUILayout.BeginArea(new Rect(18f, 18f, 320f, 198f), GUI.skin.box);
        GUILayout.Label($"SPEED  {SpeedKph:0} km/h");
        GUILayout.Label(
            $"{(grounded ? "GROUNDED" : "AIRBORNE")}  •  "
            + (onRoad ? "Road" : "Grass"));
        GUILayout.Label($"Throttle {throttleInput:0.0}  •  Steering {steeringInput:0.0}");
        GUILayout.Label("Physics: 4 wheels • suspension • slip");
        GUILayout.Label("WASD / Arrows: Drive");
        GUILayout.Label("Space: Handbrake    R: Reset");
        GUILayout.Label("V: Radio    Z / X: Previous / Next track");
        GUILayout.Label("C: Camera    3P right click / 1P mouse: Look");
        GUILayout.EndArea();
    }
}
