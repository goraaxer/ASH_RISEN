#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AshHeartsSetup
{
    [MenuItem("Tools/Ash Hearts/Create HUD for Selected Player")]
    private static void CreateHUD()
    {
        GameObject player = Selection.activeGameObject;
        if (player == null || !player.scene.IsValid() || Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Ash Hearts", "Stop Play Mode and select your player in Hierarchy.", "OK");
            return;
        }
        string folder = LocateFolder();
        if (folder == null) throw new InvalidOperationException("Cannot locate AshHeartsSetup.cs.");
        var frames = new Sprite[16];
        for (int i = 0; i < frames.Length; i++)
        {
            string path = folder + "/Sprites/heart_" + i.ToString("00") + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing sprite: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (frames[i] == null) throw new InvalidOperationException("Sprite import failed: " + path);
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Ash Hearts HUD");
        AshHealth health = player.GetComponent<AshHealth>();
        if (health == null) health = Undo.AddComponent<AshHealth>(player);
        AshHeartsHUD hud = null;
        foreach (GameObject root in player.scene.GetRootGameObjects())
        {
            foreach (AshHeartsHUD existing in root.GetComponentsInChildren<AshHeartsHUD>(true))
                if (existing.HealthSource == health) hud = existing;
        }
        if (hud == null)
        {
            var canvasGO = new GameObject("Ash Health Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(canvasGO, player.scene);
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Ash Health Canvas");
            Canvas canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = true;
            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640, 360);
            scaler.matchWidthOrHeight = 0.5f;
            var hudGO = new GameObject("Ash Hearts HUD", typeof(RectTransform), typeof(AshHeartsHUD));
            hudGO.transform.SetParent(canvasGO.transform, false);
            RectTransform rect = (RectTransform)hudGO.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(12f, -12f);
            hud = hudGO.GetComponent<AshHeartsHUD>();
        }
        Undo.RecordObject(hud, "Configure Ash Hearts HUD");
        hud.Configure(health, frames[0], frames[15], frames);
        EditorUtility.SetDirty(hud);
        EditorSceneManager.MarkSceneDirty(player.scene);
        Selection.activeGameObject = player;
        Undo.CollapseUndoOperations(group);
        Debug.Log("Ash Hearts HUD created. Press Play, then use AshHealth > Test > Take 1 damage.", player);
    }

    private static string LocateFolder()
    {
        foreach (string guid in AssetDatabase.FindAssets("AshHeartsSetup t:MonoScript"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith("/Editor/AshHeartsSetup.cs", StringComparison.Ordinal)) continue;
            return path.Substring(0, path.LastIndexOf("/Editor/", StringComparison.Ordinal));
        }
        return null;
    }
}
#endif
