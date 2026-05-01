using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// Central audio entry point. Singleton, persists across scene loads.
//
// API takes SoundLibrary.Entry directly — call sites read like
//     lib.m_target_lock.Play2D()
// Pool sources are reused across one-shots; loops are tracked by Entry reference.
//
// Tolerates null Entry / empty clips: every call becomes a silent no-op.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("References")]
    public SoundLibrary m_library;
    public AudioMixer m_mixer;
    public AudioMixerGroup m_music_group;

    [Header("Pool sizing")]
    public int m_pool_2d_size = 8;
    public int m_pool_3d_size = 6;

    [Header("3D defaults (overridable per call)")]
    // World scale matters: bodies are routinely 50k-100k+ units apart.
    // Per-call min/max overrides exist for tighter SFX (e.g. proximity-only events).
    public float m_3d_min_distance = 1000f;
    public float m_3d_max_distance = 50000f;
    // Gentle falloff: holds near-full volume for the first half of the range,
    // then tapers. Way less aggressive than the default EaseInOut S-curve.
    public AnimationCurve m_3d_rolloff = new AnimationCurve(
        new Keyframe(0f,    1.00f),
        new Keyframe(0.4f,  0.90f),
        new Keyframe(0.75f, 0.50f),
        new Keyframe(1.0f,  0.00f));
    // Doppler is a silent killer in a space sim: ship velocities (thousands of
    // u/s) vs Unity's default speed of sound (343 u/s) shift the pitch right out
    // of the audible band. Disabled on the pool — re-enable per call if needed.
    [Range(0f, 1f)] public float m_3d_doppler_level = 0f;

    [Header("Music")]
    public bool m_play_music_on_start = true;
    public float m_music_volume = 0.6f;

    private AudioSource[] m_pool_2d;
    private int m_pool_2d_cursor;

    private AudioSource[] m_pool_3d;
    private int m_pool_3d_cursor;

    private AudioSource m_music_source;

    // Active looped sounds, keyed by Entry reference (the SoundLibrary asset is a singleton).
    private readonly Dictionary<SoundLibrary.Entry, AudioSource> m_active_loops =
        new Dictionary<SoundLibrary.Entry, AudioSource>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildPool2D();
        BuildPool3D();
        BuildMusicSource();
    }

    void Start()
    {
        if (m_play_music_on_start && m_library != null)
            m_library.m_ambient_music?.PlayMusic();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void BuildPool2D()
    {
        m_pool_2d = new AudioSource[Mathf.Max(1, m_pool_2d_size)];
        for (int i = 0; i < m_pool_2d.Length; i++)
        {
            var go = new GameObject($"SFX2D_{i}");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.loop = false;
            m_pool_2d[i] = src;
        }
    }

    void BuildPool3D()
    {
        m_pool_3d = new AudioSource[Mathf.Max(1, m_pool_3d_size)];
        for (int i = 0; i < m_pool_3d.Length; i++)
        {
            var go = new GameObject($"SFX3D_{i}");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.dopplerLevel = m_3d_doppler_level;
            src.rolloffMode = AudioRolloffMode.Custom;
            src.minDistance = m_3d_min_distance;
            src.maxDistance = m_3d_max_distance;
            src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, m_3d_rolloff);
            src.loop = false;
            m_pool_3d[i] = src;
        }
    }

    void BuildMusicSource()
    {
        var go = new GameObject("Music");
        go.transform.SetParent(transform, false);
        m_music_source = go.AddComponent<AudioSource>();
        m_music_source.playOnAwake = false;
        m_music_source.spatialBlend = 0f;
        m_music_source.loop = true;
        m_music_source.volume = m_music_volume;
        if (m_music_group != null) m_music_source.outputAudioMixerGroup = m_music_group;
    }

    // -------- Public API (typically called via SoundLibrary.Entry helpers) --------

    public void Play2D(SoundLibrary.Entry e, float volumeScale = 1f)
    {
        var clip = e?.PickClip();
        if (clip == null) return;
        var src = NextFromPool(m_pool_2d, ref m_pool_2d_cursor);
        if (src == null) return;
        ConfigureSource(src, e);
        src.PlayOneShot(clip, e.volume * volumeScale);
    }

    public void Play3D(SoundLibrary.Entry e, Vector3 worldPos, float volumeScale = 1f,
                       float? minDistance = null, float? maxDistance = null)
    {
        if (e == null) { Debug.LogWarning("[AudioManager] Play3D: entry == NULL"); return; }
        var clip = e.PickClip();
        if (clip == null) { Debug.LogWarning("[AudioManager] Play3D: PickClip() returned NULL — no clip bound"); return; }
        var src = NextFromPool(m_pool_3d, ref m_pool_3d_cursor);
        if (src == null) { Debug.LogWarning("[AudioManager] Play3D: pool empty"); return; }

        src.transform.position = worldPos;
        ConfigureSource(src, e);
        src.minDistance = minDistance ?? m_3d_min_distance;
        src.maxDistance = maxDistance ?? m_3d_max_distance;
        src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, m_3d_rolloff);
        float final_volume = e.volume * volumeScale;
        src.PlayOneShot(clip, final_volume);

        var listener = Camera.main;
        float listener_dist = listener != null ? Vector3.Distance(worldPos, listener.transform.position) : -1f;
        Debug.Log($"[AudioManager] Play3D: clip='{clip.name}' source_pos={worldPos} " +
                  $"listener_dist={listener_dist:F0} src.min={src.minDistance} src.max={src.maxDistance} " +
                  $"PlayOneShot_volume={final_volume:F2} src.dopplerLevel={src.dopplerLevel:F2} " +
                  $"src.spatialBlend={src.spatialBlend:F2} group='{(src.outputAudioMixerGroup != null ? src.outputAudioMixerGroup.name : "<none>")}'");
    }

    public void StartLoop2D(SoundLibrary.Entry e, float volumeScale = 1f)
    {
        if (e == null) return;
        var clip = e.PickClip();
        if (clip == null) return;

        if (!m_active_loops.TryGetValue(e, out var src) || src == null)
        {
            var go = new GameObject("Loop2D");
            go.transform.SetParent(transform, false);
            src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.loop = true;
            m_active_loops[e] = src;
        }
        if (src.isPlaying && src.clip == clip)
        {
            src.volume = e.volume * volumeScale;
            return;
        }
        ConfigureSource(src, e);
        src.clip = clip;
        src.volume = e.volume * volumeScale;
        src.Play();
    }

    public void StopLoop2D(SoundLibrary.Entry e)
    {
        if (e == null) return;
        if (m_active_loops.TryGetValue(e, out var src) && src != null && src.isPlaying)
            src.Stop();
    }

    public bool IsLoopPlaying(SoundLibrary.Entry e)
    {
        return e != null
            && m_active_loops.TryGetValue(e, out var src)
            && src != null && src.isPlaying;
    }

    public void SetLoopPitch(SoundLibrary.Entry e, float pitch)
    {
        if (e == null) return;
        if (m_active_loops.TryGetValue(e, out var src) && src != null)
            src.pitch = pitch;
    }

    public void SetLoopVolume(SoundLibrary.Entry e, float volume)
    {
        if (e == null) return;
        if (m_active_loops.TryGetValue(e, out var src) && src != null)
            src.volume = volume;
    }

    public void PlayMusic(SoundLibrary.Entry e)
    {
        var clip = e?.PickClip();
        if (clip == null || m_music_source == null) return;
        if (m_music_source.clip == clip && m_music_source.isPlaying) return;
        m_music_source.clip = clip;
        m_music_source.outputAudioMixerGroup =
            (e.group != null) ? e.group : m_music_group;
        m_music_source.volume = m_music_volume * e.volume;
        m_music_source.Play();
    }

    public void StopMusic()
    {
        if (m_music_source != null && m_music_source.isPlaying) m_music_source.Stop();
    }

    // -------- Mixer helpers --------

    // Linear 0..1 slider → dB (-80..0). 0 mutes (-80 dB), 1 = unity gain.
    public void SetMixerVolume(string exposedParam, float linear01)
    {
        if (m_mixer == null) return;
        float db = linear01 > 0.0001f ? Mathf.Log10(linear01) * 20f : -80f;
        m_mixer.SetFloat(exposedParam, db);
    }

    // -------- Internals --------

    static void ConfigureSource(AudioSource src, SoundLibrary.Entry e)
    {
        src.outputAudioMixerGroup = e.group;
        src.pitch = e.PickPitch();
    }

    static AudioSource NextFromPool(AudioSource[] pool, ref int cursor)
    {
        if (pool == null || pool.Length == 0) return null;
        for (int i = 0; i < pool.Length; i++)
        {
            int idx = (cursor + i) % pool.Length;
            if (!pool[idx].isPlaying)
            {
                cursor = (idx + 1) % pool.Length;
                return pool[idx];
            }
        }
        var s = pool[cursor];
        cursor = (cursor + 1) % pool.Length;
        return s;
    }
}
