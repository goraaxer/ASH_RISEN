using UnityEngine;

public sealed class VillageChurchForkHint : MonoBehaviour
{
    void OnGUI()
    {
        var player = FindAnyObjectByType<playercontroler>();
        if (player == null || PauseMenu.InputBlocked ||
            Mathf.Abs(player.transform.position.x - transform.position.x) > 4f)
            return;
        var style = new GUIStyle(GUI.skin.label) {
            alignment = TextAnchor.MiddleCenter, fontSize = 18
        };
        style.normal.textColor = new Color(1f, .81f, .59f);
        GUI.Label(new Rect(0, Screen.height - 90, Screen.width, 35),
            GameText.Get("route.church_forest"), style);
    }
}
