using UnityEngine;

// Quiet room ambience and victory text. No boss attack logic lives here.
public sealed class AshenSanctumArena : MonoBehaviour
{
    public BossArenaController controller;
    public ParticleSystem ceilingAsh;
    float defeatedAt = -1f;

    void OnEnable()
    {
        if (controller != null) controller.onBossDefeated.AddListener(OnBossDefeated);
    }

    void OnDisable()
    {
        if (controller != null) controller.onBossDefeated.RemoveListener(OnBossDefeated);
    }

    public void RingIntroBell()
    {
        if (ceilingAsh != null) ceilingAsh.Emit(3);
    }

    void OnBossDefeated() { defeatedAt = Time.time; }

    void OnGUI()
    {
        if (defeatedAt < 0f) return;
        float alpha = Mathf.Clamp01((Time.time - defeatedAt) / 1.2f);
        var previous = GUI.color;
        GUI.color = new Color(.73f, .72f, .7f, alpha);
        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(Mathf.Min(38, Screen.width * .045f)),
            fontStyle = FontStyle.Bold
        };
        GUI.Label(new Rect(0, Screen.height * .24f, Screen.width, 72), "BOSS DEFEATED", style);
        GUI.color = previous;
    }
}
