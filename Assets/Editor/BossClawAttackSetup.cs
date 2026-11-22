using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu command is repeatable; existing movement/attack AI is not replaced.
[InitializeOnLoad]
public static class BossClawAttackSetup
{
    const string Art = "Assets/sprites/boss/clawattack";
    const string Request = "Artifacts/Boss_ClawAttack_Revised/unity-install.request";
    static BossClawAttackSetup() { EditorApplication.delayCall += AutoInstall; }
    static void AutoInstall()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { Install(); File.Delete(Request); }
        catch (Exception e) { Debug.LogException(e); File.WriteAllText("Artifacts/Boss_ClawAttack_Revised/unity-install-error.txt", e.ToString()); }
    }
    [MenuItem("Tools/Ashrisen/Install Revised Boss Claw Attack")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        AssetDatabase.Refresh();
        Sprite[] body = Import("Boss_ClawAttack_Frames", "Boss_ClawAttack_");
        Sprite[] trail = Import("ClawTrail_Frames", "ClawTrail_");
        int[] delays = {12,11,11,11,10,10,10,10,10,9,9,8,8,7,7,8,12,6,5,4,3,3,3,4,5,5,5,7,7,10,12,16};
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Art + "/Boss_ClawAttack.anim");
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, Art + "/Boss_ClawAttack.anim"); }
        clip.frameRate = 100;
        SetSprites(clip, "", body, delays);
        SetSprites(clip, "ClawTrail", trail, delays);
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        var trailClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Art + "/Boss_ClawTrail.anim");
        if (trailClip == null) { trailClip = new AnimationClip(); AssetDatabase.CreateAsset(trailClip, Art + "/Boss_ClawTrail.anim"); }
        trailClip.frameRate = 100; SetSprites(trailClip, "", trail, delays);
        settings = AnimationUtility.GetAnimationClipSettings(trailClip); settings.loopTime = false; AnimationUtility.SetAnimationClipSettings(trailClip, settings);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/sprites/boss/Boss.controller");
        if (controller == null) throw new InvalidOperationException("Existing Boss.controller missing.");
        if (!controller.parameters.Any(p => p.name == "BossClawAttack")) controller.AddParameter("BossClawAttack", AnimatorControllerParameterType.Trigger);
        var machine = controller.layers[0].stateMachine;
        var attack = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Boss_ClawAttack");
        if (attack == null) attack = machine.AddState("Boss_ClawAttack");
        attack.motion = clip; attack.tag = "Attack";
        if (!attack.behaviours.Any(b => b is BossClawAttackState)) attack.AddStateMachineBehaviour<BossClawAttackState>();
        foreach (var t in machine.anyStateTransitions.Where(t => t.destinationState == attack).ToArray()) machine.RemoveAnyStateTransition(t);
        var enter = machine.AddAnyStateTransition(attack); enter.hasExitTime = false; enter.duration = 0; enter.canTransitionToSelf = false;
        enter.AddCondition(AnimatorConditionMode.If, 0, "BossClawAttack");
        foreach (var t in attack.transitions.ToArray()) attack.RemoveTransition(t);
        var idle = machine.states.Select(s => s.state).First(s => s.name == "BossIdle");
        var exit = attack.AddTransition(idle); exit.hasExitTime = true; exit.exitTime = 1; exit.duration = 0;
        var prefabPath = "Assets/prefabs/Boss.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
        try { Attach(prefab); PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        foreach (var animator in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (animator.gameObject.scene.IsValid() && animator.runtimeAnimatorController == controller)
            { Attach(animator.gameObject); EditorSceneManager.MarkSceneDirty(animator.gameObject.scene); }
        EditorUtility.SetDirty(controller); EditorUtility.SetDirty(clip); EditorUtility.SetDirty(trailClip);
        AssetDatabase.SaveAssets();
        // Leave any open scene's unrelated edits unsaved; prefab integration is persisted.
        File.WriteAllText("Artifacts/Boss_ClawAttack_Revised/unity-install-report.txt", "Installed clips, controller and Boss.prefab. Open scene objects updated; save scene when ready. Play Mode not tested by this installer.");
        Debug.Log("Revised claw attack installed. AI can call BossClawAttackVisual.Play().");
    }
    static Sprite[] Import(string folder, string prefix)
    {
        var sprites = new Sprite[32];
        for (int i = 0; i < 32; i++)
        {
            string path = Art + "/" + folder + "/" + prefix + (i + 1).ToString("00") + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new FileNotFoundException(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16; importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.mipmapEnabled = false;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMode = (int)SpriteImportMode.Single; settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(.5f, 21f / 128f); importer.SetTextureSettings(settings);
            importer.SaveAndReimport(); sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprites[i] == null) throw new InvalidOperationException("Sprite import failed: " + path);
        }
        return sprites;
    }
    static void SetSprites(AnimationClip clip, string path, Sprite[] sprites, int[] delays)
    {
        var keys = new ObjectReferenceKeyframe[33]; float time = 0;
        for (int i = 0; i < 32; i++) { keys[i] = new ObjectReferenceKeyframe { time = time, value = sprites[i] }; time += delays[i] / 100f; }
        keys[32] = new ObjectReferenceKeyframe { time = time, value = sprites[31] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve(path, typeof(SpriteRenderer), "m_Sprite"), keys);
    }
    static void Attach(GameObject boss)
    {
        var child = boss.transform.Find("ClawTrail");
        if (child == null) { var obj = new GameObject("ClawTrail"); obj.transform.SetParent(boss.transform, false); child = obj.transform; }
        var renderer = child.GetComponent<SpriteRenderer>(); if (renderer == null) renderer = child.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = null;
        var visual = boss.GetComponent<BossClawAttackVisual>(); if (visual == null) visual = boss.AddComponent<BossClawAttackVisual>();
        visual.trail = renderer; EditorUtility.SetDirty(visual);
    }
}
