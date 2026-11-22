#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BossWalkSetup
{
    const string ClipPath = "Assets/sprites/boss/Boss_Walk.anim";
    [MenuItem("Tools/Ashrisen/Connect Boss Walk")]
    public static void Build()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var boss = GameObject.Find("Boss");
        if (boss == null) throw new InvalidOperationException("Open the gameplay scene containing Boss.");
        var renderer = boss.GetComponent<SpriteRenderer>(); var animator = boss.GetComponent<Animator>();
        var controller = animator.runtimeAnimatorController as AnimatorController;
        if (controller == null || renderer.sprite == null) throw new InvalidOperationException("Boss needs its existing sprite and Animator Controller.");
        float ppu = renderer.sprite.pixelsPerUnit;
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null)
        {
            var paths = Directory.GetFiles("Assets/sprites/boss/bosswalk", "BossWalk_*.png").OrderBy(p => int.Parse(System.Text.RegularExpressions.Regex.Match(Path.GetFileNameWithoutExtension(p), @"\d+$").Value)).ToArray();
            if (paths.Length != 16) throw new InvalidOperationException("Expected 16 BossWalk frames.");
            var keys = new ObjectReferenceKeyframe[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i].Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing importer: " + path);
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.textureType = TextureImporterType.Sprite; settings.spriteMode = (int)SpriteImportMode.Single;
                settings.spritePixelsPerUnit = ppu; settings.filterMode = FilterMode.Point;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                // Walk source has 21 transparent pixels below the feet; Idle has 18.
                settings.spritePivot = new Vector2(0.5f, 21f / 128f); settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings); importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) throw new InvalidOperationException("Missing sprite: " + path);
                keys[i] = new ObjectReferenceKeyframe { time = i / 12f, value = sprite };
            }
            clip = new AnimationClip { name = "Boss_Walk", frameRate = 12 };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            AssetDatabase.CreateAsset(clip, ClipPath);
            var cs = AnimationUtility.GetAnimationClipSettings(clip); cs.stopTime = 16f / 12f; cs.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, cs);
        }
        else
        { var cs = AnimationUtility.GetAnimationClipSettings(clip); cs.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, cs); }
        EditorUtility.SetDirty(clip);
        AddBool(controller, "IsMoving"); AddBool(controller, "IsAttacking"); AddBool(controller, "IsKnockedBack");
        var sm = controller.layers[0].stateMachine;
        var idle = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "BossIdle");
        if (idle == null) throw new InvalidOperationException("Existing BossIdle state not found.");
        var walk = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Boss_Walk");
        if (walk == null) walk = sm.AddState("Boss_Walk", new Vector3(460, 100));
        walk.motion = clip; idle.tag = walk.tag = "Locomotion";
        Transition(idle, walk, "IsMoving", AnimatorConditionMode.If, true);
        Transition(walk, idle, "IsMoving", AnimatorConditionMode.IfNot, false);
        Transition(walk, idle, "IsAttacking", AnimatorConditionMode.If, false);
        Transition(walk, idle, "IsKnockedBack", AnimatorConditionMode.If, false);
        // No Any State locomotion transitions: combat/hit states keep ownership.
        if (boss.GetComponent<BossLocomotionAnimation>() == null) Undo.AddComponent<BossLocomotionAnimation>(boss);
        EditorUtility.SetDirty(controller); EditorUtility.SetDirty(idle); EditorUtility.SetDirty(walk);
        if (!AssetDatabase.IsValidFolder("Assets/prefabs")) AssetDatabase.CreateFolder("Assets", "prefabs");
        string prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(boss);
        if (string.IsNullOrEmpty(prefab)) PrefabUtility.SaveAsPrefabAssetAndConnect(boss, "Assets/prefabs/Boss.prefab", InteractionMode.AutomatedAction);
        else PrefabUtility.ApplyPrefabInstance(boss, InteractionMode.AutomatedAction);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(boss.scene); EditorSceneManager.SaveScene(boss.scene);
        Debug.Log("Boss_Walk connected to existing BossIdle. No AI or attack clips existed; animation signals are ready for AI integration.", boss);
    }
    static void AddBool(AnimatorController c, string name)
    { if (!c.parameters.Any(p => p.name == name)) c.AddParameter(name, AnimatorControllerParameterType.Bool); }
    static void Transition(AnimatorState from, AnimatorState to, string parameter, AnimatorConditionMode mode, bool gated)
    {
        var t = from.transitions.FirstOrDefault(t => t.destinationState == to && t.conditions.Any(c => c.parameter == parameter && c.mode == mode));
        if (t == null) { t = from.AddTransition(to); t.AddCondition(mode, 0, parameter); }
        t.hasExitTime = false; t.duration = 0; t.canTransitionToSelf = false;
        if (gated)
            foreach (var flag in new[] { "IsAttacking", "IsKnockedBack" })
                if (!t.conditions.Any(c => c.parameter == flag)) t.AddCondition(AnimatorConditionMode.IfNot, 0, flag);
    }
}
#endif
