using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Deterministic geometry audit. It does not simulate human input or claim a manual clear.
public static class AshenVillageRouteValidator
{
    static readonly List<string> issues = new List<string>();

    static void Require(bool condition, string message)
    { if (!condition) issues.Add(message); }

    static void Cells(Tilemap map, int a, int b, int y, string label)
    {
        for (int x = a; x <= b; x++)
            if (!map.HasTile(new Vector3Int(x, y, 0)))
                issues.Add(label + " missing at x=" + x + ", y=" + y);
    }

    static void Stair(Tilemap map, int a, int y, int count, int direction,
        float maxRise, string label)
    {
        for (int i = 0; i < count; i++)
        {
            int height = y + i * direction;
            Require(map.HasTile(new Vector3Int(a + i, height, 0)),
                label + " missing step " + i);
            if (i > 0) Require(Mathf.Abs(direction) <= maxRise,
                label + " step above jump rise");
        }
    }

    static void Chasm(Tilemap ground, int a, int b)
    {
        for (int x = a; x <= b; x++)
        for (int y = -3; y <= -2; y++)
            Require(!ground.HasTile(new Vector3Int(x, y, 0)),
                "Street chasm blocked at " + x + "," + y);
        var pit = GameObject.Find("Street chasm " + a + "-" + b);
        Require(pit != null && pit.GetComponent<BoxCollider2D>() != null &&
            pit.GetComponent<BoxCollider2D>().isTrigger,
            "Street chasm respawn trigger missing at " + a);
    }

