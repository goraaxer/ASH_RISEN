using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Exact 16 px modules; scenery cutouts are downsampled to the same 16 PPU scale.
public static class AshenVillageArtBuilder
{
    public const string Root = "Assets/Art/AshenVillage/";
    public const string TileRoot = Root + "Tiles16/";
    static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    static readonly Color32 Mortar = new Color32(42, 39, 42, 255);
    static readonly Color32 Shadow = new Color32(51, 47, 49, 255);
    static readonly Color32 Stone = new Color32(76, 66, 63, 255);
    static readonly Color32 Face = new Color32(96, 79, 72, 255);
    static readonly Color32 Pale = new Color32(125, 105, 91, 255);
    static readonly Color32 Ash = new Color32(91, 88, 85, 255);
    static readonly Color32 Slate = new Color32(57, 58, 64, 255);
    static readonly Color32 SlateLight = new Color32(85, 83, 84, 255);
    static readonly Color32 Wood = new Color32(76, 53, 48, 255);
    static readonly Color32 WoodLight = new Color32(113, 74, 59, 255);
    static readonly Color32 Ember = new Color32(201, 135, 71, 255);

    static void Dot(Color32[] pixels, int x, int y, Color32 color)
    {
        if (x >= 0 && x < 16 && y >= 0 && y < 16) pixels[y * 16 + x] = color;
    }

    static void Rect(Color32[] pixels, int x0, int y0, int x1, int y1, Color32 color)
    {
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Dot(pixels, x, y, color);
    }

    static void Brick(Color32[] pixels, int x, int y, int width, int height, int seed)
    {
        Rect(pixels, x, y, x + width - 1, y + height - 1, Mortar);
        Rect(pixels, x + 1, y + 1, x + width - 2, y + height - 2,
            seed % 3 == 0 ? Face : seed % 3 == 1 ? Stone : Ash);
        Rect(pixels, x + 1, y + height - 2, x + width - 2, y + height - 2, Pale);
        Dot(pixels, x + width - 3, y + 1, Shadow);
    }

