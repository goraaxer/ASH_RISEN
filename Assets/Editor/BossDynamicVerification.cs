#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BossDynamicVerification
{
    const string Flag = "ashrisen.boss.dynamic.verify";
    static BossDynamicVerification() { EditorApplication.playModeStateChanged += Changed; }
    [MenuItem("Tools/Ashrisen/Boss/Verify Dynamic Fight")]
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
        if (Object.FindAnyObjectByType<BossDynamicPlayCheck>() == null)
            new GameObject("Boss dynamic verification").AddComponent<BossDynamicPlayCheck>();
    }
}

public sealed class BossDynamicPlayCheck : MonoBehaviour
{
    string report = "";
    int passed, failed;
    void Check(bool ok, string label)
    {
        report += (ok ? "PASS " : "FAIL ") + label + "\n";
        if (ok) passed++; else failed++;
    }
    static bool IsMove(BossMove move, string state, Animator animator, BossArenaCombat boss)
        => boss.CurrentMove == move && animator.GetCurrentAnimatorStateInfo(0).IsName(state);

    IEnumerator Start()
    {
        yield return null;
        var player = Object.FindAnyObjectByType<playercontroler>();
        var boss = Object.FindObjectsByType<BossArenaCombat>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).FirstOrDefault();
        if (player == null || boss == null) { Check(false, "scene contains player and boss"); Finish(); yield break; }
        var arrival = Object.FindAnyObjectByType<BossArrival>();
        var arena = Object.FindAnyObjectByType<BossArenaController>();
        if (arrival != null) arrival.enabled = false;
        if (arena != null) arena.enabled = false;
        bool progress = BossArenaProgress.Defeated;
        BossArenaProgress.Defeated = false;
        boss.gameObject.SetActive(true);
        boss.ResetFight();
        boss.BeginArenaCombat(player.transform);
        boss.SuspendArenaCombat(false);
        var animator = boss.GetComponent<Animator>();
        var hitbox = boss.hitbox;
        var body = boss.GetComponent<Rigidbody2D>();
        player.GetComponent<AshHealth>().ResetHealth();
        int originalDamage = boss.attackDamage;
        boss.attackDamage = 0;
        yield return new WaitForSecondsRealtime(.08f);

