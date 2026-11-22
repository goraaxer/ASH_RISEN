using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// The only source of player-facing text. Edit Resources/Localization/en.json
// to change wording without editing gameplay or menu scripts.
public static class GameText
{
    [Serializable] sealed class Entry { public string key; public string value; }
    [Serializable] sealed class Table { public Entry[] entries; }
    static Dictionary<string, string> values;

    static void Load()
    {
        if (values != null) return;
        values = new Dictionary<string, string>(StringComparer.Ordinal);
        var asset = Resources.Load<TextAsset>("Localization/en");
        if (asset == null)
        {
            Debug.LogError("Missing English text table: Resources/Localization/en.json");
            return;
        }
        var table = JsonUtility.FromJson<Table>(asset.text);
        if (table?.entries == null) return;
        foreach (var entry in table.entries)
            if (entry != null && !string.IsNullOrEmpty(entry.key))
                values[entry.key] = entry.value ?? "";
    }

    public static string Get(string key)
    {
        Load();
        return values.TryGetValue(key, out var value) ? value : key;
    }

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, Get(key), args);
}
