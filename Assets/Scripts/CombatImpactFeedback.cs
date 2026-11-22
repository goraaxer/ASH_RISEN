using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatImpactFeedback : MonoBehaviour
{
    Coroutine pulse;
    Animator playerAnimator;
    Animator victimAnimator;
    SpriteRenderer victimSprite;
    float playerSpeed, victimSpeed;
    Color victimColor;

    public static void Play(PlayerAttackAnimation attacker, Component victim, Vector2 point, bool strong)
    {
        var effect = attacker.GetComponent<CombatImpactFeedback>();
        if (effect == null) effect = attacker.gameObject.AddComponent<CombatImpactFeedback>();
        effect.Begin(attacker, victim, point, strong);
    }

    void Begin(PlayerAttackAnimation attacker, Component victim, Vector2 point, bool strong)
    {
        if (pulse != null) { StopCoroutine(pulse); Restore(); }
        playerAnimator = attacker.GetComponent<Animator>();
        victimAnimator = victim.GetComponent<Animator>();
        victimSprite = victim.GetComponent<SpriteRenderer>();
        playerSpeed = playerAnimator != null ? playerAnimator.speed : 1f;
        victimSpeed = victimAnimator != null ? victimAnimator.speed : 1f;
        victimColor = victimSprite != null ? victimSprite.color : Color.white;
        if (playerAnimator != null) playerAnimator.speed = 0f;
        if (victimAnimator != null && victimAnimator.isActiveAndEnabled) victimAnimator.speed = 0f;
        if (victimSprite != null && victimSprite.enabled) victimSprite.color = new Color(1f, .82f, .72f, victimColor.a);
        if (strong) CombatCameraNudge.Pulse(.055f, .08f);
        pulse = StartCoroutine(ReleaseAfter(.045f));
    }

    IEnumerator ReleaseAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        Restore();
        pulse = null;
    }

    void Restore()
    {
        if (playerAnimator != null) playerAnimator.speed = playerSpeed;
        if (victimAnimator != null) victimAnimator.speed = victimSpeed;
        if (victimSprite != null) victimSprite.color = victimColor;
    }
    void OnDisable() { if (pulse != null) StopCoroutine(pulse); Restore(); pulse = null; }

}

[DefaultExecutionOrder(1000)]
public sealed class CombatCameraNudge : MonoBehaviour
{
    float until, strength;
    Vector3 applied;
    public static void Pulse(float duration, float amplitude)
    {
        var camera = Camera.main;
        if (camera == null) return;
        var nudge = camera.GetComponent<CombatCameraNudge>();
        if (nudge == null) nudge = camera.gameObject.AddComponent<CombatCameraNudge>();
        nudge.until = Time.unscaledTime + duration;
        nudge.strength = amplitude;
    }
    void LateUpdate()
    {
        bool following = GetComponent<camerafollow>()?.enabled == true;
        Vector3 basePosition = following ? transform.position : transform.position - applied;
        applied = Vector3.zero;
        if (Time.unscaledTime < until && !PauseMenu.IsPaused)
            applied = new Vector3(Random.Range(-strength, strength), Random.Range(-strength, strength), 0f);
        transform.position = basePosition + applied;
    }
    void OnDisable() { if (GetComponent<camerafollow>()?.enabled != true) transform.position -= applied; applied = Vector3.zero; }
}
