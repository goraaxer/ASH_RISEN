using UnityEngine;

// Only marks the physical fork in the bridge; it does not change scenes.
public sealed class VillagePierForkHint : MonoBehaviour
{
    void OnGUI()
    {
        var player = FindAnyObjectByType<playercontroler>();
        if (player == null || PauseMenu.InputBlocked ||
            Mathf.Abs(player.transform.position.x - transform.position.x) > 3.2f ||
            player.transform.position.y < 0f) return;
        var style = new GUIStyle(GUI.skin.label) {
            alignment = TextAnchor.MiddleCenter, fontSize = 18
        };
        style.normal.textColor = new Color(1f, .81f, .59f);
        GUI.Label(new Rect(0, Screen.height - 90, Screen.width, 35),
            GameText.Get("route.pier_village"), style);
    }
}
