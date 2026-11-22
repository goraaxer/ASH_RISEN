using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class WorldAmbientBuilder
{
    const string Folder = "Assets/Art/WorldAmbient";
    const int Rate = 22050;
    const float Length = 24f;

    [MenuItem("Tools/Ashrisen/Audio/Build world ambience")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        Write("GraveyardWind", false);
        Write("VillageWind", true);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string name in new[] { "GraveyardWind", "VillageWind" })
        {
            string path = Folder + "/" + name + ".wav";
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null)
                throw new InvalidOperationException("Could not import ambience: " + path);
        }
        Debug.Log("Built two quiet, looping world ambience tracks.");
    }

    static void Write(string name, bool village)
    {
        int count = Mathf.RoundToInt(Length * Rate);
        var random = new System.Random(village ? 85427 : 54131);
        var samples = new short[count];
        float low = 0f, mid = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)Rate;
            float phase = t / Length * 2f * Mathf.PI;
            float noise = (float)random.NextDouble() * 2f - 1f;
            low = low * .996f + noise * .004f;
            mid = mid * .87f + noise * .13f;
            float gust = .54f + .2f * Mathf.Sin(phase * 3f + .4f) +
                .13f * Mathf.Sin(phase * 5f + 1.7f);
            float fade = Mathf.Min(1f, t * 2f, (Length - t) * 2f);
            float wind = (low * 2.2f + mid * .18f) * gust * fade;
            float rumble = .016f * Mathf.Sin(2f * Mathf.PI * 52f * t) +
                .009f * Mathf.Sin(2f * Mathf.PI * 78f * t);
            float sound = wind + rumble;
            if (village)
            {
                sound *= .78f;
                sound += DistantBell(t - 7.3f) + DistantBell(t - 17.8f);
            }
            else
            {
                float hollow = Mathf.Sin(2f * Mathf.PI * 39f * t) *
                    (.012f + .008f * Mathf.Sin(phase * 2f));
                sound += hollow;
            }
            samples[i] = (short)Mathf.RoundToInt(Mathf.Clamp(sound, -.9f, .9f) * 32767f);
        }
        using var stream = new FileStream(Folder + "/" + name + ".wav", FileMode.Create);
        using var writer = new BinaryWriter(stream);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + count * 2);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16); writer.Write((short)1); writer.Write((short)1);
        writer.Write(Rate); writer.Write(Rate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(count * 2);
        foreach (short sample in samples) writer.Write(sample);
    }

    static float DistantBell(float seconds)
    {
        if (seconds < 0f || seconds > 2.8f) return 0f;
        float fade = Mathf.Exp(-2.1f * seconds);
        return fade * (.035f * Mathf.Sin(2f * Mathf.PI * 233f * seconds) +
            .014f * Mathf.Sin(2f * Mathf.PI * 352f * seconds));
    }
}
