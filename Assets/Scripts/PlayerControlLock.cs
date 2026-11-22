using System.Collections.Generic;
using UnityEngine;

// All modal gameplay menus share this lock; components keep their lifecycle intact.
public static class PlayerControlLock
{
    static readonly HashSet<Object> owners = new HashSet<Object>();
    static readonly HashSet<Object> freezing = new HashSet<Object>();
    static Rigidbody2D body;
    static RigidbodyConstraints2D constraints;
    static int blockedFrame = -1, interactionFrame = -1;
    public static bool IsBlocked => owners.Count > 0 || Time.frameCount <= blockedFrame;
    public static bool IsFrozen => freezing.Count > 0;
    public static bool ConsumeInteraction()
    {
        if (IsBlocked || interactionFrame == Time.frameCount) return false;
        interactionFrame = Time.frameCount;
        return true;
    }
    public static void Acquire(Object owner, GameObject player, bool freeze)
    {
        owners.Add(owner);
        if (!freeze) return;
        freezing.Add(owner);
        BindPlayer(player);
    }
    public static void BindPlayer(GameObject player)
    {
        if (freezing.Count == 0 || player == null) return;
        var next = player.GetComponent<Rigidbody2D>();
        if (body == next) return;
        RestoreBody();
        body = next;
        if (body == null) return;
        constraints = body.constraints;
        ResetMotion(player);
        body.constraints = RigidbodyConstraints2D.FreezeAll;
    }
    public static void ResetMotion(GameObject player)
    {
        player.GetComponent<PlayerDash>()?.CancelDash();
        player.GetComponent<PlayerAttackAnimation>()?.CancelAttack();
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.angularVelocity = 0; }
        var movement = player.GetComponent<playercontroler>();
        if (movement != null) movement.isGrounded = true;
        var animator = player.GetComponent<Animator>();
        if (animator != null)
        {
            animator.ResetTrigger("Dash"); animator.ResetTrigger("Jump"); animator.ResetTrigger("Attack");
            animator.SetBool("IsDashing", false); animator.SetBool("IsWalking", false);
            animator.SetBool("IsGrounded", true); animator.SetFloat("VerticalSpeed", 0);
            animator.SetBool("SpecialIdle", false);
            animator.Play("Base Layer. idle", 0, 0);
        }
    }
    public static void Release(Object owner)
    {
        owners.Remove(owner); freezing.Remove(owner);
        if (freezing.Count == 0) RestoreBody();
        blockedFrame = Time.frameCount + 1;
    }
    static void RestoreBody()
    {
        if (body != null) { body.constraints = constraints; body.linearVelocity = Vector2.zero; }
        body = null;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { owners.Clear(); freezing.Clear(); body = null; blockedFrame = interactionFrame = -1; }
}
