using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// The source PNG remains untouched. Every rig pixel is copied verbatim from it.
public static class BossPixelRigBuilder
{
    const string SourcePath = "Assets/sprites/boss/bossmodel.png";
    const string Root = "Assets/sprites/boss/Dynamic";
    static readonly string[] Names = { "Torso", "Head", "NearForeleg", "FarForeleg", "NearHindleg", "FarHindleg" };
    static readonly Color32[] PreviewColors = {
        new Color32(74, 76, 88, 255), new Color32(197, 111, 93, 255),
        new Color32(185, 153, 98, 255), new Color32(113, 162, 147, 255),
        new Color32(130, 122, 184, 255), new Color32(171, 119, 157, 255)
    };
    static readonly Vector2Int[][] Polygons = {
        new[] { new Vector2Int(2, 41), new Vector2Int(4, 57), new Vector2Int(10, 68), new Vector2Int(18, 76), new Vector2Int(27, 75), new Vector2Int(32, 63), new Vector2Int(29, 52), new Vector2Int(22, 44), new Vector2Int(13, 41) },
        new[] { new Vector2Int(17, 21), new Vector2Int(29, 21), new Vector2Int(33, 27), new Vector2Int(32, 41), new Vector2Int(34, 55), new Vector2Int(29, 65), new Vector2Int(22, 63), new Vector2Int(19, 52), new Vector2Int(14, 35) },
        new[] { new Vector2Int(2, 20), new Vector2Int(12, 20), new Vector2Int(18, 29), new Vector2Int(21, 47), new Vector2Int(18, 55), new Vector2Int(11, 57), new Vector2Int(8, 48), new Vector2Int(9, 34) },
        new[] { new Vector2Int(45, 20), new Vector2Int(61, 20), new Vector2Int(61, 39), new Vector2Int(58, 54), new Vector2Int(50, 61), new Vector2Int(43, 54), new Vector2Int(45, 42) },
        new[] { new Vector2Int(31, 20), new Vector2Int(43, 20), new Vector2Int(48, 31), new Vector2Int(46, 43), new Vector2Int(39, 51), new Vector2Int(34, 44) }
    };

    [MenuItem("Tools/Ashrisen/Boss/Extract Original Pixel Rig")]
    public static void Extract()
    {
        Directory.CreateDirectory(Root + "/Parts");
        Directory.CreateDirectory("Artifacts/BossDynamicWorking");
        var source = Load(SourcePath);
        if (source.width != 64 || source.height != 128)
            throw new InvalidOperationException("Expected the original 64x128 church monster source.");
        var pixels = source.GetPixels32();
        var layers = new Color32[Names.Length][];
        for (int i = 0; i < layers.Length; i++) layers[i] = new Color32[pixels.Length];
        var preview = new Color32[pixels.Length];
        int opaque = 0;
        for (int y = 0; y < 128; y++)
        for (int x = 0; x < 64; x++)
        {
            int idx = y * 64 + x;
            Color32 original = pixels[idx];
            if (original.a == 0) continue;
            opaque++;
            int part = Classify(x, y);
            layers[part][idx] = original;
            preview[idx] = PreviewColors[part];
        }
        int copied = 0, mismatches = 0;
        for (int idx = 0; idx < pixels.Length; idx++)
        {
            Color32 found = default;
            for (int part = 0; part < layers.Length; part++)
                if (layers[part][idx].a != 0) { copied++; found = layers[part][idx]; }
            if (!found.Equals(pixels[idx])) mismatches++;
        }
        for (int i = 0; i < layers.Length; i++)
            SavePixels(Root + "/Parts/" + Names[i] + ".png", layers[i], 64, 128);
        SavePixels("Artifacts/BossDynamicWorking/segmentation.png", preview, 64, 128);
        AssetDatabase.Refresh();
        Debug.Log($"Boss source partition: {opaque} visible pixels, {copied} copied pixels, {mismatches} mismatches. No source colors changed.");
        UnityEngine.Object.DestroyImmediate(source);
    }

    static int Classify(int x, int y)
    {
        // Priority resolves occlusion in the original drawing: head, near paw, far paw, hind paws.
        for (int i = 0; i < Polygons.Length; i++)
            if (Inside(Polygons[i], x + .5f, y + .5f)) return i + 1;
        return 0;
    }
    static bool Inside(Vector2Int[] polygon, float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2Int a = polygon[i], b = polygon[j];
            if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (float)(b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }
    public static Texture2D Load(string path)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path))) throw new IOException("Cannot decode " + path);
        return texture;
    }
    public static void SavePixels(string path, Color32[] pixels, int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        texture.SetPixels32(pixels);
        texture.Apply(false);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }
}
