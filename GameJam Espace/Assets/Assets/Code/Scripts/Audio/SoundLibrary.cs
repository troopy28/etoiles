using System;
using UnityEngine;
using UnityEngine.Audio;

// Single source of truth for all game sounds. One named field per sound,
// grouped by usage. An empty entry (clips array empty/null) is treated as
// "not yet bound" — every API call becomes a silent no-op for it.
//
// Use from code via the entry's own helpers:
//   lib.m_target_lock.Play2D();
//   lib.m_supernova_explosion.Play3D(worldPos);
//   lib.m_fuel_low_alarm.StartLoop2D();
[CreateAssetMenu(fileName = "SoundLibrary", menuName = "Audio/Sound Library", order = 0)]
public class SoundLibrary : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        public Vector2 pitchRange = new Vector2(1f, 1f);
        public AudioMixerGroup group;

        public bool HasClip => clips != null && clips.Length > 0 && clips[0] != null;

        public AudioClip GetLoopClip()
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[0];
        }

        public AudioClip PickClip()
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[UnityEngine.Random.Range(0, clips.Length)];
        }

        public float PickPitch()
        {
            return (pitchRange.x == pitchRange.y)
                ? pitchRange.x
                : UnityEngine.Random.Range(pitchRange.x, pitchRange.y);
        }

        // -------- Playback helpers --------
        // All routes through AudioManager.Instance, so they're no-ops if the
        // manager isn't in the scene yet (e.g., editor-time inspector clicks).

        public void Play2D(float volumeScale = 1f) =>
            AudioManager.Instance?.Play2D(this, volumeScale);

        public void Play3D(Vector3 worldPos, float volumeScale = 1f,
                           float? minDistance = null, float? maxDistance = null) =>
            AudioManager.Instance?.Play3D(this, worldPos, volumeScale, minDistance, maxDistance);

        public void StartLoop2D(float volumeScale = 1f) =>
            AudioManager.Instance?.StartLoop2D(this, volumeScale);

        public void StopLoop2D() =>
            AudioManager.Instance?.StopLoop2D(this);

        public void SetLoopPitch(float pitch) =>
            AudioManager.Instance?.SetLoopPitch(this, pitch);

        public void SetLoopVolume(float volume) =>
            AudioManager.Instance?.SetLoopVolume(this, volume);

        public bool IsLoopPlaying() =>
            AudioManager.Instance != null && AudioManager.Instance.IsLoopPlaying(this);

        public void PlayMusic() =>
            AudioManager.Instance?.PlayMusic(this);
    }

    [Header("Engines")]
    public Entry m_engine_main_loop;
    public Entry m_engine_rcs_loop;
    public Entry m_engine_empty;
    public Entry m_boost_start;

    [Header("Alarms")]
    public Entry m_fuel_low_alarm;
    public Entry m_supernova_proximity_alarm;

    [Header("Stellar")]
    public Entry m_supernova_explosion;
    public Entry m_star_drone_o;
    public Entry m_star_drone_b;
    public Entry m_star_drone_a;
    public Entry m_star_drone_f;

    [Header("UI / Targeting")]
    public Entry m_target_lock;
    public Entry m_target_unlock;
    public Entry m_target_hover;
    public Entry m_autopilot_engage;
    public Entry m_autopilot_disengage;
    public Entry m_refuel_hum;
    public Entry m_cut_throttle;
    public Entry m_trajectory_scan;

    [Header("Music")]
    public Entry m_ambient_music;
}
