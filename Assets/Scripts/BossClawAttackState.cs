using UnityEngine;

public sealed class BossClawAttackState : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo state, int layer)
    {
        var visual = animator.GetComponent<BossClawAttackVisual>();
        if (visual != null) visual.SetActiveAttack(true);
        animator.GetComponent<BossArenaCombat>()?.NotifyAttackEntered();
    }
    public override void OnStateUpdate(Animator animator, AnimatorStateInfo state, int layer)
    { animator.GetComponent<BossArenaCombat>()?.SyncAttackWindow(state.normalizedTime); }
    public override void OnStateExit(Animator animator, AnimatorStateInfo state, int layer)
    {
        var visual = animator.GetComponent<BossClawAttackVisual>();
        if (visual != null) visual.SetActiveAttack(false);
        animator.GetComponent<BossArenaCombat>()?.EndAttack();
    }
}
