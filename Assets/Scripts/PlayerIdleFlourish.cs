using UnityEngine;

[RequireComponent(typeof(Animator), typeof(Rigidbody2D))]
public class PlayerIdleFlourish : MonoBehaviour
{
    [Min(1f)] public float idleDelay = 12f;

    Animator animator;
    Rigidbody2D body;
    float idleTimer;

    static readonly int SpecialIdle =
        Animator.StringToHash("SpecialIdle");

    void Awake()
    {
        animator = GetComponent<Animator>();
        body = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (PauseMenu.InputBlocked) return;
        bool interrupted =
            Input.anyKey || body.linearVelocity.sqrMagnitude > 0.05f;

        if (interrupted)
        {
            idleTimer = 0f;
            animator.SetBool(SpecialIdle, false);
            return;
        }

        idleTimer += Time.deltaTime;

        if (idleTimer >= idleDelay)
            animator.SetBool(SpecialIdle, true);
    }
}