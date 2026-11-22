using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Builds the cemetery and all five village districts into one continuous playable scene.
public static class AshenVillageWorldBuilder
{
    const string Art = "Assets/Art/AshenVillage/";
    const string ScenePath = "Assets/Scenes/AshenVillage.unity";
    static Tile[] tile;
    static Transform root;
    static Transform grid;
    static Tilemap ground, platforms, masonry, details, backRailings;
    static int backdropIndex;

    static Tile T(int id) { return tile[id]; }
    static void Put(Tilemap map, int x, int y, int id)
    { map.SetTile(new Vector3Int(x, y, 0), T(id)); }

    static Tilemap Map(string name, int order, bool solid)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(grid);
        go.GetComponent<TilemapRenderer>().sortingOrder = order;
        if (solid)
        {
            go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            var collider = go.AddComponent<TilemapCollider2D>();
            var composite = go.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            collider.usedByComposite = true;
        }
        return go.GetComponent<Tilemap>();
    }

    static void Soil(int a, int b, int topY, int style)
    {
        for (int x = a; x <= b; x++)
        {
            Put(ground, x, topY, style == 0 ? x % 4 : 20 + x % 4);
            Put(ground, x, topY - 1, 4 + x % 4);
        }
    }

    // A visible break in the street below a roof gap. Falling into it returns
    // the player to the last lantern instead of leaving them below the roofs.
    static void StreetChasm(int a, int b)
    {
        for (int x = a; x <= b; x++)
        for (int y = -3; y <= -2; y++)
            ground.SetTile(new Vector3Int(x, y, 0), null);
        var trigger = new GameObject("Street chasm " + a + "-" + b,
            typeof(BoxCollider2D), typeof(VillageFallPit));
        trigger.transform.SetParent(root);
        trigger.transform.position = new Vector3((a + b + 1) * .5f, -3.5f, 0f);
        var shape = trigger.GetComponent<BoxCollider2D>();
        shape.isTrigger = true;
        shape.size = new Vector2(b - a + 1, 1.5f);
    }

    static void Walkway(int a, int b, int y, bool wood,
        bool supportColumns = true)
    {
        for (int x = a; x <= b; x++)
        {
            Put(platforms, x, y, wood ? 46 + x % 2 : 20 + x % 4);
            if (supportColumns && ((x == a + 1 && a != 94) || x == b - 1 ||
                (b-a > 16 && (x-a) % 8 == 1)) && y <= 6)
                for (int cy = a == 214 ? -5 : -1; cy < y; cy++)
                    Put(details, x, cy, 32 + x % 4);
        }
        for (int x = a; x <= b; x++)
            Put(backRailings, x, y + 1, 40 + x % 4);
    }

    static void StoneArchUnderRoof(int left, int roofY)
    {
        for (int part = 0; part < 3; part++)
            Picture("Square arcade arch " + part,
                "Assets/Art/AshenGraveyard/BridgeArch" + part + ".png",
                left + 1.5f + part * 3f, roofY - 1.5f, -4,
                new Color(.85f, .81f, .79f, 1f));
        foreach (int x in new[] { left, left + 8 })
        for (int y = -1; y <= 0; y++)
            Put(details, x, y, 32 + x % 4);
    }

    static void Steps(int a, int y, int count, int direction)
    {
        for (int i = 0; i < count; i++)
        {
            int x = a + i;
            int level = y + i * direction;
            Put(platforms, x, level, 20 + x % 4);
            int bottom = level < -1 ? -6 : -1;
            for (int cy = bottom; cy < level; cy++)
                Put(platforms, x, cy, 8 + (x + cy + 32) % 4);
        }
    }

    static void EntranceStair()
    {
        // Two-cell-wide connected terraces, one tile of rise at each landing.
        for (int step = 0; step < 5; step++)
        for (int x = 86 + step * 2; x <= 87 + step * 2; x++)
        {
            int top = -1 + step;
            for (int y = -1; y < top; y++)
                Put(platforms, x, y, 8 + (x + y + 32) % 4);
            Put(platforms, x, top, 20 + x % 4);
        }
    }

    static SpriteRenderer Picture(string name, string path, float x, float y,
        int order, Color tint)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(root);
        go.transform.position = new Vector3(x, y, 0);
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sr.sprite == null) throw new Exception("Missing village sprite " + path);
        sr.sortingOrder = order;
        sr.color = tint;
        return sr;
    }

    static SpriteRenderer Facade(string name, string asset, float x, int order, Color tint)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            Art + "Prefabs/" + asset + ".prefab");
        if (prefab == null) throw new Exception("Missing facade prefab " + asset);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = name;
        go.transform.SetParent(root);
        var renderer = go.GetComponent<SpriteRenderer>();
        go.transform.position = new Vector3(x,
            -1f + renderer.sprite.bounds.size.y * .5f, 0f);
        renderer.sortingOrder = order;
        renderer.color = tint;
        return renderer;
    }

    static void Backdrop(string name, string asset, float center)
    {
        var sr = Picture(name, Art + "Backgrounds/" + asset + ".png",
            center, 5.25f, -82, new Color(.82f, .8f, .78f, .98f));
        sr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            Art + "BackdropFeather.mat");
        if (sr.sharedMaterial == null) throw new Exception("Missing backdrop feather material");
        sr.sortingOrder = -82 + backdropIndex++;
        sr.transform.localScale = new Vector3(1.8f, 1f, 1f);
        var backdrop = sr.gameObject.AddComponent<VillageDistrictBackdrop>();
        backdrop.districtCenter = center;
        backdrop.parallax = .035f;
    }

    static void BranchBackdrop(string name, string asset, float center, bool forest)
    {
        var sr = Picture(name, Art + "Backgrounds/" + asset + ".png",
            center, 5.25f, -70, new Color(.82f, .8f, .78f, 0f));
        sr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            Art + "BackdropFeather.mat");
        if (sr.sharedMaterial == null) throw new Exception("Missing backdrop feather material");
        sr.transform.localScale = new Vector3(1.8f, 1f, 1f);
        var backdrop = sr.gameObject.AddComponent<VillageBranchBackdrop>();
        backdrop.centerX = center;
        backdrop.forest = forest;
    }

    static void DistantArchitecture(string name, string asset, float x,
        float y, float center)
    {
        var sr = Picture(name, Art + asset + ".png", x, y, -61,
            new Color(.54f, .5f, .51f, .24f));
        sr.transform.localScale = Vector3.one * .82f;
        var parallax = sr.gameObject.AddComponent<VillageDistrictBackdrop>();
        parallax.districtCenter = center;
        parallax.baseAlpha = .24f;
        parallax.parallax = .09f;
        parallax.baseAlpha = .24f;
    }

    static void House(string name, string asset, float x, int order)
    {
        Facade(name, asset, x, order, new Color(.79f, .73f, .7f, .96f));
    }

    static void BuildingTiles(int a, int width, int height, int roofY, bool timber)
    {
        for (int x = a; x < a + width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x != a && x != a + width - 1) continue;
                int id = timber ? 12 + x % 4 : 32 + x % 4;
                Put(masonry, x, y, id);
            }
        }
        Put(masonry, a, roofY, 34);
        Put(masonry, a+width-1, roofY, 34);
    }

    static void Ruin(int x, int height)
    {
        for (int y = 0; y < height; y++)
            Put(masonry, x, y, y == height-1 ? 56 + x % 4 : 32 + x % 4);
        Put(masonry, x+1, height-1, 57);
        Put(masonry, x-1, 0, 58);
    }

    static void Lantern(string name, float x, float y)
    {
        var sr = Picture(name, "Assets/Art/NewWorld/AshLantern.png",
            x, y, 12, Color.white);
        var go = sr.gameObject;
        go.AddComponent<CircleCollider2D>().isTrigger = true;
        var lantern = go.AddComponent<AshLantern>();
        lantern.radius = 1.8f;
        lantern.spawnOffset = new Vector3(.7f, .7f, 0f);
    }

    static void ClosedGate(string name, float x, float y, string destination)
    {
        bool pier = destination == "location.pier";
        float floor = pier ? -5f : 5f;
        var gate = new GameObject(name, typeof(SpriteRenderer),
            typeof(BoxCollider2D), typeof(VillageBranchGate));
        gate.transform.SetParent(root);
        gate.transform.position = new Vector3(x, floor + 2.35f, 0);
        var renderer = gate.GetComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Art/AshenGraveyard/VillageGate.png");
        renderer.color = pier ?
            new Color(.76f, .69f, .63f, 1f) : Color.white;
        renderer.sortingOrder = 9;
        gate.GetComponent<BoxCollider2D>().size =
            new Vector2(renderer.sprite.bounds.size.x - .2f,
                renderer.sprite.bounds.size.y - .2f);
        gate.GetComponent<VillageBranchGate>().destinationName = GameText.Get(destination);
        gate.GetComponent<VillageBranchGate>().destinationKey = destination;
        gate.GetComponent<VillageBranchGate>().radius = 4f;
    }

    static void CemeterySeam()
    {
        var boundary = GameObject.Find("Invisible eastern boundary");
        if (boundary != null) UnityEngine.Object.DestroyImmediate(boundary);
        var gate = GameObject.Find("Ancient village gate");
        if (gate != null) UnityEngine.Object.DestroyImmediate(gate);
        var oldGround = GameObject.Find("Walkable cobblestone and soil").GetComponent<Tilemap>();
        for (int x = -12; x < 79; x++)
        {
            int variant = (x % 4 + 4) % 4;
            if (oldGround.HasTile(new Vector3Int(x, -1, 0)))
                oldGround.SetTile(new Vector3Int(x, -1, 0), T(variant));
            if (oldGround.HasTile(new Vector3Int(x, -2, 0)))
                oldGround.SetTile(new Vector3Int(x, -2, 0), T(4 + variant));
            if (oldGround.HasTile(new Vector3Int(x, 0, 0)))
                oldGround.SetTile(new Vector3Int(x, 0, 0), T(8 + variant));
        }
        oldGround.color = new Color(.82f, .84f, .84f, 1f);
        for (int x = 79; x <= 100; x++)
        for (int y = -3; y <= 0; y++) oldGround.SetTile(new Vector3Int(x, y, 0), null);
        oldGround.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
        oldGround.GetComponent<CompositeCollider2D>().GenerateGeometry();
        var oldBridges = GameObject.Find("Solid elevated stone bridges").GetComponent<Tilemap>();
        var cells = oldBridges.cellBounds;
        foreach (var cell in cells.allPositionsWithin)
            if (oldBridges.HasTile(cell)) oldBridges.SetTile(cell, T(20 + (cell.x % 4 + 4) % 4));
        oldBridges.color = new Color(.8f, .84f, .84f, 1f);
        oldBridges.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
        oldBridges.GetComponent<CompositeCollider2D>().GenerateGeometry();
        var tutorial = UnityEngine.Object.FindFirstObjectByType<GraveyardTutorial>();
        if (tutorial != null) tutorial.opensIntoVillage = true;
        var oldBounds = Camera.main.GetComponent<AshenGraveyardCameraBounds>();
        if (oldBounds != null) UnityEngine.Object.DestroyImmediate(oldBounds);
        var camera = Camera.main.gameObject.AddComponent<AshenVillageCameraBounds>();
        camera.maximumX = 342f;
        var respawn = UnityEngine.Object.FindFirstObjectByType<PlayerRespawn>();
        if (respawn != null) respawn.fallLimitY = -10f;
    }

    static void Outskirts()
    {
        Backdrop("Outskirts sunset and old roofs", "Outskirts", 104f);
        DistantArchitecture("Distant outskirts tenement", "LowerStreetHouse", 119f, 4f, 104f);
        Soil(79, 128, -2, 0);
        House("Collapsed house beyond first courtyard", "OutskirtsHouse", 102f, -16);
        EntranceStair();
        Walkway(94, 119, 3, false);
        Walkway(123, 128, 3, true);
        StreetChasm(120, 122);
        Lantern("Outskirts checkpoint lantern", 125f, 4.8f);
    }

    static void LowerStreets()
    {
        Backdrop("Lower streets layered facades", "LowerStreets", 155f);
        DistantArchitecture("Distant lower city roofline", "UpperTownhouse", 164f, 4f, 155f);
        Soil(129, 180, -2, 1);
        House("Tall tenement with damaged bay", "LowerStreetHouse", 140f, -16);
        House("Collapsed workshop beneath the balcony", "OutskirtsHouse", 157f, -16);
        House("Deep lower street house", "OutskirtsHouse", 168f, -18);
        Walkway(131, 146, 3, false);
        Walkway(150, 166, 3, false);
        Walkway(170, 179, 3, true);
        StreetChasm(147, 149);
        StreetChasm(167, 169);
    }

    static void CentralSquare()
    {
        Backdrop("Central square clock and sunset", "CentralSquare", 207f);
        DistantArchitecture("Distant square houses", "OutskirtsHouse", 222f, 4f, 207f);
        Soil(181, 216, -2, 1);
        StreetChasm(180, 182);
        House("Square guild hall", "SquareHall", 196f, -18);
        Walkway(183, 191, 3, false, false);
        StoneArchUnderRoof(183, 3);
        Steps(192, 2, 4, -1);
        // Ruined fountain: low, traversable masonry at the center of the square.
        for (int x = 197; x <= 201; x++) Put(platforms, x, -1, 20 + x % 4);
        Put(details, 199, 0, 33); Put(details, 199, 1, 56);
        Lantern("Central square checkpoint lantern", 204f, -.2f);
        // Main street rises over the pier fork. The lower route is reached from the square edge.
        Steps(211, -1, 4, 1);
        Walkway(214, 248, 2, false);
    }

    static void PierBranch()
    {
        BranchBackdrop("Harbor silhouettes, cranes and fog", "PierApproach", 233f, false);
        // Four open cells give the three-unit-tall heroine room to descend
        // below the bridge before she reaches its right-hand underside.
        for (int x = 216; x <= 219; x++)
        {
            platforms.SetTile(new Vector3Int(x, 2, 0), null);
            backRailings.SetTile(new Vector3Int(x, 3, 0), null);
        }
        Soil(217, 250, -6, 0);
        // Taper the old square's foundation down into the lower quay. This
        // closes the exposed left seam without occupying the iron climb shaft.
        for (int x = 211; x <= 216; x++)
        {
            int lowest = Mathf.Max(-7, -4 - (x - 211));
            for (int y = -4; y >= lowest; y--)
                Put(ground, x, y, 8 + (x + y + 32) % 4);
        }
        var ladder = new GameObject("Pier bridge iron climb",
            typeof(VillagePierLadder));
        ladder.transform.SetParent(root);
        ladder.transform.position = new Vector3(218.5f, -.5f, 0f);
        for (int section = 0; section < 2; section++)
        {
            var iron = Picture("Iron climb section " + section,
                "Assets/Art/AshenGraveyard/BridgeRailing.png",
                218.5f, -3f + section * 4f, -2,
                new Color(.7f, .65f, .62f, 1f));
            iron.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        }
        // The accessible lower quay ends at a sealed hatch inset in its
        // collidable timber deck; the future pier remains beyond this point.
        for (int x = 239; x <= 250; x++) Put(ground, x, -6, 46 + x % 2);
        // The raised waterfront street needs a visible retaining wall all the
        // way down to the dock. Its solid tiles also make the quay's end clear.
        for (int x = 249; x <= 251; x++)
        for (int y = -7; y <= -4; y++)
            Put(ground, x, y, 8 + (x + y + 32) % 4);
        for (int y = -5; y < 2; y++)
            details.SetTile(new Vector3Int(247, y, 0), null);
        Picture("Locked hatch toward the future pier",
            Art + "HarborHatch.png", 247f, -5.5f, 8, Color.white)
            .gameObject.AddComponent<VillageHarborHatch>();
    }

    static void UpperCity()
    {
        Backdrop("Upper city stacked roofscape", "UpperCity", 265f);
        DistantArchitecture("Upper city distant civic hall", "SquareHall", 271f, 5f, 265f);
        Soil(249, 291, -2, 1);
        House("High rooftop townhouse", "UpperTownhouse", 255f, -17);
        House("Upper city central townhouse", "UpperTownhouse", 269f, -16);
        House("Upper city distant tenement", "LowerStreetHouse", 280f, -18);
        Walkway(250, 261, 3, true);
        Walkway(264, 274, 4, false);
        StreetChasm(262, 263);
    }

    static void ForestBranch()
    {
        BranchBackdrop("Forest roots, sealed gate and mist", "DarkForestApproach", 294f, true);
        // An optional roof jump leads to the sealed forest gate. The church
        // route stays open on the street beneath this roof.
        Walkway(279, 295, 4, false);
        var route = new GameObject("Church street and forest fork",
            typeof(VillageChurchForkHint));
        route.transform.SetParent(root);
        route.transform.position = new Vector3(276f, 2f, 0f);
        Picture("Ash trees at the forest approach",
            "Assets/Art/AshenGraveyard/DeadAshTree.png", 294f, 5.2f, -9,
            new Color(.55f, .56f, .52f, .83f));
        ClosedGate("Broken gate to the dark forest", 292f, 6.2f,
            "location.forest");
        var forestGate = GameObject.Find("Broken gate to the dark forest")
            .GetComponent<VillageBranchGate>();
        forestGate.alternateRouteHint = GameText.Get("route.church_below");
        forestGate.alternateRouteHintKey = "route.church_below";
    }

    static void ChurchQuarter()
    {
        Backdrop("Church quarter sunset and spires", "ChurchQuarter", 319f);
        DistantArchitecture("Churchward distant tenement", "UpperTownhouse", 307f, 5f, 319f);
        Soil(292, 354, -2, 1);
        var church = Facade("Full gothic church exterior", "ChurchBuilt", 329f,
            -14, Color.white);
        // The church image has five transparent pixels below its visible
        // plinth. Align the painted stone, rather than the texture rectangle.
        church.transform.position += new Vector3(0f, -5f / 16f, 0f);
        var door = new GameObject("Church entrance to BossArena", typeof(WorldDoor));
        door.transform.SetParent(root);
        door.transform.position = new Vector3(329f, -.3f, 0);
        var passage = door.GetComponent<WorldDoor>();
        passage.targetScene = "BossArena";
        passage.arrivalPosition = new Vector3(4f, 1.5f, 0f);
        passage.interactionRadius = 2.4f;
        passage.prompt = GameText.Get("interaction.enter_church");
        passage.promptKey = "interaction.enter_church";
        Lantern("Church quarter checkpoint lantern", 311f, -.2f);
        var castleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Art/NewWorld/CastleGate.png");
        var castle = new GameObject("Castle gate after the boss",
            typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(CastleGateUnlock));
        castle.transform.SetParent(root);
        castle.transform.position = new Vector3(348f, 1f, 0f);
        castle.GetComponent<SpriteRenderer>().sprite = castleSprite;
        castle.GetComponent<SpriteRenderer>().sortingOrder = 10;
        var blocker = castle.GetComponent<BoxCollider2D>();
        blocker.size = new Vector2(2f, 6f);
        castle.GetComponent<CastleGateUnlock>().blockingCollider = blocker;
        // No castle region yet: camera ends at the far side of this visible gate.
        var edge = new GameObject("Castleward scene edge", typeof(BoxCollider2D));
        edge.transform.SetParent(root);
        edge.transform.position = new Vector3(354.5f, 3f, 0);
        edge.GetComponent<BoxCollider2D>().size = new Vector2(1f, 14f);
        for (int y = -2; y <= 5; y++) Put(details, 354, y, 8 + (y + 8) % 4);
        Put(details, 354, 6, 58);
    }

    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play Mode before building village");
        tile = new Tile[64];
        backdropIndex = 0;
        for (int i = 0; i < tile.Length; i++)
        {
            tile[i] = AssetDatabase.LoadAssetAtPath<Tile>(Art + "Tiles16/Village_" + i.ToString("D2") + ".asset");
            if (tile[i] == null) throw new Exception("Build village art first: tile " + i);
        }
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/AshenGraveyard.unity", OpenSceneMode.Single);
        CemeterySeam();
        root = new GameObject("Ashen Village — five connected districts").transform;
        grid = new GameObject("Village editable 16x16 Tilemaps", typeof(Grid)).transform;
        grid.SetParent(root);
        ground = Map("Solid cobblestone and ash soil", 5, true);
        platforms = Map("Solid roofs balconies stairs and bridges", 7, true);
        masonry = Map("Facade architecture windows and ruins", -15, false);
        backRailings = Map("Iron railings behind the heroine", -3, false);
        details = Map("Railings columns details and branch gates", 8, false);
        Outskirts();
        LowerStreets();
        CentralSquare();
        PierBranch();
        UpperCity();
        ForestBranch();
        ChurchQuarter();
        foreach (var map in new[] { ground, platforms })
        {
            map.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
            map.GetComponent<CompositeCollider2D>().GenerateGeometry();
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/Enemies/GraveWarden.prefab") != null)
            OrdinaryEnemyBuilder.Place("Grave Warden - burial lane", "GraveWarden", 61f);
        if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/Enemies/BellMarksman.prefab") != null)
            OrdinaryEnemyBuilder.Place("Bell Marksman - central roof", "BellMarksman", 184f);
        EditorSceneManager.SaveScene(scene, ScenePath, false);
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene("Assets/Scenes/BossArena.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/AshenGraveyard.unity", true)
        };
        AssetDatabase.SaveAssets();
        return "Built AshenVillage: five districts, pier and forest forks, church, castle gate, " +
            ground.cellBounds.size.x + " street cells, all in one scene.";
    }
}
