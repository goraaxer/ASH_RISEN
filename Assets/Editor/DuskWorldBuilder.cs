using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class DuskWorldBuilder
{
    const string Art = "Assets/Art/NewWorld/";
    const string Tiles = Art + "Tiles16/";
    const string WorldScene = "Assets/Scenes/DuskWorld.unity";
    const string ArenaScene = "Assets/Scenes/BossArena.unity";
    static Tile[] palette;

    [MenuItem("Ashrisen/Build New Dusk World")]
    public static void Build()
    {
        ConfigureArtwork();
        CreateTiles();
        BuildPalette();
        BuildExterior();
        BuildArena();
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/AshenVillage.unity", true),
            new EditorBuildSettingsScene(WorldScene, true),
            new EditorBuildSettingsScene(ArenaScene, true)
        };
        EditorSceneManager.OpenScene(WorldScene, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EnsurePaletteSettings(Art + "Gothic16Palette.prefab");
        AssetDatabase.SaveAssets();
        Debug.Log("New Dusk World built: exterior, boss arena, 64 art tiles and palette.");
    }

    static void ConfigureArtwork()
    {
        ImportSprite("SunsetWorld", 14);
        ImportSprite("VillageFacades", 55);
        ImportSprite("GrandChurch", 56);
        ImportSprite("BossNave", 50);
        ImportSprite("CastleGate", 145);
        ImportSprite("AshLantern", 600);
        ImportSprite("CemeteryMidground", 60);
        ImportSprite("VillageSkyline", 45);
        var path = Art + "GothicAtlasSource.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.isReadable = true;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    static void ImportSprite(string name, float pixelsPerUnit)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Art + name + ".png");
        if (importer == null) throw new Exception("Missing artwork: " + name);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    static void CreateTiles()
    {
        Directory.CreateDirectory(Tiles);
        var source = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "GothicAtlasSource.png");
        if (source == null) throw new Exception("Tile artwork missing");
        for (int row = 0; row < 8; row++)
        for (int col = 0; col < 8; col++)
        {
            var tile = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            tile.filterMode = FilterMode.Point;
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float u = (col + (x + .5f) / 16f) / 8f;
                float v = 1f - (row + (15 - y + .5f) / 16f) / 8f;
                tile.SetPixel(x, y, source.GetPixelBilinear(u, v));
            }
            tile.Apply();
            File.WriteAllBytes(Tiles + $"Gothic_{row:D2}_{col:D2}.png", tile.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tile);
        }
        AssetDatabase.Refresh();
        palette = new Tile[64];
        for (int row = 0; row < 8; row++)
        for (int col = 0; col < 8; col++)
        {
            string name = $"Gothic_{row:D2}_{col:D2}";
            string imagePath = Tiles + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            string assetPath = Tiles + name + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            asset.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
            asset.colliderType = Tile.ColliderType.Grid;
            EditorUtility.SetDirty(asset);
            palette[row * 8 + col] = asset;
        }
    }

    static void BuildPalette()
    {
        var root = new GameObject("Gothic 16x16 Palette", typeof(Grid));
        var child = new GameObject("Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
        child.transform.SetParent(root.transform);
        var map = child.GetComponent<Tilemap>();
        for (int row = 0; row < 8; row++)
        for (int col = 0; col < 8; col++)
            map.SetTile(new Vector3Int(col, 7 - row, 0), palette[row * 8 + col]);
        string prefabPath = Art + "Gothic16Palette.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        EnsurePaletteSettings(prefabPath);
    }

    static void EnsurePaletteSettings(string prefabPath)
    {
        var settingsType = Type.GetType("UnityEditor.GridPalette, UnityEditor");
        bool hasSettings = false;
        foreach (var subAsset in AssetDatabase.LoadAllAssetsAtPath(prefabPath))
            if (settingsType != null && subAsset.GetType() == settingsType) hasSettings = true;
        if (settingsType != null && !hasSettings)
        {
            var settings = ScriptableObject.CreateInstance(settingsType);
            settings.name = "Palette Settings";
            AssetDatabase.AddObjectToAsset(settings, prefabPath);
        }
    }


    static Scene BaseScene(string path, Vector3 spawn)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/EmptyStage.unity", OpenSceneMode.Single);
        var player = GameObject.Find("player");
        player.transform.position = spawn;
        var body = player.GetComponent<Rigidbody2D>();
        body.position = spawn;
        body.gravityScale = 1f;
        var respawn = player.GetComponent<PlayerRespawn>();
        if (respawn != null) respawn.respawnPoint = spawn;
        var cam = Camera.main;
        cam.orthographicSize = 8f;
        cam.backgroundColor = new Color(.075f, .035f, .08f);
        var follow = cam.GetComponent<camerafollow>();
        if (follow != null) { follow.target = player.transform; follow.SnapNow(); }
        EditorSceneManager.SaveScene(scene, path, false);
        return scene;
    }

    static SpriteRenderer Picture(string name, string assetName, Vector3 position, int order, Transform parent = null)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        if (parent != null) go.transform.SetParent(parent);
        go.transform.position = position;
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + assetName + ".png");
        renderer.sortingOrder = order;
        return renderer;
    }

    static Tilemap Map(string name, Transform parent, int order, bool solid)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(parent);
        go.transform.localPosition = Vector3.zero;
        var renderer = go.GetComponent<TilemapRenderer>();
        renderer.sortingOrder = order;
        if (solid) go.AddComponent<TilemapCollider2D>();
        return go.GetComponent<Tilemap>();
    }

    static Tilemap OneWayMap(string name, Transform parent, int order)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(parent);
        go.transform.localPosition = Vector3.zero;
        go.GetComponent<TilemapRenderer>().sortingOrder = order;
        return go.GetComponent<Tilemap>();
    }

    static void TopEdge(Tilemap visibleTiles, int x0, int x1, int tileY)
    {
        var go = new GameObject("Collision aligned to visible tile tops",
            typeof(Rigidbody2D), typeof(EdgeCollider2D), typeof(PlatformEffector2D));
        go.transform.SetParent(visibleTiles.transform);
        go.transform.localPosition = Vector3.zero;
        go.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        var edge = go.GetComponent<EdgeCollider2D>();
        edge.points = new[] { new Vector2(x0, tileY + 1f),
            new Vector2(x1 + 1f, tileY + 1f) };
        edge.usedByEffector = true;
        var effector = go.GetComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.useOneWayGrouping = true;
        effector.surfaceArc = 120f;
        go.AddComponent<VisiblePlatformCollision>();
    }

    static void Ground(Tilemap map, int from, int to, Func<int, bool> gap)
    {
        for (int x = from; x <= to; x++)
        {
            if (gap != null && gap(x)) continue;
            map.SetTile(new Vector3Int(x, -1, 0), palette[x % 8]);
            map.SetTile(new Vector3Int(x, -2, 0), palette[16 + ((x + 30) % 8)]);
        }
    }

    static void Platform(Tilemap solid, Tilemap decor, int x0, int x1, int y)
    {
        for (int x = x0; x <= x1; x++)
            solid.SetTile(new Vector3Int(x, y, 0), palette[40 + (x % 8)]);
        TopEdge(solid, x0, x1, y);
        for (int cy = 0; cy < y; cy++)
        {
            decor.SetTile(new Vector3Int(x0, cy, 0), palette[48 + cy % 8]);
            decor.SetTile(new Vector3Int(x1, cy, 0), palette[48 + (cy + 3) % 8]);
        }
    }

    static void BuildExterior()
    {
        var scene = BaseScene(WorldScene, new Vector3(3f, 1.5f, 0));
        var root = new GameObject("Dusk World — Cemetery to Church").transform;
        var sky = Picture("Rose sunset and distant city", "SunsetWorld", new Vector3(72f, 11f, 0), -100, root);
        sky.transform.localScale = new Vector3(1.3f, 1f, 1f);
        sky.gameObject.AddComponent<WorldParallax>().horizontalFactor = .035f;
        var graves = Picture("Cemetery distant ruins", "CemeteryMidground",
            new Vector3(18f, 6f, 0), -55, root);
        graves.color = new Color(.68f, .56f, .65f, .9f);
        graves.gameObject.AddComponent<WorldParallax>().horizontalFactor = .12f;
        var roofs = Picture("Village distant rooftops", "VillageSkyline",
            new Vector3(63f, 8f, 0), -54, root);
        roofs.gameObject.AddComponent<WorldParallax>().horizontalFactor = .09f;
        var castleHills = Picture("Castle approach skyline", "VillageSkyline",
            new Vector3(123f, 8f, 0), -53, root);
        castleHills.color = new Color(.69f, .59f, .72f, .88f);
        castleHills.gameObject.AddComponent<WorldParallax>().horizontalFactor = .07f;
        Picture("Village facades west", "VillageFacades", new Vector3(51f, 8f, 0), -25, root);
        var east = Picture("Village facades east", "VillageFacades", new Vector3(76f, 8f, 0), -26, root);
        east.flipX = true;
        Picture("Grand church", "GrandChurch", new Vector3(110f, 10f, 0), -12, root);
        var grid = new GameObject("Hand-tiled Gothic architecture", typeof(Grid)).transform;
        grid.SetParent(root);
        var decor = Map("Arches and balcony supports", grid, -5, false);
        var walkable = Map("Visible walkable stone", grid, 10, true);
        var balconies = OneWayMap("Visible one-way balconies", grid, 11);
        Ground(walkable, 0, 148, x =>
            x >= 26 && x <= 28 || x >= 57 && x <= 59 ||
            x >= 84 && x <= 86 || x >= 119 && x <= 121);
        // Reachable alternate roofs and balconies. The visual supports touch the road.
        Platform(balconies, decor, 6, 13, 1);
        Platform(balconies, decor, 17, 23, 3);
        Platform(balconies, decor, 31, 39, 1);
        Platform(balconies, decor, 43, 52, 3);
        Platform(balconies, decor, 63, 72, 1);
        Platform(balconies, decor, 74, 82, 3);
        Platform(balconies, decor, 90, 99, 1);
        Platform(balconies, decor, 101, 107, 3);
        Platform(balconies, decor, 125, 133, 1);
        Platform(balconies, decor, 135, 142, 3);
        // A hidden lower ledge sits one unit below the street gap.
        for (int x = 26; x <= 28; x++)
            balconies.SetTile(new Vector3Int(x, -2, 0), palette[40 + x % 8]);
        TopEdge(balconies, 26, 28, -2);
        // Church entrance: direct interaction loads only the boss chamber.
        var door = new GameObject("Church door — E", typeof(WorldDoor));
        door.transform.SetParent(root);
        door.transform.position = new Vector3(110f, 1.5f, 0);
        var entrance = door.GetComponent<WorldDoor>();
        entrance.targetScene = "BossArena";
        entrance.arrivalPosition = new Vector3(4f, 1.5f, 0);
        entrance.prompt = GameText.Get("interaction.enter_church");
        entrance.promptKey = "interaction.enter_church";
        // Future paths are authored branches, clearly closed for now.
        Branch(root, GameText.Get("route.pier_closed"), 51f, 5.4f);
        Branch(root, GameText.Get("route.forest_closed"), 80f, 5.4f);
        var castle = Picture("Castle gate", "CastleGate", new Vector3(143f, 3.5f, 0), 12, root);
        var gateBox = castle.gameObject.AddComponent<BoxCollider2D>();
        gateBox.size = new Vector2(2.3f, 8f);
        var gate = castle.gameObject.AddComponent<CastleGateUnlock>();
        gate.blockingCollider = gateBox;
        Lantern(root, 5f);
        Lantern(root, 41f);
        Lantern(root, 88f);
        Lantern(root, 108f);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void Lantern(Transform root, float x)
    {
        var picture = Picture("Ash lantern checkpoint", "AshLantern",
            new Vector3(x, 1.3f, 0), 14, root);
        picture.gameObject.AddComponent<AshLantern>();
    }

    static void Branch(Transform root, string name, float x, float y)
    {
        var marker = new GameObject(name, typeof(SpriteRenderer));
        marker.transform.SetParent(root);
        marker.transform.position = new Vector3(x, y, 0);
        var renderer = marker.GetComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "CastleGate.png");
        renderer.sortingOrder = 12;
        marker.transform.localScale = new Vector3(.4f, .4f, 1f);
        var box = marker.AddComponent<BoxCollider2D>();
        box.size = new Vector2(4f, 8f);
    }

    static void BuildArena()
    {
        var scene = BaseScene(ArenaScene, new Vector3(4f, 1.5f, 0));
        var root = new GameObject("Church nave — single boss chamber").transform;
        var nave = Picture("Ruined nave at sunset", "BossNave", new Vector3(20f, 4.5f, 0), -30, root);
        nave.transform.localScale = new Vector3(1.7f, 1.5f, 1f);
        var grid = new GameObject("Arena stone", typeof(Grid)).transform;
        grid.SetParent(root);
        var floor = Map("Visible arena floor", grid, 10, true);
        Ground(floor, 0, 40, null);
        // The arena stands on a complete carved arcade, not an unsupported slab.
        for (int x = 0; x <= 40; x++)
        for (int y = -8; y <= -3; y++)
            floor.SetTile(new Vector3Int(x, y, 0),
                palette[y >= -5 ? 16 + (x + y + 32) % 8 : 48 + (x + y + 64) % 8]);
        var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/Boss.prefab");
        var boss = (GameObject)PrefabUtility.InstantiatePrefab(bossPrefab);
        boss.transform.SetParent(root);
        boss.transform.position = new Vector3(28f, 2f, 0);
        var arena = new GameObject("Boss combat area", typeof(BoxCollider2D), typeof(BossArenaController));
        arena.transform.SetParent(root);
        arena.transform.position = new Vector3(20f, 4f, 0);
        var area = arena.GetComponent<BoxCollider2D>();
        area.isTrigger = true;
        area.size = new Vector2(37f, 8f);
        var controller = arena.GetComponent<BossArenaController>();
        controller.boss = boss;
        controller.combatArea = area;
        var arrival = arena.AddComponent<BossArrival>();
        arrival.boss = boss.GetComponent<BossArenaCombat>();
        arrival.combatArea = area;
        var exit = new GameObject("Church exit — E", typeof(WorldDoor));
        exit.transform.SetParent(root);
        exit.transform.position = new Vector3(2f, 1.5f, 0);
        var door = exit.GetComponent<WorldDoor>();
        door.targetScene = "DuskWorld";
        door.arrivalPosition = new Vector3(111f, 1.5f, 0);
        door.prompt = GameText.Get("interaction.leave_church");
        door.promptKey = "interaction.leave_church";
        door.requiresBossDefeated = true;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
