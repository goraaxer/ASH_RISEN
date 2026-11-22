using System.Collections.Generic;
using UnityEngine;

public enum SwordDirection { Horizontal, Up, Down }

[RequireComponent(typeof(Animator), typeof(Rigidbody2D), typeof(playercontroler))]
[DefaultExecutionOrder(10)]
public class PlayerAttackAnimation : MonoBehaviour
{
    [Range(.25f, .45f)] public float attackDuration = .34f;
    [Range(0f, .2f)] public float attackRecovery = .07f;
    public float activeFrom = 0f;
    public float activeUntil = .17f;
    public Vector2 horizontalOffset = new Vector2(1.25f, .15f);
    public Vector2 horizontalSize = new Vector2(1.9f, 2.2f);
    public Vector2 upwardOffset = new Vector2(.15f, 1.95f);
    public Vector2 upwardSize = new Vector2(1.45f, 2f);
    public Vector2 downwardOffset = new Vector2(.1f, -1.85f);
    public Vector2 downwardSize = new Vector2(1.45f, 1.65f);
    // Current enemies and boss use Default; include any future pogo-surface layer in the inspector.
    [SerializeField] LayerMask hittableLayers = 1 << 0;

    Animator anim;
    playercontroler player;
    PlayerDash dash;
    Rigidbody2D rb;
    bool isAttacking;
    bool isAirAttacking;
    bool windowOpen;
    float startedAt;
    float nextAttackAt;
    float risePhase, fallPhase;
    float riseLength = .8f, fallLength = 2.67f;
    SwordDirection direction;
    readonly HashSet<Component> struck = new HashSet<Component>();
    public bool IsAttacking => isAttacking;
    public bool HitboxActive => windowOpen;
    public SwordDirection Direction => direction;

    void Awake()
    {
        anim = GetComponent<Animator>();
        player = GetComponent<playercontroler>();
        dash = GetComponent<PlayerDash>();
        rb = GetComponent<Rigidbody2D>();
        foreach (var clip in anim.runtimeAnimatorController.animationClips)
        {
            if (clip.name == "JumpRise") riseLength = Mathf.Max(.01f, clip.length);
            if (clip.name == "JumpFall") fallLength = Mathf.Max(.01f, clip.length);
        }
        ClearAttackFlags();
    }

    void Update()
    {
        if (PauseMenu.InputBlocked) { if (isAttacking) CancelAttack(); return; }
        if (Input.GetMouseButtonDown(0)) TryAttack();
        if (!isAttacking) return;
        float elapsed = Time.time - startedAt;
        windowOpen = elapsed >= activeFrom && elapsed < activeUntil;
        if (windowOpen) CheckHitbox();
        if (elapsed >= attackDuration) EndAttack();
    }

