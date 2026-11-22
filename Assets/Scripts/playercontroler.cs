using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class playercontroler : MonoBehaviour
{
    public float speed = 6f;
    public float jumpForce = 7f;
    public float fallMultiplier = 1.6f;
    public float lowJumpMultiplier = 2.5f;
    [Range(0f, .3f)] public float coyoteTime = .1f;
    [Range(0f, .3f)] public float jumpBuffer = .12f;
    public float pogoForce = 8.5f;
    public bool isGrounded;
    public float FacingDirection => sr != null && sr.flipX ? -1f : 1f;

    Rigidbody2D rb;
    Animator anim;
    SpriteRenderer sr;
    Collider2D col;
    PlayerDash dash;
    ContactFilter2D groundFilter;
    readonly Collider2D[] hits = new Collider2D[8];
    float lastGroundedAt = -100f;
    float jumpRequestedAt = -100f;
    bool jumpHeld;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        dash = GetComponent<PlayerDash>();
        groundFilter = new ContactFilter2D { useTriggers = false };
    }

    void Update()
    {
        if (PauseMenu.InputBlocked) { jumpRequestedAt = -100f; return; }
        CheckGround();
        float now = Time.time;
        bool dashing = dash != null && dash.IsDashing;
        float x = Input.GetAxisRaw("Horizontal");
        jumpHeld = Input.GetButton("Jump");
        if (Input.GetButtonDown("Jump")) RequestJump();

        if (!dashing)
        {
            // Dash is the only other writer of horizontal velocity; hit reaction uses the input lock.
            rb.linearVelocity = new Vector2(x * speed, rb.linearVelocity.y);
            if (now - jumpRequestedAt <= jumpBuffer && now - lastGroundedAt <= coyoteTime)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                isGrounded = false;
                lastGroundedAt = jumpRequestedAt = -100f;
            }
            ApplyBetterGravity();
            if (x > 0f) sr.flipX = false;
            else if (x < 0f) sr.flipX = true;
        }

        if (anim != null)
        {
            anim.SetBool("IsGrounded", isGrounded);
            anim.SetFloat("VerticalSpeed", rb.linearVelocity.y);
            anim.SetBool("IsDashing", dashing);
            anim.SetBool("IsWalking", isGrounded && x != 0f && !dashing);
        }
    }

    public void ApplyPogo()
    {
        if (rb == null || !isActiveAndEnabled || PauseMenu.InputBlocked) return;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, pogoForce);
        isGrounded = false;
        lastGroundedAt = jumpRequestedAt = -100f;
        if (anim != null) { anim.SetBool("IsGrounded", false); anim.SetFloat("VerticalSpeed", pogoForce); }
    }

    // The same request path is used by keyboard input and Play Mode verification.
    public void RequestJump()
    {
        if (!PauseMenu.InputBlocked && isActiveAndEnabled) jumpRequestedAt = Time.time;
    }

    void ApplyBetterGravity()
    {
        float g = Physics2D.gravity.y * rb.gravityScale;
        Vector2 v = rb.linearVelocity;
        if (v.y < 0f) v.y += g * (fallMultiplier - 1f) * Time.deltaTime;
        else if (v.y > 0f && !jumpHeld) v.y += g * (lowJumpMultiplier - 1f) * Time.deltaTime;
        rb.linearVelocity = v;
    }

    void CheckGround()
    {
        isGrounded = false;
        if (rb.linearVelocity.y > .1f) return;
        Bounds b = col.bounds;
        Vector2 point = new Vector2(b.center.x, b.min.y - .05f);
        Vector2 size = new Vector2(b.size.x * .9f, .1f);
        int n = Physics2D.OverlapBox(point, size, 0f, groundFilter, hits);
        for (int i = 0; i < n; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit == col || hit.attachedRigidbody == rb || Physics2D.GetIgnoreCollision(col, hit) ||
                hit.GetComponentInParent<BossArenaCombat>() != null) continue;
            isGrounded = true;
            lastGroundedAt = Time.time;
            break;
        }
    }
}
