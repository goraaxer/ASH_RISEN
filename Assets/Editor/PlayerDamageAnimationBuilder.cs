using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class PlayerDamageAnimationBuilder
{
    const string Source = "Assets/sprites/idle animation/frame_00.png";
    const string Root = "Assets/sprites/PlayerDamage/";
    const int Size = 80;

    readonly struct Pose
    {
        public readonly int x, y;
        public readonly float angle, scale, ash;
        public Pose(int x, int y, float angle = 0, float scale = 1, float ash = 0)
        { this.x = x; this.y = y; this.angle = angle; this.scale = scale; this.ash = ash; }
    }

    static readonly Pose[] Hurt = {
        new Pose(0,0), new Pose(2,-1,-8), new Pose(2,-1,-5), new Pose(0,0)
    };
    static readonly Pose[] Death = {
        new Pose(0,0), new Pose(1,-1,8), new Pose(3,-2,17,.96f),
        new Pose(5,-3,27,.88f,.15f), new Pose(7,-4,32,.78f,.35f),
        new Pose(8,-5,37,.70f,.55f), new Pose(8,-5,37,.62f,.75f),
        new Pose(8,-5,37,.56f,.9f), new Pose(8,-5,37,.52f,1f),
        new Pose(8,-5,37,.52f,1f)
    };

    [MenuItem("Tools/Ashrisen/Combat/Build heroine hurt and death")]
    public static void Build()
    {
        Directory.CreateDirectory(Root + "Hurt");
        Directory.CreateDirectory(Root + "Death");
        var original = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!original.LoadImage(File.ReadAllBytes(Source)) ||
            original.width != Size || original.height != Size)
            throw new InvalidOperationException("Heroine's original 80x80 idle sprite is missing.");
        Color32[] source = original.GetPixels32();
        UnityEngine.Object.DestroyImmediate(original);
        Write("Hurt", Hurt, source);
        Write("Death", Death, source);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Configure("Hurt", Hurt.Length);
        Configure("Death", Death.Length);
        var hurt = Clip("HitReaction", "Hurt", new[] { 0f,.3f,.6f,1f }, .3f);
        var death = Clip("Death", "Death",
            new[] { 0f,.08f,.16f,.25f,.36f,.48f,.61f,.74f,.86f,1f }, 1.25f);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/sprites/player.controller");
        if (controller == null) throw new InvalidOperationException("Player controller is missing.");
        var machine = controller.layers[0].stateMachine;
        SetState(machine, "HitReaction", hurt);
        SetState(machine, "Death", death);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("Built heroine hurt (4 frames) and ash death (10 frames) from her original pixel sprite.");
    }

    static void Write(string name, Pose[] poses, Color32[] source)
    {
        for (int frame = 0; frame < poses.Length; frame++)
        {
            Pose p = poses[frame];
            var output = new Color32[Size * Size];
            float radians = p.angle * Mathf.Deg2Rad;
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = x - 40f - p.x, dy = y - 14f - p.y;
                int sx = Mathf.RoundToInt(40f + dx * c + dy * s);
                int sy = Mathf.RoundToInt(14f + (-dx * s + dy * c) / p.scale);
                if (sx < 0 || sx >= Size || sy < 0 || sy >= Size) continue;
                Color32 pixel = source[sy * Size + sx];
                if (pixel.a == 0) continue;
                uint hash = (uint)(x * 73856093 ^ y * 19349663);
                if (name == "Death" && hash % 1000 < p.ash * 1000f)
                {
                    uint scatterRate = frame == poses.Length - 1 ? 4u : 3u;
                    if (hash % scatterRate == 0)
                    {
                        int spread = Mathf.RoundToInt(((int)(hash % 17) - 8) * p.ash * 1.5f);
                        float settle = frame == poses.Length - 1 ? .96f : p.ash * .7f;
                        int dustX = Mathf.Clamp(x + spread, 0, Size - 1);
                        int dustY = Mathf.Clamp(Mathf.RoundToInt(13f + (y - 13f) * (1f - settle) -
                            (hash % 5) * p.ash), 0, Size - 1);
                        output[dustY * Size + dustX] = pixel;
                    }
                    continue;
                }
                output[y * Size + x] = pixel;
            }
            var image = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            image.filterMode = FilterMode.Point;
            image.SetPixels32(output);
            image.Apply(false, false);
            File.WriteAllBytes(Root + name + "/" + name + "_" + frame + ".png", image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    static void Configure(string name, int count)
    {
        for (int frame = 0; frame < count; frame++)
        {
            string path = Root + name + "/" + name + "_" + frame + ".png";
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
            settings.spritePivot = new Vector2(.5f, .5f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }

    static AnimationClip Clip(string clipName, string folder, float[] times, float duration)
    {
        string path = "Assets/Animations/Combat/" + clipName + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = clipName };
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.frameRate = 60f;
        clip.wrapMode = WrapMode.Once;
        var keys = new ObjectReferenceKeyframe[times.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            string spritePath = Root + folder + "/" + folder + "_" + i + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null) throw new InvalidOperationException("Damage sprite missing: " + spritePath);
            keys[i] = new ObjectReferenceKeyframe {
                time = i == keys.Length - 1 ? duration - 1f / clip.frameRate : duration * times[i],
                value = sprite
            };
        }
        AnimationUtility.SetObjectReferenceCurve(clip,
            new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" }, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static void SetState(AnimatorStateMachine machine, string name, AnimationClip clip)
    {
        AnimatorState state = null;
        foreach (var child in machine.states)
            if (child.state.name == name) { state = child.state; break; }
        if (state == null) state = machine.AddState(name);
        state.motion = clip;
        EditorUtility.SetDirty(state);
    }
}
