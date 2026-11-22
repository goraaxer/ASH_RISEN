using UnityEngine;
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerAnimation : MonoBehaviour
{
    Animator animator;
    Rigidbody2D rb;
    playercontroler movement;
    void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<playercontroler>();
    }
    void Update()
    {
        if (PauseMenu.InputBlocked || (movement != null && movement.isActiveAndEnabled)) return;
        bool isMoving = Mathf.Abs(rb.linearVelocity.x) > 0.1f;
        animator.SetBool("IsWalking", isMoving);
    }
}
