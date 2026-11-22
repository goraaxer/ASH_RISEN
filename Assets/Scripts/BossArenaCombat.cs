using UnityEngine;

public enum BossMove
{
    None, Observe, Approach, Reposition, RendingClaw, DoubleClawCombo,
    HuntersLeap, AshenSweep, FallenBell, FalseOpening, AntiAirClaw,
    CeilingDescent, TripleAshWave, LastToll, ClawLeapSlam,
    PhaseTransition, Stagger, Flinch, Death,
    WendigoAntlerRush, WendigoCrimsonSigil, WendigoScarletRain
}

[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D), typeof(BossClawAttackVisual))]
public sealed class BossArenaCombat : MonoBehaviour, IBossArenaCombat
{
    [Min(2)] public int maxHealth = 26;
    public float walkSpeed = 6.5f, chaseAcceleration = 26f, detectionRange = 24f,
        attackRange = 2.1f, attackCooldown = 1.2f, playerHitRange = 2.2f;
    public bool debugMode;
    [Min(1)] public int attackDamage = 1;
    [Range(.5f, 2f)] public float attackAnimationSpeed = 1.5f;
    [Range(.5f, 4f)] public float walkAnimationSpeed = 2.8f;
    public Vector2 attackHitboxSize = new Vector2(3.2f, 2.5f),
        attackHitboxOffset = new Vector2(1.5f, 1.65f), knockbackForce = new Vector2(4f, 2.5f);
    public BossAttackHitbox hitbox;

    public int CurrentHealth { get; private set; }
    public int CurrentPhase { get; private set; } = 1;
    public BossMove CurrentMove { get; private set; } = BossMove.None;
    public bool IsDefeated => deathFinished;
    public bool IsFighting { get; private set; }
    public bool IsAttacking => Offensive(CurrentMove);
    public bool IsHighClaw => CurrentMove == BossMove.AntiAirClaw;
    public bool IsStepClaw => CurrentMove == BossMove.RendingClaw && stepClaw;
    public bool WendigoForm => CurrentPhase == 2;
    public Vector2 CurrentHitboxSize => CurrentMove switch
    {
        BossMove.AntiAirClaw => new Vector2(2.5f, 2.4f),
        BossMove.FallenBell => new Vector2(3.4f, 1.5f),
        BossMove.HuntersLeap => new Vector2(2.3f, 1.2f),
        BossMove.CeilingDescent => new Vector2(3.1f, 1.25f),
        BossMove.WendigoAntlerRush => new Vector2(2.65f, 2.1f),
        _ => attackHitboxSize
    };
    public Vector2 CurrentHitboxOffset => CurrentMove switch
    {
        BossMove.AntiAirClaw => new Vector2(1.15f, 2.45f),
        BossMove.FallenBell => new Vector2(.1f, .85f),
        BossMove.HuntersLeap => new Vector2(1.2f, .55f),
        BossMove.CeilingDescent => new Vector2(.1f, .8f),
        BossMove.WendigoAntlerRush => new Vector2(1.3f, 1.45f),
        _ => attackHitboxOffset
    };
    public bool TargetWithinClawHeight
    {
        get
        {
            if (target == null) return false;
            var collider = target.GetComponent<Collider2D>();
            if (collider == null) return false;
            float low = transform.position.y + CurrentHitboxOffset.y - CurrentHitboxSize.y * .5f;
            float high = transform.position.y + CurrentHitboxOffset.y + CurrentHitboxSize.y * .5f;
            return collider.bounds.min.y < high && collider.bounds.max.y > low;
        }
    }

    Rigidbody2D body;
    BossLocomotionAnimation locomotion;
    BossClawAttackVisual legacyVisual;
    Animator animator;
    SpriteRenderer sprite;
    Transform target;
    Collider2D solid;
    CapsuleCollider2D bodyCapsule;
    Vector2 originalCapsuleSize, originalCapsuleOffset;
    bool suspended, deathFinished, stepClaw, airborne, descendHidden, markerShown,
        leapDownstrike;
    int pendingPhase, windowIndex, eventIndex, repeatCount;
    float moveStarted, moveDuration, nextDecision, nextLeap, nextBell, nextDescent,
        nextAntiAir, nextRush, nextSigil, nextRain, nextReactionAt, nextStaggerAt,
        aboveSince = -1f, facing = -1f,
        leapDirection, landingX, originalGravity;
    BossMove previousMove;
    BossLandingMarker marker;

