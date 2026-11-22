#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class BossArenaCollisionVerification
{
    [MenuItem("Tools/Ashrisen/Boss/Verify Arena Traversal")]
    public static void Verify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run the arena traversal check in Edit Mode.");

        var report = "";
        void Check(bool passed, string description)
        {
            report += (passed ? "PASS " : "FAIL ") + description + "\n";
            if (!passed) throw new InvalidOperationException(description);
        }

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/BossArena.unity");
        var floor = GameObject.Find("Continuous cathedral paving");
        var player = UnityEngine.Object.FindAnyObjectByType<playercontroler>();
        var boss = UnityEngine.Object.FindAnyObjectByType<BossArenaCombat>();
        if (floor == null || player == null || boss == null)
            throw new InvalidOperationException("The arena, heroine or boss is missing.");
        var paving = floor.GetComponent<BoxCollider2D>();
        var heroine = player.GetComponent<CapsuleCollider2D>();
        var body = player.GetComponent<Rigidbody2D>();
        var bossBody = boss.GetComponent<Rigidbody2D>();
        Check(paving != null && floor.GetComponent<TilemapCollider2D>() == null &&
            Mathf.Abs(paving.bounds.min.x) < .01f && Mathf.Abs(paving.bounds.max.x - 26f) < .01f &&
            Mathf.Abs(paving.bounds.max.y) < .01f, "floor is one flat collider across the arena");
        Check(heroine != null && player.GetComponent<BoxCollider2D>() == null &&
            body.collisionDetectionMode == CollisionDetectionMode2D.Continuous,
            "heroine has a rounded continuous collision shape");
        Check(GameObject.Find("West stone pier")?.GetComponent<BoxCollider2D>() != null &&
            GameObject.Find("East sanctuary pier")?.GetComponent<BoxCollider2D>() != null,
            "both arena boundaries remain solid");

        var oldSimulation = Physics2D.simulationMode;
        var oldPosition = body.position;
        var oldVelocity = body.linearVelocity;
        bool bossSimulated = bossBody.simulated;
        try
        {
            bossBody.simulated = false;
            Physics2D.simulationMode = SimulationMode2D.Script;
            const float step = .02f;
            void Simulate(float horizontal)
            {
                body.linearVelocity = new Vector2(horizontal, body.linearVelocity.y);
                Physics2D.Simulate(step);
            }
            void Place(float x)
            {
                body.position = new Vector2(x, 1.5f);
                body.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
                for (int i = 0; i < 8; i++) Simulate(0f);
            }
            bool Drive(float target, float speed, int steps)
            {
                int stalled = 0;
                for (int i = 0; i < steps; i++)
                {
                    float before = body.position.x;
                    Simulate(speed);
                    if (Mathf.Abs(body.position.x - before) < .01f) stalled++;
                    else stalled = 0;
                    if (stalled > 12 || Mathf.Abs(body.position.y - 1.5f) > .2f) return false;
                    if ((speed > 0f && body.position.x >= target) ||
                        (speed < 0f && body.position.x <= target)) return true;
                }
                return false;
            }

            Place(1.2f);
            Check(Drive(24.4f, 6f, 230), "heroine walks across every floor seam to the east");
            Check(Drive(1.2f, -6f, 230), "heroine walks back across the entire floor");

            Place(24.2f);
            for (int i = 0; i < 35; i++) Simulate(18f);
            Check(body.position.x <= 25.52f && body.position.x >= 24.2f,
                "right wall contains the dash");
            Check(Drive(22.5f, -6f, 40), "heroine can move away from the right wall");

            Place(1.8f);
            for (int i = 0; i < 35; i++) Simulate(-18f);
            Check(body.position.x >= .48f && body.position.x <= 1.8f,
                "left wall contains the dash");
            Check(Drive(3.5f, 6f, 40), "heroine can move away from the left wall");

            Place(12f);
            body.linearVelocity = new Vector2(6f, player.jumpForce);
            float peak = body.position.y;
            for (int i = 0; i < 50; i++)
            {
                Simulate(6f);
                peak = Mathf.Max(peak, body.position.y);
            }
            Check(peak > 2.5f && body.position.x > 17f,
                "jump carries the heroine cleanly across the arena");
        }
        finally
        {
            body.position = oldPosition;
            body.linearVelocity = oldVelocity;
            bossBody.simulated = bossSimulated;
            Physics2D.simulationMode = oldSimulation;
            Physics2D.SyncTransforms();
        }
        report += "TOTAL 10 passed, 0 failed\n";
        var path = Path.Combine(Path.GetDirectoryName(Application.dataPath),
            "Artifacts/BossArenaCollisionVerification.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, report);
        Debug.Log(report, player);
    }
}
#endif
