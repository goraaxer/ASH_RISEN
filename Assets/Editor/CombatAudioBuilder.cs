using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CombatAudioBuilder
{
    const string Folder = "Assets/Resources/CombatAudio";
    const int Rate = 22050;

    [MenuItem("Tools/Ashrisen/Combat/Build sound effects")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        foreach (CombatCue cue in Enum.GetValues(typeof(CombatCue)))
            Write(cue);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (CombatCue cue in Enum.GetValues(typeof(CombatCue)))
        {
            string path = Folder + "/" + cue + ".wav";
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null)
                throw new InvalidOperationException("Could not import sound: " + path);
        }
        Debug.Log("Built " + Enum.GetValues(typeof(CombatCue)).Length + " combat sound effects.");
    }

    static float Length(CombatCue cue) => cue switch
    {
        CombatCue.PlayerDeath => 1.3f,
        CombatCue.BossDeath => 1.4f,
        CombatCue.EnemyDeath => .62f,
        CombatCue.BossHurt => .32f,
        CombatCue.PlayerHurt => .33f,
        CombatCue.EnemyHurt => .25f,
        CombatCue.WardenSwing => .3f,
        CombatCue.BossSwing => .36f,
        CombatCue.HoundBite => .22f,
        CombatCue.BoltShot => .22f,
        CombatCue.BoltImpact => .16f,
        CombatCue.SwordSwing => .27f,
        CombatCue.SwordImpact => .24f,
        CombatCue.PlayerDash => .3f,
        CombatCue.ProjectileParry => .18f,
        _ => .18f
    };

    static void Write(CombatCue cue)
    {
        int count = Mathf.RoundToInt(Length(cue) * Rate);
        var samples = new short[count];
        var random = new System.Random(7001 + (int)cue * 917);
        float filtered = 0f;
        float ember = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)Rate;
            float u = i / (float)(count - 1);
            float noise = (float)random.NextDouble() * 2f - 1f;
            filtered = filtered * .64f + noise * .36f;
            ember *= .91f;
            if ((cue == CombatCue.PlayerDash || cue == CombatCue.PlayerDeath) &&
                random.NextDouble() < .0025 * (1f - u))
                ember = .35f + (float)random.NextDouble() * .35f;
            float attack = Mathf.Min(1f, t * 180f);
            float tail = Mathf.Pow(1f - u, 1.5f);
            float sound = cue switch
            {
                CombatCue.SwordSwing => (filtered * .43f +
                    (noise - filtered) * .18f +
                    Mathf.Sin(2f * Mathf.PI * 92f * t) * .025f) *
                    Mathf.Pow(Mathf.Sin(Mathf.PI * u), 1.2f),
                CombatCue.PlayerDash => (filtered * .21f + noise * .07f + ember * .4f) *
                    Mathf.Sin(Mathf.PI * u) * Mathf.Pow(1f - u, .4f),
                CombatCue.WardenSwing => (filtered * .52f +
                    Mathf.Sin(2f * Mathf.PI * (440f * t - 460f * t * t)) * .2f) * tail,
                CombatCue.BossSwing => (filtered * .4f +
                    Mathf.Sin(2f * Mathf.PI * (240f * t - 115f * t * t)) * .24f) * tail,
                CombatCue.HoundBite => (noise * .32f +
                    Mathf.Sin(2f * Mathf.PI * 160f * t) * .18f) * tail,
                CombatCue.SwordImpact =>
                    noise * .32f * Mathf.Exp(-90f * t) +
                    Mathf.Sin(2f * Mathf.PI * (160f * t - 55f * t * t)) *
                    .42f * Mathf.Exp(-23f * t) +
                    filtered * .14f * Mathf.Exp(-17f * t),
                CombatCue.BoltImpact =>
                    (noise * .28f + Mathf.Sin(2f * Mathf.PI * 115f * t) * .4f) *
                    Mathf.Exp(-24f * t),
                CombatCue.ProjectileParry =>
                    noise * .3f * Mathf.Exp(-70f * t) +
                    Mathf.Sin(2f * Mathf.PI * (850f * t - 530f * t * t)) *
                    .32f * Mathf.Exp(-27f * t),
                CombatCue.BoltShot => (noise * .16f * Mathf.Exp(-30f * t) +
                    Mathf.Sin(2f * Mathf.PI * (530f * t - 350f * t * t)) *
                    Mathf.Exp(-13f * t) * .33f),
                CombatCue.PlayerHurt => (filtered * .17f +
                    Mathf.Sin(2f * Mathf.PI * (270f * t - 120f * t * t)) * .25f) * tail,
                CombatCue.EnemyHurt => (filtered * .24f +
                    Mathf.Sin(2f * Mathf.PI * (190f * t - 100f * t * t)) * .24f) * tail,
                CombatCue.BossHurt => (filtered * .22f +
                    Mathf.Sin(2f * Mathf.PI * (105f * t - 26f * t * t)) * .3f) * tail,
                CombatCue.PlayerDeath =>
                    (filtered * .19f + noise * .06f + ember * .24f) *
                    Mathf.Exp(-2.3f * t),
                CombatCue.EnemyDeath or CombatCue.BossDeath =>
                    (Mathf.Sin(2f * Mathf.PI * (150f * t - 36f * t * t)) * .28f +
                    Mathf.Sin(2f * Mathf.PI * (223f * t - 12f * t * t)) * .14f +
                    filtered * .1f) * Mathf.Exp(-3.2f * t),
                _ => 0f
            };
            sound *= attack * Mathf.Min(1f, (count - i) / (Rate * .025f));
            samples[i] = (short)Mathf.RoundToInt(Mathf.Clamp(sound, -.95f, .95f) * 32767f);
        }
        string path = Folder + "/" + cue + ".wav";
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);
        int dataBytes = count * 2;
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataBytes);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16); writer.Write((short)1); writer.Write((short)1);
        writer.Write(Rate); writer.Write(Rate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataBytes);
        foreach (short sample in samples) writer.Write(sample);
    }
}
