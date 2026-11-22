using UnityEngine;

// Contextual lessons for the first cemetery. Hints never block movement or combat.
public sealed class GraveyardTutorial : MonoBehaviour
{
    public playercontroler player;
    public AshLantern checkpoint;
    public bool opensIntoVillage;

    Rigidbody2D playerBody;
    PlayerDash dash;
    bool jumped;
    bool dashed;
    GUIStyle hintStyle;

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<playercontroler>();
        if (player == null) { enabled = false; return; }
        playerBody = player.GetComponent<Rigidbody2D>();
        dash = player.GetComponent<PlayerDash>();
    }

    void Update()
    {
        if (playerBody != null && playerBody.linearVelocity.y > 1f) jumped = true;
        if (dash != null && dash.IsDashing) dashed = true;
    }

    public string CurrentHint()
    {
        if (player == null) return null;
        float x = player.transform.position.x;
        if (x < 1.8f) return GameText.Get("tutorial.move");
        if (x < 5.5f && checkpoint != null && !checkpoint.IsLit)
            return GameText.Get("tutorial.lantern");
        if (x < 7f && !jumped) return GameText.Get("tutorial.jump");
        if (x >= 7f && x < 11.5f) return GameText.Get("tutorial.attack");
        if (x >= 11.5f && x < 18f && player.transform.position.y < 2.2f)
            return GameText.Get("tutorial.bridge_up");
        if (x >= 20f && x < 29f)
            return GameText.Get("tutorial.bridge_gap");
        if (x >= 60f && x < 70f && !dashed) return GameText.Get("tutorial.dash");
        if (x >= 73f && x < 83f && opensIntoVillage) return GameText.Get("tutorial.village_ahead");
        if (x >= 83f && opensIntoVillage) return null;
        if (x >= 73f) return GameText.Get("tutorial.gate_closed");
        return null;
    }

    void OnGUI()
    {
        if (PauseMenu.InputBlocked) return;
        string hint = CurrentHint();
        if (string.IsNullOrEmpty(hint)) return;
        if (hintStyle == null)
        {
            hintStyle = new GUIStyle(GUI.skin.label) {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * .036f), 14, 21)
            };
            hintStyle.normal.textColor = new Color(.98f, .88f, .74f);
        }
        float width = Mathf.Min(Screen.width - 32,
            Mathf.Max(350f, hint.Length * 9f + 42f));
        var box = new Rect((Screen.width - width) * .5f, 66, width, 40);
        Color before = GUI.color;
        GUI.color = new Color(.08f, .055f, .06f, .72f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = before;
        GUI.Label(box, hint, hintStyle);
    }
}
