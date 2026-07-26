using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class RadioMusicController : MonoBehaviour
{
    [Tooltip("Radyonun chill muzik listesindeki parcalar.")]
    public AudioClip[] playlist = System.Array.Empty<AudioClip>();

    [Tooltip("Muzigi calan AudioSource. Bos birakilirsa ayni objede aranir.")]
    public AudioSource audioSource;

    [Tooltip("Oyun basladiginda ilk parcayi otomatik calar.")]
    public bool playOnStart;

    [Tooltip("Liste bittiginde basa doner.")]
    public bool loopPlaylist = true;

    [Tooltip("Her geciste farkli bir parcayi rastgele secer.")]
    public bool shufflePlaylist = true;

    [Tooltip("Parcalar arasinda gecis yaparken korunacak ses seviyesi.")]
    [Range(0f, 1f)] public float volume = 0.55f;

    private int trackIndex;
    private bool waitingForAutomaticAdvance;
    private bool pausedByPlayer;
    private double expectedTrackEndTime;

    public string CurrentTrackName => HasTracks
        ? playlist[trackIndex].name
        : "No music loaded";

    private bool HasTracks => playlist != null && playlist.Length > 0;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        AudioClip[] bundledTracks = Resources.LoadAll<AudioClip>("Audio/Radio");
        if (bundledTracks.Length > 0)
        {
            playlist = bundledTracks;
            System.Array.Sort(playlist, (left, right) =>
                string.CompareOrdinal(left.name, right.name));
            playOnStart = playlist.Length > 0;
        }
        RefreshUserVolume();
    }

    private void Start()
    {
        if (playOnStart)
        {
            if (shufflePlaylist && HasTracks)
                trackIndex = Random.Range(0, playlist.Length);
            Play();
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.vKey.wasPressedThisFrame)
                TogglePlayPause();
            if (keyboard.xKey.wasPressedThisFrame)
                NextTrack();
            if (keyboard.zKey.wasPressedThisFrame)
                PreviousTrack();
        }

        if (!waitingForAutomaticAdvance || pausedByPlayer || audioSource == null)
            return;
        if (!audioSource.isPlaying && AudioSettings.dspTime >= expectedTrackEndTime - 0.05d)
        {
            waitingForAutomaticAdvance = false;
            NextTrack();
        }
    }

    public void TogglePlayPause()
    {
        if (audioSource == null || !HasTracks)
            return;
        if (audioSource.isPlaying)
        {
            audioSource.Pause();
            pausedByPlayer = true;
        }
        else if (audioSource.clip != null)
        {
            audioSource.UnPause();
            pausedByPlayer = false;
            expectedTrackEndTime = AudioSettings.dspTime
                                 + Mathf.Max(0f, audioSource.clip.length - audioSource.time);
        }
        else
            Play();
    }

    public void Play()
    {
        if (audioSource == null || !HasTracks)
            return;
        trackIndex = Mathf.Clamp(trackIndex, 0, playlist.Length - 1);
        audioSource.clip = playlist[trackIndex];
        RefreshUserVolume();
        audioSource.Play();
        pausedByPlayer = false;
        waitingForAutomaticAdvance = true;
        expectedTrackEndTime = AudioSettings.dspTime + audioSource.clip.length;
    }

    public void NextTrack()
    {
        if (!HasTracks)
            return;
        if (!loopPlaylist && trackIndex >= playlist.Length - 1)
        {
            waitingForAutomaticAdvance = false;
            return;
        }
        if (shufflePlaylist && playlist.Length > 1)
        {
            int next = Random.Range(0, playlist.Length - 1);
            trackIndex = next >= trackIndex ? next + 1 : next;
        }
        else
        {
            trackIndex = (trackIndex + 1) % playlist.Length;
        }
        Play();
    }

    public void PreviousTrack()
    {
        if (!HasTracks)
            return;
        trackIndex = (trackIndex - 1 + playlist.Length) % playlist.Length;
        Play();
    }

    public void SetVolume(float value)
    {
        volume = Mathf.Clamp01(value);
        GameSettings.MusicVolume = volume;
        RefreshUserVolume();
    }

    public void RefreshUserVolume()
    {
        if (audioSource != null)
            audioSource.volume = GameSettings.MusicMuted
                ? 0f
                : GameSettings.MusicVolume;
    }
}
