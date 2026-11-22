using UnityEngine;

// The hatch marks the unbuilt pier route. The dock below the bridge is playable.
public sealed class VillageHarborHatch : MonoBehaviour
{
    void OnGUI()
    {
        var player = FindAnyObjectByType<playercontroler>();
        if (player == null || PauseMenu.InputBlocked ||
            Mathf.Abs(player.transform.position.x - transform.position.x) > 3f ||
            Mathf.Abs(player.transform.position.y + 3.49f) > 2f) return;
        var style = new GUIStyle(GUI.skin.label) {
            alignment = TextAnchor.MiddleCenter, fontSize = 18
        };
        style.normal.textColor = new Color(1f, .81f, .59f);
        GUI.Label(new Rect(0, Screen.height - 90, Screen.width, 35),
            GameText.Get("interaction.pier_hatch"), style);
    }
}
