#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

public static class CompactCathedralBuilder
{
    const string Art = "Assets/Art/DarkChurch/";
    const string WallArt = "Assets/Art/ReferenceChurch/ChurchWall.png";
    const float Width = 26f;

    [MenuItem("Tools/Ashrisen/Build Screenshot Church")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        Directory.CreateDirectory(Art + "Tiles");
        ImportSprite(WallArt, 63, new Vector2(.5f, .5f));
        var tiles = BuildTiles();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/BossArena.unity");
        Transform root = null;
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == "THE ASHEN SANCTUM" || go.name == "COMPACT GOTHIC CATHEDRAL" || go.name == "ABANDONED CHURCH ARENA" || go.name == "SCREENSHOT CHURCH ARENA" || go.name.StartsWith("Church nave")) root = go.transform;
        if (root == null) throw new InvalidOperationException("Arena root not found.");
        root.name = "SCREENSHOT CHURCH ARENA";
        foreach (string name in new[] { "Distant Sky", "Cathedral Architecture", "Main Interior", "Foreground Details", "Ceiling ash and combat reactions", "Subtle ash and combat response", "Quiet church atmosphere", "Background", "Midground", "Gameplay Floor", "Foreground" }) Clear(root, name);

        var background = Group(root, "Background");
        var wall = Picture(background, "Broken statues and Gothic pillars", WallArt, 13f, 6.4f, -45, 1f);
        wall.transform.localScale = new Vector3(1f, 1.13f, 1f);
        wall.GetComponent<SpriteRenderer>().color = new Color(.96f, .96f, .96f);

        var gameplay = Group(root, "Gameplay Floor");
        var grid = new GameObject("16x16 stone grid", typeof(Grid));
        grid.transform.SetParent(gameplay, false);
        var groundObject = new GameObject("Continuous cathedral paving", typeof(Tilemap), typeof(TilemapRenderer), typeof(BoxCollider2D));
        groundObject.transform.SetParent(grid.transform, false);
        var ground = groundObject.GetComponent<Tilemap>();
        var floorCollider = groundObject.GetComponent<BoxCollider2D>();
        floorCollider.offset = new Vector2(Width * .5f, -1f);
        floorCollider.size = new Vector2(Width, 2f);
        groundObject.GetComponent<TilemapRenderer>().sortingOrder = -8;
        ground.color = new Color(.83f, .84f, .86f);
        for (int x = 0; x < Width; x++)
        {
            ground.SetTile(new Vector3Int(x, -1, 0), tiles[x % 6 == 0 ? 4 + (x % 4) : (x * 3 + 1) % 4]);
            ground.SetTile(new Vector3Int(x, -2, 0), tiles[(x * 5 + 2) % 4]);
        }
        Boundary(gameplay, "West stone pier", -.35f);
        Boundary(gameplay, "East sanctuary pier", 26.35f);

        var arena = root.Find("Boss combat area")?.GetComponent<BossArenaController>();
        if (arena == null || arena.boss == null) throw new InvalidOperationException("Boss combat controller not found.");
        arena.transform.position = new Vector3(13, 4, 0);
        var area = arena.GetComponent<BoxCollider2D>();
        area.size = new Vector2(24, 8); arena.combatArea = area;
        var boss = arena.boss.GetComponent<BossArenaCombat>();
        arena.boss.transform.position = new Vector3(21.5f, 2f, 0);
        boss.maxHealth = 26;
        boss.walkSpeed = 6.5f;
        boss.chaseAcceleration = 26f;
        boss.detectionRange = 24f;
        boss.attackRange = 2.1f;
        boss.attackCooldown = 1.2f;
        boss.attackAnimationSpeed = 1.5f;
        boss.walkAnimationSpeed = 2.8f;
        var arrival = arena.GetComponent<BossArrival>();
        arrival.approachDistance = 3.5f;
        var player = UnityEngine.Object.FindAnyObjectByType<playercontroler>();
        player.transform.position = new Vector3(5.2f, 1.5f, 0);
        SmoothPlayerCollision(player);
        player.jumpForce = 7f;
        var dash = player.GetComponent<PlayerDash>();
        if (dash != null) dash.dashSpeed = 18f;
        var exit = root.Find("Church exit — E");
        if (exit == null) foreach (Transform child in root) if (child.GetComponent<WorldDoor>() != null) exit = child;
        if (exit != null) exit.position = new Vector3(1.5f, 1.5f, 0);

