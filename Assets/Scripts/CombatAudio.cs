using System.Collections.Generic;
using UnityEngine;

public enum CombatCue
{
    SwordSwing, SwordImpact, PlayerHurt, PlayerDeath,
    EnemyHurt, EnemyDeath, WardenSwing, HoundBite,
    BoltShot, BoltImpact, BossHurt, BossDeath, BossSwing, PlayerDash,
    ProjectileParry
}

public static class CombatAudio
{
    static readonly Dictionary<CombatCue, AudioClip> clips = new Dictionary<CombatCue, AudioClip>();
    static readonly HashSet<CombatCue> missing = new HashSet<CombatCue>();
    static AudioSource source;
    public static int PlayCount { get; private set; }
    public static CombatCue LastCue { get; private set; }

    public static bool Play(CombatCue cue, float volume = .5f)
    {
        if (!Application.isPlaying) return false;
        if (!clips.TryGetValue(cue, out var clip) || clip == null)
        {
            clip = Resources.Load<AudioClip>("CombatAudio/" + cue);
            clips[cue] = clip;
        }
        if (clip == null)
        {
            if (missing.Add(cue)) Debug.LogWarning("Missing combat sound: " + cue);
            return false;
        }
        if (source == null)
        {
            var host = new GameObject("Combat sound effects");
            Object.DontDestroyOnLoad(host);
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
        LastCue = cue;
        PlayCount++;
        return true;
    }
}