    static Color32[] DrawTile(int id)
    {
        var pixels = new Color32[256];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Clear;
        int v = id % 4;
        if (id < 8) // two-cell street section: cobbles above compact ash fill
        {
            Rect(pixels, 0, 0, 15, 15, Shadow);
            for (int y = 0; y < 11; y++) for (int x = 0; x < 16; x++)
                if ((x * 23 + y * 37 + id * 11 + x * y) % 47 < 2)
                    Dot(pixels, x, y, id % 3 == 0 ? Stone : Ash);
            if (id < 4)
            {
                Rect(pixels, 0, 10, 15, 10, Mortar);
                for (int n = -1; n < 5; n++)
                    Brick(pixels, n * 5 + (v % 2), 11, 5, 5, n + id);
                for (int x = 0; x < 16; x++) Dot(pixels, x, 15, x % 5 == v ? Pale : Stone);
            }
        }
        else if (id < 12) // small coursed brick wall
        {
            Rect(pixels, 0, 0, 15, 15, Mortar);
            for (int row = 0; row < 4; row++)
                for (int x = -8 + ((row + v) % 2) * 5; x < 16; x += 10)
                    Brick(pixels, x, row * 4, 10, 4, x + row + id);
            Dot(pixels, 3 + v * 3, 8, Shadow);
        }
        else if (id < 16) // timber framework and joinery
        {
            Rect(pixels, 0, 0, 15, 15, Stone);
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                if ((x * 7 + y * 13 + id) % 17 == 0) Dot(pixels, x, y, Shadow);
            Rect(pixels, v < 2 ? 1 : 12, 0, v < 2 ? 3 : 14, 15, Wood);
            Rect(pixels, 0, v % 2 == 0 ? 2 : 12, 15, v % 2 == 0 ? 4 : 14, WoodLight);
            for (int y = 1; y < 15; y++) Dot(pixels, v < 2 ? y : 15 - y, y, Wood);
        }
        else if (id < 20) // overlapping slate roof courses
        {
            Rect(pixels, 0, 0, 15, 15, Slate);
            for (int row = 0; row < 4; row++)
                for (int x = -3 + ((row + v) % 2) * 3; x < 16; x += 6)
                {
                    Rect(pixels, x, row * 4 + 2, x + 5, row * 4 + 2, Mortar);
                    Rect(pixels, x + 1, row * 4 + 3, x + 4, row * 4 + 3, SlateLight);
                    Dot(pixels, x, row * 4 + 1, Shadow);
                }
        }
        else if (id < 24) // visible solid platform lip
        {
            Rect(pixels, 0, 0, 15, 15, Stone);
            Rect(pixels, 0, 12, 15, 15, Face);
            Rect(pixels, 0, 15, 15, 15, Pale);
            Rect(pixels, 0, 11, 15, 11, Mortar);
            for (int x = 1 + v; x < 16; x += 6) Rect(pixels, x, 4, x + 2, 7, Shadow);
            Rect(pixels, 0, 1, 15, 1, Mortar);
        }
        else if (id < 28) // chipped edges and stone transitions
        {
            Rect(pixels, 0, 0, 15, 15, Shadow);
            Rect(pixels, 0, 11, 15, 15, Stone);
            Rect(pixels, 0, 15, 15, 15, Pale);
            for (int x = 0; x < 16; x++)
                if ((x * 7 + id * 3) % 11 < 3) Dot(pixels, x, 14, Clear);
            Rect(pixels, v < 2 ? 0 : 13, 0, v < 2 ? 2 : 15, 10, Face);
        }
        else if (id < 32) // segmented pointed arches, decorative only
        {
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                int dx = Math.Abs(x - 8);
                bool stone = y > 12 || dx > 5 + (y / 5) || (v < 2 && x < 3);
                if (stone) Dot(pixels, x, y, (x + y + v) % 6 == 0 ? Pale : Stone);
            }
            for (int x = 0; x < 16; x++) Dot(pixels, x, 15, Face);
        }
        else if (id < 36) // fluted columns and capitals
        {
            Rect(pixels, 3, 0, 12, 15, Stone);
            Rect(pixels, 0, 12, 15, 15, Face);
            Rect(pixels, 0, 13, 15, 13, Pale);
            Rect(pixels, 2, 0, 13, 2, Mortar);
            for (int x = 5; x <= 11; x += 3) Rect(pixels, x, 3, x, 11, x == 5 ? Pale : Shadow);
            Dot(pixels, 2 + v * 3, 14, Mortar);
        }
        else if (id < 40) // stair and corner modules
        {
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    if (y < 4 + (v < 2 ? x : 15 - x) / 2)
                        Dot(pixels, x, y, Stone);
            for (int step = 0; step < 4; step++)
                Rect(pixels, v < 2 ? step * 4 : 0, 4 + step * 3,
                    v < 2 ? 15 : 15 - step * 4, 4 + step * 3, Pale);
        }
        else if (id < 44) // carved balcony rail, intentionally transparent
        {
            Rect(pixels, 0, 1, 15, 2, Stone);
            Rect(pixels, 0, 13, 15, 15, Face);
            Rect(pixels, 0, 15, 15, 15, Pale);
            for (int x = 1 + v % 2; x < 16; x += 4)
            {
                Rect(pixels, x, 3, x + 1, 12, Stone);
                Rect(pixels, x - 1, 10, x + 2, 11, Pale);
            }
        }
        else if (id < 48) // bridge decks, stone or old wood
        {
            Rect(pixels, 0, 0, 15, 15, id < 46 ? Stone : Wood);
            Rect(pixels, 0, 14, 15, 15, id < 46 ? Pale : WoodLight);
            Rect(pixels, 0, 4, 15, 4, Mortar);
            for (int x = (v % 2) * 4; x < 16; x += 8)
                Rect(pixels, x, 5, x, 13, id < 46 ? Shadow : Mortar);
            for (int x = 2; x < 16; x += 5) Dot(pixels, x, 8, Shadow);
        }
        else if (id < 52) // amber gothic windows in masonry
        {
            Rect(pixels, 0, 0, 15, 15, Stone);
            Rect(pixels, 3, 2, 12, 12, Mortar);
            Rect(pixels, 5, 3, 10, 11, Ember);
            Rect(pixels, 7, 3, 8, 11, Wood);
            Rect(pixels, 4, 12, 11, 14, Face);
            Dot(pixels, 2 + v * 3, 6, Pale);
        }
        else if (id < 56) // doors and thresholds
        {
            Rect(pixels, 0, 0, 15, 15, Stone);
            Rect(pixels, 3, 0, 12, 12, Mortar);
            Rect(pixels, 5, 1, 10, 11, Wood);
            Rect(pixels, 7, 1, 7, 11, WoodLight);
            Rect(pixels, 3, 13, 12, 15, Face);
            Dot(pixels, 9, 6, Ember);
        }
        else if (id < 60) // broken masonry silhouettes
        {
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    if (y < 7 + ((x * 7 + id * 3) % 7))
                        Dot(pixels, x, y, (x + y) % 5 == 0 ? Pale : Stone);
            Rect(pixels, 0, 0, 15, 1, Mortar);
        }
        else // cobble, timber and roots at district seams
        {
            Rect(pixels, 0, 0, 15, 15, id % 2 == 0 ? Stone : Wood);
            for (int y = 0; y < 16; y++)
                Dot(pixels, (y * 3 + v * 2) % 16, y, id % 2 == 0 ? Wood : Ash);
            Rect(pixels, 0, 14, 15, 15, Face);
        }
        return pixels;
    }

    static Color32[] SamplePaintedTile(Texture2D source, int row, int column, bool clearBlack)
    {
        var pixels = new Color32[256];
        float cell = source.width / 8f;
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
        {
            float cx = column * cell + 3f + (x + .5f) * (cell - 6f) / 16f;
            float cy = (7 - row) * cell + 3f + (y + .5f) * (cell - 6f) / 16f;
            int r = 0, g = 0, b = 0;
            for (int oy = -3; oy <= 3; oy += 3)
            for (int ox = -3; ox <= 3; ox += 3)
            {
                var c = source.GetPixel(Mathf.RoundToInt(cx + ox), Mathf.RoundToInt(cy + oy));
                r += Mathf.RoundToInt(c.r * 255f);
                g += Mathf.RoundToInt(c.g * 255f);
                b += Mathf.RoundToInt(c.b * 255f);
            }
            r /= 9; g /= 9; b /= 9;
            bool transparent = clearBlack && r < 34 && g < 32 && b < 38;
            pixels[y * 16 + x] = new Color32((byte)r, (byte)g, (byte)b,
                transparent ? (byte)0 : (byte)255);
        }
        return pixels;
    }

    static Color32[] PaintedTile(Texture2D source, int id)
    {
        if (id < 4) return SamplePaintedTile(source, 0, id, false);
        if (id < 8) return SamplePaintedTile(source, 1, id - 4, false);
        if (id < 12) return SamplePaintedTile(source, 2, id - 8, false);
        if (id < 16) return SamplePaintedTile(source, 2, id - 8, false);
        if (id < 20) return SamplePaintedTile(source, 3, id - 16, false);
        if (id < 24)
        {
            int variant = id - 20;
            var wall = SamplePaintedTile(source, 2, variant, false);
            var cobble = SamplePaintedTile(source, 0, variant, false);
            var carved = SamplePaintedTile(source, 3, 4 + variant, true);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                int index = y * 16 + x;
                if (y >= 12) wall[index] = cobble[index];
                else if (y < 8 && carved[index].a > 0) wall[index] = carved[index];
                else if (y == 11) wall[index] = new Color32(43, 37, 42, 255);
            }
            return wall;
        }
        if (id < 28) return SamplePaintedTile(source, 4, id - 24, true);
        if (id < 32) return SamplePaintedTile(source, 4, id - 26, true);
        if (id < 36) return DrawTile(id); // Continuous fluted column modules.
        if (id < 40) return SamplePaintedTile(source, 5, id - 36, true);
        if (id < 44) return SamplePaintedTile(source, 5, id - 40 + 4, true);
        if (id < 48) return SamplePaintedTile(source, 6, id - 44, true);
        if (id < 52) return SamplePaintedTile(source, 6, 6 + (id % 2), true);
        if (id < 56) return SamplePaintedTile(source, 7, id - 52, true);
        if (id < 60) return SamplePaintedTile(source, 7, id - 52, true);
        return SamplePaintedTile(source, 7, id - 56, true);
    }

    static void ImportSprite(string path, float ppu)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new Exception("Missing village artwork: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 4096;
        importer.SaveAndReimport();
    }

    static void CropToFile(Texture2D source, string name, int x0, int x1,
        int y0, int y1, int width, int height)
    {
        var output = new Texture2D(width, height, TextureFormat.RGBA32, false);
        output.filterMode = FilterMode.Point;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int sx = x0 + Mathf.FloorToInt((x + .5f) * (x1 - x0 + 1f) / width);
            int sy = y0 + Mathf.FloorToInt((y + .5f) * (y1 - y0 + 1f) / height);
            output.SetPixel(x, y, source.GetPixel(Mathf.Min(x1, sx), Mathf.Min(y1, sy)));
        }
        output.Apply();
        File.WriteAllBytes(Root + name + ".png", output.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(output);
    }

    static void CreateLargeSprites()
    {
        var sheet = new Texture2D(2, 2);
        sheet.LoadImage(File.ReadAllBytes(Root + "VillageFacadesSource.png"));
        CropToFile(sheet, "OutskirtsHouse", 8, 451, 8, 415, 96, 88);
        CropToFile(sheet, "LowerStreetHouse", 524, 880, 8, 715, 88, 174);
        CropToFile(sheet, "SquareHall", 961, 1553, 8, 715, 128, 152);
        CropToFile(sheet, "UpperTownhouse", 1616, 2158, 8, 702, 112, 143);
        UnityEngine.Object.DestroyImmediate(sheet);
        var church = new Texture2D(2, 2);
        church.LoadImage(File.ReadAllBytes(Root + "ChurchExterior.png"));
        CropToFile(church, "ChurchBuilt", 0, church.width - 1, 0,
            church.height - 1, 384, 256);
        UnityEngine.Object.DestroyImmediate(church);
    }

    static void HatchRect(Color32[] pixels, int x0, int y0, int x1, int y1,
        Color32 color)
    {
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            pixels[y * 64 + x] = color;
    }

    static void CreateHarborHatch()
    {
        var pixels = new Color32[64 * 16];
        var iron = new Color32(39, 39, 43, 255);
        var rim = new Color32(91, 79, 76, 255);
        var glint = new Color32(139, 115, 101, 255);
        var oak = new Color32(73, 48, 45, 255);
        var oakLight = new Color32(105, 68, 57, 255);
        HatchRect(pixels, 0, 0, 63, 15, iron);
        HatchRect(pixels, 1, 2, 62, 14, rim);
        HatchRect(pixels, 3, 3, 60, 12, oak);
        for (int x = 4; x < 60; x++)
        {
            if (x % 7 < 4) HatchRect(pixels, x, 9, x, 11, oakLight);
            if (x % 11 == 2) HatchRect(pixels, x, 4, x, 6, oakLight);
        }
        HatchRect(pixels, 3, 7, 60, 8, iron);
        HatchRect(pixels, 3, 12, 60, 12, glint);
        for (int x = 7; x <= 56; x += 8)
        {
            HatchRect(pixels, x, 3, x + 1, 12, rim);
            HatchRect(pixels, x, 13, x + 1, 13, glint);
        }
        foreach (int x in new[] { 4, 58 })
        {
            HatchRect(pixels, x, 4, x + 2, 6, glint);
            HatchRect(pixels, x, 10, x + 2, 12, glint);
        }
        HatchRect(pixels, 29, 5, 35, 11, iron);
        HatchRect(pixels, 30, 6, 34, 10, glint);
        HatchRect(pixels, 31, 7, 33, 9, iron);
        pixels[8 * 64 + 32] = new Color32(196, 139, 81, 255);
        var texture = new Texture2D(64, 16, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(Root + "HarborHatch.png", texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    static void CreateFacadePrefabs()
    {
        Directory.CreateDirectory(Root + "Prefabs/");
        foreach (var name in new[] { "OutskirtsHouse", "LowerStreetHouse",
            "SquareHall", "UpperTownhouse", "ChurchBuilt" })
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + name + ".png");
            renderer.sortingOrder = -16;
            PrefabUtility.SaveAsPrefabAsset(go, Root + "Prefabs/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    public static Tile[] Build()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(TileRoot);
        var painted = new Texture2D(2, 2);
        painted.LoadImage(File.ReadAllBytes(Root + "VillageTilesSource.png"));
        for (int id = 0; id < 64; id++)
        {
            var t = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            t.SetPixels32(PaintedTile(painted, id));
            t.Apply();
            File.WriteAllBytes(TileRoot + "Village_" + id.ToString("D2") + ".png", t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
        }
        UnityEngine.Object.DestroyImmediate(painted);
        CreateLargeSprites();
        CreateHarborHatch();
        AssetDatabase.Refresh();
        foreach (var name in new[] { "Outskirts", "LowerStreets", "CentralSquare",
            "UpperCity", "ChurchQuarter", "PierApproach", "DarkForestApproach" })
            ImportSprite(Root + "Backgrounds/" + name + ".png", 40f);
        foreach (var name in new[] { "OutskirtsHouse", "LowerStreetHouse", "SquareHall",
            "UpperTownhouse", "ChurchBuilt" })
            ImportSprite(Root + name + ".png", 16f);
        ImportSprite(Root + "HarborHatch.png", 16f);
        CreateFacadePrefabs();
        var tiles = new Tile[64];
        for (int id = 0; id < 64; id++)
        {
            string name = "Village_" + id.ToString("D2");
            string png = TileRoot + name + ".png";
            ImportSprite(png, 16f);
            string path = TileRoot + name + ".asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
            tile.colliderType = Tile.ColliderType.Grid;
            EditorUtility.SetDirty(tile);
            tiles[id] = tile;
        }
        var root = new GameObject("Ashen Village 16x16 Palette", typeof(Grid));
        var child = new GameObject("Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
        child.transform.SetParent(root.transform);
        var map = child.GetComponent<Tilemap>();
        for (int id = 0; id < 64; id++)
            map.SetTile(new Vector3Int(id % 8, 7 - id / 8, 0), tiles[id]);
        const string palettePath = Root + "AshenVillagePalette.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, palettePath);
        UnityEngine.Object.DestroyImmediate(root);
        var type = Type.GetType("UnityEditor.GridPalette, UnityEditor");
        bool hasSettings = false;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(palettePath))
            if (type != null && asset.GetType() == type) hasSettings = true;
        if (type != null && !hasSettings)
        {
            var settings = ScriptableObject.CreateInstance(type);
            settings.name = "Palette Settings";
            AssetDatabase.AddObjectToAsset(settings, palettePath);
        }
        AssetDatabase.SaveAssets();
        return tiles;
    }
}
