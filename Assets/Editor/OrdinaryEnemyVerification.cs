#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class OrdinaryEnemyVerification
{
    const string Flag = "ashrisen.ordinary.enemies.verify";
    static OrdinaryEnemyVerification() { EditorApplication.playModeStateChanged += Changed; }

    [MenuItem("Tools/Ashrisen/Enemies/Verify two ordinary enemies")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new System.InvalidOperationException("Save the scene before verification.");
            EditorSceneManager.OpenScene("Assets/Scenes/AshenVillage.unity");
        }
        SessionState.SetBool(Flag, true);
        if (EditorApplication.isPlaying) Spawn();
        else EditorApplication.isPlaying = true;
    }

    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Flag, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) Spawn();
        if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Flag, false);
    }

    static void Spawn()
    {
        if (Object.FindAnyObjectByType<OrdinaryEnemyPlayCheck>() == null)
            new GameObject("Ordinary enemy verification").AddComponent<OrdinaryEnemyPlayCheck>();
    }
}

public sealed class OrdinaryEnemyPlayCheck : MonoBehaviour
{
    int passed, failed;
    string report = "";
    void Check(bool result, string label)
    {
        report += (result ? "PASS " : "FAIL ") + label + "\n";
        if (result) passed++; else failed++;
    }

    IEnumerator Start()
    {
        yield return null;
        var player = Object.FindAnyObjectByType<playercontroler>();
        var enemies = Object.FindObjectsByType<GothicEnemy>(FindObjectsSortMode.None);
        var warden = enemies.FirstOrDefault(e => e.kind == GothicEnemyKind.GraveWarden);
        var marksman = enemies.FirstOrDefault(e => e.kind == GothicEnemyKind.BellMarksman);
        Check(player != null && warden != null && marksman != null,
            "village contains the heroine and both new enemy types");
        if (player == null || warden == null || marksman == null) { Finish(); yield break; }

        Check(warden.transform.position.x > 55f && warden.transform.position.x < 67f &&
            marksman.transform.position.x > 180f && marksman.transform.position.x < 188f &&
            marksman.transform.position.y > 3f,
            "warden guards the burial lane and marksman stands on a roof");
        Check(warden.GetComponent<SpriteRenderer>().sprite.texture.filterMode == FilterMode.Point &&
            marksman.GetComponent<SpriteRenderer>().sprite.texture.filterMode == FilterMode.Point &&
            warden.idle.Length == 3 && warden.walk.Length == 6 && warden.attack.Length == 6 &&
            warden.hurt.Length == 4 && warden.death.Length == 8 &&
            marksman.idle.Length == 3 && marksman.walk.Length == 6 && marksman.attack.Length == 6 &&
            marksman.hurt.Length == 4 && marksman.death.Length == 8,
            "both original pixel characters have imported animation poses");
        Check(PixelDifference(warden.walk[0], warden.walk[3]) > 500 &&
            PixelDifference(warden.attack[2], warden.attack[3]) > 1000 &&
            PixelDifference(marksman.walk[0], marksman.walk[3]) > 350 &&
            PixelDifference(marksman.attack[2], marksman.attack[3]) > 450,
            "walk and attack poses visibly change both enemy sprites");

        float wardenStartX = warden.transform.position.x;
        float marksmanStartX = marksman.transform.position.x;
        yield return new WaitForSecondsRealtime(.5f);
        Check(warden.State == "Walk" && Mathf.Abs(warden.transform.position.x - wardenStartX) > .3f &&
            System.Array.IndexOf(warden.walk, warden.GetComponent<SpriteRenderer>().sprite) >= 0,
            "grave warden walks with its six-frame stride");
        Check(marksman.State == "Walk" && Mathf.Abs(marksman.transform.position.x - marksmanStartX) > .2f &&
            System.Array.IndexOf(marksman.walk, marksman.GetComponent<SpriteRenderer>().sprite) >= 0,
            $"bell marksman patrols the roof with a walk animation (state={marksman.State}, dx={marksman.transform.position.x-marksmanStartX:F2}, sprite={marksman.GetComponent<SpriteRenderer>().sprite?.name})");

        var body = player.GetComponent<Rigidbody2D>();
        var health = player.GetComponent<AshHealth>();
        health.ResetHealth();
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
        player.transform.position = warden.transform.position + new Vector3(-1.5f, .1f, 0f);
        body.position = player.transform.position;
        Physics2D.SyncTransforms();
        int beforeHealth = health.CurrentHealth;
        float until = Time.time + 2.5f;
        bool windup = false, strike = false, wardenAttackPose = false;
        float closest = float.PositiveInfinity;
        while (Time.time < until && health.CurrentHealth == beforeHealth)
        {
            windup |= warden.State == "Windup";
            strike |= warden.State == "Strike";
            wardenAttackPose |= System.Array.IndexOf(warden.attack,
                warden.GetComponent<SpriteRenderer>().sprite) >= 0;
            closest = Mathf.Min(closest, Mathf.Abs(warden.transform.position.x - player.transform.position.x));
            yield return new WaitForSecondsRealtime(.02f);
        }
        Check(windup, "grave warden telegraphs a melee swing");
        Check(wardenAttackPose, "grave warden uses its attack frames during the swing");
        Check(health.CurrentHealth < beforeHealth,
            $"grave warden strike damages the heroine (strike={strike}, closest={closest:F2}, position={warden.transform.position.x:F2})");

        warden.ResetEnemy();
        warden.enabled = false;
        yield return new WaitForSecondsRealtime(.5f);
        player.transform.position = warden.transform.position + new Vector3(-1.25f, .1f, 0f);
        body.position = player.transform.position;
        body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        int enemyHealth = warden.Health;
        bool sword = player.GetComponent<PlayerAttackAnimation>().TryAttack(SwordDirection.Horizontal);
        Check(sword && warden.Health < enemyHealth, "heroine's normal sword damages the grave warden");
        yield return new WaitForSecondsRealtime(.38f);
        Check(warden.TakeDamage(warden.Health, player.transform.position) && warden.Health == 0 &&
            !warden.hurtbox.enabled, "grave warden death disables its sword hurtbox");
        var deathStart = warden.GetComponent<SpriteRenderer>().sprite;
        warden.enabled = true;
        yield return new WaitForSecondsRealtime(.3f);
        Check(warden.State == "Death" && warden.GetComponent<SpriteRenderer>().sprite != deathStart,
            "grave warden plays distinct death frames");
        GothicEnemy.ResetAll();
        Check(warden.Health == warden.maxHealth && warden.hurtbox.enabled,
            "checkpoint reset restores the new enemies");
        warden.enabled = false;

        foreach (var dog in Object.FindObjectsByType<AshHound>(FindObjectsSortMode.None))
            dog.gameObject.SetActive(false);
        health.ResetHealth();
        player.transform.position = marksman.transform.position + new Vector3(4f, .05f, 0f);
        body.position = player.transform.position;
        body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        beforeHealth = health.CurrentHealth;
        until = Time.time + 3.5f;
        bool rangedWindup = false, boltSeen = false, marksmanAttackPose = false;
        while (Time.time < until)
        {
            rangedWindup |= marksman.State == "Windup";
            boltSeen |= Object.FindAnyObjectByType<GothicBolt>() != null;
            marksmanAttackPose |= System.Array.IndexOf(marksman.attack,
                marksman.GetComponent<SpriteRenderer>().sprite) >= 0;
            yield return new WaitForSecondsRealtime(.02f);
        }
        Check(rangedWindup, "rooftop marksman telegraphs a shot");
        Check(marksmanAttackPose, "rooftop marksman uses its firing frames");
        Check(boltSeen, "rooftop marksman releases a visible projectile");
        Check(health.CurrentHealth < beforeHealth, "rooftop bolt damages the heroine");

        marksman.ResetEnemy();
        marksman.enabled = false;
        health.ResetHealth();
        player.transform.position = marksman.transform.position + new Vector3(3f, .05f, 0f);
        body.position = player.transform.position;
        body.linearVelocity = Vector2.zero;
        player.GetComponent<SpriteRenderer>().flipX = true;
        Physics2D.SyncTransforms();
        var reflectedBolt = Instantiate(marksman.boltPrefab,
            player.transform.position + new Vector3(-.8f, 1.1f, 0f), Quaternion.identity);
        reflectedBolt.Launch(Vector2.right, marksman);
        int beforeParryHealth = health.CurrentHealth;
        int beforeParryEnemyHealth = marksman.Health;
        bool parrySwing = player.GetComponent<PlayerAttackAnimation>().TryAttack(SwordDirection.Horizontal);
        Check(parrySwing && reflectedBolt.IsDeflected && health.CurrentHealth == beforeParryHealth,
            "sword deflects the marksman's bolt without hurting the heroine");
        yield return new WaitForSecondsRealtime(.35f);
        Check(marksman.Health < beforeParryEnemyHealth,
            "reflected bolt returns and damages its shooter");
        marksman.enabled = true;
        yield return new WaitForSecondsRealtime(.38f);
        Check(marksman.TakeDamage(1, player.transform.position) && marksman.State == "Hurt",
            "marksman plays a hurt pose");
        yield return new WaitForSecondsRealtime(.38f);
        Check(marksman.TakeDamage(marksman.Health, player.transform.position) &&
            marksman.State == "Death", "marksman plays death after a lethal hit");

        Finish();
    }

    static int PixelDifference(Sprite first, Sprite second)
    {
        var a = new Texture2D(2, 2);
        var b = new Texture2D(2, 2);
        try
        {
            a.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(first)));
            b.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(second)));
            Color32[] left = a.GetPixels32(), right = b.GetPixels32();
            int changed = 0;
            for (int i = 0; i < left.Length; i++)
                if (!left[i].Equals(right[i])) changed++;
            return changed;
        }
        finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
    }

    void Finish()
    {
        report += "TOTAL " + passed + " passed, " + failed + " failed\n";
        Directory.CreateDirectory("Artifacts/OrdinaryEnemyVerification");
        File.WriteAllText("Artifacts/OrdinaryEnemyVerification/results.txt", report);
        Debug.Log("Ordinary enemy verification: " + passed + " passed, " + failed + " failed");
        EditorApplication.isPlaying = false;
    }
}
#endif
