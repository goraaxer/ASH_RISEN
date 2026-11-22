#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ResponsiveCombatVerification
{
    const string Flag = "ashrisen.combat.verify";
    static ResponsiveCombatVerification() { EditorApplication.playModeStateChanged += Changed; }

    [MenuItem("Tools/Ashrisen/Verify Responsive Combat")]
    public static void Run()
    {
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
        if (Object.FindAnyObjectByType<ResponsiveCombatPlayCheck>() == null)
            new GameObject("Responsive combat verification").AddComponent<ResponsiveCombatPlayCheck>();
    }
}

public sealed class ResponsiveCombatPlayCheck : MonoBehaviour
{
    string report = "";
    int passed, failed;
    void Check(bool result, string label)
    {
        report += (result ? "PASS " : "FAIL ") + label + "\n";
        if (result) passed++; else failed++;
    }
    static IEnumerator WaitSwing() { yield return new WaitForSecondsRealtime(.42f); }

    IEnumerator Start()
    {
        yield return null;
        var player = Object.FindAnyObjectByType<playercontroler>();
        var attack = player.GetComponent<PlayerAttackAnimation>();
        var body = player.GetComponent<Rigidbody2D>();
        var animator = player.GetComponent<Animator>();
        var dash = player.GetComponent<PlayerDash>();
        var boss = Object.FindObjectsByType<BossArenaCombat>(FindObjectsInactive.Include, FindObjectsSortMode.None)[0];
        var controller = Object.FindAnyObjectByType<BossArenaController>();
        var arrival = Object.FindAnyObjectByType<BossArrival>();
        if (arrival != null) arrival.enabled = false;
        if (controller != null) controller.enabled = false;
        BossArenaProgress.Defeated = false;
        boss.gameObject.SetActive(true);
        boss.ResetFight();
        boss.BeginArenaCombat(player.transform);
        boss.SuspendArenaCombat(false);
        boss.enabled = false;
        body.gravityScale = 0f;
        player.GetComponent<SpriteRenderer>().flipX = false;
        yield return new WaitForSecondsRealtime(.1f);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/Enemies/AshHound.prefab");
        var hound = Object.Instantiate(prefab, new Vector3(1.45f, 0, 0), Quaternion.identity).GetComponent<AshHound>();
        hound.enabled = false;
        player.transform.position = new Vector3(0, 1.52f, 0);
        body.linearVelocity = Vector2.zero;
        player.isGrounded = true;
        Physics2D.SyncTransforms();
        int hp = hound.Health;
        Check(attack.TryAttack(SwordDirection.Horizontal) && hound.Health == hp - 1, "ground slash damages on press");
        Check(CombatAudio.LastCue == CombatCue.SwordImpact,
            "enemy contact uses the impact sound");
        Check(GameObject.Find("Sword ash impact") == null,
            "sword hit has no unsupported particle flash");
        Check(attack.HitboxActive, "hitbox opens immediately");
        Check(!attack.TryAttack(SwordDirection.Horizontal), "same swing cannot restart");
        yield return new WaitForSecondsRealtime(.24f);
        Check(!attack.TryAttack(SwordDirection.Horizontal), "follow-up waits for readable slash recovery");
        yield return new WaitForSecondsRealtime(.18f);
        Check(!attack.IsAttacking && !attack.HitboxActive && animator.speed == 1f, "swing recovers without stuck animation");
        Check(hound.Health == hp - 1, "one target receives one hit per swing");
        hound.transform.position = new Vector3(1.45f, 0, 0);
        Physics2D.SyncTransforms();
        hp = hound.Health;
        Check(attack.TryAttack(SwordDirection.Horizontal) && hound.Health == hp - 1, "quick follow-up slash damages light enemy");
        yield return WaitSwing();

        // Running and airborne attacks keep their own original-art poses.
        hound.transform.position = new Vector3(20, 0, 0);
        body.linearVelocity = new Vector2(3, 0);
        player.isGrounded = true;
        Check(attack.TryAttack(SwordDirection.Horizontal) && animator.GetCurrentAnimatorStateInfo(0).IsName("RunningSlash"), "running slash state");
        yield return WaitSwing();
        body.linearVelocity = new Vector2(0, 2);
        player.isGrounded = false;
        animator.Play("JumpRise", 0, .4f);
        animator.Update(0f);
        Check(attack.TryAttack(SwordDirection.Horizontal) && animator.GetCurrentAnimatorStateInfo(0).IsName("JumpAttack"), "air slash state");
        yield return WaitSwing();
        var airRecovery = animator.GetCurrentAnimatorStateInfo(0);
        Check(!attack.IsAttacking && airRecovery.IsName("JumpRise") && airRecovery.normalizedTime > .45f, "air slash resumes current jump phase");

        boss.transform.position = new Vector3(.2f, 2.72f, 0);
        Physics2D.SyncTransforms();
        hp = boss.CurrentHealth;
        Check(attack.TryAttack(SwordDirection.Up) && animator.GetCurrentAnimatorStateInfo(0).IsName("UpwardSlash") && boss.CurrentHealth == hp - 1, "up slash hits target above");
        yield return WaitSwing();

        player.transform.position = new Vector3(boss.transform.position.x, boss.GetComponent<Collider2D>().bounds.max.y + 1.5f, 0);
        body.linearVelocity = Vector2.down;
        player.isGrounded = false;
        Physics2D.SyncTransforms();
        hp = boss.CurrentHealth;
        Check(attack.TryAttack(SwordDirection.Down) && boss.CurrentHealth == hp - 1 && body.linearVelocity.y >= player.pogoForce - .01f, "down slash pogos from boss");
        yield return WaitSwing();

        player.transform.position = new Vector3(-8, 8, 0);
        body.linearVelocity = Vector2.down;
        player.isGrounded = false;
        Physics2D.SyncTransforms();
        Check(attack.TryAttack(SwordDirection.Down) && body.linearVelocity.y <= 0f, "down slash misses without pogo");
        Check(CombatAudio.LastCue == CombatCue.SwordSwing,
            "empty slash uses only the air swing sound");
        yield return WaitSwing();

        hound.ResetDog();
        hound.transform.position = new Vector3(-8, 6, 0);
        player.transform.position = new Vector3(-8, 8, 0);
        body.linearVelocity = Vector2.down;
        player.isGrounded = false;
        Physics2D.SyncTransforms();
        hp = hound.Health;
        Check(attack.TryAttack(SwordDirection.Down) && hound.Health == hp - 1 && body.linearVelocity.y >= player.pogoForce - .01f, "down slash pogos from enemy");
        yield return WaitSwing();

        player.transform.position = new Vector3(0, 1.52f, 0);
        body.linearVelocity = Vector2.zero;
        player.isGrounded = true;
        boss.transform.position = new Vector3(-2, 1.3f, 0);
        Physics2D.SyncTransforms();
        hp = boss.CurrentHealth;
        bool behindAttack = attack.TryAttack(SwordDirection.Horizontal);
        Check(behindAttack && boss.CurrentHealth == hp, "forward hitbox rejects target behind (accepted=" + behindAttack + ", hp=" + hp + "->" + boss.CurrentHealth + ", facing=" + player.FacingDirection + ")");
        yield return WaitSwing();
        boss.transform.position = new Vector3(2, 1.3f, 0);
        Physics2D.SyncTransforms();
        hp = boss.CurrentHealth;
        Check(attack.TryAttack(SwordDirection.Horizontal) && boss.CurrentHealth == hp - 1, "repeat slash hits after recovery");
        yield return WaitSwing();

        player.transform.position = boss.transform.position + new Vector3(.6f, 3f, 0);
        Physics2D.SyncTransforms();
        boss.BeginAttack();
        Check(boss.IsAttacking && boss.IsHighClaw && boss.CurrentHitboxOffset.y > boss.attackHitboxOffset.y, "boss answers an airborne player with high claw");
        boss.CancelAttack();
        player.transform.position = boss.transform.position + new Vector3(3f, 1.5f, 0);
        body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        int originalBossDamage = boss.attackDamage;
        boss.attackDamage = 0;
        boss.enabled = true;
        boss.BeginAttack();
        bool stepSelected = boss.IsAttacking && boss.IsStepClaw && !boss.IsHighClaw;
        float beforeStep = boss.transform.position.x;
        yield return new WaitForSecondsRealtime(.18f);
        Check(stepSelected && boss.transform.position.x > beforeStep + .1f, "boss steps into a claw from longer range");
        boss.CancelAttack();
        boss.enabled = false;
        boss.attackDamage = originalBossDamage;

        boss.transform.position = new Vector3(20, 0, 0);
        Check(attack.TryAttack(SwordDirection.Horizontal), "slash begins before dash");
        int beforeDashSound = CombatAudio.PlayCount;
        typeof(PlayerDash).GetMethod("StartDash", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(dash, null);
        Check(dash.IsDashing && !attack.IsAttacking, "dash cancels swing cleanly");
        Check(CombatAudio.PlayCount > beforeDashSound && CombatAudio.LastCue == CombatCue.PlayerDash,
            "dash plays its own sound on start");
        dash.CancelDash();
        yield return WaitSwing();
        Check(attack.TryAttack(SwordDirection.Horizontal), "attack works after dash");
        attack.EndAttack();
        body.linearVelocity = new Vector2(0, player.jumpForce);
        player.isGrounded = false;
        Check(body.linearVelocity.y > 0 && !attack.IsAttacking, "jump movement remains available after attack");

        // Queue the same request that keyboard input sends, then exercise each grace window.
        player.transform.position = new Vector3(-8, 8, 0);
        body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        var groundedField = typeof(playercontroler).GetField("lastGroundedAt", BindingFlags.NonPublic | BindingFlags.Instance);
        groundedField.SetValue(player, Time.time);
        player.RequestJump();
        yield return null;
        Check(body.linearVelocity.y > player.jumpForce * .8f, "coyote jump accepts a recent ledge departure");
        body.linearVelocity = Vector2.zero;
        groundedField.SetValue(player, -100f);
        player.transform.position = new Vector3(-8, 8, 0);
        Physics2D.SyncTransforms();
        player.RequestJump();
        yield return null;
        bool waitedInAir = body.linearVelocity.y == 0f;
        player.transform.position = new Vector3(5.2f, 1.52f, 0);
        body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        yield return null;
        Check(waitedInAir && body.linearVelocity.y > player.jumpForce * .8f,
            "jump buffer fires on landing (wait=" + waitedInAir + ", vy=" + body.linearVelocity.y +
            ", grounded=" + player.isGrounded + ", blocked=" + PauseMenu.InputBlocked + ")");

        hound.ResetDog();
        Check(hound.TakeDamage(1, player.transform.position) && hound.State == "Hurt", "enemy hurt reaction");
        yield return new WaitForSecondsRealtime(.5f);
        Check(hound.TakeDamage(2, player.transform.position) && hound.State == "Death", "enemy death reaction");
        Check(player.GetComponent<CombatImpactFeedback>() != null && Camera.main.GetComponent<CombatCameraNudge>() != null, "impact feedback and restrained boss shake present");

        var originalTint = player.GetComponent<SpriteRenderer>().color;
        PlayerHitReaction.Apply(player.gameObject, player.transform.position + Vector3.right, new Vector2(2, 2));
        Check(animator.GetCurrentAnimatorStateInfo(0).IsName("HitReaction") && player.GetComponent<SpriteRenderer>().color != originalTint, "player hurt pose and flash");
        yield return new WaitForSecondsRealtime(.34f);
        Check(!PlayerControlLock.IsBlocked && player.GetComponent<SpriteRenderer>().color == originalTint &&
            !animator.GetCurrentAnimatorStateInfo(0).IsName("HitReaction"), "player hurt state releases movement and animation");

        var playerHealth = player.GetComponent<AshHealth>();
        playerHealth.ResetHealth();
        player.transform.position = new Vector3(0f, 1.52f, 0f);
        body.position = player.transform.position;
        body.linearVelocity = Vector2.zero;
        player.isGrounded = true;
        player.GetComponent<SpriteRenderer>().flipX = false;
        boss.ResetFight();
        boss.transform.position = new Vector3(5.5f, 2f, 0f);
        boss.GetComponent<Rigidbody2D>().position = boss.transform.position;
        boss.BeginArenaCombat(player.transform);
        boss.enabled = false;
        Physics2D.SyncTransforms();
        var wave = BossAshWave.Spawn(boss, new Vector2(1.2f, .3f), -1f, 8f, 1);
        int waveBossHealth = boss.CurrentHealth;
        int wavePlayerHealth = playerHealth.CurrentHealth;
        Check(attack.TryAttack(SwordDirection.Horizontal) && wave.IsDeflected &&
            playerHealth.CurrentHealth == wavePlayerHealth,
            "ground slash reflects a boss ash wave without taking damage");
        yield return new WaitForSecondsRealtime(.8f);
        Check(boss.CurrentHealth < waveBossHealth,
            "reflected ash wave returns to the boss");

        int wholePixels = OpaquePixels("Assets/sprites/PlayerDamage/Death/Death_0.png");
        int ashPixels = OpaquePixels("Assets/sprites/PlayerDamage/Death/Death_9.png");
        Check(wholePixels > 0 && ashPixels > 0 && ashPixels < wholePixels / 3 &&
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/sprites/PlayerDamage/Death/Death_9.png")
                .texture.filterMode == FilterMode.Point,
            "heroine death dissolves into sparse point-filtered ash");

        bool allSoundsLoaded = true;
        foreach (CombatCue cue in System.Enum.GetValues(typeof(CombatCue)))
            allSoundsLoaded &= Resources.Load<AudioClip>("CombatAudio/" + cue) != null;
        Check(allSoundsLoaded && CombatAudio.PlayCount > 0,
            "combat sounds import and play during combat events");
        boss.enabled = false;
        playerHealth.ResetHealth();
        int soundsBeforeDeath = CombatAudio.PlayCount;
        Check(playerHealth.TakeDamage(playerHealth.CurrentHealth) &&
            animator.GetCurrentAnimatorStateInfo(0).IsName("Death") &&
            !attack.TryAttack(SwordDirection.Horizontal) &&
            CombatAudio.PlayCount > soundsBeforeDeath,
            "lethal hit plays heroine death, sound and locks attack");
        var deathStartSprite = player.GetComponent<SpriteRenderer>().sprite;
        yield return new WaitForSecondsRealtime(.45f);
        Check(playerHealth.IsDead && animator.GetCurrentAnimatorStateInfo(0).IsName("Death") &&
            player.GetComponent<SpriteRenderer>().sprite != deathStartSprite,
            "heroine remains in changing death frames before respawn");
        yield return new WaitForSecondsRealtime(.95f);
        Check(playerHealth.CurrentHealth == playerHealth.MaxHealth && !PlayerControlLock.IsBlocked &&
            !animator.GetCurrentAnimatorStateInfo(0).IsName("Death"),
            "heroine respawns after the death animation and regains control");

        report += "TOTAL " + passed + " passed, " + failed + " failed\n";
        Directory.CreateDirectory("Artifacts/CombatVerification");
        File.WriteAllText("Artifacts/CombatVerification/results.txt", report);
        Debug.Log("Responsive combat verification: " + passed + " passed, " + failed + " failed");
        EditorApplication.isPlaying = false;
    }

    static int OpaquePixels(string path)
    {
        var texture = new Texture2D(2, 2);
        try
        {
            texture.LoadImage(File.ReadAllBytes(path));
            int count = 0;
            foreach (Color32 pixel in texture.GetPixels32()) if (pixel.a > 0) count++;
            return count;
        }
        finally { Object.DestroyImmediate(texture); }
    }
}
#endif
