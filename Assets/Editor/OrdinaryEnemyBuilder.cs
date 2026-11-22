using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OrdinaryEnemyBuilder
{
    const string SpriteRoot = "Assets/sprites/Enemies/";
    const string PrefabRoot = "Assets/prefabs/Enemies/";
    const string GraveyardScene = "Assets/Scenes/AshenGraveyard.unity";
    const string VillageScene = "Assets/Scenes/AshenVillage.unity";

    readonly struct Pose
    {
        public readonly int x, y;
        public readonly float angle, height;
        public Pose(int x, int y, float angle = 0f, float height = 1f)
        { this.x = x; this.y = y; this.angle = angle; this.height = height; }
    }

    static readonly Pose[] Idle = { new Pose(0,0), new Pose(0,1), new Pose(0,0) };
    static readonly Pose[] Walk = {
        new Pose(0,0,-1), new Pose(-1,1,-2), new Pose(0,1,0),
        new Pose(0,0,1), new Pose(1,1,2), new Pose(0,1,0)
    };
    static readonly Pose[] WardenAttack = {
        new Pose(0,0), new Pose(1,-1,-2), new Pose(2,-2,-4),
        new Pose(-2,0,4), new Pose(-1,0,2), new Pose(0,0)
    };
    static readonly Pose[] MarksmanAttack = {
        new Pose(0,0), new Pose(0,0,-1), new Pose(1,-1,-2),
        new Pose(2,-1,3), new Pose(1,0,1), new Pose(0,0)
    };
    static readonly Pose[] Hurt = {
        new Pose(0,0), new Pose(2,-1,-7), new Pose(3,-1,-10), new Pose(1,0,-3)
    };
    static readonly Pose[] Death = {
        new Pose(0,0), new Pose(1,-1,8), new Pose(4,-2,20,.95f),
        new Pose(9,-3,40,.85f), new Pose(17,-4,58,.75f),
        new Pose(18,-5,68,.6f), new Pose(18,-5,68,.42f),
        new Pose(18,-6,68,.3f)
    };

    [MenuItem("Tools/Ashrisen/Enemies/Build two ordinary enemies")]
    public static void BuildAndPlace()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before building enemies.");
        BuildAssets();
        PlaceInScenes();
        Debug.Log("Built Grave Warden and Bell Marksman, then placed them in Graveyard and Village.");
    }

    [MenuItem("Tools/Ashrisen/Enemies/Build enemy art and prefabs")]
    public static void BuildAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before building enemy assets.");
        Directory.CreateDirectory(PrefabRoot);
        BuildFrames("GraveWarden", "GraveWardenMaster.png", 56, WardenAttack);
        BuildFrames("BellMarksman", "BellMarksmanMaster.png", 50, MarksmanAttack);
        BuildBolt();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureFrames("GraveWarden");
        ConfigureFrames("BellMarksman");
        ConfigureSprite(SpriteRoot + "BellMarksman/BellBolt.png", false);
        BuildBoltPrefab();
        BuildEnemyPrefab("GraveWarden", GothicEnemyKind.GraveWarden);
        BuildEnemyPrefab("BellMarksman", GothicEnemyKind.BellMarksman);
        AssetDatabase.SaveAssets();
    }

    static void BuildFrames(string folder, string sourceName, int targetHeight, Pose[] attack)
    {
        string path = SpriteRoot + folder + "/";
        Directory.CreateDirectory(path + "Frames");
        string sourcePath = path + "Source/" + sourceName;
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Missing original enemy artwork", sourcePath);
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!source.LoadImage(File.ReadAllBytes(sourcePath)))
            throw new InvalidOperationException("Could not decode " + sourcePath);
        Color32[] pixels = source.GetPixels32();
        int minX = source.width, minY = source.height, maxX = -1, maxY = -1;
        for (int y = 0; y < source.height; y++)
        for (int x = 0; x < source.width; x++)
        {
            if (pixels[y * source.width + x].a < 128) continue;
            minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
            maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
        }
        if (maxX < minX) throw new InvalidOperationException("Enemy artwork has no opaque pixels: " + sourcePath);
        int boundsWidth = maxX - minX + 1, boundsHeight = maxY - minY + 1;
        float scale = Mathf.Min(58f / boundsWidth, targetHeight / (float)boundsHeight);
        int width = Mathf.Max(1, Mathf.RoundToInt(boundsWidth * scale));
        int height = Mathf.Max(1, Mathf.RoundToInt(boundsHeight * scale));
        var basePixels = new Color32[64 * 64];
        int left = (64 - width) / 2;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int sx = Mathf.Clamp(minX + Mathf.FloorToInt((x + .5f) * boundsWidth / width), minX, maxX);
            int sy = Mathf.Clamp(minY + Mathf.FloorToInt((y + .5f) * boundsHeight / height), minY, maxY);
            Color32 color = pixels[sy * source.width + sx];
            if (color.a < 128) continue;
            // Fixed stepped channels retain hard pixels and a restrained palette after downsampling.
            basePixels[y * 64 + left + x] = new Color32(
                Step(color.r), Step(color.g), Step(color.b), 255);
        }
        UnityEngine.Object.DestroyImmediate(source);
        bool warden = folder == "GraveWarden";
        WriteFrames(path + "Frames/", "Idle", basePixels, Idle, warden);
        WriteFrames(path + "Frames/", "Walk", basePixels, Walk, warden);
        WriteFrames(path + "Frames/", "Attack", basePixels, attack, warden);
        WriteFrames(path + "Frames/", "Hurt", basePixels, Hurt, warden);
        WriteFrames(path + "Frames/", "Death", basePixels, Death, warden);
    }

    static byte Step(byte channel) => (byte)Mathf.Clamp(Mathf.RoundToInt(channel / 12f) * 12, 0, 255);

    static void WriteFrames(string folder, string name, Color32[] source, Pose[] poses, bool warden)
    {
        for (int frame = 0; frame < poses.Length; frame++)
        {
            Pose pose = poses[frame];
            float angle = pose.angle * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
            var pixels = new Color32[64 * 64];
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float dx = x - 32f - pose.x, dy = y - 5f - pose.y;
                int sx = Mathf.RoundToInt(32f + dx * cosine + dy * sine);
                int sy = Mathf.RoundToInt(5f + (-dx * sine + dy * cosine) / pose.height);
                if (sx >= 0 && sx < 64 && sy >= 0 && sy < 64)
                    pixels[y * 64 + x] = source[sy * 64 + sx];
            }
            if (name == "Walk") AnimateStride(pixels, frame, warden);
            if (name == "Attack") AnimateWeapon(pixels, frame, warden, pose);
            if (name == "Death" && frame >= 5)
            {
                float vanish = frame == 5 ? .12f : frame == 6 ? .42f : .72f;
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    int index = y * 64 + x;
                    if (pixels[index].a == 0) continue;
                    uint hash = (uint)(x * 73856093 ^ y * 19349663);
                    if (hash % 1000 < vanish * 1000f) pixels[index] = default;
                }
            }
            SavePng(folder + name + "_" + frame + ".png", pixels, 64, 64);
        }
    }

    static void AnimateStride(Color32[] pixels, int frame, bool warden)
    {
        var original = (Color32[])pixels.Clone();
        int split = warden ? 35 : 41;
        int top = warden ? 14 : 16;
        int[] steps = { -3, -1, 1, 3, 1, -1 };
        int leftShift = steps[frame], rightShift = -steps[frame];
        for (int y = 0; y < top; y++)
        for (int x = 0; x < 64; x++) pixels[y * 64 + x] = default;
        for (int y = 0; y < top; y++)
        for (int x = 0; x < 64; x++)
        {
            Color32 color = original[y * 64 + x];
            if (color.a == 0) continue;
            int nextX = x + (x < split ? leftShift : rightShift);
            int nextY = y + ((frame == 1 || frame == 4) && x < split ? 1 : 0);
            if (nextX >= 0 && nextX < 64 && nextY < 64)
                pixels[nextY * 64 + nextX] = color;
        }
    }

    static void AnimateWeapon(Color32[] pixels, int frame, bool warden, Pose pose)
    {
        float[] angle = warden ? new[] { 0f, -15f, -27f, 31f, 15f, 0f } :
            new[] { 0f, -7f, -13f, 10f, 5f, 0f };
        if (angle[frame] == 0f) return;
        var original = (Color32[])pixels.Clone();
        int edgeX = (warden ? 27 : 42) + pose.x;
        int lowerY = (warden ? 17 : 34) + pose.y;
        int upperY = (warden ? 32 : 52) + pose.y;
        int pivotX = (warden ? 27 : 43) + pose.x;
        int pivotY = (warden ? 25 : 42) + pose.y;
        float radians = angle[frame] * Mathf.Deg2Rad;
        float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
        for (int y = Mathf.Max(0, lowerY); y <= Mathf.Min(63, upperY); y++)
        for (int x = 0; x <= Mathf.Min(63, edgeX); x++) pixels[y * 64 + x] = default;
        for (int y = Mathf.Max(0, lowerY); y <= Mathf.Min(63, upperY); y++)
        for (int x = 0; x <= Mathf.Min(63, edgeX); x++)
        {
            Color32 color = original[y * 64 + x];
            if (color.a == 0) continue;
            float dx = x - pivotX, dy = y - pivotY;
            int nextX = Mathf.RoundToInt(pivotX + dx * c - dy * s + (frame == 3 ? 1 : 0));
            int nextY = Mathf.RoundToInt(pivotY + dx * s + dy * c);
            if (nextX >= 0 && nextX < 64 && nextY >= 0 && nextY < 64)
                pixels[nextY * 64 + nextX] = color;
        }
    }

    static void SavePng(string path, Color32[] pixels, int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    static void ConfigureFrames(string folder)
    {
        foreach (string file in Directory.GetFiles(SpriteRoot + folder + "/Frames", "*.png"))
            ConfigureSprite(file.Replace('\\', '/'), true);
    }

    static void ConfigureSprite(string path, bool bottomPivot)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 16;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = bottomPivot ? new Vector2(.5f, 0f) : new Vector2(.5f, .5f);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    static void BuildBolt()
    {
        var pixels = new Color32[20 * 8];
        var edge = new Color32(55, 44, 45, 255);
        var metal = new Color32(151, 143, 135, 255);
        var glint = new Color32(215, 194, 174, 255);
        var darkRed = new Color32(95, 37, 43, 255);
        for (int x = 1; x <= 16; x++)
        {
            pixels[3 * 20 + x] = edge;
            pixels[4 * 20 + x] = x % 3 == 0 ? glint : metal;
        }
        for (int x = 15; x <= 19; x++)
        for (int y = 1; y <= 6; y++)
            if (Mathf.Abs(y - 3.5f) <= (19 - x) * .65f + .4f)
                pixels[y * 20 + x] = y == 3 || y == 4 ? glint : metal;
        pixels[2 * 20 + 3] = darkRed;
        pixels[5 * 20 + 3] = darkRed;
        SavePng(SpriteRoot + "BellMarksman/BellBolt.png", pixels, 20, 8);
    }

    static Sprite[] Frames(string folder, string state, int count)
    {
        var result = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            string path = SpriteRoot + folder + "/Frames/" + state + "_" + i + ".png";
            result[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (result[i] == null) throw new InvalidOperationException("Sprite missing: " + path);
        }
        return result;
    }

    static void BuildBoltPrefab()
    {
        var gameObject = new GameObject("Bell bolt", typeof(SpriteRenderer),
            typeof(CircleCollider2D), typeof(GothicBolt));
        var renderer = gameObject.GetComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + "BellMarksman/BellBolt.png");
        renderer.sortingOrder = 14;
        var hitbox = gameObject.GetComponent<CircleCollider2D>();
        hitbox.isTrigger = true;
        hitbox.radius = .23f;
        PrefabUtility.SaveAsPrefabAsset(gameObject, PrefabRoot + "BellBolt.prefab");
        UnityEngine.Object.DestroyImmediate(gameObject);
    }

    static void BuildEnemyPrefab(string folder, GothicEnemyKind kind)
    {
        var gameObject = new GameObject(kind == GothicEnemyKind.GraveWarden ? "Grave Warden" : "Bell Marksman",
            typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CapsuleCollider2D), typeof(GothicEnemy));
        var renderer = gameObject.GetComponent<SpriteRenderer>();
        renderer.sortingOrder = 10;
        renderer.sprite = Frames(folder, "Idle", 3)[0];
        var body = gameObject.GetComponent<Rigidbody2D>();
        body.gravityScale = 3f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        var shape = gameObject.GetComponent<CapsuleCollider2D>();
        shape.direction = CapsuleDirection2D.Vertical;
        shape.size = kind == GothicEnemyKind.GraveWarden ? new Vector2(.88f, 2.75f) : new Vector2(.72f, 2.55f);
        shape.offset = new Vector2(0f, shape.size.y * .5f);
        var hurt = new GameObject("Sword hurtbox", typeof(BoxCollider2D));
        hurt.transform.SetParent(gameObject.transform, false);
        var hurtCollider = hurt.GetComponent<BoxCollider2D>();
        hurtCollider.isTrigger = true;
        hurtCollider.size = kind == GothicEnemyKind.GraveWarden ? new Vector2(1.3f, 3.1f) : new Vector2(1.15f, 2.95f);
        hurtCollider.offset = new Vector2(0f, hurtCollider.size.y * .5f);
        var enemy = gameObject.GetComponent<GothicEnemy>();
        enemy.kind = kind;
        enemy.maxHealth = kind == GothicEnemyKind.GraveWarden ? 4 : 3;
        enemy.speed = kind == GothicEnemyKind.GraveWarden ? 2.2f : 1.4f;
        enemy.detection = kind == GothicEnemyKind.GraveWarden ? 7.5f : 12f;
        enemy.leash = kind == GothicEnemyKind.GraveWarden ? 5f : 2.5f;
        enemy.hurtbox = hurtCollider;
        enemy.idle = Frames(folder, "Idle", 3);
        enemy.walk = Frames(folder, "Walk", 6);
        enemy.attack = Frames(folder, "Attack", 6);
        enemy.hurt = Frames(folder, "Hurt", 4);
        enemy.death = Frames(folder, "Death", 8);
        if (kind == GothicEnemyKind.BellMarksman)
            enemy.boltPrefab = AssetDatabase.LoadAssetAtPath<GothicBolt>(PrefabRoot + "BellBolt.prefab");
        PrefabUtility.SaveAsPrefabAsset(gameObject, PrefabRoot + folder + ".prefab");
        UnityEngine.Object.DestroyImmediate(gameObject);
    }

    [MenuItem("Tools/Ashrisen/Enemies/Place two ordinary enemies")]
    public static void PlaceInScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before placing enemies.");
        string previous = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the currently open scene before placing enemies.");
        try
        {
            var graveyard = EditorSceneManager.OpenScene(GraveyardScene, OpenSceneMode.Single);
            Place("Grave Warden - burial lane", "GraveWarden", 61f);
            EditorSceneManager.SaveScene(graveyard);
            var village = EditorSceneManager.OpenScene(VillageScene, OpenSceneMode.Single);
            Place("Grave Warden - burial lane", "GraveWarden", 61f);
            Place("Bell Marksman - central roof", "BellMarksman", 185.5f);
            EditorSceneManager.SaveScene(village);
        }
        finally
        {
            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        }
    }

    public static void Place(string name, string prefabName, float x)
    {
        var root = GameObject.Find("Ordinary Enemy Encounters");
        if (root == null) root = new GameObject("Ordinary Enemy Encounters");
        if (root.transform.Find(name) != null) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + prefabName + ".prefab");
        if (prefab == null) throw new InvalidOperationException("Build enemy prefab first: " + prefabName);
        Physics2D.SyncTransforms();
        float ground = float.NegativeInfinity;
        foreach (var hit in Physics2D.RaycastAll(new Vector2(x, 15f), Vector2.down, 28f))
        {
            if (hit.collider == null || hit.collider.isTrigger ||
                hit.collider.GetComponentInParent<playercontroler>() != null ||
                hit.collider.GetComponentInParent<GothicEnemy>() != null ||
                hit.collider.GetComponentInParent<AshHound>() != null) continue;
            ground = Mathf.Max(ground, hit.point.y);
        }
        if (float.IsNegativeInfinity(ground)) throw new InvalidOperationException("No walkable surface at " + x);
        var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        enemy.name = name;
        enemy.transform.SetParent(root.transform);
        enemy.transform.position = new Vector3(x, ground + .03f, 0f);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
