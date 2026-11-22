using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BossIdleSetup
{
    [MenuItem("Tools/Ashrisen/Set Up Boss Idle")]
    public static void Build()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var player = GameObject.Find("player");
        if (player == null) throw new InvalidOperationException("Player not found.");
        var playerRenderer = player.GetComponent<SpriteRenderer>();
        float ppu = playerRenderer.sprite.pixelsPerUnit;
        var frames = new Sprite[8];
        for (int i = 0; i < frames.Length; i++)
        {
            string path = "Assets/sprites/boss/idle_" + i.ToString("00") + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing frame: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, 18f / 128f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            foreach (var platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
                importer.ClearPlatformTextureSettings(platform);
            importer.SaveAndReimport();
            frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (frames[i] == null) throw new InvalidOperationException("Failed to import: " + path);
        }
        const string clipPath = "Assets/sprites/boss/BossIdle.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
        clip.name = "BossIdle";
        clip.frameRate = 8f;
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / 8f, value = frames[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.startTime = 0f;
        clipSettings.stopTime = 1f;
        clipSettings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
        EditorUtility.SetDirty(clip);
        const string controllerPath = "Assets/sprites/boss/Boss.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var sm = controller.layers[0].stateMachine;
        AnimatorState state = null;
        foreach (var entry in sm.states) if (entry.state.name == "BossIdle") state = entry.state;
        if (state == null) state = sm.AddState("BossIdle");
        state.motion = clip;
        state.speed = 1f;
        sm.defaultState = state;
        EditorUtility.SetDirty(controller);
        GameObject boss = null;
        foreach (var root in player.scene.GetRootGameObjects())
            if (string.Equals(root.name, "Boss", StringComparison.OrdinalIgnoreCase)) boss = root;
        bool created = boss == null;
        if (created)
        {
            boss = new GameObject("Boss");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(boss, player.scene);
            Undo.RegisterCreatedObjectUndo(boss, "Create Boss Idle");
        }
        var renderer = boss.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = Undo.AddComponent<SpriteRenderer>(boss);
        var animator = boss.GetComponent<Animator>();
        if (animator == null) animator = Undo.AddComponent<Animator>(boss);
        Undo.RecordObjects(new UnityEngine.Object[] { boss.transform, renderer, animator }, "Configure Boss Idle");
        renderer.sprite = frames[0];
        renderer.sharedMaterial = playerRenderer.sharedMaterial;
        renderer.sortingLayerID = playerRenderer.sortingLayerID;
        renderer.sortingOrder = playerRenderer.sortingOrder;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        if (created)
        {
            boss.transform.localScale = player.transform.lossyScale;
            var ground = GameObject.Find("ground");
            var collider = ground == null ? null : ground.GetComponent<Collider2D>();
            float floor = collider == null ? playerRenderer.bounds.min.y : collider.bounds.max.y;
            float x = playerRenderer.bounds.min.x - frames[0].bounds.extents.x * boss.transform.lossyScale.x - 1.5f;
            boss.transform.position = new Vector3(x, floor, player.transform.position.z);
        }
        EditorUtility.SetDirty(boss);
        EditorSceneManager.MarkSceneDirty(player.scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(player.scene);
        Debug.Log("BossIdle configured: idle_00..idle_07, 8 FPS, 1 second loop, PPU=" + ppu, boss);
    }
}
