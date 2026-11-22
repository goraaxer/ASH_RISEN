using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

public static class AshMenuAssetBuilder
{
    const string Art = "Assets/Art/Menu/";
    const string ThemePath = "Assets/Resources/AshMenuTheme.asset";

    [MenuItem("Ashrisen/Build Ash Menu")]
    public static string Build()
    {
        Directory.CreateDirectory(Art);
        Directory.CreateDirectory("Assets/Resources");
        WriteNoise();
        WriteCracks();
        WritePanel();
        WriteSounds();
        AssetDatabase.Refresh();
        ConfigureTexture("AshNoise", false, true);
        ConfigureTexture("AshCracks", true, false);
        ConfigureTexture("AshPanel", true, false);
        ConfigureEmblem();
        var font = BuildFont();
        var theme = AssetDatabase.LoadAssetAtPath<AshMenuTheme>(ThemePath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<AshMenuTheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }
        theme.font = font;
        theme.city = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Art/AshenVillage/Backgrounds/UpperCity.png");
        theme.noise = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "AshNoise.png");
        theme.cracks = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "AshCracks.png");
        theme.panel = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "AshPanel.png");
        theme.emblem = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "AshrisenEmblem.png");
        theme.heroineIdle = new Sprite[6];
        for (int i = 0; i < theme.heroineIdle.Length; i++)
            theme.heroineIdle[i] = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/sprites/idle_exhale_" + i.ToString("D2") + ".png");
        theme.open = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "MenuOpen.wav");
        theme.ember = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "MenuEmber.wav");
        theme.ash = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "MenuAsh.wav");
        theme.close = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "MenuClose.wav");
        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
        BuildTitleScene();
        return "Ash menu assets, dynamic Cyrillic TMP font, four sounds and MainMenu scene built.";
    }

    static void ConfigureTexture(string name, bool sprite, bool readable)
    {
        string path = Art + name + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16;
        importer.isReadable = readable;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.sRGBTexture = sprite;
        importer.alphaIsTransparency = sprite;
        importer.SaveAndReimport();
    }

    static void ConfigureEmblem()
    {
        string path = Art + "AshrisenEmblem.png";
        if (!File.Exists(path)) return;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    static TMP_FontAsset BuildFont()
    {
        const string path = "Assets/Resources/AshMenuFont.asset";
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (asset == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(
                "Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
            asset = TMP_FontAsset.CreateFontAsset(source, 72, 9,
                GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            if (asset == null) throw new Exception("Could not create TMP font asset");
            asset.name = "Ash Menu English";
            AssetDatabase.CreateAsset(asset, path);
            if (asset.material != null && !AssetDatabase.Contains(asset.material))
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (var atlas in asset.atlasTextures)
                if (atlas != null && !AssetDatabase.Contains(atlas))
                    AssetDatabase.AddObjectToAsset(atlas, asset);
        }
        string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
            "0123456789!?.,:;—–’%/()[]+-•› ";
        asset.TryAddCharacters(letters, out string missing);
        EditorUtility.SetDirty(asset);
        if (!string.IsNullOrEmpty(missing))
            Debug.LogWarning("Ash menu font missing glyphs: " + missing);
        return asset;
    }

    static void WriteNoise()
    {
        const int size = 128;
        var random = new System.Random(94721);
        var coarse = new float[17, 17];
        for (int y = 0; y < 17; y++)
        for (int x = 0; x < 17; x++) coarse[x, y] = (float)random.NextDouble();
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int cx = x / 8, cy = y / 8;
            float u = (x % 8) / 8f, v = (y % 8) / 8f;
            float a = Mathf.Lerp(coarse[cx, cy], coarse[cx + 1, cy], u);
            float b = Mathf.Lerp(coarse[cx, cy + 1], coarse[cx + 1, cy + 1], u);
            float value = Mathf.Clamp01(Mathf.Lerp(a, b, v) * .62f +
                (float)random.NextDouble() * .38f);
            byte channel = (byte)(value * 255f);
            pixels[y * size + x] = new Color32(channel, channel, channel, 255);
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(Art + "AshNoise.png", texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    static void Dot(Color32[] pixels, int width, int height, int x, int y,
        Color32 value, int radius = 0)
    {
        for (int yy = -radius; yy <= radius; yy++)
        for (int xx = -radius; xx <= radius; xx++)
        {
            int px = x + xx, py = y + yy;
            if (px >= 0 && py >= 0 && px < width && py < height)
                pixels[py * width + px] = value;
        }
    }
    static void Segment(Color32[] pixels, int width, int height,
        Vector2Int a, Vector2Int b, Color32 color, int radius = 0)
    {
        int count = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        for (int i = 0; i <= count; i++)
        {
            float t = count == 0 ? 0f : i / (float)count;
            Dot(pixels, width, height,
                Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t)),
                Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t)), color, radius);
        }
    }
    static void SaveTexture(string path, int width, int height, Color32[] pixels)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    static void WriteCracks()
    {
        const int size = 512;
        var pixels = new Color32[size * size];
        var random = new System.Random(5176);
        for (int side = 0; side < 4; side++)
        for (int branch = 0; branch < 13; branch++)
        {
            int along = 14 + random.Next(484);
            var point = side == 0 ? new Vector2Int(0, along) :
                side == 1 ? new Vector2Int(511, along) :
                side == 2 ? new Vector2Int(along, 0) :
                    new Vector2Int(along, 511);
            for (int step = 0; step < 5; step++)
            {
                var next = point + (side == 0 ? new Vector2Int(7 + random.Next(10),
                    random.Next(-12, 13)) : side == 1 ?
                    new Vector2Int(-7 - random.Next(10), random.Next(-12, 13)) :
                    side == 2 ? new Vector2Int(random.Next(-12, 13),
                        7 + random.Next(10)) :
                    new Vector2Int(random.Next(-12, 13), -7 - random.Next(10)));
                Segment(pixels, size, size, point, next,
                    new Color32(124, 44, 32, 80), 2);
                Segment(pixels, size, size, point, next,
                    new Color32(255, 135, 61, (byte)(225 - step * 28)));
                point = next;
            }
        }
        SaveTexture(Art + "AshCracks.png", size, size, pixels);
    }

    static void WritePanel()
    {
        const int width = 512, height = 512;
        var pixels = new Color32[width * height];
        var random = new System.Random(7351);
        for (int x = 14; x < width - 14; x++)
        {
            int crown = 448 + Mathf.RoundToInt(42f *
                (1f - Mathf.Abs(x - 256f) / 242f));
            for (int y = 15; y <= crown; y++)
            {
                int ash = random.Next(-8, 9);
                bool mortar = y % 36 < 2 && y < 430;
                byte r = (byte)Mathf.Clamp((mortar ? 34 : 47) + ash, 0, 255);
                byte g = (byte)Mathf.Clamp((mortar ? 27 : 35) + ash, 0, 255);
                byte b = (byte)Mathf.Clamp((mortar ? 31 : 39) + ash, 0, 255);
                pixels[y * width + x] = new Color32(r, g, b, 231);
            }
            if (x > 15 && x < 496)
            {
                Dot(pixels, width, height, x, crown,
                    new Color32(131, 96, 74, 255), 1);
                Dot(pixels, width, height, x, crown - 4,
                    new Color32(92, 64, 54, 255));
            }
        }
        for (int y = 16; y < 449; y++)
        {
            Dot(pixels, width, height, 15, y, new Color32(130, 92, 69, 255), 1);
            Dot(pixels, width, height, 496, y, new Color32(130, 92, 69, 255), 1);
            Dot(pixels, width, height, 21, y, new Color32(93, 55, 44, 220));
            Dot(pixels, width, height, 490, y, new Color32(93, 55, 44, 220));
        }
        Segment(pixels, width, height, new Vector2Int(15, 15),
            new Vector2Int(496, 15), new Color32(157, 102, 69, 255), 1);
        foreach (int x in new[] { 48, 84, 428, 464 })
        {
            Segment(pixels, width, height, new Vector2Int(x, 20),
                new Vector2Int(x, 64), new Color32(108, 77, 62, 230));
            Segment(pixels, width, height, new Vector2Int(x - 6, 64),
                new Vector2Int(x, 76), new Color32(161, 93, 51, 240));
            Segment(pixels, width, height, new Vector2Int(x, 76),
                new Vector2Int(x + 6, 64), new Color32(161, 93, 51, 240));
        }
        SaveTexture(Art + "AshPanel.png", width, height, pixels);
    }

    static void WriteSounds()
    {
        WriteWave("MenuOpen", .48f, 87f, 271);
        WriteWave("MenuEmber", .19f, 261f, 733);
        WriteWave("MenuAsh", .38f, 120f, 421);
        WriteWave("MenuClose", .39f, 68f, 177);
    }
    static void WriteWave(string name, float seconds, float pitch, int seed)
    {
        const int sampleRate = 22050;
        int count = Mathf.CeilToInt(seconds * sampleRate);
        var random = new System.Random(seed);
        using (var stream = new FileStream(Art + name + ".wav", FileMode.Create))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + count * 2);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(sampleRate); writer.Write(sampleRate * 2);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(count * 2);
            float filtered = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float phase = i / (float)count;
                float envelope = Mathf.Sin(Mathf.PI * phase);
                float noise = (float)random.NextDouble() * 2f - 1f;
                filtered = Mathf.Lerp(filtered, noise, .18f);
                float crackle = random.NextDouble() < .0035 ? noise * .26f : 0f;
                float drone = Mathf.Sin(2f * Mathf.PI * pitch * t) * .13f +
                    Mathf.Sin(2f * Mathf.PI * pitch * .5f * t) * .07f;
                float sound = (drone + filtered * .07f + crackle) * envelope;
                writer.Write((short)(Mathf.Clamp(sound, -.7f, .7f) * 32767f));
            }
        }
    }

    static void BuildTitleScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera),
            typeof(AudioListener));
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 0, -10);
        camera.GetComponent<Camera>().orthographic = true;
        camera.GetComponent<Camera>().orthographicSize = 5f;
        camera.GetComponent<Camera>().backgroundColor = new Color(.12f, .04f, .055f);
        var light = new GameObject("Directional Light", typeof(Light));
        light.GetComponent<Light>().type = LightType.Directional;
        new GameObject("Ashrisen main menu", typeof(AshMainMenu));
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene(GameSaveSystem.StagePath, true),
            new EditorBuildSettingsScene(GameSaveSystem.ArenaPath, true),
            new EditorBuildSettingsScene(GameSaveSystem.LegacyGraveyardPath, true)
        };
        AssetDatabase.SaveAssets();
    }
}
