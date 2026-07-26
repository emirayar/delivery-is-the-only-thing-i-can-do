using UnityEngine;

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

    private Vector3 positionVelocity;
    private Camera attachedCamera;
    private ArcadeDeliveryVan van;
    private float baseFieldOfView;

    private void Awake()
    {
        attachedCamera = GetComponent<Camera>();
        baseFieldOfView = attachedCamera.fieldOfView;
        ResolveVan();
        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            ResolveVan();
            if (target == null)
                return;
        }

        Vector3 flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.forward;
        flatForward.Normalize();

        Vector3 desiredPosition = target.position
                                - flatForward * followDistance
                                + Vector3.up * followHeight;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime);

        Vector3 lookTarget = target.position
                           + flatForward * lookAhead
                           + Vector3.up * 0.75f;
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
            float speed01 = van != null
                ? Mathf.InverseLerp(0f, van.roadTopSpeedKph, van.SpeedKph)
                : 0f;
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
    }
}
