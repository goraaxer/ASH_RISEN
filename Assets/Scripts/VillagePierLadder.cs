using System.Collections;
using UnityEngine;

// A visible local climb between the harbor floor and the bridge. No scene load.
public sealed class VillagePierLadder : MonoBehaviour
{
    const float ShaftX = 218.5f;
    static readonly Vector3 UpperExit = new Vector3(214.5f, 4.51f, 0f);
    static readonly Vector3 LowerExit = new Vector3(222f, -3.49f, 0f);

    bool riding;
    playercontroler rider;
    Rigidbody2D body;
    PlayerDash dash;
    bool controllerWasEnabled;
    bool dashWasEnabled;

    public bool TryUse(playercontroler player)
    {
        if (riding || player == null || PauseMenu.InputBlocked) return false;
        Vector3 position = player.transform.position;
        bool fromBelow = position.y < 0f;
        if (Mathf.Abs(position.x - ShaftX) > 4.2f ||
            Mathf.Abs(position.y - (fromBelow ? LowerExit.y : UpperExit.y)) > 2f)
            return false;
        StartCoroutine(Ride(player, fromBelow));
        return true;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
            TryUse(FindAnyObjectByType<playercontroler>());
    }

    IEnumerator Ride(playercontroler player, bool ascending)
    {
        riding = true;
        rider = player;
        body = player.GetComponent<Rigidbody2D>();
        dash = player.GetComponent<PlayerDash>();
        controllerWasEnabled = player.enabled;
        dashWasEnabled = dash != null && dash.enabled;
        player.enabled = false;
        if (dash != null) dash.enabled = false;
        body.linearVelocity = Vector2.zero;
        body.simulated = false;

        Vector3 start = player.transform.position;
        Vector3 shaftStart = new Vector3(ShaftX, start.y, 0f);
        Vector3 shaftEnd = new Vector3(ShaftX,
            ascending ? UpperExit.y : LowerExit.y, 0f);
        yield return Move(start, shaftStart, .3f);
        yield return Move(shaftStart, shaftEnd, 1.35f);
        Vector3 exit = ascending ? UpperExit : LowerExit;
        yield return Move(shaftEnd, exit, .35f);
        Restore(exit);
    }

    IEnumerator Move(Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && rider != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(elapsed / duration));
            rider.transform.position = Vector3.LerpUnclamped(from, to, t);
            yield return null;
        }
        if (rider != null) rider.transform.position = to;
    }

    void Restore(Vector3 exit)
    {
        if (rider != null && body != null)
        {
            rider.transform.position = exit;
            body.position = exit;
            body.simulated = true;
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            rider.enabled = controllerWasEnabled;
            if (dash != null) dash.enabled = dashWasEnabled;
        }
        riding = false;
        rider = null;
        body = null;
        dash = null;
    }

    void OnDisable()
    {
        if (riding) Restore(rider != null ? rider.transform.position : LowerExit);
    }

    void OnGUI()
    {
        if (riding) return;
        var player = FindAnyObjectByType<playercontroler>();
        if (player == null || PauseMenu.InputBlocked) return;
        bool below = player.transform.position.y < 0f;
        if (Mathf.Abs(player.transform.position.x - ShaftX) > 4.2f ||
            Mathf.Abs(player.transform.position.y -
                (below ? LowerExit.y : UpperExit.y)) > 2f) return;
        var style = new GUIStyle(GUI.skin.label) {
            alignment = TextAnchor.MiddleCenter, fontSize = 18
        };
        style.normal.textColor = new Color(1f, .81f, .59f);
        GUI.Label(new Rect(0, Screen.height - 90, Screen.width, 35),
            GameText.Get(below ? "interaction.pier_climb" : "interaction.pier_descend"),
            style);
    }
}