    static bool Offensive(BossMove move) => (move >= BossMove.RendingClaw && move <= BossMove.ClawLeapSlam) ||
        move >= BossMove.WendigoAntlerRush;
    static float Duration(BossMove move) => move switch
    {
        BossMove.RendingClaw => .62f, BossMove.DoubleClawCombo => .94f,
        BossMove.HuntersLeap => .92f, BossMove.AshenSweep => 1f,
        BossMove.FallenBell => 1.12f, BossMove.FalseOpening => .85f,
        BossMove.AntiAirClaw => .76f, BossMove.CeilingDescent => 1.25f,
        BossMove.TripleAshWave => 1.42f, BossMove.LastToll => 1.75f,
        BossMove.ClawLeapSlam => 1.35f,
        BossMove.WendigoAntlerRush => .98f, BossMove.WendigoCrimsonSigil => 1.3f,
        BossMove.WendigoScarletRain => 1.55f,
        BossMove.PhaseTransition => 1.4f, BossMove.Stagger => .5f,
        BossMove.Flinch => .22f,
        BossMove.Death => 1.6f, BossMove.Reposition => .55f, _ => .4f
    };

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        locomotion = GetComponent<BossLocomotionAnimation>();
        legacyVisual = GetComponent<BossClawAttackVisual>();
        animator = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();
        solid = GetComponent<Collider2D>();
        bodyCapsule = GetComponent<CapsuleCollider2D>();
        if (bodyCapsule != null) { originalCapsuleSize = bodyCapsule.size; originalCapsuleOffset = bodyCapsule.offset; }
        originalGravity = body.gravityScale;
        CurrentHealth = maxHealth;
        if (hitbox == null) hitbox = GetComponentInChildren<BossAttackHitbox>(true);
        if (hitbox != null) hitbox.Configure(this);
    }

    public void BeginArenaCombat(Transform player)
    {
        target = player;
        IsFighting = true;
        IgnorePlayerBody();
        nextDecision = Time.time + .28f;
        PlayMove(BossMove.Observe);
    }
    void IgnorePlayerBody()
    {
        if (target == null) return;
        foreach (var own in GetComponents<Collider2D>())
            if (!own.isTrigger)
                foreach (var other in target.GetComponentsInChildren<Collider2D>())
                    if (!other.isTrigger) Physics2D.IgnoreCollision(own, other, true);
    }
    public void SuspendArenaCombat(bool value)
    {
        suspended = value;
        if (value) { DisableAttackHitbox(); StopMoving(); }
    }
    void StopMoving()
    {
        if (body != null && body.simulated) body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        if (locomotion != null) locomotion.SetMovementIntent(0f);
    }
    void FaceTarget()
    {
        if (target == null) return;
        float dx = target.position.x - transform.position.x;
        if (Mathf.Abs(dx) < .05f) return;
        facing = Mathf.Sign(dx);
        sprite.flipX = facing > 0f;
    }
    void Update()
    {
        if (target == null && IsFighting)
        {
            var player = FindAnyObjectByType<playercontroler>();
            if (player != null) { target = player.transform; IgnorePlayerBody(); }
        }
        if (!IsFighting || suspended || target == null || PauseMenu.InputBlocked || SceneTransitionManager.IsBusy)
        { DisableAttackHitbox(); StopMoving(); return; }
        if (CurrentMove == BossMove.Death) { UpdateDeath(); return; }
        var health = target.GetComponent<AshHealth>();
        if (health == null || health.IsDead) { DisableAttackHitbox(); StopMoving(); return; }
        float elapsed = Time.time - moveStarted;
        if (CurrentMove == BossMove.PhaseTransition || CurrentMove == BossMove.Stagger ||
            CurrentMove == BossMove.Flinch)
        {
            StopMoving();
            if (elapsed >= moveDuration) CompleteMove();
            return;
        }
        if (Offensive(CurrentMove))
        {
            UpdateAttack(elapsed);
            if (elapsed >= moveDuration) CompleteMove();
            return;
        }
        if (CurrentMove == BossMove.Reposition)
        {
            if (elapsed >= moveDuration) CompleteMove();
            return;
        }
        ObserveAndChoose();
    }
    void FixedUpdate()
    {
        if (!IsFighting || suspended || target == null || CurrentHealth <= 0) return;
        if (CurrentMove == BossMove.Approach)
        {
            float dx = target.position.x - transform.position.x;
            float desired = Mathf.Sign(dx) * Mathf.Min(walkSpeed, 4.4f);
            body.linearVelocity = new Vector2(Mathf.MoveTowards(body.linearVelocity.x, desired,
                chaseAcceleration * Time.fixedDeltaTime), body.linearVelocity.y);
            locomotion?.SetMovementIntent(desired);
        }
        else if (CurrentMove == BossMove.Reposition)
        {
            float retreat = -facing;
            if ((transform.position.x < 3f && retreat < 0f) ||
                (transform.position.x > 23f && retreat > 0f)) retreat = -retreat;
            body.linearVelocity = new Vector2(retreat * 3.8f, body.linearVelocity.y);
            locomotion?.SetMovementIntent(-facing);
        }
        else if ((CurrentMove == BossMove.HuntersLeap || CurrentMove == BossMove.ClawLeapSlam) && airborne)
        {
            float horizontal = leapDirection;
            if ((transform.position.x < 2f && horizontal < 0f) ||
                (transform.position.x > 24f && horizontal > 0f)) horizontal = 0f;
            body.linearVelocity = new Vector2(horizontal, body.linearVelocity.y);
        }
        else if (CurrentMove == BossMove.RendingClaw && stepClaw && Time.time - moveStarted < .22f)
            body.linearVelocity = new Vector2(facing * 3.7f, body.linearVelocity.y);
        else if (CurrentMove == BossMove.WendigoAntlerRush && eventIndex == 1)
        {
            float remaining = (landingX - transform.position.x) * facing;
            float speed = remaining <= 0f ? 0f : Mathf.Clamp(remaining / .19f, 9f, 18f);
            body.linearVelocity = new Vector2(facing * speed, body.linearVelocity.y);
        }
    }
    void ObserveAndChoose()
    {
        if (CurrentMove != BossMove.Approach) StopMoving();
        if (CurrentMove != BossMove.Approach && CurrentMove != BossMove.Observe) PlayMove(BossMove.Observe);
        float dx = target.position.x - transform.position.x;
        float distance = Mathf.Abs(dx);
        bool above = target.position.y > transform.position.y + 2.2f && distance < 2.5f;
        if (above) { if (aboveSince < 0f) aboveSince = Time.time; }
        else aboveSince = -1f;
        if (Time.time < nextDecision) return;

        if (CurrentPhase == 2) { ChooseWendigoMove(distance, above); return; }

        if (CurrentPhase == 2 && distance < 6.2f && previousMove != BossMove.LastToll &&
            Time.time >= nextBell && Random.value < .18f)
        { StartAttack(BossMove.LastToll); return; }
        if (above)
        {
            if (aboveSince >= 0f && Time.time - aboveSince >= .38f && Time.time >= nextAntiAir)
                StartAttack(BossMove.AntiAirClaw);
            else { PlayMove(BossMove.Observe); nextDecision = Time.time + .2f; }
            return;
        }
        if (distance > 6.2f)
        {
            if (distance > 9.5f && CurrentPhase < 2) { Approach(); return; }
            if (CurrentPhase >= 2 && Time.time >= nextDescent && CanChoose(BossMove.CeilingDescent))
                StartAttack(BossMove.CeilingDescent);
            else if (distance > 9.5f) Approach();
            else if (Time.time >= nextLeap && CanChoose(BossMove.HuntersLeap))
                StartAttack(BossMove.HuntersLeap);
            else if (CanChoose(BossMove.AshenSweep)) StartAttack(BossMove.AshenSweep);
            else Approach();
            return;
        }
        if (distance > 3.4f)
        {
            if (CurrentPhase >= 2 && CanChoose(BossMove.TripleAshWave) && Random.value > .55f)
                StartAttack(BossMove.TripleAshWave);
            else if (CanChoose(BossMove.AshenSweep) && Random.value > .38f)
                StartAttack(BossMove.AshenSweep);
            else Approach();
            return;
        }
        float choice = Random.value;
        if (choice < .14f && CanChoose(BossMove.Reposition)) { PlayMove(BossMove.Reposition); return; }
        if (choice < .26f && Time.time >= nextBell && CanChoose(BossMove.FallenBell))
            StartAttack(BossMove.FallenBell);
        else if (choice < .42f && CurrentPhase >= 2 && CanChoose(BossMove.ClawLeapSlam))
            StartAttack(BossMove.ClawLeapSlam);
        else if (choice < .59f && CanChoose(BossMove.DoubleClawCombo))
            StartAttack(BossMove.DoubleClawCombo);
        else if (choice < .72f && CanChoose(BossMove.FalseOpening))
            StartAttack(BossMove.FalseOpening);
        else if (CanChoose(BossMove.RendingClaw)) StartAttack(BossMove.RendingClaw);
        else StartAttack(BossMove.FallenBell);
    }
    void ChooseWendigoMove(float distance, bool above)
    {
        if (above && Time.time >= nextRain && CanChoose(BossMove.WendigoScarletRain))
        { StartAttack(BossMove.WendigoScarletRain); return; }
        if (distance > 7.5f)
        {
            if (Time.time >= nextRain && CanChoose(BossMove.WendigoScarletRain))
                StartAttack(BossMove.WendigoScarletRain);
            else if (Time.time >= nextSigil && CanChoose(BossMove.WendigoCrimsonSigil))
                StartAttack(BossMove.WendigoCrimsonSigil);
            else Approach();
            return;
        }
        if (distance > 4f && Time.time >= nextRush && CanChoose(BossMove.WendigoAntlerRush))
        { StartAttack(BossMove.WendigoAntlerRush); return; }
        float choice = Random.value;
        if (choice < .4f && Time.time >= nextSigil && CanChoose(BossMove.WendigoCrimsonSigil))
            StartAttack(BossMove.WendigoCrimsonSigil);
        else if (choice < .7f && Time.time >= nextRush && CanChoose(BossMove.WendigoAntlerRush))
            StartAttack(BossMove.WendigoAntlerRush);
        else if (Time.time >= nextRain && CanChoose(BossMove.WendigoScarletRain))
            StartAttack(BossMove.WendigoScarletRain);
        else if (Time.time >= nextSigil && CanChoose(BossMove.WendigoCrimsonSigil))
            StartAttack(BossMove.WendigoCrimsonSigil);
        else if (Time.time >= nextRush && CanChoose(BossMove.WendigoAntlerRush))
            StartAttack(BossMove.WendigoAntlerRush);
        else Approach();
    }
    bool CanChoose(BossMove move) => move != previousMove || repeatCount < 2;
    void Approach()
    {
        FaceTarget();
        PlayMove(BossMove.Approach);
        nextDecision = Time.time + .32f;
    }
    void PlayMove(BossMove move)
    {
        if (CurrentMove == move) return;
        CurrentMove = move;
        moveStarted = Time.time;
        moveDuration = Duration(move);
        windowIndex = eventIndex = 0;
        if (move == BossMove.Observe) animator.Play(CurrentPhase == 2 ? "BossDyn_WendigoIdle" : "BossDyn_Idle", 0, 0f);
        else if (move == BossMove.Approach) animator.Play(CurrentPhase == 2 ? "BossDyn_WendigoWalk" : "BossDyn_Walk", 0, 0f);
        else if (move == BossMove.Reposition && CurrentPhase == 2) animator.Play("BossDyn_WendigoReposition", 0, 0f);
        else if (move == BossMove.PhaseTransition && pendingPhase == 2) animator.Play("BossDyn_WendigoAwakening", 0, 0f);
        else if ((move == BossMove.Stagger || move == BossMove.Flinch || move == BossMove.Death) && CurrentPhase == 2)
            animator.Play("BossDyn_Wendigo" + move, 0, 0f);
        else if (move == BossMove.HuntersLeap && leapDownstrike)
            animator.Play("BossDyn_HuntersLeapStrike", 0, 0f);
        else if (move == BossMove.FalseOpening && !falseOpeningAttacks)
            animator.Play("BossDyn_TrueOpening", 0, 0f);
        else animator.Play("BossDyn_" + move, 0, 0f);
        if (move != BossMove.Approach) locomotion?.SetMovementIntent(0f);
    }
    public bool ForceMove(BossMove move)
    {
        if (!IsFighting || CurrentHealth <= 0 || suspended || PauseMenu.InputBlocked) return false;
        if (move == BossMove.None || move == BossMove.Death || move == BossMove.PhaseTransition) return false;
        StartAttack(move);
        return CurrentMove == move;
    }
    void StartAttack(BossMove move)
    {
        DisableAttackHitbox();
        StopMoving();
        FaceTarget();
        stepClaw = move == BossMove.RendingClaw && Mathf.Abs(target.position.x - transform.position.x) > attackRange + .15f;
        if (move == previousMove) repeatCount++; else { previousMove = move; repeatCount = 1; }
        if (move == BossMove.HuntersLeap || move == BossMove.ClawLeapSlam) nextLeap = Time.time + 3f;
        if (move == BossMove.FallenBell || move == BossMove.LastToll) nextBell = Time.time + 3.2f;
        if (move == BossMove.CeilingDescent) nextDescent = Time.time + 5f;
        if (move == BossMove.AntiAirClaw) nextAntiAir = Time.time + 2.3f;
        if (move == BossMove.WendigoAntlerRush) nextRush = Time.time + 2.4f;
        if (move == BossMove.WendigoCrimsonSigil) nextSigil = Time.time + 3.2f;
        if (move == BossMove.WendigoScarletRain) nextRain = Time.time + 3.5f;
        if (move == BossMove.FalseOpening) falseOpeningAttacks = Random.value > .45f;
        if (move == BossMove.HuntersLeap) leapDownstrike = Random.value > .5f;
        airborne = descendHidden = markerShown = false;
        landingX = Mathf.Clamp(target.position.x + facing * 1.2f, 2.2f, 23.8f);
        leapDirection = Mathf.Clamp((landingX - transform.position.x) / .41f, -20f, 20f);
        if (move == BossMove.WendigoAntlerRush)
        {
            landingX = Mathf.Clamp(target.position.x + facing * 1.1f, 2.2f, 23.8f);
            marker = BossLandingMarker.Show(new Vector2(landingX, GroundY() + .08f), .38f);
        }
        PlayMove(move);
        CombatAudio.Play(move == BossMove.WendigoCrimsonSigil || move == BossMove.WendigoScarletRain
            ? CombatCue.BoltShot : CombatCue.BossSwing, .31f);
        if (debugMode) Debug.Log("Boss move: " + move + " phase " + CurrentPhase, this);
    }
    bool falseOpeningAttacks;
    void UpdateAttack(float elapsed)
    {
        float t = elapsed / moveDuration;
        if (CurrentMove != BossMove.HuntersLeap && CurrentMove != BossMove.CeilingDescent &&
            CurrentMove != BossMove.RendingClaw && CurrentMove != BossMove.WendigoAntlerRush) StopMoving();
        switch (CurrentMove)
        {
            case BossMove.RendingClaw:
                HitWindow(t, .45f, .57f, 1);
                if (elapsed >= .22f) StopMoving();
                break;
            case BossMove.DoubleClawCombo:
                HitWindow(t, .23f, .30f, 1);
                HitWindow(t, .49f, .56f, 2);
                break;
            case BossMove.HuntersLeap:
                if (t >= .32f && eventIndex == 0) { LaunchLeap(); eventIndex = 1; }
                if (leapDownstrike && t >= .58f) HitWindow(t, .58f, .64f, 1);
                if (t >= .76f)
                {
                    airborne = false; StopMoving();
                    if (leapDownstrike) HitWindow(t, .76f, .85f, 2);
                }
                break;
            case BossMove.AshenSweep:
                if (t >= .46f && eventIndex == 0) { Wave(facing, 8f); Impact(false); eventIndex = 1; }
                break;
            case BossMove.FallenBell:
                if (t >= .51f && eventIndex == 0) { Wave(-1, 7f); Wave(1, 7f); Impact(true); eventIndex = 1; }
                HitWindow(t, .50f, .59f, 1);
                break;
            case BossMove.FalseOpening:
                if (falseOpeningAttacks) HitWindow(t, .73f, .82f, 1);
                break;
            case BossMove.AntiAirClaw:
                HitWindow(t, .51f, .64f, 1);
                break;
            case BossMove.CeilingDescent:
                UpdateDescent(t);
                break;
            case BossMove.TripleAshWave:
                if (t >= .20f && eventIndex == 0) { Wave(facing, 7f); eventIndex++; }
                if (t >= .48f && eventIndex == 1) { Wave(facing, 8f); eventIndex++; }
                if (t >= .77f && eventIndex == 2) { Wave(facing, 9f); eventIndex++; }
                break;
            case BossMove.LastToll:
                if (t >= .21f && eventIndex == 0) { Wave(-1, 5.5f); Wave(1, 5.5f); eventIndex++; }
                if (t >= .48f && eventIndex == 1) { Wave(-1, 7.5f); Wave(1, 7.5f); eventIndex++; }
                if (t >= .76f && eventIndex == 2) { Wave(-1, 9f); Wave(1, 9f); Impact(true); eventIndex++; }
                break;
            case BossMove.ClawLeapSlam:
                HitWindow(t, .18f, .25f, 1);
                if (t >= .42f && eventIndex == 0) { LaunchLeap(); eventIndex = 1; }
                if (t >= .72f && eventIndex == 1)
                { airborne = false; StopMoving(); Wave(-1, 7f); Wave(1, 7f); Impact(true); eventIndex = 2; }
                HitWindow(t, .72f, .81f, 2);
                break;
            case BossMove.WendigoAntlerRush:
                if (t >= .34f && eventIndex == 0) { eventIndex = 1; marker?.Hide(); marker = null; }
                HitWindow(t, .37f, .69f, 1);
                if (t >= .7f && eventIndex == 1) { eventIndex = 2; StopMoving(); }
                break;
            case BossMove.WendigoCrimsonSigil:
                if (t >= .32f && eventIndex == 0)
                {
                    float x = Mathf.Clamp(target.position.x, 4.5f, 21.5f);
                    for (int i = -1; i <= 1; i++)
                        BossScarletMagic.SpawnSigil(this, new Vector2(x + i * 2.4f, GroundY() + .12f), attackDamage);
                    eventIndex = 1;
                }
                if (t >= .7f && eventIndex == 1) { CombatAudio.Play(CombatCue.BoltImpact, .38f); eventIndex = 2; }
                break;
            case BossMove.WendigoScarletRain:
                if (t >= .22f && eventIndex == 0) { SpawnScarletShard(-1.8f); eventIndex++; }
                if (t >= .46f && eventIndex == 1) { SpawnScarletShard(1.8f); eventIndex++; }
                if (t >= .7f && eventIndex == 2) { SpawnScarletShard(0f); eventIndex++; }
                break;
        }
    }
    void SpawnScarletShard(float offset)
    {
        float x = Mathf.Clamp(target.position.x + offset, 2.2f, 23.8f);
        BossScarletMagic.SpawnShard(this, new Vector2(x, 8.5f), GroundY() + .35f, attackDamage);
        BossLandingMarker.Show(new Vector2(x, GroundY() + .08f), .55f);
    }
    void HitWindow(float t, float from, float to, int index)
    {
        if (t >= from && t < to)
        {
            if (windowIndex != index) { hitbox?.BeginSwing(); windowIndex = index; }
            EnableAttackHitbox();
        }
        else if (windowIndex == index && t >= to) DisableAttackHitbox();
    }
    void LaunchLeap()
    {
        body.gravityScale = 7f;
        body.linearVelocity = new Vector2(leapDirection, 14f);
        airborne = true;
    }
    void UpdateDescent(float t)
    {
        if (t >= .33f && !descendHidden)
        {
            descendHidden = true;
            body.simulated = false;
            transform.position = new Vector3(transform.position.x, 13f, transform.position.z);
        }
        if (t >= .47f && !markerShown)
        {
            markerShown = true;
            landingX = Mathf.Clamp(target.position.x, 2.2f, 23.8f);
            marker = BossLandingMarker.Show(new Vector2(landingX, GroundY() + .08f), .42f);
        }
        if (t >= .55f && descendHidden)
        {
            descendHidden = false;
            transform.position = new Vector3(landingX, 8.5f, transform.position.z);
            body.simulated = true;
            body.linearVelocity = new Vector2(0f, -25f);
        }
        if (t >= .78f && eventIndex == 0)
        {
            eventIndex = 1;
            Wave(-1, 7.5f); Wave(1, 7.5f); Impact(true);
            marker?.Hide();
        }
        HitWindow(t, .77f, .87f, 1);
    }
    float GroundY()
    {
        Vector2 start = new Vector2(transform.position.x, transform.position.y + 1f);
        foreach (var hit in Physics2D.RaycastAll(start, Vector2.down, 16f))
            if (hit.collider != solid && !hit.collider.isTrigger &&
                hit.collider.GetComponentInParent<playercontroler>() == null &&
                hit.collider.GetComponentInParent<BossArenaCombat>() == null) return hit.point.y;
        return 0f;
    }
    void Wave(float direction, float speed)
    {
        if (CurrentHealth <= 0) return;
        BossAshWave.Spawn(this, new Vector2(transform.position.x + direction * 1.4f, GroundY() + .3f),
            direction, speed, attackDamage);
    }
    void Impact(bool strong)
    {
        if (strong) CombatCameraNudge.Pulse(.08f, .1f);
    }
    void CompleteMove()
    {
        DisableAttackHitbox();
        marker?.Hide(); marker = null;
        if (!body.simulated) body.simulated = true;
        body.gravityScale = originalGravity;
        airborne = descendHidden = markerShown = false;
        StopMoving();
        if (CurrentMove == BossMove.PhaseTransition)
        {
            CurrentPhase = pendingPhase;
            SetUprightCollider(CurrentPhase == 2);
            BossFightEffects.Bell(.24f);
        }
        float recovery = CurrentMove switch
        {
            BossMove.Flinch => .05f,
            BossMove.Stagger => .22f,
            BossMove.FallenBell or BossMove.LastToll or BossMove.CeilingDescent or BossMove.WendigoScarletRain => .8f,
            BossMove.HuntersLeap or BossMove.DoubleClawCombo or BossMove.ClawLeapSlam => .56f,
            BossMove.WendigoAntlerRush or BossMove.WendigoCrimsonSigil => .62f,
            _ => .38f
        };
        if (CurrentPhase == 2 && CurrentMove != BossMove.Flinch && CurrentMove != BossMove.Stagger)
            recovery = Mathf.Max(.25f, recovery - .08f);
        nextDecision = Time.time + recovery;
        PlayMove(BossMove.Observe);
    }
    void SetUprightCollider(bool upright)
    {
        if (bodyCapsule == null) return;
        bodyCapsule.size = upright ? new Vector2(2.6f, 4.8f) : originalCapsuleSize;
        bodyCapsule.offset = upright ? new Vector2(0f, 2.4f) : originalCapsuleOffset;
    }
    void StartPhase(int phase)
    {
        pendingPhase = phase;
        DisableAttackHitbox();
        InterruptAirMove();
        StopMoving();
        PlayMove(BossMove.PhaseTransition);
        BossFightEffects.Bell(.2f);
    }
    void StartDeath()
    {
        BossAshWave.ClearFor(this);
        BossScarletMagic.ClearFor(this);
        DisableAttackHitbox();
        InterruptAirMove();
        StopMoving();
        PlayMove(BossMove.Death);
        CombatAudio.Play(CombatCue.BossDeath, .65f);
        BossFightEffects.Bell(.18f);
    }
    void UpdateDeath()
    {
        StopMoving();
        if (Time.time - moveStarted < moveDuration) return;
        deathFinished = true;
        BossFightEffects.VictoryBanner();
        var arena = FindAnyObjectByType<BossArenaController>();
        if (arena != null && arena.isActiveAndEnabled) arena.MarkDefeated();
        gameObject.SetActive(false);
    }
    public bool TryTakeDamage(int amount)
    {
        if (amount <= 0 || CurrentHealth <= 0 || !IsFighting || SceneTransitionManager.IsBusy ||
            CurrentMove == BossMove.PhaseTransition) return false;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        CombatAudio.Play(CombatCue.BossHurt, .5f);
        if (CurrentHealth == 0) StartDeath();
        else if (CurrentPhase == 1 && CurrentHealth <= maxHealth / 2) StartPhase(2);
        else
        {
            hitsSinceStagger++;
            // Damage still counts during an attack. Ordinary sword hits cannot cancel its
            // windup or repeatedly restart the hurt pose.
            if (!Offensive(CurrentMove) && CurrentMove != BossMove.Flinch &&
                CurrentMove != BossMove.Stagger)
            {
                bool stagger = (amount >= 3 || hitsSinceStagger >= 6) &&
                    Time.time >= nextStaggerAt;
                if (stagger)
                {
                    hitsSinceStagger = 0;
                    nextStaggerAt = Time.time + 5f;
                    nextReactionAt = Time.time + 1.6f;
                    StopMoving();
                    PlayMove(BossMove.Stagger);
                }
                else if (Time.time >= nextReactionAt)
                {
                    nextReactionAt = Time.time + 1.6f;
                    StopMoving();
                    PlayMove(BossMove.Flinch);
                }
            }
        }
        return true;
    }
    void InterruptAirMove()
    {
        if (!body.simulated)
        {
            float floor = GroundY();
            transform.position = new Vector3(Mathf.Clamp(landingX, 2.2f, 23.8f), floor + 2f,
                transform.position.z);
            body.simulated = true;
        }
        body.gravityScale = originalGravity;
        airborne = descendHidden = markerShown = false;
        marker?.Hide(); marker = null;
    }
    int hitsSinceStagger;
    public void TakeDamage(int amount) => TryTakeDamage(amount);
    public void ResetFight()
    {
        BossAshWave.ClearFor(this);
        BossScarletMagic.ClearFor(this);
        DisableAttackHitbox();
        marker?.Hide(); marker = null;
        body.simulated = true;
        body.gravityScale = originalGravity;
        CurrentHealth = maxHealth;
        CurrentPhase = 1;
        SetUprightCollider(false);
        pendingPhase = 0;
        IsFighting = deathFinished = false;
        CurrentMove = BossMove.None;
        previousMove = BossMove.None;
        repeatCount = hitsSinceStagger = 0;
        nextDecision = nextLeap = nextBell = nextDescent = nextAntiAir = nextRush = nextSigil =
            nextRain = nextReactionAt = nextStaggerAt = 0f;
        aboveSince = -1f;
        StopMoving();
        if (animator != null && animator.isActiveAndEnabled) animator.Play("BossIdle", 0, 0f);
    }
    // Compatibility with the original claw animation state and older scene hooks.
    public void BeginAttack()
    {
        if (!IsFighting || CurrentHealth <= 0 || suspended || target == null || PauseMenu.InputBlocked) return;
        if (CurrentPhase == 2)
            StartAttack(target.position.y > transform.position.y + 2.2f ?
                BossMove.WendigoScarletRain : BossMove.WendigoAntlerRush);
        else StartAttack(target.position.y > transform.position.y + 2.2f ?
            BossMove.AntiAirClaw : BossMove.RendingClaw);
    }
    public void NotifyAttackEntered() { }
    public void SyncAttackWindow(float phase) { }
    public void EnableAttackHitbox()
    {
        if (IsAttacking && !suspended && CurrentHealth > 0 && !PauseMenu.InputBlocked)
            hitbox?.SetActiveWindow(true);
    }
    public void DisableAttackHitbox() => hitbox?.SetActiveWindow(false);
    public void EndAttack() { if (IsAttacking) CompleteMove(); }
    public void CancelAttack() { if (IsAttacking) CompleteMove(); }
    void OnDisable() { DisableAttackHitbox(); marker?.Hide(); marker = null; }
}
