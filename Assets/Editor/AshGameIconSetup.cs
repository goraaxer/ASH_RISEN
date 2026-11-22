#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AshGameIconSetup
{
    const string IconPath = "Assets/Art/Menu/AshrisenEmblem.png";

    static AshGameIconSetup()
    {
        EditorApplication.delayCall += Apply;
    }

    [MenuItem("Ashrisen/Apply Game Icon")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null) return;
        var current = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Standalone);
        int count = current != null && current.Length > 0 ? current.Length :
            PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Standalone).Length;
        if (count == 0) return;
        var icons = new Texture2D[count];
        bool alreadyAssigned = true;
        for (int i = 0; i < count; i++)
        {
            icons[i] = icon;
            alreadyAssigned &= current != null && current.Length > i && current[i] == icon;
        }
        if (alreadyAssigned) return;
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, icons);
        AssetDatabase.SaveAssets();
        Debug.Log("Ashrisen game emblem assigned as the Standalone application icon.");
    }

    public static void Verify()
    {
        Apply();
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        var theme = AssetDatabase.LoadAssetAtPath<AshMenuTheme>(
            "Assets/Resources/AshMenuTheme.asset");
        var slots = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Standalone);
        if (icon == null || sprite == null || theme == null || theme.emblem != sprite ||
            slots == null || slots.Length == 0)
            throw new InvalidOperationException("Main menu emblem or game icon is missing.");
        foreach (var slot in slots)
            if (slot != icon) throw new InvalidOperationException("A Standalone icon slot is empty.");
        Debug.Log("Main menu emblem and all Standalone icon slots verified.");
    }
}
#endif
