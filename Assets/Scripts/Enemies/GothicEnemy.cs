using UnityEngine;

public enum GothicEnemyKind { GraveWarden, BellMarksman }

[RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public sealed class GothicEnemy : MonoBehaviour
{
    public GothicEnemyKind kind;
    public Sprite[] idle, walk, attack, hurt, death;
    public BoxCollider2D hurtbox;
    public GothicBolt boltPrefab;
    public int maxHealth = 3;
    public float detection = 8f, leash = 8f, speed = 2.3f;

    public int Health { get; private set; }
    public string State { get; private set; } = "Idle";

    Rigidbody2D body;
    CapsuleCollider2D shape;
    SpriteRenderer visual;
    playercontroler player;
    Vector3 post;
    float facing = -1f, stateAt, nextAttack, hurtUntil;
    bool swingHit, boltFired;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        shape = GetComponent<CapsuleCollider2D>();
        visual = GetComponent<SpriteRenderer>();
        post = transform.position;
        Health = maxHealth;
    }

    void Start()
    {
        BindPlayer();
        Enter("Idle");
        nextAttack = Time.time + .8f;
    }

    void BindPlayer()
    {
        player = FindAnyObjectByType<playercontroler>();
        if (player == null) return;
        foreach (var collider in player.GetComponentsInChildren<Collider2D>())
            if (!collider.isTrigger) Physics2D.IgnoreCollision(shape, collider, true);
        foreach (var enemy in FindObjectsByType<GothicEnemy>(FindObjectsSortMode.None))
            if (enemy != this) Physics2D.IgnoreCollision(shape, enemy.shape, true);
    }

    void Enter(string state)
    {
        State = state;
        stateAt = Time.time;
        if (state == "Windup") { swingHit = false; boltFired = false; }
        if (state == "Strike" && kind == GothicEnemyKind.GraveWarden)
            CombatAudio.Play(CombatCue.WardenSwing, .52f);
        else if (state == "Hurt") CombatAudio.Play(CombatCue.EnemyHurt, .47f);
        else if (state == "Death") CombatAudio.Play(CombatCue.EnemyDeath, .56f);
        UpdateSprite();
    }

    void UpdateSprite()
    {
        Sprite[] frames = State switch
        {
            "Walk" => walk,
            "Windup" or "Strike" or "Recover" => attack,
            "Hurt" => hurt,
            "Death" => death,
            _ => idle
        };
        if (frames == null || frames.Length == 0) return;
        float elapsed = Mathf.Max(0f, Time.time - stateAt);
        float fps = State switch { "Walk" => 9f, "Windup" or "Strike" or "Recover" => 9f,
            "Death" => 8f, "Hurt" => 12f, _ => 4f };
        int index = Mathf.FloorToInt(elapsed * fps);
        if (State == "Idle" || State == "Walk") index %= frames.Length;
        else if (State == "Windup")
            index = Mathf.Min(Mathf.FloorToInt(elapsed *
                (kind == GothicEnemyKind.GraveWarden ? 8f : 5f)), 2);
        else if (State == "Strike") index = 3 + Mathf.Min(Mathf.FloorToInt(elapsed * 10f), 1);
        else if (State == "Recover") index = 5;
        else index = Mathf.Min(index, frames.Length - 1);
        index = Mathf.Min(index, frames.Length - 1);
        visual.sprite = frames[index];
    }

    void StopHorizontal() => body.linearVelocity = new Vector2(0f, body.linearVelocity.y);

    bool Solid(Collider2D collider) => collider != null && !collider.isTrigger &&
        collider.attachedRigidbody != body &&
        collider.GetComponentInParent<playercontroler>() == null &&
        collider.GetComponentInParent<GothicEnemy>() == null &&
        collider.GetComponentInParent<AshHound>() == null;

    bool GroundAhead(float direction)
    {
        var bounds = shape.bounds;
        Vector2 point = new Vector2(bounds.center.x + direction * (bounds.extents.x + .38f),
            bounds.min.y + .15f);
        foreach (var hit in Physics2D.RaycastAll(point, Vector2.down, .55f))
            if (Solid(hit.collider)) return true;
        return false;
    }

    bool WallAhead(float direction)
    {
        var bounds = shape.bounds;
        Vector2 point = new Vector2(bounds.center.x + direction * (bounds.extents.x + .1f),
            bounds.min.y + .65f);
        foreach (var hit in Physics2D.RaycastAll(point, Vector2.right * direction, .3f))
            if (Solid(hit.collider)) return true;
        return false;
    }

    bool HasLineOfSight(Vector2 origin, Vector2 destination)
    {
        Vector2 delta = destination - origin;
        foreach (var hit in Physics2D.RaycastAll(origin, delta.normalized, delta.magnitude))
            if (Solid(hit.collider)) return false;
        return true;
    }

    void Update()
    {
        if (PauseMenu.InputBlocked || SceneTransitionManager.IsBusy)
        { StopHorizontal(); return; }
        UpdateSprite();
        if (Health <= 0)
        {
            StopHorizontal();
            if (Time.time - stateAt > 1.12f)
            { visual.enabled = false; shape.enabled = false; body.simulated = false; }
            return;
        }
        if (player == null) { BindPlayer(); return; }
        var playerHealth = player.GetComponent<AshHealth>();
        if (playerHealth == null || playerHealth.IsDead)
        { StopHorizontal(); return; }
        if (State == "Hurt")
        {
            StopHorizontal();
            if (Time.time >= hurtUntil) Enter("Idle");
            return;
        }
        if (State == "Windup" || State == "Strike" || State == "Recover")
        { UpdateAttack(); return; }

        float dx = player.transform.position.x - transform.position.x;
        float dy = player.transform.position.y - transform.position.y;
        bool detected = Mathf.Abs(dx) < detection && Mathf.Abs(dy) <
            (kind == GothicEnemyKind.GraveWarden ? 2.5f : 6f);
        if (Mathf.Abs(dx) > .2f && detected)
        { facing = Mathf.Sign(dx); visual.flipX = facing > 0f; }

        if (kind == GothicEnemyKind.BellMarksman && detected)
        {
            StopHorizontal();
            if (State != "Idle") Enter("Idle");
            if (Time.time >= nextAttack &&
                HasLineOfSight(LaunchPoint(), TargetPoint())) Enter("Windup");
            return;
        }

        if (kind == GothicEnemyKind.GraveWarden && detected &&
            Mathf.Abs(dx) < 2.5f && Time.time >= nextAttack)
        { StopHorizontal(); Enter("Windup"); return; }
        if (kind == GothicEnemyKind.GraveWarden && detected && Mathf.Abs(dx) < 1.6f)
        {
            StopHorizontal();
            if (State != "Idle") Enter("Idle");
            return;
        }

        float fromPost = transform.position.x - post.x;
        float desired;
        if (kind == GothicEnemyKind.GraveWarden && detected && Mathf.Abs(fromPost) < leash)
            desired = Mathf.Sign(dx);
        else
        {
            if (fromPost >= leash - .15f) facing = -1f;
            else if (fromPost <= -leash + .15f) facing = 1f;
            desired = facing;
        }
        if (Mathf.Abs(desired) < .01f) desired = facing;
        if (GroundAhead(desired) && !WallAhead(desired))
        {
            facing = desired;
            visual.flipX = facing > 0f;
            body.linearVelocity = new Vector2(desired * speed, body.linearVelocity.y);
            if (State != "Walk") Enter("Walk");
        }
        else
        {
            StopHorizontal();
            if (State != "Idle") Enter("Idle");
            if (!detected) facing = -desired;
        }
    }

    void UpdateAttack()
    {
        StopHorizontal();
        float elapsed = Time.time - stateAt;
        if (State == "Windup")
        {
            float wait = kind == GothicEnemyKind.GraveWarden ? .38f : .55f;
            if (elapsed < wait) return;
            Enter("Strike");
            if (kind == GothicEnemyKind.GraveWarden) TryMeleeHit();
            else FireBolt();
            return;
        }
        if (State == "Strike")
        {
            if (kind == GothicEnemyKind.GraveWarden && elapsed < .16f) TryMeleeHit();
            if (elapsed >= .18f) Enter("Recover");
            return;
        }
        if (elapsed >= (kind == GothicEnemyKind.GraveWarden ? .45f : .5f))
        {
            nextAttack = Time.time + (kind == GothicEnemyKind.GraveWarden ? 1.3f : 1.8f);
            Enter("Idle");
        }
    }

    void TryMeleeHit()
    {
        if (swingHit || player == null) return;
        Vector2 center = (Vector2)transform.position + new Vector2(facing * .9f, 1.2f);
        foreach (var collider in Physics2D.OverlapBoxAll(center, new Vector2(2.2f, 2f), 0f))
        {
            if (collider.GetComponentInParent<playercontroler>() != player) continue;
            var dash = player.GetComponent<PlayerDash>();
            if (dash != null && dash.IsDashing) return;
            swingHit = true;
            if (player.GetComponent<AshHealth>().TakeDamage(1))
                PlayerHitReaction.Apply(player.gameObject, transform.position, new Vector2(3f, 2f));
            return;
        }
    }

    Vector2 LaunchPoint() => (Vector2)transform.position + new Vector2(facing * 1.15f, 2.1f);
    Vector2 TargetPoint() => (Vector2)player.transform.position + new Vector2(0f, 1.15f);

    void FireBolt()
    {
        if (boltFired || boltPrefab == null || player == null) return;
        boltFired = true;
        Vector2 start = LaunchPoint();
        Vector2 direction = (TargetPoint() - start).normalized;
        var bolt = Instantiate(boltPrefab, start, Quaternion.identity);
        bolt.Launch(direction, this);
        CombatAudio.Play(CombatCue.BoltShot, .5f);
    }

    public bool TakeDamage(int amount, Vector3 source)
    {
        if (Health <= 0 || amount <= 0 || Time.time < hurtUntil) return false;
        Health = Mathf.Max(0, Health - amount);
        hurtUntil = Time.time + .34f;
        StopHorizontal();
        if (Health == 0)
        {
            if (hurtbox != null) hurtbox.enabled = false;
            Enter("Death");
        }
        else Enter("Hurt");
        return true;
    }

    public void ResetEnemy()
    {
        body.simulated = true;
        shape.enabled = true;
        if (hurtbox != null) hurtbox.enabled = true;
        visual.enabled = true;
        transform.position = post;
        body.position = post;
        body.linearVelocity = Vector2.zero;
        Health = maxHealth;
        hurtUntil = 0f;
        nextAttack = Time.time + .8f;
        facing = -1f;
        visual.flipX = false;
        Enter("Idle");
    }

    public static void ResetAll()
    {
        foreach (var bolt in FindObjectsByType<GothicBolt>(FindObjectsSortMode.None))
            Destroy(bolt.gameObject);
        foreach (var enemy in FindObjectsByType<GothicEnemy>(FindObjectsSortMode.None)) enemy.ResetEnemy();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (kind == GothicEnemyKind.GraveWarden)
            Gizmos.DrawWireCube(transform.position + new Vector3(facing * .9f, 1.2f), new Vector3(2.2f, 2f));
        else
            Gizmos.DrawWireSphere(LaunchPoint(), .2f);
    }
}