        var camera = Camera.main;
        if (camera == null) throw new InvalidOperationException("Main Camera missing.");
        var pixelPerfect = camera.GetComponent<PixelPerfectCamera>();
        if (pixelPerfect != null) pixelPerfect.enabled = false;
        camera.orthographicSize = 6.4f;
        camera.backgroundColor = new Color(.025f, .023f, .025f);
        camera.transform.position = new Vector3(13, 6.2f, -10);
        var follow = camera.GetComponent<camerafollow>();
        if (follow != null) { follow.offset = new Vector2(7.8f, 3.7f); follow.verticalSmoothTime = .5f; }
        var cameraBounds = camera.GetComponent<AshenSanctumCamera>() ?? camera.gameObject.AddComponent<AshenSanctumCamera>();
        cameraBounds.left = 0; cameraBounds.right = Width; cameraBounds.restingHeight = 6.2f; cameraBounds.arrival = arrival;

        var global = UnityEngine.Object.FindAnyObjectByType<Light2D>();
        if (global != null) { global.color = new Color(.96f, .96f, .96f); global.intensity = .98f; }

        var effectsObject = new GameObject("Quiet church atmosphere", typeof(AshenSanctumArena));
        effectsObject.transform.SetParent(root, false);
        var effects = effectsObject.GetComponent<AshenSanctumArena>();
        effects.controller = arena;
        effects.ceilingAsh = Particles(effectsObject.transform, "Sparse falling ash", 12.1f, false);
        foreach (var component in arena.GetComponents<MonoBehaviour>())
            if (component != null && component.GetType().Name == "BossAshOnDefeat")
                UnityEngine.Object.DestroyImmediate(component);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        BuildPalette(tiles);
        AssetDatabase.SaveAssets();
        Debug.Log("Screenshot church built with original claw-only boss combat and normal player jump.");
    }

    [MenuItem("Tools/Ashrisen/Boss/Repair Arena Collision")]
    public static void RepairCollision()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before repairing the arena.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/BossArena.unity");
        var floor = GameObject.Find("Continuous cathedral paving");
        var player = UnityEngine.Object.FindAnyObjectByType<playercontroler>();
        if (floor == null || player == null)
            throw new InvalidOperationException("Boss arena floor or heroine is missing.");

        // TilemapCollider2D exposes each paving tile as a separate contact edge.
        // One box keeps the same visible floor and gives both fighters a flat surface.
        var oldTiles = floor.GetComponent<TilemapCollider2D>();
        if (oldTiles != null) UnityEngine.Object.DestroyImmediate(oldTiles);
        var floorCollider = floor.GetComponent<BoxCollider2D>() ?? floor.AddComponent<BoxCollider2D>();
        floorCollider.offset = new Vector2(Width * .5f, -1f);
        floorCollider.size = new Vector2(Width, 2f);
        floorCollider.edgeRadius = 0f;
        SmoothPlayerCollision(player);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Boss arena collision repaired: one continuous floor and rounded heroine collider.");
    }

    static void SmoothPlayerCollision(playercontroler player)
    {
        var box = player.GetComponent<BoxCollider2D>();
        var capsule = player.GetComponent<CapsuleCollider2D>();
        if (capsule == null) capsule = player.gameObject.AddComponent<CapsuleCollider2D>();
        if (box != null)
        {
            capsule.size = box.size;
            capsule.offset = box.offset;
            capsule.sharedMaterial = box.sharedMaterial;
            UnityEngine.Object.DestroyImmediate(box);
        }
        capsule.direction = CapsuleDirection2D.Vertical;
        player.GetComponent<Rigidbody2D>().collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    static Transform Group(Transform parent, string name)
    { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }
    static void Clear(Transform parent, string name)
    { Transform child; while ((child = parent.Find(name)) != null) UnityEngine.Object.DestroyImmediate(child.gameObject); }
    static GameObject Picture(Transform parent, string name, string path, float x, float y, int order, float scale)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(parent, false); go.transform.position = new Vector3(x, y, 0); go.transform.localScale = Vector3.one * scale;
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path); renderer.sortingOrder = order;
        if (renderer.sprite == null) throw new FileNotFoundException("Sprite missing: " + path);
        return go;
    }
    static void Boundary(Transform parent, string name, float x)
    {
        var go = new GameObject(name, typeof(BoxCollider2D)); go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, 5f, 0); go.GetComponent<BoxCollider2D>().size = new Vector2(.7f, 14f);
    }
    static ParticleSystem Particles(Transform parent, string name, float y, bool debris)
    {
        var go = new GameObject(name, typeof(ParticleSystem)); go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(13, y, 0);
        var p = go.GetComponent<ParticleSystem>(); p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = p.main; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = debris ? 80 : 200;
        main.startLifetime = debris ? 2.2f : 10f; main.startSpeed = debris ? .5f : .2f;
        main.startSize = debris ? .13f : .055f;
        main.startColor = debris ? new Color(.46f, .39f, .35f, .7f) : new Color(.69f, .61f, .53f, .45f);
        if (debris) main.gravityModifier = .28f;
        var shape = p.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(22, .5f, 0);
        var emission = p.emission; emission.rateOverTime = debris ? 0 : .6f;
        var velocity = p.velocityOverLifetime; velocity.enabled = !debris;
        if (!debris) { velocity.space = ParticleSystemSimulationSpace.World; velocity.y = -.7f; }
        var renderer = p.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        renderer.sortingOrder = 3;
        return p;
    }
    static void ImportSprite(string path, float ppu, Vector2 pivot)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new FileNotFoundException(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu; importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = pivot;
        importer.SetTextureSettings(settings); importer.SaveAndReimport();
    }
    static Tile[] BuildTiles()
    {
        var image = new Texture2D(2, 2); image.LoadImage(File.ReadAllBytes(Art + "FloorTilesSheet.png"));
        var result = new Tile[16];
        for (int i = 0; i < 16; i++)
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            int column = i % 4, row = i / 4;
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            {
                float u = (column + .025f + .95f * (x + .5f) / 16f) / 4f;
                float v = (3 - row + .025f + .95f * (y + .5f) / 16f) / 4f;
                texture.SetPixel(x, y, image.GetPixelBilinear(u, v));
            }
            texture.Apply();
            string path = Art + "Tiles/Paving_" + i.ToString("00") + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            ImportSprite(path, 16, new Vector2(.5f, .5f));
            string assetPath = Art + "Tiles/Paving_" + i.ToString("00") + ".asset";
            result[i] = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
            if (result[i] == null) { result[i] = ScriptableObject.CreateInstance<Tile>(); AssetDatabase.CreateAsset(result[i], assetPath); }
            result[i].sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            result[i].colliderType = Tile.ColliderType.Grid;
            EditorUtility.SetDirty(result[i]);
        }
        UnityEngine.Object.DestroyImmediate(image);
        return result;
    }
    static void BuildPalette(Tile[] tiles)
    {
        var root = new GameObject("Dark Church 16x16 Palette", typeof(Grid));
        var child = new GameObject("Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
        child.transform.SetParent(root.transform, false);
        var map = child.GetComponent<Tilemap>();
        for (int i = 0; i < tiles.Length; i++) map.SetTile(new Vector3Int(i % 4, 3 - i / 4, 0), tiles[i]);
        string path = Art + "DarkChurchPalette.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        var settingsType = Type.GetType("UnityEditor.GridPalette, UnityEditor.TilemapModule");
        if (settingsType == null) return;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path)) if (asset.GetType() == settingsType) return;
        var settings = ScriptableObject.CreateInstance(settingsType);
        settings.name = "Palette Settings";
        AssetDatabase.AddObjectToAsset(settings, path);
    }
}
#endif
