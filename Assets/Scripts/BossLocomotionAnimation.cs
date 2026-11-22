using UnityEngine;

// Optional contract for an AI/motor: intent distinguishes walking from external forces.
public interface IBossAnimationContext
{
    float HorizontalMoveIntent { get; }
    bool IsAttacking { get; }
    bool IsKnockedBack { get; }
}

[DisallowMultipleComponent, RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
[DefaultExecutionOrder(100)]
public sealed class BossLocomotionAnimation : MonoBehaviour
{
    [SerializeField] Rigidbody2D body;
    [SerializeField] Transform motionRoot;
    [SerializeField] bool spriteFacesLeft = true;
    [SerializeField, Min(0.001f)] float startSpeed = 0.06f;
    [SerializeField, Min(0f)] float stopSpeed = 0.03f;
    [SerializeField, Min(0.1f)] float teleportDistance = 2f;
    [Tooltip("Without AI intent, Rigidbody velocity could be knockback. Transform-only motion is observed automatically.")]
    [SerializeField] bool requireIntentForRigidbody = true;
    Animator animator;
    SpriteRenderer renderer2D;
    IBossAnimationContext context;
    Vector3 previousPosition;
    bool explicitIntent, attackRequested, knockbackRequested;
    float intent, suppressedUntil;
    public bool IsMoving { get; private set; }
    public float HorizontalSpeed { get; private set; }
    static readonly int Moving = Animator.StringToHash("IsMoving");
    static readonly int Attacking = Animator.StringToHash("IsAttacking");
    static readonly int KnockedBack = Animator.StringToHash("IsKnockedBack");

    void Awake()
    {
        animator = GetComponent<Animator>(); renderer2D = GetComponent<SpriteRenderer>();
        if (motionRoot == null) motionRoot = transform;
        if (body == null) body = motionRoot.GetComponent<Rigidbody2D>();
        foreach (var component in motionRoot.GetComponents<MonoBehaviour>())
            if (component is IBossAnimationContext source) { context = source; break; }
        previousPosition = motionRoot.position;
    }
    void OnEnable() { previousPosition = motionRoot != null ? motionRoot.position : transform.position; }
    // The AI can use this API instead of implementing IBossAnimationContext.
    public void SetMovementIntent(float horizontal) { explicitIntent = true; intent = horizontal; }
    public void SetAttacking(bool value)
    {
        attackRequested = value;
        if (animator != null) animator.SetBool(Attacking, value);
        if (value) SetMoving(false);
    }
    public void SetKnockedBack(bool value)
    {
        knockbackRequested = value;
        if (animator != null) animator.SetBool(KnockedBack, value);
        if (value) SetMoving(false);
    }
    public void SuppressLocomotion(float seconds)
    { suppressedUntil = Mathf.Max(suppressedUntil, Time.time + Mathf.Max(0, seconds)); SetMoving(false); }
    public void ResetMotionSample()
    { previousPosition = motionRoot.position; HorizontalSpeed = 0; SetMoving(false); }
    static bool Locomotion(AnimatorStateInfo state) => state.IsTag("Locomotion") || state.IsName("BossIdle") || state.IsName("Boss_Walk");
    void Update()
    {
        Vector3 position = motionRoot.position;
        Vector3 delta = position - previousPosition;
        previousPosition = position;
        if (Time.deltaTime <= 0 || animator.runtimeAnimatorController == null) return;
        bool physics = body != null && body.simulated && body.bodyType != RigidbodyType2D.Static;
        HorizontalSpeed = physics ? body.linearVelocity.x : delta.x / Time.deltaTime;
        var state = animator.GetCurrentAnimatorStateInfo(0);
        bool allowedState = Locomotion(state) && (!animator.IsInTransition(0) || Locomotion(animator.GetNextAnimatorStateInfo(0)));
        bool blocked = attackRequested || knockbackRequested || Time.time < suppressedUntil ||
            animator.GetBool(Attacking) || animator.GetBool(KnockedBack) ||
            (context != null && (context.IsAttacking || context.IsKnockedBack));
        float moveIntent = context != null ? context.HorizontalMoveIntent : intent;
        bool knowsIntent = context != null || explicitIntent;
        bool intended = knowsIntent ? Mathf.Abs(moveIntent) > 0.01f && moveIntent * HorizontalSpeed > 0 : !physics || !requireIntentForRigidbody;
        bool teleported = delta.sqrMagnitude > teleportDistance * teleportDistance;
        float threshold = IsMoving ? Mathf.Min(stopSpeed, startSpeed) : startSpeed;
        bool moving = allowedState && !blocked && intended && !teleported && Mathf.Abs(HorizontalSpeed) > threshold;
        SetMoving(moving);
        if (moving) renderer2D.flipX = spriteFacesLeft ? HorizontalSpeed > 0 : HorizontalSpeed < 0;
    }
    void SetMoving(bool value)
    {
        IsMoving = value;
        if (animator != null && animator.runtimeAnimatorController != null && animator.GetBool(Moving) != value)
            animator.SetBool(Moving, value);
    }
    void OnDisable() { SetMoving(false); }
}
