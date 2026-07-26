using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ArcadeDeliveryVan))]
public sealed class VehicleEngineAudio : MonoBehaviour
{
    [Header("Clips")]
    [Tooltip("Arac calisirken bir kere oynatilan mars sesi.")]
    public AudioClip engineStartClip;

    [Tooltip("Arac calisirken surekli tekrar eden motor sesi.")]
    public AudioClip engineLoopClip;

    [Header("Response")]
    [Tooltip("Arac dururken motor sesinin pitch degeri.")]
    [Range(0.4f, 1.2f)] public float idlePitch = 0.68f;

    [Tooltip("Arac maksimum hiza yaklastiginda motor sesinin pitch degeri.")]
    [Range(1f, 2.5f)] public float maximumPitch = 1.55f;

    [Tooltip("Rölantideki motor ses seviyesi.")]
    [Range(0f, 1f)] public float idleVolume = 0.18f;

    [Tooltip("Gaz verildiginde motor sesine eklenecek seviye.")]
    [Range(0f, 1f)] public float throttleVolume = 0.24f;

    private ArcadeDeliveryVan van;
    private AudioSource loopSource;
    private AudioSource startSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureVehicleAudioExists()
    {
        ArcadeDeliveryVan activeVan = FindAnyObjectByType<ArcadeDeliveryVan>();
        if (activeVan == null)
            return;
        VehicleEngineAudio audio = activeVan.GetComponent<VehicleEngineAudio>();
        if (audio == null)
            audio = activeVan.gameObject.AddComponent<VehicleEngineAudio>();
        if (audio.engineStartClip == null)
            audio.engineStartClip = Resources.Load<AudioClip>("Audio/Vehicle/Engine_Start_CC0");
        if (audio.engineLoopClip == null)
            audio.engineLoopClip = Resources.Load<AudioClip>("Audio/Vehicle/Engine_Loop_CC0");
    }

    private void Awake()
    {
        van = GetComponent<ArcadeDeliveryVan>();
        if (engineStartClip == null)
            engineStartClip = Resources.Load<AudioClip>("Audio/Vehicle/Engine_Start_CC0");
        if (engineLoopClip == null)
            engineLoopClip = Resources.Load<AudioClip>("Audio/Vehicle/Engine_Loop_CC0");
        loopSource = CreateSource("Engine Loop Audio", 0.35f);
        startSource = CreateSource("Engine Start Audio", 0.35f);
        loopSource.clip = engineLoopClip;
        loopSource.loop = true;
    }

    private void Start()
    {
        if (engineStartClip != null)
        {
            startSource.clip = engineStartClip;
            startSource.volume = 0.48f;
            startSource.Play();
        }

        if (engineLoopClip != null)
            loopSource.Play();
    }

    private void Update()
    {
        if (loopSource == null || van == null)
            return;

        float speed01 = Mathf.InverseLerp(0f, van.roadTopSpeedKph, van.SpeedKph);
        float throttle01 = Mathf.Abs(van.ThrottleInput);
        float targetPitch = Mathf.Lerp(idlePitch, maximumPitch, Mathf.Sqrt(speed01));
        targetPitch += throttle01 * 0.12f;
        loopSource.pitch = Mathf.Lerp(loopSource.pitch, targetPitch, Time.deltaTime * 5f);
        loopSource.volume = Mathf.Lerp(
            loopSource.volume,
            idleVolume + throttle01 * throttleVolume,
            Time.deltaTime * 7f);
    }

    private AudioSource CreateSource(string objectName, float spatialBlend)
    {
        Transform existing = transform.Find(objectName);
        GameObject holder = existing != null ? existing.gameObject : new GameObject(objectName);
        holder.transform.SetParent(transform, false);
        AudioSource source = holder.GetComponent<AudioSource>();
        if (source == null)
            source = holder.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = spatialBlend;
        source.dopplerLevel = 0.15f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 3f;
        source.maxDistance = 55f;
        return source;
    }
}
