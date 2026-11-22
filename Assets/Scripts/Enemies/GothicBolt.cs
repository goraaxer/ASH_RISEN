using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
public sealed class GothicBolt : MonoBehaviour
{
    public float speed = 11f;
    public float lifetime = 2.5f;
    Vector2 direction;
    GothicEnemy owner;
    float expires;
    bool deflected;
    Vector3 deflectedFrom;
    public bool IsDeflected => deflected;

    void Awake()
    {
        var hitbox = GetComponent<CircleCollider2D>();
        if (hitbox == null) hitbox = gameObject.AddComponent<CircleCollider2D>();
        hitbox.isTrigger = true;
        hitbox.radius = .23f;
    }

    public void Launch(Vector2 heading, GothicEnemy source)
    {
        direction = heading.sqrMagnitude > .01f ? heading.normalized : Vector2.left;
        owner = source;
        deflected = false;
        expires = Time.time + lifetime;
        transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    public bool Deflect(Vector2 heading, Vector3 attacker)
    {
        if (deflected || heading.sqrMagnitude < .01f) return false;
        deflected = true;
        deflectedFrom = attacker;
        direction = heading.normalized;
        speed *= 1.25f;
        expires = Time.time + 1.8f;
        transform.position += (Vector3)(direction * .3f);
        transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        GetComponent<SpriteRenderer>().color = new Color(1f, .84f, .72f);
        CombatAudio.Play(CombatCue.ProjectileParry, .58f);
        return true;
    }

    void FixedUpdate()
    {
        if (Time.time >= expires || PauseMenu.InputBlocked)
        { Destroy(gameObject); return; }
        Vector2 position = transform.position;
        float distance = speed * Time.fixedDeltaTime;
        foreach (var hit in Physics2D.CircleCastAll(position, .23f, direction, distance))
        {
            if (hit.collider == null || hit.collider.isTrigger ||
                hit.collider.transform.IsChildOf(transform)) continue;
            var enemy = hit.collider.GetComponentInParent<GothicEnemy>();
            if (!deflected && enemy == owner) continue;
            var player = hit.collider.GetComponentInParent<playercontroler>();
            if (player != null)
            {
                if (deflected) continue;
                if (player.GetComponent<PlayerAttackAnimation>()?.TryDeflectProjectile(this) == true)
                    return;
                var dash = player.GetComponent<PlayerDash>();
                if (dash == null || !dash.IsDashing)
                {
                    if (player.GetComponent<AshHealth>().TakeDamage(1))
                        PlayerHitReaction.Apply(player.gameObject, transform.position,
                            new Vector2(direction.x * 3f, 2f));
                }
                Destroy(gameObject);
                return;
            }
            if (enemy != null)
            {
                if (deflected && enemy.TakeDamage(1, deflectedFrom))
                    CombatAudio.Play(CombatCue.SwordImpact, .48f);
                if (deflected) { Destroy(gameObject); return; }
                continue;
            }
            var hound = hit.collider.GetComponentInParent<AshHound>();
            if (hound != null)
            {
                if (deflected && hound.TakeDamage(1, deflectedFrom))
                    CombatAudio.Play(CombatCue.SwordImpact, .48f);
                if (deflected) { Destroy(gameObject); return; }
                continue;
            }
            CombatAudio.Play(CombatCue.BoltImpact, .32f);
            Destroy(gameObject);
            return;
        }
        transform.position = position + direction * distance;
    }
}