    public static string Validate()
    {
        issues.Clear();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/AshenVillage.unity",
            OpenSceneMode.Single);
        var player = UnityEngine.Object.FindAnyObjectByType<playercontroler>();
        Require(player != null, "Heroine missing");
        if (player == null) return string.Join("; ", issues.ToArray());
        var body = player.GetComponent<Rigidbody2D>();
        float gravity = Mathf.Abs(Physics2D.gravity.y) * body.gravityScale;
        float maxRise = player.jumpForce * player.jumpForce / (2f * gravity);
        float upwardTime = player.jumpForce / gravity;
        float downwardTime = Mathf.Sqrt(2f * maxRise /
            (gravity * player.fallMultiplier));
        float flatReach = player.speed * (upwardTime + downwardTime);
        Require(maxRise > 1.4f, "Heroine jump rise too low for stairs");
        Require(flatReach > 4f, "Heroine jump reach too short for graveyard gap");
        var ground = GameObject.Find("Solid cobblestone and ash soil").GetComponent<Tilemap>();
        var platforms = GameObject.Find("Solid roofs balconies stairs and bridges")
            .GetComponent<Tilemap>();
        var grave = GameObject.Find("Walkable cobblestone and soil").GetComponent<Tilemap>();
        var graveBridges = GameObject.Find("Solid elevated stone bridges")
            .GetComponent<Tilemap>();
        Require(GameObject.Find("Stone collision inside visible arch lintel") == null,
            "Old arch collider still blocks the cemetery street");
        for (int i = 0; i < 3; i++)
        {
            var arch = GameObject.Find("Assembled Gothic bridge arch " + i);
            var shape = arch != null ? arch.GetComponent<PolygonCollider2D>() : null;
            Require(shape != null && shape.pathCount > 0,
                "Gothic arch stone is missing its shaped collider: " + i);
        }
        Require(graveBridges.HasTile(new Vector3Int(27, 0, 0)),
            "Left arch jump buttress missing");
        foreach (int x in new[] { 18, 22, 40, 45, 49, 52 })
        {
            var column = GameObject.Find("Solid bridge column " + x);
            var collider = column != null ? column.GetComponent<BoxCollider2D>() : null;
            Require(collider != null &&
                Mathf.Abs(collider.bounds.min.y -
                    grave.CellToWorld(new Vector3Int(x, 0, 0)).y) < .05f,
                "Bridge column collider missing or misaligned at x=" + x);
        }
        foreach (var step in new[] { new Vector3Int(16,0,0),
            new Vector3Int(38,0,0), new Vector3Int(47,1,0),
            new Vector3Int(54,1,0), new Vector3Int(55,0,0) })
            Require(graveBridges.HasTile(step),
                "Cemetery parkour step missing at " + step);
        foreach (int x in new[] { 47, 54 })
            Require(grave.HasTile(new Vector3Int(x, 0, 0)),
                "Cemetery parkour step has no solid masonry at x=" + x);
        Require(ground.GetComponent<TilemapCollider2D>() != null, "Village ground collider missing");
        Require(platforms.GetComponent<TilemapCollider2D>() != null, "Village platforms collider missing");
        Cells(ground, 79, 119, -2, "Main street west A");
        Cells(ground, 123, 146, -2, "Main street west B");
        Cells(ground, 150, 166, -2, "Main street west C");
        Cells(ground, 170, 179, -2, "Main street west D");
        Cells(ground, 183, 216, -2, "Main street west E");
        Cells(ground, 249, 261, -2, "Main street east A");
        Cells(ground, 264, 354, -2, "Main street east B");
        foreach (var pair in new[] { new Vector2Int(120,122),
            new Vector2Int(147,149), new Vector2Int(167,169),
            new Vector2Int(180,182), new Vector2Int(262,263) })
            Chasm(ground, pair.x, pair.y);
        Cells(platforms, 214, 215, 2, "Viaduct before pier fork");
        Cells(platforms, 220, 248, 2, "Viaduct after pier fork");
        bool pierOpening = true;
        for (int x = 216; x <= 219; x++)
            pierOpening &= !platforms.HasTile(new Vector3Int(x, 2, 0));
        var harborClimb = GameObject.Find("Pier bridge iron climb");
        Require(pierOpening && harborClimb != null &&
            harborClimb.GetComponent<VillagePierLadder>() != null,
            "Pier climb or bridge opening missing");
        Cells(ground, 217, 250, -6, "Pier approach");
        Cells(ground, 214, 216, -6, "Left quay retaining wall");
        Cells(ground, 215, 216, -7, "Left quay foundation");
        for (int x = 249; x <= 251; x++)
        for (int y = -7; y <= -4; y++)
            Require(ground.HasTile(new Vector3Int(x, y, 0)),
                "Right quay retaining wall missing at " + x + "," + y);
        for (int x = 217; x <= 219; x++)
        for (int y = -5; y <= 2; y++)
            Require(!ground.HasTile(new Vector3Int(x, y, 0)) &&
                !platforms.HasTile(new Vector3Int(x, y, 0)),
                "Pier climb shaft blocked at " + x + "," + y);
        var decor = GameObject.Find("Railings columns details and branch gates")
            .GetComponent<Tilemap>();
        for (int x = 221; x <= 248; x++)
        {
            var visible = decor.GetTile(new Vector3Int(x, -5, 0));
            Require(visible == null ||
                (visible.name != "Village_46" && visible.name != "Village_47"),
                "Pier deck has visible tiles without collision at " + x);
        }
        Cells(platforms, 279, 295, 4, "Forest approach west");
        Stair(platforms, 211, -1, 4, 1, maxRise, "Viaduct ascent");
        Stair(platforms, 192, 2, 4, -1, maxRise, "Square roof descent");
        Require(!platforms.HasTile(new Vector3Int(249,1,0)),
            "Old viaduct brick stack remains");
        for (int y = -5; y < 2; y++)
            Require(!decor.HasTile(new Vector3Int(247, y, 0)),
                "Old bridge pillar still stands over the pier hatch");
        Require(GameObject.Find("Closed route to the pier") == null &&
            GameObject.Find("Locked hatch toward the future pier")
                .GetComponent<VillageHarborHatch>() != null,
            "Old pier gate remains or locked hatch is missing");
        for (int x = 276; x <= 278; x++)
        for (int y = 0; y <= 4; y++)
            Require(!platforms.HasTile(new Vector3Int(x, y, 0)),
                "Church street blocked by old forest stair at " + x + "," + y);
        Require(!platforms.HasTile(new Vector3Int(303, 4, 0)),
            "Floating forest gate island remains");
        for (int step = 0; step < 5; step++)
        for (int x = 86 + step * 2; x <= 87 + step * 2; x++)
        {
            int top = -1 + step;
            Require(platforms.HasTile(new Vector3Int(x, top, 0)),
                "Village entrance stair missing top at " + x);
            for (int y = -1; y < top; y++)
                Require(platforms.HasTile(new Vector3Int(x, y, 0)),
                    "Village entrance stair missing solid support at " + x + "," + y);
        }
        foreach (var ledge in new[] { new Vector3Int(98,3,0), new Vector3Int(100,3,0),
            new Vector3Int(106,3,0), new Vector3Int(119,3,0),
            new Vector3Int(123,3,0), new Vector3Int(128,3,0),
            new Vector3Int(131,3,0), new Vector3Int(146,3,0),
            new Vector3Int(150,3,0), new Vector3Int(166,3,0),
            new Vector3Int(170,3,0), new Vector3Int(179,3,0),
            new Vector3Int(183,3,0), new Vector3Int(191,3,0) })
            Require(platforms.HasTile(ledge), "Roof route missing " + ledge);
        Require(flatReach > 4f, "Roof gaps above jump reach");
        Require(!platforms.HasTile(new Vector3Int(121,3,0)),
            "Roof drop gap has been blocked");
        foreach (int x in new[] { 148, 168, 181 })
            Require(!platforms.HasTile(new Vector3Int(x, 3, 0)),
                "Lower street drop gap blocked at " + x);
        foreach (var ledge in new[] { new Vector3Int(250,3,0),
            new Vector3Int(261,3,0), new Vector3Int(264,4,0),
            new Vector3Int(274,4,0) })
            Require(platforms.HasTile(ledge), "Upper city balcony missing " + ledge);
        Require(!platforms.HasTile(new Vector3Int(262, 3, 0)),
            "Upper city drop gap blocked");
        foreach (var name in new[] { "Outskirts checkpoint lantern" })
        {
            var lantern = GameObject.Find(name);
            Require(lantern != null && lantern.transform.position.y > 4f,
                name + " is still below the roof route");
        }
        var details = GameObject.Find("Railings columns details and branch gates")
            .GetComponent<Tilemap>();
        Require(!details.HasTile(new Vector3Int(240,5,0)),
            "Floating decorative deck remains above the viaduct");
        var railings = GameObject.Find("Iron railings behind the heroine").GetComponent<Tilemap>();
        Require(railings.GetComponent<Collider2D>() == null,
            "Decorative roof railings have collider");
        Require(grave.HasTile(new Vector3Int(78,-1,0)) &&
                ground.HasTile(new Vector3Int(79,-2,0)), "Cemetery and village do not meet");
        Require(GameObject.Find("Invisible eastern boundary") == null,
            "Old cemetery invisible boundary remains");
        var door = UnityEngine.Object.FindAnyObjectByType<WorldDoor>();
        Require(door != null && door.targetScene == "BossArena", "Church door target invalid");
        Require(UnityEngine.Object.FindObjectsByType<VillageBranchGate>(
            FindObjectsInactive.Include).Length == 1, "Forest gate missing");
        Require(UnityEngine.Object.FindObjectsByType<VillageDistrictBackdrop>(
            FindObjectsInactive.Include).Length == 10, "District parallax layers missing");
        return issues.Count == 0 ?
            "PASS: main street, viaduct, roof route, pier, forest, cemetery seam, " +
            "church door and colliders. Jump rise=" + maxRise.ToString("F2") +
            "u, flat reach≈" + flatReach.ToString("F2") + "u. " +
            "This is a geometry audit, not a manual playthrough." :
            "FAIL: " + string.Join("; ", issues.ToArray());
    }
}
