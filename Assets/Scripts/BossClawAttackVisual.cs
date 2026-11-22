using UnityEngine;

// Visual entry point only: the AI remains responsible for movement and damage.
[DisallowMultipleComponent]
public sealed class BossClawAttackVisual : MonoBehaviour
{
    public SpriteRenderer trail;
    Animator animator;
    SpriteRenderer body;
    bool active;
    void Awake() { animator = GetComponent<Animator>(); body = GetComponent<SpriteRenderer>(); }
    public bool Play()
    {
        if (active || animator == null || animator.GetBool("IsKnockedBack")) return false;
        animator.SetTrigger("BossClawAttack"); active = true; return true;
    }
    public void SetActiveAttack(bool value)
    {
        active = value;
        var locomotion = GetComponent<BossLocomotionAnimation>();
        if (locomotion != null) locomotion.SetAttacking(value);
        else if (animator != null) animator.SetBool("IsAttacking", value);
        if (!value && trail != null) trail.sprite = null;
    }
    void LateUpdate()
    {
        if (trail == null || body == null) return;
        trail.flipX = body.flipX; trail.flipY = body.flipY;
        trail.sortingLayerID = body.sortingLayerID;
        trail.sortingOrder = body.sortingOrder + 1;
    }
    void OnDisable() { if (animator != null) animator.ResetTrigger("BossClawAttack"); SetActiveAttack(false); }
}
