using UnityEngine;

public sealed class PlayerAttackStateExit : StateMachineBehaviour
{
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var attack = animator.GetComponent<PlayerAttackAnimation>();
        if (attack != null) attack.AttackStateExited();
    }
}
