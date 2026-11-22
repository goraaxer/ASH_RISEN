using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// A separate, deliberately small-scale test scene. It does not rebuild DuskWorld.
public static class AshenGraveyardBuilder
{
    const string Art = "Assets/Art/AshenGraveyard/";
    const string TilePath = Art + "Tiles16/";
    const string ScenePath = "Assets/Scenes/AshenGraveyard.unity";
    static Tile[] tiles;
    static readonly Color32 Void = new Color32(0, 0, 0, 0);
    static readonly Color32 Mortar = new Color32(43, 39, 41, 255);
    static readonly Color32 Deep = new Color32(49, 44, 44, 255);
    static readonly Color32 Stone = new Color32(65, 58, 56, 255);
    static readonly Color32 Face = new Color32(83, 70, 64, 255);
    static readonly Color32 Light = new Color32(111, 89, 73, 255);
    static readonly Color32 Ash = new Color32(97, 91, 88, 255);
    static readonly Color32 Moss = new Color32(71, 67, 56, 255);

    [MenuItem("Ashrisen/Build Ashen Graveyard")]
    public static void Build()
    {
        Directory.CreateDirectory(Art);
        Directory.CreateDirectory(TilePath);
        ConfigureSource("CemeterySky", 20);
        ConfigureSource("GraveyardMid", 100);
        ConfigureSource("GraveyardProps", 16, true);
        ConfigureSource("CemeterySpawnPropsSource", 16, true);
        ConfigureSource("BridgeUnderSource", 16, true);
        ConfigureSource("CheckpointLanternSource", 16, true);
        CreateTiles();
        CreatePalette();
        CreatePropSprites();
        BuildScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EnsurePaletteSettings();
        AssetDatabase.SaveAssets();
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/AshenVillage.unity", true),
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene("Assets/Scenes/DuskWorld.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/BossArena.unity", true)
        };
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("AshenGraveyard built: 40 new 16x16 tiles, palette, three parallax depths, and test route.");
    }

    static void ConfigureSource(string name, float ppu, bool readable = false)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Art + name + ".png");
        if (importer == null) throw new Exception("Missing artwork: " + name);
        importer.textureType = readable ? TextureImporterType.Default : TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu;
        importer.isReadable = readable;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    static void Set(Color32[] a, int x, int y, Color32 c)
    {
        if (x >= 0 && x < 16 && y >= 0 && y < 16) a[y * 16 + x] = c;
    }
    static void Box(Color32[] a, int x0, int y0, int x1, int y1, Color32 c)
    {
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Set(a, x, y, c);
    }
    static void StoneBlock(Color32[] a, int x0, int y0, int w, int h, int seed)
    {
        var color = seed % 3 == 0 ? Face : seed % 3 == 1 ? Stone : Ash;
        Box(a, x0, y0, x0 + w - 1, y0 + h - 1, Mortar);
        Box(a, x0 + 1, y0 + 1, x0 + w - 2, y0 + h - 2, color);
        Box(a, x0 + 1, y0 + h - 2, x0 + w - 2, y0 + h - 2, Light);
        Set(a, x0 + Math.Max(1, w - 3), y0 + 1, Deep);
        if (seed % 4 == 0) Set(a, x0 + 2, y0 + 2, Moss);
    }

    static Color32[] DrawTile(int id)
    {
        var a = new Color32[256];
        for (int i = 0; i < a.Length; i++) a[i] = Void;
        int variant = id % 4;
        if (id < 8) // small cobbles above irregular ash soil; no torso-sized stones
        {
            Box(a, 0, 0, 15, 15, Deep);
            for (int y = 0; y < 11; y++) for (int x = 0; x < 16; x++)
            {
                int h = (x * 37 + y * 19 + id * 53 + x * y * 7) % 41;
                if (h == 3 || h == 11) Set(a, x, y, Stone);
                else if (h == 21) Set(a, x, y, Moss);
            }
            if (id < 4)
            {
                Box(a, 0, 10, 15, 10, Mortar);
                for (int n = -1; n < 5; n++)
                {
                    int left = n * 4 + (variant + n * n) % 2;
                    Box(a, left, 11, left + 3, 14, n % 3 == 0 ? Ash : n % 2 == 0 ? Face : Stone);
                    Box(a, left, 14, left + 2, 14, Light);
                    Set(a, left + 3, 11, Mortar);
                }
                for (int x = 0; x < 16; x++) Set(a, x, 15, x % 5 == variant ? Moss : Light);
                Set(a, 2 + variant * 3, 12, Deep);
            }
            else
            {
                for (int n = 0; n < 7; n++)
                {
                    int x = (n * 7 + variant * 3) % 16;
                    int y = (n * 5 + variant * 2) % 16;
                    Box(a, x, y, x + (n % 3), y + (n % 2), n % 3 == 0 ? Ash : Stone);
                }
            }
        }
        else if (id < 16) // weathered wall, corner and edge courses
        {
            Box(a, 0, 0, 15, 15, Deep);
            StoneBlock(a, -2 + variant, 8, 10, 8, id);
            StoneBlock(a, 8 + variant, 8, 10, 8, id + 1);
            StoneBlock(a, 1 + variant, 0, 10, 7, id + 2);
            StoneBlock(a, 12 + variant, 0, 8, 7, id + 3);
            if (id == 14) Box(a, 0, 0, 1, 15, Light);
            if (id == 15) Box(a, 14, 0, 15, 15, Light);
        }
        else if (id < 24) // narrow dressed bridge course with chipped lower outline
        {
            Box(a, 0, 0, 15, 8, Stone);
            for (int n = 0; n < 5; n++)
            {
                int left = n * 4 - 1 + variant % 2;
                Box(a, left, 2, left + 3, 5, n % 2 == 0 ? Face : Ash);
                Box(a, left, 5, left + 2, 5, Light);
                Set(a, left + 3, 2, Mortar);
            }
            for (int x = 0; x < 16; x += 5) Set(a, x + variant % 2, 0, Void);
            Box(a, 0, 9, 15, 14, Stone);
            Box(a, 0, 8, 15, 8, Deep);
            for (int n = -1; n < 5; n++)
            {
                int left = n * 4 + (variant + n * n) % 2;
                Box(a, left, 10, left + 3, 13, n % 2 == 0 ? Face : Ash);
                Box(a, left, 13, left + 2, 13, Light);
                Set(a, left + 3, 10, Mortar);
            }
            for (int x = 0; x < 16; x++) Set(a, x, 15, x % 5 == variant ? Moss : Light);
            for (int x = 1; x < 15; x += 5) Set(a, x + variant % 2, 7, Stone);
            if (id == 22) { Box(a, 0, 8, 2, 12, Void); Set(a, 1, 13, Ash); }
            if (id == 23) { Box(a, 13, 8, 15, 12, Void); Set(a, 14, 13, Ash); }
        }
        else if (id < 28) // modular pillar: base, shaft, capital, narrow pier
        {
            int left = id == 27 ? 5 : 3;
            int right = id == 27 ? 10 : 12;
            Box(a, left, 0, right, 15, Mortar);
            Box(a, left + 1, 0, right - 1, 15, Stone);
            Box(a, left + 1, 0, left + 2, 15, Light);
            Box(a, right - 2, 0, right - 1, 15, Deep);
            if (id == 24 || id == 26) {
                Box(a, 1, id == 24 ? 0 : 12, 14, id == 24 ? 3 : 15, Stone);
                Box(a, 1, id == 24 ? 3 : 15, 14, id == 24 ? 3 : 15, Light);
            }
            if (id == 25) { Set(a, 7, 5, Ash); Set(a, 9, 10, Deep); }
        }
        else if (id < 32) // ruined step/rubble variants
        {
            if (id < 30)
            {
                Box(a, 0, 0, 15, id == 28 ? 7 : 11, Stone);
                Box(a, 0, id == 28 ? 7 : 11, 15, id == 28 ? 8 : 12, Light);
                Box(a, 0, 0, 15, 1, Mortar);
            }
            else
            {
                Box(a, 1, 0, 7, 4, Stone);
                Box(a, 9, 0, 14, 6, Face);
                Box(a, 2, 5, 4, 7, Ash);
                Set(a, 11, 7, Light);
            }
        }
        else if (id < 36) // Gothic arch fragments, open beneath the bridge
        {
            for (int x = 0; x < 16; x++)
            {
                int rise = id == 32 ? 2 + x * 10 / 15 :
                    id == 33 ? 2 + (15 - x) * 10 / 15 :
                    id == 34 ? 12 + x * 3 / 15 : 12 + (15 - x) * 3 / 15;
                for (int y = rise; y < 16; y++)
                {
                    Color32 color = y == rise ? Light : y == rise + 1 ? Face :
                        (x * 7 + y * 11 + id) % 9 == 0 ? Ash : Stone;
                    Set(a, x, y, color);
                }
                if (x % 5 == 0) Set(a, x, rise + 2, Mortar);
            }
        }
        else if (id == 38) // carved keystone above the arch opening
        {
            Box(a, 0, 14, 15, 15, Stone);
            for (int x = 4; x <= 11; x++) Box(a, x, 10 + Math.Abs(7 - x), x, 15, x % 3 == 0 ? Light : Stone);
            Set(a, 7, 10, Ash);
        }
        else // fence and small carved ornaments
        {
            if (id < 38)
            {
                for (int x = 2 + variant % 2; x < 16; x += 5) {
                    Box(a, x, 1, x + 1, 12, Mortar);
                    Set(a, x, 13, Light); Set(a, x + 1, 14, Stone);
                }
                Box(a, 0, 4, 15, 5, Stone);
            }
            else
            {
                Box(a, 6, 1, 9, 12, Stone);
                Box(a, 2, 6, 13, 8, Stone);
                Box(a, 4, 12, 11, 14, Light);
                Box(a, 5, 0, 10, 2, Mortar);
            }
        }
        return a;
    }

    static void CreateTiles()
    {
        for (int id = 0; id < 40; id++)
        {
            var image = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            image.SetPixels32(DrawTile(id)); image.filterMode = FilterMode.Point; image.Apply();
            File.WriteAllBytes(TilePath + "Ashen_" + id.ToString("D2") + ".png", image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
        AssetDatabase.Refresh();
        tiles = new Tile[40];
        for (int id = 0; id < 40; id++)
        {
            string basePath = TilePath + "Ashen_" + id.ToString("D2");
            var importer = (TextureImporter)AssetImporter.GetAtPath(basePath + ".png");
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var asset = AssetDatabase.LoadAssetAtPath<Tile>(basePath + ".asset");
            if (asset == null) { asset = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(asset, basePath + ".asset"); }
            asset.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(basePath + ".png");
            asset.colliderType = Tile.ColliderType.Grid;
            EditorUtility.SetDirty(asset);
            tiles[id] = asset;
        }
    }

    static void CreatePalette()
    {
        var root = new GameObject("Ashen Graveyard 16x16 Palette", typeof(Grid));
        var child = new GameObject("Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
        child.transform.SetParent(root.transform);
        for (int id = 0; id < 40; id++) child.GetComponent<Tilemap>().SetTile(new Vector3Int(id % 8, 4 - id / 8, 0), tiles[id]);
        PrefabUtility.SaveAsPrefabAsset(root, Art + "AshenGraveyardPalette.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        EnsurePaletteSettings();
    }

    static void EnsurePaletteSettings()
    {
        string path = Art + "AshenGraveyardPalette.prefab";
        var type = Type.GetType("UnityEditor.GridPalette, UnityEditor");
        if (type == null) return;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path)) if (asset.GetType() == type) return;
        var settings = ScriptableObject.CreateInstance(type);
        settings.name = "Palette Settings";
        AssetDatabase.AddObjectToAsset(settings, path);
    }

    static void CropProp(string name, RectInt sourceRect, int width, int height,
        string sourceName = "GraveyardProps")
    {
        var source = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + sourceName + ".png");
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            int sx = sourceRect.x + Mathf.Clamp(Mathf.FloorToInt((x + .5f) * sourceRect.width / width), 0, sourceRect.width - 1);
            int sy = sourceRect.y + Mathf.Clamp(Mathf.FloorToInt((y + .5f) * sourceRect.height / height), 0, sourceRect.height - 1);
            var pixel = source.GetPixel(sx, sy);
            if (pixel.a < .25f) pixel = Color.clear;
            image.SetPixel(x, y, pixel);
        }
        image.filterMode = FilterMode.Point; image.Apply();
        File.WriteAllBytes(Art + name + ".png", image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
    }

    static void CreatePropSprites()
    {
        CropProp("BrokenArch", new RectInt(120, 404, 450, 620), 64, 96);
        CropProp("VillageGate", new RectInt(635, 400, 405, 470), 64, 75);
        CropProp("Angel", new RectInt(1070, 390, 340, 430), 36, 46);
        CropProp("Gravestone", new RectInt(130, 0, 245, 290), 24, 30);
        CropProp("GraveCross", new RectInt(370, 0, 285, 300), 28, 30);
        CropProp("IronFence", new RectInt(640, 0, 420, 300), 64, 42);
        CropProp("BridgeRailing", new RectInt(640, 0, 420, 300), 64, 20);
        CropProp("Column", new RectInt(1160, 0, 200, 310), 20, 45);
        CropProp("DeadAshTree", new RectInt(90, 35, 560, 600), 56, 72,
            "CemeterySpawnPropsSource");
        CropProp("FamilyMausoleum", new RectInt(740, 30, 660, 680), 64, 72,
            "CemeterySpawnPropsSource");
        CropProp("BurialCluster", new RectInt(1510, 25, 600, 300), 64, 32,
            "CemeterySpawnPropsSource");
        for (int i = 0; i < 3; i++) CropBridgeModule(i);
        CropCheckpointLantern();
        AssetDatabase.Refresh();
        foreach (var name in new[] { "BrokenArch", "VillageGate", "Angel", "Gravestone", "GraveCross", "IronFence", "BridgeRailing", "Column", "DeadAshTree", "FamilyMausoleum", "BurialCluster", "BridgeArch0", "BridgeArch1", "BridgeArch2", "CheckpointLantern" })
            ConfigureSource(name, 16);
    }

    static void CropCheckpointLantern()
    {
        var source = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "CheckpointLanternSource.png");
        int minX = source.width, minY = source.height, maxX = 0, maxY = 0;
        for (int y = 0; y < source.height; y++) for (int x = 0; x < source.width; x++)
            if (source.GetPixel(x, y).a > .35f)
            {
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
        if (minX >= maxX || minY >= maxY) throw new Exception("Lantern source has no opaque pixels");
        var image = new Texture2D(24, 43, TextureFormat.RGBA32, false);
        for (int y = 0; y < 43; y++) for (int x = 0; x < 24; x++)
        {
            int sx = minX + Mathf.FloorToInt((x + .5f) * (maxX - minX + 1) / 24f);
            int sy = minY + Mathf.FloorToInt((y + .5f) * (maxY - minY + 1) / 43f);
            var color = source.GetPixel(sx, sy);
            image.SetPixel(x, y, color.a < .35f ? Color.clear : color);
        }
        image.filterMode = FilterMode.Point; image.Apply();
        File.WriteAllBytes(Art + "CheckpointLantern.png", image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
    }

    static void CropBridgeModule(int part)
    {
        var source = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "BridgeUnderSource.png");
        var image = new Texture2D(48, 48, TextureFormat.RGBA32, false);
        for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++)
        {
            int sx = 50 + part * 690 + Mathf.FloorToInt((x + .5f) * 690f / 48f);
            int sy = 59 + Mathf.FloorToInt((y + .5f) * 509f / 48f);
            var color = source.GetPixel(sx, sy);
            image.SetPixel(x, y, color.a < .25f ? Color.clear : color);
        }
        image.filterMode = FilterMode.Point; image.Apply();
        File.WriteAllBytes(Art + "BridgeArch" + part + ".png", image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
    }

    static SpriteRenderer Picture(string name, string asset, Transform parent, Vector3 position, int order, Color color)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(parent); go.transform.position = position;
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + asset + ".png");
        sr.sortingOrder = order; sr.color = color;
        return sr;
    }

    static Tilemap Map(string name, Transform grid, int order, bool collider)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(grid); go.transform.localPosition = Vector3.zero;
        go.GetComponent<TilemapRenderer>().sortingOrder = order;
        if (collider)
        {
            go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            var tileCollider = go.AddComponent<TilemapCollider2D>();
            go.AddComponent<CompositeCollider2D>().geometryType = CompositeCollider2D.GeometryType.Polygons;
            tileCollider.usedByComposite = true;
        }
        return go.GetComponent<Tilemap>();
    }

    static void Bridge(Tilemap walkway, Tilemap architecture, int a, int b, int y)
    {
        for (int x = a; x <= b; x++) walkway.SetTile(new Vector3Int(x, y, 0), tiles[x == a ? 22 : x == b ? 23 : 16 + x % 6]);
        // Supports and arch pieces are art only; every collidable surface is the visible top course.
        int rightPier = b == 25 ? b - 2 : b - 1;
        foreach (int x in new[] { a + 1, rightPier })
        {
            for (int cy = 0; cy < y - 1; cy++) architecture.SetTile(new Vector3Int(x, cy, 0), tiles[25]);
            architecture.SetTile(new Vector3Int(x, y - 1, 0), tiles[26]);
            var pillar = new GameObject("Solid bridge column " + x,
                typeof(BoxCollider2D));
            pillar.transform.SetParent(architecture.transform, false);
            pillar.transform.localPosition = new Vector3(x + .5f, y * .5f, 0f);
            pillar.GetComponent<BoxCollider2D>().size = new Vector2(.95f, y);
        }
        if (rightPier - a >= 5)
        {
            int count = rightPier - a - 2;
            for (int x = a + 2; x < rightPier; x++)
            {
                int place = (x - a - 2) * 4 / Math.Max(1, count - 1);
                int tile = place == 0 ? 32 : place == 1 ? 34 : place == 2 ? 38 : place == 3 ? 35 : 33;
                architecture.SetTile(new Vector3Int(x, y - 1, 0), tiles[tile]);
            }
        }
    }

    static void Ground(Tilemap map, int a, int b)
    {
        for (int x = a; x <= b; x++)
        {
            int variant = (x % 4 + 4) % 4;
            map.SetTile(new Vector3Int(x, -1, 0), tiles[variant]);
            map.SetTile(new Vector3Int(x, -2, 0), tiles[4 + variant]);
        }
    }

    static void BuildScene()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/EmptyStage.unity", OpenSceneMode.Single);
        var player = GameObject.Find("player");
        if (player == null) throw new Exception("Original heroine missing from EmptyStage");
        player.transform.position = new Vector3(0f, .5f, 0);
        var playerBody = player.GetComponent<Rigidbody2D>();
        playerBody.position = player.transform.position;
        playerBody.gravityScale = 1f;
        var graveyardRespawn = player.GetComponent<PlayerRespawn>();
        graveyardRespawn.respawnPoint = player.transform.position;
        graveyardRespawn.fallLimitY = -3.25f;
        var camera = Camera.main;
        camera.orthographicSize = 5f;
        camera.backgroundColor = new Color(.23f, .16f, .17f);
        var pixelCamera = camera.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
        if (pixelCamera != null) {
            pixelCamera.assetsPPU = 16;
            pixelCamera.refResolutionX = 288;
            pixelCamera.refResolutionY = 160;
        }
        var follow = camera.GetComponent<camerafollow>();
        follow.target = player.transform; follow.offset = new Vector2(1f, 1.3f);
        follow.horizontalSmoothTime = .18f; follow.verticalSmoothTime = .24f; follow.SnapNow();
        var root = new GameObject("Ashen Graveyard — first playable level").transform;
        var sky = Picture("Distant cemetery sunset", "CemeterySky", root,
            new Vector3(40, 15, 0), -100, Color.white);
        sky.gameObject.AddComponent<WorldParallax>().horizontalFactor = .015f;
        var cameraBounds = camera.gameObject.GetComponent<AshenGraveyardCameraBounds>();
        if (cameraBounds == null) cameraBounds = camera.gameObject.AddComponent<AshenGraveyardCameraBounds>();
        cameraBounds.minimumX = 4f;
        cameraBounds.maximumX = 80f;
        cameraBounds.minimumY = 4.7f;
        cameraBounds.maximumY = 6.2f;
        cameraBounds.skyCoverage = sky;
        for (int n = 0; n < 4; n++)
        {
            var mid = Picture("Mausoleum silhouettes " + n, "GraveyardMid", root,
                new Vector3(10 + n * 21, 3.8f, 0), -60,
                new Color(.68f, .64f, .63f, .38f));
            mid.gameObject.AddComponent<WorldParallax>().horizontalFactor = .065f;
        }
        var grid = new GameObject("Editable 16x16 graveyard", typeof(Grid)).transform;
        grid.SetParent(root);
        var ground = Map("Walkable cobblestone and soil", grid, 5, true);
        var bridge = Map("Solid elevated stone bridges", grid, 7, true);
        var masonry = Map("Arches columns and ruins", grid, -12, false);
        Ground(ground, -12, 23);
        Ground(ground, 27, 100);
        var westernBoundary = new GameObject("Invisible western boundary", typeof(BoxCollider2D));
        westernBoundary.transform.SetParent(root);
        westernBoundary.transform.position = new Vector3(-2.5f, 5f, 0);
        westernBoundary.GetComponent<BoxCollider2D>().size = new Vector2(1f, 40f);
        var easternBoundary = new GameObject("Invisible eastern boundary", typeof(BoxCollider2D));
        easternBoundary.transform.SetParent(root);
        easternBoundary.transform.position = new Vector3(79f, 5f, 0);
        easternBoundary.GetComponent<BoxCollider2D>().size = new Vector2(1f, 40f);
        ground.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
        ground.GetComponent<CompositeCollider2D>().GenerateGeometry();
        // Three-cell broken span. The main route is jumpable at the original 7-unit jump velocity.
        Bridge(bridge, masonry, 17, 23, 1);
        bridge.SetTile(new Vector3Int(16, 0, 0), tiles[22]);
        // A grounded buttress gives the heroine a jump onto the solid arch.
        bridge.SetTile(new Vector3Int(27, 0, 0), tiles[22]);
        for (int i = 0; i < 3; i++)
            Picture("Assembled Gothic bridge arch " + i, "BridgeArch" + i, root,
                new Vector3(29.5f + 3f * i, 1.5f, 0), -11, Color.white)
                .gameObject.AddComponent<PolygonCollider2D>();
        Bridge(bridge, masonry, 39, 46, 1);
        bridge.SetTile(new Vector3Int(38, 0, 0), tiles[22]);
        bridge.SetTile(new Vector3Int(47, 1, 0), tiles[23]);
        ground.SetTile(new Vector3Int(47, 0, 0), tiles[8]);
        Bridge(bridge, masonry, 48, 53, 2);
        bridge.SetTile(new Vector3Int(54, 1, 0), tiles[22]);
        ground.SetTile(new Vector3Int(54, 0, 0), tiles[10]);
        bridge.SetTile(new Vector3Int(55, 0, 0), tiles[23]);
        ground.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
        ground.GetComponent<CompositeCollider2D>().GenerateGeometry();
        bridge.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
        bridge.GetComponent<CompositeCollider2D>().GenerateGeometry();
        foreach (var rail in new[] {
            new Vector3(19.5f, 2.5f, 0),
            new Vector3(41f, 2.5f, 0), new Vector3(50f, 3.5f, 0) })
            Picture("Broken bridge balustrade", "BridgeRailing", root, rail, 6,
                new Color(.8f, .76f, .72f, .9f));
        // Distinct small architectural props are carefully spaced; gameplay surfaces remain tilemap geometry.
        Picture("Collapsed entrance arch", "BrokenArch", root, new Vector3(9.5f, 3.0f, 0), -4, Color.white);
        Picture("Ruined family mausoleum", "FamilyMausoleum", root,
            new Vector3(-.8f, 2.2f, 0), -7, new Color(.78f, .74f, .72f, .9f));
        Picture("Dead ash tree between graves", "DeadAshTree", root,
            new Vector3(6.1f, 2.25f, 0), -7, new Color(.76f, .72f, .7f, .88f));
        Picture("Old burial plots", "BurialCluster", root,
            new Vector3(12.3f, 1f, 0), -3, new Color(.85f, .81f, .78f, 1f));
        Picture("Further burial plots", "BurialCluster", root,
            new Vector3(16.8f, 1f, 0), -3, new Color(.69f, .66f, .63f, .85f));
        var villageGate = Picture("Ancient village gate", "VillageGate", root,
            new Vector3(76f, 2.15f, 0), 8, Color.white);
        villageGate.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(2.6f, 4.2f);
        foreach (var x in new[] { 5f, 13f, 35f, 58f, 65f })
            Picture("Small gravestone", x == 35f || x == 65f ? "GraveCross" : "Gravestone", root,
                new Vector3(x, .65f, 0), -2, new Color(.78f, .77f, .75f));
        foreach (var x in new[] { 7f, 14f, 55f, 69f })
        {
            var fence = Picture("Near fence parallax", "IronFence", root, new Vector3(x, .7f, 0), -9,
                new Color(.65f, .59f, .55f, .7f));
            fence.gameObject.AddComponent<WorldParallax>().horizontalFactor = .11f;
        }
        var lanternArt = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Art/NewWorld/AshLantern.png");
        var checkpoint = new GameObject("Graveyard checkpoint", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(AshLantern));
        checkpoint.transform.SetParent(root); checkpoint.transform.position = new Vector3(3.2f, 1.25f, 0);
        checkpoint.GetComponent<SpriteRenderer>().sprite = lanternArt;
        checkpoint.GetComponent<SpriteRenderer>().sortingOrder = 12;
        checkpoint.GetComponent<CircleCollider2D>().isTrigger = true;
        var checkpointLantern = checkpoint.GetComponent<AshLantern>();
        checkpointLantern.radius = 1.6f;
        checkpointLantern.spawnOffset = new Vector3(.8f, .25f, 0);
        checkpointLantern.showPrompt = false;
        var tutorial = new GameObject("Graveyard tutorial guidance", typeof(GraveyardTutorial));
        tutorial.transform.SetParent(root);
        tutorial.GetComponent<GraveyardTutorial>().player = player.GetComponent<playercontroler>();
        tutorial.GetComponent<GraveyardTutorial>().checkpoint = checkpointLantern;
        // Lower the complete playable world one 16 px tile, including its art and colliders.
        root.position = Vector3.down;
        if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/Enemies/GraveWarden.prefab") != null)
            OrdinaryEnemyBuilder.Place("Grave Warden - burial lane", "GraveWarden", 61f);
        EditorSceneManager.SaveScene(scene, ScenePath, false);
    }
}
