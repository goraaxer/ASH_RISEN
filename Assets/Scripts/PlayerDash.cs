using UnityEngine;
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(playercontroler))]
public class PlayerDash : MonoBehaviour
{
    public float dashSpeed = 14f;
    public float dashDuration = 0.27f;   
    public float dashCooldown = 0.6f;
    public bool IsDashing { get; private set; }
    Rigidbody2D rb;
    playercontroler movement;
    SpriteRenderer sr;
    Animator anim;
    float dashTimer;
    float cooldownTimer;
    float savedGravity;
    float dashDirection;
    bool dashRequested;
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<playercontroler>();
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
    }
    void Update()
    {
        if (PauseMenu.InputBlocked) return;
        if (!movement.isActiveAndEnabled)
            return;
        if (Input.GetKeyDown(KeyCode.Q)
            && !IsDashing && cooldownTimer <= 0f)
        {
            float x = Input.GetAxisRaw("Horizontal");
            dashDirection = x != 0f
                ? Mathf.Sign(x)
                : movement.FacingDirection;
            dashRequested = true;
        }
    }
    void FixedUpdate()
    {
        if (PlayerControlLock.IsFrozen) { CancelDash(); return; }
        if (!movement.isActiveAndEnabled)
        {
            dashRequested = false;
            if (IsDashing)
                EndDash();
            return;
        }

        cooldownTimer = Mathf.Max(
            0f, cooldownTimer - Time.fixedDeltaTime
        );

        if (dashRequested)
        {
            dashRequested = false;

            if (!IsDashing && cooldownTimer <= 0f)
                StartDash();
        }

        if (!IsDashing)
            return;

        if (dashTimer <= 0f)
        {
            EndDash();
            return;
        }

        rb.linearVelocity = new Vector2(
            dashDirection * dashSpeed, 0f
        );

        dashTimer -= Time.fixedDeltaTime;
    }
    void StartDash()
    {
        GetComponent<PlayerAttackAnimation>()?.CancelAttack();
        IsDashing = true;
        dashTimer = dashDuration;

        savedGravity = rb.gravityScale;
        rb.gravityScale = 0f;

        if (sr != null)
            sr.flipX = dashDirection < 0f;

        if (anim != null)
            anim.SetTrigger("Dash");
        CombatAudio.Play(CombatCue.PlayerDash, .6f);
    }
    void EndDash()
    {
        IsDashing = false;
        rb.gravityScale = savedGravity;
        rb.linearVelocity = new Vector2(dashDirection * movement.speed, rb.linearVelocity.y);

        cooldownTimer = dashCooldown;
    }

    public void CancelDash()
    {
        dashRequested = false;
        if (IsDashing) EndDash();
        if (anim != null) { anim.ResetTrigger("Dash"); anim.SetBool("IsDashing", false); }
    }

    void OnDisable()
    {
        dashRequested = false;

        if (rb != null && IsDashing)
            EndDash();
    }
}
