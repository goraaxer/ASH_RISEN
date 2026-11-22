using UnityEditor;
using UnityEngine;

// Editor-only physical traversal check for the village entrance stair.
public static class AshenVillageTraversalProbe
{
    static playercontroler player;
    static Rigidbody2D body;
    static Collider2D playerCollider;
    static float started;
    static float lastJump;
    static int jumps;
    static string status = "Not started";

    public static string Begin()
    {
        if (!EditorApplication.isPlaying) return "Play Mode required";
        EditorApplication.update -= Tick;
        player = Object.FindAnyObjectByType<playercontroler>();
        if (player == null) return "No heroine";
        body = player.GetComponent<Rigidbody2D>();
        playerCollider = player.GetComponent<Collider2D>();
        player.enabled = false;
        body.position = new Vector2(84.5f, .5f);
        player.transform.position = body.position;
        body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        started = Time.time;
        lastJump = started - 2f;
        jumps = 0;
        status = "Running";
        EditorApplication.update += Tick;
        return "Started physical ascent at x=84.5";
    }

    static bool Grounded()
    {
        var bounds = playerCollider.bounds;
        var hits = Physics2D.OverlapBoxAll(
            new Vector2(bounds.center.x, bounds.min.y - .05f),
            new Vector2(bounds.size.x * .8f, .1f), 0f);
        foreach (var hit in hits)
            if (hit != null && hit != playerCollider && !hit.isTrigger) return true;
        return false;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || player == null)
        { EditorApplication.update -= Tick; return; }
        if (Time.time - started > 13f)
        { Finish("FAIL timeout at " + player.transform.position +
            " after " + jumps + " jumps"); return; }
        if (body.position.x >= 100f && body.position.y >= 4.5f)
        { Finish("PASS reached upper deck at " + body.position +
            " using " + jumps + " jumps"); return; }
        var velocity = body.linearVelocity;
        velocity.x = player.speed;
        if (velocity.y < 0f)
            velocity.y += Physics2D.gravity.y * body.gravityScale *
                (player.fallMultiplier - 1f) * Time.deltaTime;
        if (Grounded() && Time.time - lastJump > .4f && body.position.x < 100f)
        {
            velocity.y = player.jumpForce;
            lastJump = Time.time;
            jumps++;
        }
        body.linearVelocity = velocity;
    }

    static void Finish(string message)
    {
        status = message;
        EditorApplication.update -= Tick;
        if (player != null) player.enabled = true;
    }

    public static string Status() { return status; }
}