    public bool TryAttack()
    {
        SwordDirection selected = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) ? SwordDirection.Up :
            (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) && !player.isGrounded ? SwordDirection.Down :
            SwordDirection.Horizontal;
        return TryAttack(selected);
    }

    // Explicit direction also supports deterministic Play Mode verification.
    public bool TryAttack(SwordDirection selected)
    {
        if (PauseMenu.InputBlocked || PlayerControlLock.IsBlocked || !isActiveAndEnabled || !player.isActiveAndEnabled || isAttacking ||
            Time.time < nextAttackAt || (dash != null && dash.IsDashing)) return false;
        if (selected == SwordDirection.Down && player.isGrounded) selected = SwordDirection.Horizontal;
        var previousState = anim.GetCurrentAnimatorStateInfo(0);
        risePhase = previousState.IsName("JumpRise") ? previousState.normalizedTime : 0f;
        fallPhase = previousState.IsName("JumpFall") ? previousState.normalizedTime : 0f;
        direction = selected;
        isAirAttacking = !player.isGrounded;
        isAttacking = true;
        windowOpen = true;
        startedAt = Time.time;
        nextAttackAt = startedAt + attackDuration + attackRecovery;
        struck.Clear();
        anim.SetBool("SpecialIdle", false);
        anim.SetBool("IsAttacking", true);
        anim.SetBool("IsAirAttacking", isAirAttacking);
        string state = selected == SwordDirection.Up ? "UpwardSlash" :
            selected == SwordDirection.Down ? "DownwardSlash" :
            isAirAttacking ? "JumpAttack" : Mathf.Abs(rb.linearVelocity.x) > .1f ? "RunningSlash" : "Attack";
        anim.Play("Base Layer." + state, 0, 0f);
        anim.Update(0f);
        Physics2D.SyncTransforms();
        CombatAudio.Play(CombatCue.SwordSwing, .48f);
        CheckHitbox();
        return true;
    }

    Vector2 ParryDirection() => direction == SwordDirection.Up ? Vector2.up :
        direction == SwordDirection.Down ? Vector2.down :
        Vector2.right * player.FacingDirection;

    public bool TryDeflectProjectile(GothicBolt bolt)
    {
        if (bolt == null || !isAttacking || !windowOpen || struck.Contains(bolt) || bolt.IsDeflected)
            return false;
        Vector2 offset = direction == SwordDirection.Up ? upwardOffset :
            direction == SwordDirection.Down ? downwardOffset : horizontalOffset;
        Vector2 size = direction == SwordDirection.Up ? upwardSize :
            direction == SwordDirection.Down ? downwardSize : horizontalSize;
        offset.x *= player.FacingDirection;
        Vector2 center = (Vector2)transform.position + offset;
        Vector2 point = bolt.transform.position;
        if (Mathf.Abs(point.x - center.x) > size.x * .5f + .23f ||
            Mathf.Abs(point.y - center.y) > size.y * .5f + .23f) return false;
        if (!bolt.Deflect(ParryDirection(), transform.position)) return false;
        struck.Add(bolt);
        return true;
    }

    void CheckHitbox()
    {
        Vector2 offset = direction == SwordDirection.Up ? upwardOffset :
            direction == SwordDirection.Down ? downwardOffset : horizontalOffset;
        Vector2 size = direction == SwordDirection.Up ? upwardSize :
            direction == SwordDirection.Down ? downwardSize : horizontalSize;
        offset.x *= player.FacingDirection;
        Vector2 center = (Vector2)transform.position + offset;
        foreach (Collider2D collider in Physics2D.OverlapBoxAll(center, size, 0f, hittableLayers))
        {
            if (collider == null || collider.transform.IsChildOf(transform)) continue;
            var bolt = collider.GetComponentInParent<GothicBolt>();
            if (bolt != null) { TryDeflectProjectile(bolt); continue; }
            var wave = collider.GetComponentInParent<BossAshWave>();
            if (wave != null)
            {
                if (!struck.Contains(wave) && wave.Deflect()) struck.Add(wave);
                continue;
            }
            var scarlet = collider.GetComponentInParent<BossScarletMagic>();
            if (scarlet != null)
            {
                if (!struck.Contains(scarlet) && scarlet.Deflect()) struck.Add(scarlet);
                continue;
            }
            var dog = collider.GetComponentInParent<AshHound>();
            if (dog != null)
            {
                if (struck.Contains(dog) || collider != dog.hurtbox) continue;
                if (dog.TakeDamage(1, transform.position)) ConfirmHit(dog, collider.ClosestPoint(center), false);
                continue;
            }
            var ordinaryEnemy = collider.GetComponentInParent<GothicEnemy>();
            if (ordinaryEnemy != null)
            {
                if (struck.Contains(ordinaryEnemy) || collider != ordinaryEnemy.hurtbox) continue;
                if (ordinaryEnemy.TakeDamage(1, transform.position))
                    ConfirmHit(ordinaryEnemy, collider.ClosestPoint(center), false);
                continue;
            }
            var boss = collider.GetComponentInParent<BossArenaCombat>();
            if (boss != null)
            {
                if (struck.Contains(boss) || collider.isTrigger) continue;
                if (boss.TryTakeDamage(1)) ConfirmHit(boss, collider.ClosestPoint(center), true);
                continue;
            }
            var surface = collider.GetComponentInParent<PogoSurface>();
            if (direction == SwordDirection.Down && surface != null && !struck.Contains(surface))
                ConfirmHit(surface, collider.ClosestPoint(center), false);
        }
        if (direction == SwordDirection.Horizontal)
            foreach (Collider2D collider in Physics2D.OverlapBoxAll(
                center + Vector2.down * .35f, size + Vector2.up * .7f, 0f, hittableLayers))
            {
                var wave = collider.GetComponentInParent<BossAshWave>();
                if (wave != null && !struck.Contains(wave) && wave.Deflect()) struck.Add(wave);
            }
    }

    void ConfirmHit(Component victim, Vector2 point, bool strong)
    {
        struck.Add(victim);
        if (direction == SwordDirection.Down) player.ApplyPogo();
        CombatImpactFeedback.Play(this, victim, point, strong);
        CombatAudio.Play(CombatCue.SwordImpact, strong ? .62f : .46f);
    }

    public void EndAirAttack() { if (isAirAttacking) EndAttack(); }
    public void EndAttack()
    {
        if (!isAttacking) return;
        float elapsed = Time.time - startedAt;
        ClearAttackFlags();
        if (dash != null && dash.IsDashing) return;
        if (player.isGrounded)
            anim.Play(Mathf.Abs(rb.linearVelocity.x) > .1f ? "Base Layer.Walk" : "Base Layer. idle", 0, 0f);
        else
        {
            if (rb.linearVelocity.y > .1f)
                anim.Play("Base Layer.JumpRise", 0, Mathf.Clamp(risePhase + elapsed / riseLength, 0f, .99f));
            else
                anim.Play("Base Layer.JumpFall", 0, Mathf.Repeat(fallPhase + elapsed / fallLength, 1f));
        }
    }

    public void AttackStateExited() { /* Runtime swing clock owns the attack window. */ }
    public void CancelAttack() { if (isAttacking) { ClearAttackFlags(); } }
    void ClearAttackFlags()
    {
        isAttacking = isAirAttacking = windowOpen = false;
        if (anim == null) return;
        anim.SetBool("IsAttacking", false);
        anim.SetBool("IsAirAttacking", false);
        anim.ResetTrigger("Attack");
    }
    void OnDisable() => ClearAttackFlags();
    void OnDrawGizmosSelected()
    {
        var movement = GetComponent<playercontroler>();
        float sign = movement != null ? movement.FacingDirection : 1f;
        Gizmos.color = Color.yellow; Gizmos.DrawWireCube(transform.position + new Vector3(horizontalOffset.x * sign, horizontalOffset.y), horizontalSize);
        Gizmos.color = Color.cyan; Gizmos.DrawWireCube(transform.position + new Vector3(upwardOffset.x * sign, upwardOffset.y), upwardSize);
        Gizmos.color = Color.red; Gizmos.DrawWireCube(transform.position + new Vector3(downwardOffset.x * sign, downwardOffset.y), downwardSize);
    }
}
