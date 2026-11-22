using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class GameSaveData
{
    public int version = 2;
    public string scenePath = "Assets/Scenes/AshenVillage.unity";
    public Vector3 position;
    public Vector2 velocity;
    public Vector3 checkpoint;
    public int health;
    public bool facingLeft;
    public bool churchBossDefeated;
    public string savedAt;
}

public static class GameSaveSystem
{
    public const string StagePath = "Assets/Scenes/AshenVillage.unity";
    public const string LegacyGraveyardPath = "Assets/Scenes/AshenGraveyard.unity";
    public const string ArenaPath = "Assets/Scenes/BossArena.unity";
    public const string LegacyDuskPath = "Assets/Scenes/DuskWorld.unity";
    static GameSaveData pending;
    public static string SavePath
    {
        get
        {
#if UNITY_EDITOR
            var testPath = UnityEditor.EditorPrefs.GetString("Ashrisen.EditorTestSavePath", "");
            if (!string.IsNullOrEmpty(testPath)) return testPath;
#endif
            return Path.Combine(Application.persistentDataPath, "ashrisen-reset-save.json");
        }
    }
    public static bool HasSave => TryRead(out _, out _);

    public static bool Save(GameObject player, out string error)
    {
        error = null;
        if (player == null) { error = GameText.Get("save.player_missing"); return false; }
        var hp = player.GetComponent<AshHealth>();
        var body = player.GetComponent<Rigidbody2D>();
        if (hp == null || hp.IsDead || body == null) { error = GameText.Get("save.player_cannot"); return false; }
        var data = new GameSaveData {
            scenePath = player.scene.path,
            position = player.transform.position,
            velocity = body.linearVelocity,
            checkpoint = player.GetComponent<PlayerRespawn>()?.respawnPoint ?? player.transform.position,
            health = hp.CurrentHealth,
            facingLeft = player.GetComponent<SpriteRenderer>()?.flipX ?? false,
            churchBossDefeated = BossArenaProgress.Defeated,
            savedAt = DateTime.UtcNow.ToString("O")
        };
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
            return true;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        { error = e.Message; return false; }
    }

    public static bool TryRead(out GameSaveData data, out string error)
    {
        data = null;
        error = null;
        try
        {
            if (!File.Exists(SavePath)) { error = GameText.Get("save.no_save"); return false; }
            data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(SavePath));
            if (data == null || data.version != 2 ||
                (data.scenePath != StagePath && data.scenePath != LegacyGraveyardPath && data.scenePath != ArenaPath &&
                 data.scenePath != LegacyDuskPath) || data.health < 1)
                throw new InvalidDataException(GameText.Get("save.incompatible"));
            return true;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
        { data = null; error = e.Message; return false; }
    }

    public static bool Continue(string fallbackScene, out string error)
    {
        pending = null;
        if (!TryRead(out var data, out error)) return false;
        pending = data;
        PlayerRespawn.useSave = false;
        SceneManager.LoadScene(data.scenePath);
        return true;
    }

    public static bool RestorePending(GameObject player)
    {
        if (pending == null || player.scene.path != pending.scenePath) return false;
        var data = pending;
        pending = null;
        BossArenaProgress.Defeated = data.churchBossDefeated;
        player.transform.position = data.position;
        var body = player.GetComponent<Rigidbody2D>();
        if (body != null) { body.position = data.position; body.linearVelocity = data.velocity; }
        var respawn = player.GetComponent<PlayerRespawn>();
        if (respawn != null) respawn.respawnPoint = data.checkpoint;
        var sprite = player.GetComponent<SpriteRenderer>();
        if (sprite != null) sprite.flipX = data.facingLeft;
        player.GetComponent<AshHealth>()?.RestoreHealth(data.health);
        Physics2D.SyncTransforms();
        return true;
    }

    public static void ClearProgress()
    {
        pending = null;
        BossArenaProgress.Defeated = false;
        if (File.Exists(SavePath)) File.Delete(SavePath);
    }
}
