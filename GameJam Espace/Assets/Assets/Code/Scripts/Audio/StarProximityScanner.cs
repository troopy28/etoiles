using System.Collections.Generic;
using UnityEngine;
using Assets.Code.Scripts.Generation;

// Constant-cost proximity audio for stars. The procedural galaxy can hold
// thousands of stars; we never put an AudioSource per star. Instead we keep
// a fixed pool of K 3D AudioSources and reassign them to the K nearest stars
// at a low frequency (~5 Hz). Sources crossfade their volume when their
// assigned star changes.
//
// Drop on the same GameObject as AudioManager (or anywhere in the scene).
public class StarProximityScanner : MonoBehaviour
{
    [Header("Pool")]
    public int m_pool_size = 5;
    public float m_scan_interval = 0.2f;       // seconds between top-K refresh

    [Header("Listener (defaults to Camera.main)")]
    public Transform m_listener;

    [Header("3D rolloff (calibrated for stellar distances)")]
    public float m_min_distance = 200f;        // full volume below this
    public float m_max_distance = 6000f;       // silent past this
    [Range(0f, 1f)] public float m_spatial_blend = 1f;
    public float m_base_volume = 0.5f;
    public float m_volume_smoothing = 4f;      // higher = faster fade in/out

    private class Slot
    {
        public AudioSource src;
        public SimGravityBody body;            // currently assigned star (or null = free)
        public StellarClass current_class;
        public float current_volume;           // smoothed
    }
    private Slot[] m_slots;

    private float m_scan_timer;
    private readonly List<(SimGravityBody body, float distSq)> m_candidates =
        new List<(SimGravityBody, float)>(64);

    void Awake()
    {
        m_slots = new Slot[Mathf.Max(1, m_pool_size)];
        for (int i = 0; i < m_slots.Length; i++)
        {
            var go = new GameObject($"StarDrone_{i}");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = m_spatial_blend;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = m_min_distance;
            src.maxDistance = m_max_distance;
            src.volume = 0f;
            m_slots[i] = new Slot { src = src };
        }
    }

    void Start()
    {
        if (m_listener == null && Camera.main != null) m_listener = Camera.main.transform;
        Rescan(true);
    }

    void Update()
    {
        m_scan_timer += Time.deltaTime;
        if (m_scan_timer >= m_scan_interval)
        {
            m_scan_timer = 0f;
            Rescan(false);
        }
        FadeVolumes();
    }

    void Rescan(bool snap)
    {
        if (m_listener == null && Camera.main != null) m_listener = Camera.main.transform;
        if (m_listener == null) return;

        Vector3 lp = m_listener.position;
        float maxSq = m_max_distance * m_max_distance;

        m_candidates.Clear();
        var all = SimGravityBody.AllRegistered;
        for (int i = 0; i < all.Count; i++)
        {
            var b = all[i];
            if (b == null || b.kind != BodyKind.Star) continue;
            float dSq = (b.transform.position - lp).sqrMagnitude;
            if (dSq > maxSq) continue;
            m_candidates.Add((b, dSq));
        }

        // Partial sort: full Sort is fine for typical K (a few hundred candidates max
        // after the maxSq cull). If the pool size grows or candidates explode, swap
        // for a heap-based top-K.
        m_candidates.Sort((a, b) => a.distSq.CompareTo(b.distSq));

        int k = Mathf.Min(m_slots.Length, m_candidates.Count);

        // Free slots whose star is no longer in the top K.
        for (int i = 0; i < m_slots.Length; i++)
        {
            var slot = m_slots[i];
            if (slot.body == null) continue;
            bool still_topK = false;
            for (int j = 0; j < k; j++)
            {
                if (m_candidates[j].body == slot.body) { still_topK = true; break; }
            }
            if (!still_topK) slot.body = null;
        }

        // Assign top-K candidates to free slots, preferring slots that already host them.
        for (int j = 0; j < k; j++)
        {
            var cand = m_candidates[j].body;
            bool already_assigned = false;
            for (int i = 0; i < m_slots.Length; i++)
            {
                if (m_slots[i].body == cand) { already_assigned = true; break; }
            }
            if (already_assigned) continue;

            // Find a free slot.
            for (int i = 0; i < m_slots.Length; i++)
            {
                if (m_slots[i].body == null)
                {
                    AssignToSlot(m_slots[i], cand, snap);
                    break;
                }
            }
        }

        // Reposition assigned slots' AudioSources on each rescan (stars drift).
        for (int i = 0; i < m_slots.Length; i++)
        {
            var slot = m_slots[i];
            if (slot.body != null)
                slot.src.transform.position = slot.body.transform.position;
        }
    }

    void AssignToSlot(Slot slot, SimGravityBody star, bool snap)
    {
        slot.body = star;
        slot.current_class = star.spectral_class;
        slot.src.transform.position = star.transform.position;

        var entry = ClassToEntry(star.spectral_class, AudioManager.Instance?.m_library);
        if (entry != null)
        {
            if (entry.group != null) slot.src.outputAudioMixerGroup = entry.group;
            var clip = entry.PickClip();
            if (clip != null && slot.src.clip != clip)
            {
                slot.src.clip = clip;
                slot.src.Play();
            }
            else if (clip != null && !slot.src.isPlaying)
            {
                slot.src.Play();
            }
        }
        if (snap) { slot.current_volume = 0f; slot.src.volume = 0f; }
    }

    void FadeVolumes()
    {
        float k = 1f - Mathf.Exp(-m_volume_smoothing * Time.deltaTime);
        for (int i = 0; i < m_slots.Length; i++)
        {
            var slot = m_slots[i];
            float target = (slot.body != null) ? m_base_volume : 0f;
            slot.current_volume = Mathf.Lerp(slot.current_volume, target, k);
            slot.src.volume = slot.current_volume;
        }
    }

    static SoundLibrary.Entry ClassToEntry(StellarClass c, SoundLibrary lib)
    {
        if (lib == null) return null;
        switch (c)
        {
            case StellarClass.O: return lib.m_star_drone_o;
            case StellarClass.B: return lib.m_star_drone_b;
            case StellarClass.A: return lib.m_star_drone_a;
            // Cooler stars and remnants fall back to the calmest available drone.
            default: return lib.m_star_drone_f;
        }
    }
}
