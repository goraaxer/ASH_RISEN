using UnityEngine;

public sealed class VillageBranchGate : MonoBehaviour
{
    public string destinationName = "Dark Forest";
    public string destinationKey = "location.forest";
    public float radius = 2.5f;
    public string alternateRouteHint;
    public string alternateRouteHintKey = "route.church_below";

    void OnGUI()
    {
        var player = FindAnyObjectByType<playercontroler>();
        if (player == null || PauseMenu.InputBlocked ||
            Vector2.Distance(player.transform.position, transform.position) > radius) return;
        var style = new GUIStyle(GUI.skin.label);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 18;
        style.normal.textColor = new Color(1f, .81f, .59f);
        string message = GameText.Format("route.closed",
            GameText.Get(destinationKey));
        if (!string.IsNullOrEmpty(alternateRouteHintKey))
            message += ". " + GameText.Get(alternateRouteHintKey);
        GUI.Label(new Rect(0, Screen.height - 90, Screen.width, 35),
            message, style);
    }
}
