using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class ResponsiveCombatAnimationBuilder
{
    const string Folder = "Assets/Animations/Combat";

    [MenuItem("Tools/Ashrisen/Build Responsive Combat Animations")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Animations", "Combat");
        const float slashLength = 1f / 3f;
        var ground = MakeClip("GroundSlash", "Assets/sprites/Attack/attack_", new[] { 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40, 42, 44, 45, 46, 48 }, slashLength);
        var running = MakeClip("RunningSlash", "Assets/sprites/Attack/attack_", new[] { 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40, 42, 44, 45, 46, 48 }, slashLength);
        var air = MakeClip("AirSlash", "Assets/sprites/jump/AirAttack_", new[] { 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 24, 26, 28, 30, 32 }, slashLength);
        var up = MakeClip("UpwardSlash", "Assets/sprites/Attack/attack_", new[] { 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24 }, slashLength);
        var down = MakeClip("DownwardSlash", "Assets/sprites/jump/AirAttack_", new[] { 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 }, slashLength);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/sprites/player.controller");
        if (controller == null) throw new System.Exception("Player animator controller is missing");
        var states = controller.layers[0].stateMachine;
        SetState(states, "Attack", ground);
        SetState(states, "JumpAttack", air);
        SetState(states, "RunningSlash", running);
        SetState(states, "UpwardSlash", up);
        SetState(states, "DownwardSlash", down);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        PlayerDamageAnimationBuilder.Build();
        Debug.Log("Responsive combat clips built from original player art (0.34 s, twenty slash poses each).");
    }

    static AnimationClip MakeClip(string name, string prefix, int[] frames, float duration = .2f)
    {
        var clip = new AnimationClip { name = name, frameRate = 60f, wrapMode = WrapMode.Once };
        var keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++)
        {
            var path = prefix + frames[i].ToString("00") + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new System.Exception("Missing original sprite: " + path);
            keys[i] = new ObjectReferenceKeyframe { time = duration * i / frames.Length, value = sprite };
        }
        keys[frames.Length] = new ObjectReferenceKeyframe { time = duration, value = keys[frames.Length - 1].value };
        AnimationUtility.SetObjectReferenceCurve(clip,
            new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" }, keys);
        string pathOut = Folder + "/" + name + ".anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(pathOut);
        if (existing == null) AssetDatabase.CreateAsset(clip, pathOut);
        else
        {
            EditorUtility.CopySerialized(clip, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(clip);
            clip = existing;
        }
        return clip;
    }

    static void SetState(AnimatorStateMachine machine, string name, AnimationClip clip)
    {
        AnimatorState state = null;
        foreach (var child in machine.states) if (child.state.name == name) { state = child.state; break; }
        if (state == null) state = machine.AddState(name);
        state.motion = clip;
        state.speed = 1f;
        EditorUtility.SetDirty(state);
    }
}
