using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class WorldDoor : MonoBehaviour
{
    public string targetScene;
    public Vector3 arrivalPosition;
    public float interactionRadius = 2.5f;
    public string prompt = "E — Enter";
    public string promptKey = "interaction.enter";
    public bool requiresBossDefeated;
    static Vector3? pendingArrival;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Initialize()
    {
        pendingArrival = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!pendingArrival.HasValue) return;
        var player = Object.FindFirstObjectByType<playercontroler>();
        if (player == null) return;
        var destination = pendingArrival.Value;
        pendingArrival = null;
        player.transform.position = destination;
        var body = player.GetComponent<Rigidbody2D>();
        if (body != null) { body.position = destination; body.linearVelocity = Vector2.zero; }
        var respawn = player.GetComponent<PlayerRespawn>();
        if (respawn != null) respawn.respawnPoint = destination;
    }

    bool Nearby()
    {
        var player = Object.FindFirstObjectByType<playercontroler>();
        return player != null && !PauseMenu.InputBlocked &&
            (!requiresBossDefeated || BossArenaProgress.Defeated) &&
            Vector2.Distance(player.transform.position, transform.position) <= interactionRadius;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E)) TryUse();
    }

    public bool TryUse()
    {
        if (!Nearby()) return false;
        pendingArrival = arrivalPosition;
        SceneManager.LoadScene(targetScene);
        return true;
    }

    void OnGUI()
    {
        if (!Nearby()) return;
        var style = new GUIStyle(GUI.skin.label) {
            alignment = TextAnchor.MiddleCenter, fontSize = 18
        };
        style.normal.textColor = new Color(1f, .84f, .62f);
        GUI.Label(new Rect(0, Screen.height - 90, Screen.width, 35),
            GameText.Get(promptKey), style);
    }
}
