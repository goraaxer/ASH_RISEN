using UnityEngine;

public sealed class PlayerHitReaction : MonoBehaviour
{
    float until;
    bool ownsLock;
    bool dying;
    SpriteRenderer sprite;
    Color baseColor;

    public static void Apply(GameObject player, Vector3 source, Vector2 force)
    {
        var reaction = player.GetComponent<PlayerHitReaction>();
        if (reaction == null) reaction = player.AddComponent<PlayerHitReaction>();
        if (player.GetComponent<AshHealth>()?.IsDead == true) { reaction.BeginDeath(); return; }
        if (reaction.dying) return;
        player.GetComponent<PlayerDash>()?.CancelDash();
        player.GetComponent<PlayerAttackAnimation>()?.CancelAttack();
        if (!reaction.ownsLock)
        {
            PlayerControlLock.Acquire(reaction, player, false);
            reaction.ownsLock = true;
            reaction.sprite = player.GetComponent<SpriteRenderer>();
            if (reaction.sprite != null) reaction.baseColor = reaction.sprite.color;
        }
        reaction.until = Time.time + .28f;
        if (reaction.sprite != null)
            reaction.sprite.color = new Color(1f, .72f, .68f, reaction.baseColor.a);
        var body = player.GetComponent<Rigidbody2D>();
        body.linearVelocity = new Vector2(
            (player.transform.position.x >= source.x ? 1f : -1f) * force.x, force.y);
        var animator = player.GetComponent<Animator>();
        if (animator != null) { animator.Play("Base Layer.HitReaction", 0, 0f); animator.Update(0f); }
        CombatAudio.Play(CombatCue.PlayerHurt, .56f);
    }

    public static void PlayDeath(GameObject player)
    {
        var reaction = player.GetComponent<PlayerHitReaction>();
        if (reaction == null) reaction = player.AddComponent<PlayerHitReaction>();
        reaction.BeginDeath();
    }

    void BeginDeath()
    {
        if (dying) return;
        dying = true;
        gameObject.GetComponent<PlayerDash>()?.CancelDash();
        gameObject.GetComponent<PlayerAttackAnimation>()?.CancelAttack();
        sprite = GetComponent<SpriteRenderer>();
        if (!ownsLock && sprite != null) baseColor = sprite.color;
        PlayerControlLock.Acquire(this, gameObject, true);
        ownsLock = true;
        if (sprite != null) sprite.color = new Color(.88f, .83f, .82f, baseColor.a);
        var animator = GetComponent<Animator>();
        if (animator != null) { animator.Play("Base Layer.Death", 0, 0f); animator.Update(0f); }
        CombatAudio.Play(CombatCue.PlayerDeath, .62f);
    }

    void Update()
    {
        if (!ownsLock || dying || Time.time < until) return;
        Release();
        var animator = GetComponent<Animator>();
        var body = GetComponent<Rigidbody2D>();
        var movement = GetComponent<playercontroler>();
        if (animator == null || body == null || movement == null) return;
        if (movement.isGrounded)
            animator.Play(Mathf.Abs(body.linearVelocity.x) > .1f ? "Base Layer.Walk" : "Base Layer. idle", 0, 0f);
        else
            animator.Play(body.linearVelocity.y > .1f ? "Base Layer.JumpRise" : "Base Layer.JumpFall", 0, 0f);
    }

    void Release()
    {
        if (!ownsLock) return;
        PlayerControlLock.Release(this);
        ownsLock = false;
        if (sprite != null) sprite.color = baseColor;
    }
    public void ResetAfterRespawn()
    {
        dying = false;
        Release();
        var animator = GetComponent<Animator>();
        if (animator != null) animator.Play("Base Layer. idle", 0, 0f);
    }
    void OnDisable() { dying = false; Release(); }
}
