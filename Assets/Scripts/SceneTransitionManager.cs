using UnityEngine;

// Shared hook used by the preserved heroine and boss. World loading was removed.
public sealed class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }
    public static bool IsBusy => false;

    public static SceneTransitionManager Ensure()
    {
        if (Instance == null) Instance = FindFirstObjectByType<SceneTransitionManager>();
        if (Instance == null) Instance = new GameObject("Respawn Helper").AddComponent<SceneTransitionManager>();
        return Instance;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    public void Notify(string message)
    {
        if (!string.IsNullOrEmpty(message)) Debug.LogWarning(message);
    }

    public void Respawn(PlayerRespawn player)
    {
        if (player == null) return;
        var body = player.GetComponent<Rigidbody2D>();
        if (body != null) body.linearVelocity = Vector2.zero;
        player.transform.position = player.respawnPoint;
        if (body != null) body.position = player.respawnPoint;
        player.GetComponent<AshHealth>()?.ResetHealth();
        BossArenaController.ResetAfterRespawn(true);
        AshHound.ResetAll();
        GothicEnemy.ResetAll();
    }
}
