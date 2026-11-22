using UnityEngine;
using UnityEngine.SceneManagement;
public class PlayerRespawn : MonoBehaviour
{
    public static bool useSave = false; 
    public float fallLimitY = -20f;
    public Vector3 respawnPoint;
    public string checkpointScene;
    Rigidbody2D rb;
    bool deathRespawnPending;
    float deathRespawnAt;
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        respawnPoint = transform.position;
        checkpointScene = gameObject.scene.path;
        var health = GetComponent<AshHealth>();
        if (health != null) health.HealthChanged += HealthChanged;

        if (useSave && PlayerPrefs.HasKey("saveX"))
        {
            respawnPoint = new Vector3(
                PlayerPrefs.GetFloat("saveX"),
                PlayerPrefs.GetFloat("saveY"),
                0f);
            transform.position = respawnPoint;
        }
    }
    void Update()
    {
        if (PauseMenu.InputBlocked) return;
        if (deathRespawnPending) return;
        if (transform.position.y < fallLimitY) Respawn();
        if (Input.GetKeyDown(KeyCode.R)) Respawn();                          
    }
    void LateUpdate()
    {
        // HealthChanged must finish notifying the HUD before health is refilled.
        // Respawning inside the event reorders the 0 HP and full HP notifications.
        if (!deathRespawnPending) return;
        if (Time.time < deathRespawnAt) return;
        deathRespawnPending = false;
        Respawn();
    }
    public void SetCheckpoint(Vector3 pos)
    {
        respawnPoint = pos;
        checkpointScene = gameObject.scene.path;
        PlayerPrefs.SetFloat("saveX", pos.x);
        PlayerPrefs.SetFloat("saveY", pos.y);
        PlayerPrefs.Save();
    }
    public void Respawn()
    {
        deathRespawnPending = false;
        SceneTransitionManager.Ensure().Respawn(this);
        GetComponent<PlayerHitReaction>()?.ResetAfterRespawn();
    }
    void HealthChanged(int oldHealth, int newHealth, int maxHealth)
    {
        if (newHealth == 0)
        {
            deathRespawnPending = true;
            deathRespawnAt = Time.time + 1.3f;
            PlayerHitReaction.PlayDeath(gameObject);
        }
    }
    void OnDestroy() { var health=GetComponent<AshHealth>(); if(health!=null) health.HealthChanged-=HealthChanged; }
}