        var source = BossPixelRigBuilder.Load("Assets/sprites/boss/bossmodel.png");
        var first = BossPixelRigBuilder.Load("Assets/sprites/boss/Dynamic/Frames/Idle/Idle_00.png");
        bool identical = true;
        for (int y = 0; y < 128 && identical; y++)
        for (int x = 0; x < 64; x++)
            if (!source.GetPixel(x, y).Equals(first.GetPixel(x + 16, y))) { identical = false; break; }
        Check(identical, "first dynamic pose exactly reconstructs original pixels");
        Object.DestroyImmediate(source); Object.DestroyImmediate(first);
        var clips = AssetDatabase.FindAssets("BossDyn_ t:AnimationClip",
            new[] { "Assets/sprites/boss/Dynamic/Clips" });
        Check(clips.Length >= 30, "original and wendigo animation clips exported");
        var wendigoSheet = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/sprites/boss/Dynamic/Sheets/WendigoAntlerRush.png");
        var wendigoSprites = AssetDatabase.LoadAllAssetsAtPath(
            "Assets/sprites/boss/Dynamic/Sheets/WendigoAntlerRush.png").OfType<Sprite>().ToArray();
        Check(wendigoSheet != null && wendigoSheet.filterMode == FilterMode.Point &&
            wendigoSprites.Length == 11 && wendigoSprites.All(s => s.rect.width == 96 &&
                s.rect.height == 128 && s.pivot.x == 48 && s.pivot.y == 21),
            "wendigo attack keeps the same canvas, pivot and pixel filtering");
        var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/sprites/boss/Dynamic/Sheets/RendingClaw.png");
        var sprites = AssetDatabase.LoadAllAssetsAtPath(
            "Assets/sprites/boss/Dynamic/Sheets/RendingClaw.png").OfType<Sprite>().ToArray();
        Check(sheet.filterMode == FilterMode.Point && sprites.Length == 11 &&
            sprites.All(s => s.rect.width == 96 && s.rect.height == 128 &&
                s.pivot.x == 48 && s.pivot.y == 21 && s.pixelsPerUnit == 16),
            "all Rending Claw frames share point filtering, canvas and pivot");
        var clawClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/sprites/boss/Dynamic/Clips/BossDyn_RendingClaw.anim");
        var clawKeys = AnimationUtility.GetObjectReferenceCurve(clawClip,
            new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" });
        Check(clawKeys.Length == 11 && Mathf.Abs(clawClip.length - .62f) < .01f &&
            Mathf.Abs(clawKeys[6].time - .62f * .49f) < .005f &&
            clawKeys[6].time - clawKeys[5].time < .04f,
            "eleven poses keep the claw's full timing and a rapid impact");
        int hurtSoundBefore = CombatAudio.PlayCount;
        bool tookHurt = boss.TryTakeDamage(1);
        yield return null;
        Check(tookHurt && boss.CurrentMove == BossMove.Flinch &&
            animator.GetCurrentAnimatorStateInfo(0).IsName("BossDyn_Flinch") &&
            CombatAudio.PlayCount > hurtSoundBefore,
            $"ordinary boss hit plays a flinch and hurt sound (took={tookHurt}, move={boss.CurrentMove}, pose={animator.GetCurrentAnimatorStateInfo(0).shortNameHash}, sounds={CombatAudio.PlayCount-hurtSoundBefore})");

        boss.ResetFight();
        boss.transform.position = new Vector3(12f, 2f, 0f);
        player.transform.position = new Vector3(13.6f, 1.5f, 0f);
        Physics2D.SyncTransforms();
        boss.BeginArenaCombat(player.transform);
        boss.SuspendArenaCombat(false);
        int healthBeforeAttack = boss.CurrentHealth;
        Check(boss.ForceMove(BossMove.RendingClaw) && boss.TryTakeDamage(1) &&
            boss.CurrentHealth == healthBeforeAttack - 1 &&
            boss.CurrentMove == BossMove.RendingClaw,
            "a sword hit damages the boss without cancelling its claw windup");
        int windowsBeforeHit = hitbox.WindowCount;
        yield return new WaitForSecondsRealtime(.36f);
        Check(boss.CurrentMove == BossMove.RendingClaw &&
            hitbox.WindowCount > windowsBeforeHit,
            "the claw hitbox still activates after the boss takes damage");

        boss.ResetFight();
        boss.transform.position = new Vector3(12f, 2f, 0f);
        player.transform.position = new Vector3(13.6f, 1.5f, 0f);
        Physics2D.SyncTransforms();
        boss.BeginArenaCombat(player.transform);
        boss.SuspendArenaCombat(false);
        int windowsBeforeSpam = hitbox.WindowCount;
        bool answeredSpam = false;
        for (int hit = 0; hit < 8; hit++)
        {
            boss.TryTakeDamage(1);
            float untilHit = Time.time + .41f;
            while (Time.time < untilHit)
            {
                answeredSpam |= boss.IsAttacking;
                yield return null;
            }
        }
        Check(boss.CurrentHealth == boss.maxHealth - 8 &&
            answeredSpam && hitbox.WindowCount > windowsBeforeSpam,
            "repeated clicks cannot keep the boss from answering with an attack");

        boss.ResetFight();
        boss.BeginArenaCombat(player.transform);
        boss.SuspendArenaCombat(false);

        var moves = new[] {
            BossMove.RendingClaw, BossMove.DoubleClawCombo, BossMove.HuntersLeap,
            BossMove.AshenSweep, BossMove.FallenBell, BossMove.FalseOpening,
            BossMove.AntiAirClaw, BossMove.CeilingDescent, BossMove.TripleAshWave,
            BossMove.LastToll, BossMove.ClawLeapSlam, BossMove.Reposition
        };
        foreach (var move in moves)
        {
            player.transform.position = new Vector3(15f, move == BossMove.AntiAirClaw ? 5f : 1.5f, 0f);
            player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            boss.transform.position = new Vector3(12f, 2f, 0f);
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            int startWindow = hitbox.WindowCount;
            bool selected = boss.ForceMove(move);
            yield return null;
            string expected = "BossDyn_" + move;
            bool pose = animator.GetCurrentAnimatorStateInfo(0).IsName(expected);
            if (move == BossMove.HuntersLeap)
                pose |= animator.GetCurrentAnimatorStateInfo(0).IsName("BossDyn_HuntersLeapStrike");
            if (move == BossMove.FalseOpening)
                pose |= animator.GetCurrentAnimatorStateInfo(0).IsName("BossDyn_TrueOpening");
            Check(selected && pose, move + " selects a distinct body animation");
            float duration = move switch {
                BossMove.RendingClaw => .62f, BossMove.DoubleClawCombo => .94f,
                BossMove.HuntersLeap => .92f, BossMove.AshenSweep => 1f,
                BossMove.FallenBell => 1.12f, BossMove.FalseOpening => .85f,
                BossMove.AntiAirClaw => .76f, BossMove.CeilingDescent => 1.25f,
                BossMove.TripleAshWave => 1.42f, BossMove.LastToll => 1.75f,
                BossMove.ClawLeapSlam => 1.35f, _ => .55f
            };
            yield return new WaitForSecondsRealtime(duration + .08f);
            Check(!hitbox.WindowActive && !boss.IsAttacking,
                move + " closes active collider and reaches recovery");
            if (move == BossMove.RendingClaw || move == BossMove.DoubleClawCombo ||
                move == BossMove.FallenBell || move == BossMove.AntiAirClaw ||
                move == BossMove.ClawLeapSlam)
                Check(hitbox.WindowCount > startWindow, move + " opens a finite active window");
        }

        // Let the live selector drive a full encounter without forcing individual moves.
        boss.ResetFight();
        boss.transform.position = new Vector3(20f, 2f, 0f);
        player.transform.position = new Vector3(5.2f, 1.5f, 0f);
        player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        boss.BeginArenaCombat(player.transform);
        boss.SuspendArenaCombat(false);
        float startingX = boss.transform.position.x;
        var naturalMoves = new HashSet<BossMove>();
        BossMove lastAttack = BossMove.None;
        BossMove lastObserved = BossMove.None;
        int sameAttack = 0, maximumRepeat = 0;
        float until = Time.time + 14f;
        while (Time.time < until)
        {
            BossMove current = boss.CurrentMove;
            if (current != lastObserved && boss.IsAttacking)
            {
                naturalMoves.Add(current);
                sameAttack = current == lastAttack ? sameAttack + 1 : 1;
                maximumRepeat = Mathf.Max(maximumRepeat, sameAttack);
                lastAttack = current;
            }
            lastObserved = current;
            yield return new WaitForSecondsRealtime(.02f);
        }
        Check(startingX - boss.transform.position.x > 3f,
            "natural AI approaches the player across the arena");
        Check(naturalMoves.Count >= 2,
            "natural AI chooses different attacks without forced moves");
        Check(maximumRepeat <= 2,
            "natural AI never repeats an attack more than twice");

        boss.ResetFight();
        boss.transform.position = new Vector3(12f, 2f, 0f);
        player.transform.position = new Vector3(5.2f, 1.5f, 0f);
        body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        boss.BeginArenaCombat(player.transform);
        boss.SuspendArenaCombat(false);
        Check(boss.ForceMove(BossMove.HuntersLeap), "Hunter's Leap launches from mid range");
        yield return new WaitForSecondsRealtime(.82f);
        Check(boss.transform.position.x < player.transform.position.x - .2f,
            "Hunter's Leap crosses the player and lands behind");

        boss.ResetFight();
        boss.transform.position = new Vector3(12f, 2f, 0f);
        body.linearVelocity = Vector2.zero;
        boss.BeginArenaCombat(player.transform);
        boss.SuspendArenaCombat(false);
        Check(boss.ForceMove(BossMove.CeilingDescent),
            "Ceiling Descent begins from the arena");
        float hideDeadline = Time.realtimeSinceStartup + 1.1f;
        while (body.simulated && Time.realtimeSinceStartup < hideDeadline) yield return null;
        bool hiddenAtCeiling = !body.simulated;
        boss.TryTakeDamage(boss.maxHealth / 2);
        Check(hiddenAtCeiling && body.simulated && boss.transform.position.y < 5f &&
            boss.CurrentMove == BossMove.PhaseTransition,
            $"damage during ceiling ascent returns boss to the arena (hidden={hiddenAtCeiling}, simulated={body.simulated}, y={boss.transform.position.y:F2}, move={boss.CurrentMove})");

        boss.ResetFight();
        boss.BeginArenaCombat(player.transform);
        boss.SuspendArenaCombat(false);
        Check(boss.maxHealth == 26 && boss.CurrentHealth == 26, "boss starts with 26 health");
        int halfHealth = boss.maxHealth / 2;
        Check(boss.TryTakeDamage(halfHealth) && boss.CurrentHealth == halfHealth &&
            boss.CurrentMove == BossMove.PhaseTransition, "half health starts phase transition");
        yield return null;
        Check(animator.GetCurrentAnimatorStateInfo(0).IsName("BossDyn_WendigoAwakening"),
            "transformation plays dedicated wendigo animation");
        yield return new WaitForSecondsRealtime(1.5f);
        Check(boss.CurrentPhase == 2 && boss.CurrentHealth == halfHealth && boss.WendigoForm,
            "second phase retains health and changes form");
        var upright = boss.GetComponent<CapsuleCollider2D>();
        Check(upright != null && upright.size.y >= 4.8f && upright.offset.y >= 2.4f,
            "upright form remains hittable through the head and chest");
        Check(animator.GetCurrentAnimatorStateInfo(0).IsName("BossDyn_WendigoIdle"),
            "second phase has its own idle animation");
        Check(boss.TryTakeDamage(2) && boss.CurrentHealth == halfHealth - 2 &&
            boss.CurrentPhase == 2 && boss.CurrentMove != BossMove.PhaseTransition,
            "second phase remains active below half health");
        Check(boss.ForceMove(BossMove.WendigoCrimsonSigil), "wendigo casts crimson sigils");
        yield return new WaitForSecondsRealtime(.48f);
        Check(Object.FindObjectsByType<BossScarletMagic>(FindObjectsSortMode.None)
            .Count(m => m.name.Contains("sigil")) == 3, "sigils show three floor warnings");
        Check(boss.ForceMove(BossMove.WendigoScarletRain), "wendigo casts scarlet rain");
        yield return new WaitForSecondsRealtime(.42f);
        Check(Object.FindObjectsByType<BossScarletMagic>(FindObjectsSortMode.None)
            .Any(m => m.name.Contains("shard")), "scarlet rain spawns falling shards");
        Check(boss.ForceMove(BossMove.WendigoAntlerRush), "wendigo starts antler rush");
        yield return new WaitForSecondsRealtime(.55f);
        Check(boss.CurrentMove == BossMove.WendigoAntlerRush && boss.hitbox.WindowActive,
            "antler rush has a finite active hitbox");

        boss.enabled = false;
        player.transform.position = boss.transform.position + Vector3.up * 4.4f;
        player.isGrounded = false;
        player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.down;
        Physics2D.SyncTransforms();
        int hp = boss.CurrentHealth;
        bool pogo = player.GetComponent<PlayerAttackAnimation>().TryAttack(SwordDirection.Down);
        Check(pogo && boss.CurrentHealth == hp - 1 &&
            player.GetComponent<Rigidbody2D>().linearVelocity.y >= player.pogoForce - .01f,
            "downward sword hit damages boss and confirms pogo");
        boss.enabled = true;
        boss.attackDamage = originalDamage;
        boss.TryTakeDamage(boss.CurrentHealth);
        Check(boss.CurrentMove == BossMove.Death && !boss.IsDefeated,
            "lethal blow begins death animation before victory");
        float deathDeadline = Time.realtimeSinceStartup + 2.5f;
        while (!boss.IsDefeated && Time.realtimeSinceStartup < deathDeadline) yield return null;
        Check(boss.IsDefeated && !boss.gameObject.activeSelf,
            "death animation completes before boss disappears");
        Check(Object.FindAnyObjectByType<BossVictoryBanner>() != null,
            "BOSS DEFEATED appears after the collapse");
        BossArenaProgress.Defeated = progress;
        Finish();
    }
    void Finish()
    {
        report += "TOTAL " + passed + " passed, " + failed + " failed\n";
        Directory.CreateDirectory("Artifacts/BossDynamicVerification");
        File.WriteAllText("Artifacts/BossDynamicVerification/results.txt", report);
        Debug.Log("Dynamic boss verification: " + passed + " passed, " + failed + " failed");
        EditorApplication.isPlaying = false;
    }
}
#endif
