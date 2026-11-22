using UnityEngine;

// Phase-two magic is deliberately small, pixel sharp and fully telegraphed.
public sealed class BossScarletMagic : MonoBehaviour
{
    enum Kind { Sigil, Shard }

    static Sprite sigilSprite, burstSprite, shardSprite;
    static readonly Color32 Dark = new Color32(85, 25, 28, 235);
    static readonly Color32 Red = new Color32(153, 49, 46, 255);
    static readonly Color32 Hot = new Color32(211, 91, 67, 255);
    static readonly Color32 Bone = new Color32(177, 155, 132, 255);

    BossArenaCombat owner;
    SpriteRenderer visual;
    BoxCollider2D hitbox;
    Kind kind;
    int damage;
    float activeAt, expiresAt, groundY;
    Vector2 velocity;
    bool deflected, attempted, burst;

    public bool Deflect()
    {
        if (kind != Kind.Shard || deflected || Time.time < activeAt || owner == null) return false;
        deflected = true;
        velocity = ((Vector2)owner.transform.position + Vector2.up - (Vector2)transform.position).normalized * 16f;
        expiresAt = Time.time + 1.6f;
        visual.color = Bone;
        CombatAudio.Play(CombatCue.ProjectileParry, .55f);
        return true;
    }

    public static BossScarletMagic SpawnSigil(BossArenaCombat owner, Vector2 position, int damage)
        => Spawn(owner, position, damage, Kind.Sigil, position.y);

    public static BossScarletMagic SpawnShard(BossArenaCombat owner, Vector2 position, float groundY, int damage)
        => Spawn(owner, position, damage, Kind.Shard, groundY);

    static BossScarletMagic Spawn(BossArenaCombat owner, Vector2 position, int damage, Kind kind, float groundY)
    {
        var go = new GameObject(kind == Kind.Sigil ? "Wendigo crimson sigil" : "Wendigo scarlet shard");
        go.transform.position = position;
        var magic = go.AddComponent<BossScarletMagic>();
        magic.owner = owner;
        magic.damage = damage;
        magic.kind = kind;
        magic.groundY = groundY;
        magic.activeAt = Time.time + (kind == Kind.Sigil ? .48f : .44f);
        magic.expiresAt = Time.time + (kind == Kind.Sigil ? .75f : 2f);
        magic.visual = go.AddComponent<SpriteRenderer>();
        magic.visual.sprite = kind == Kind.Sigil ? SigilSprite() : ShardSprite();
        magic.visual.sortingOrder = 24;
        magic.hitbox = go.AddComponent<BoxCollider2D>();
        magic.hitbox.isTrigger = true;
        magic.hitbox.enabled = false;
        magic.hitbox.size = kind == Kind.Sigil ? new Vector2(1.4f, 3.2f) : new Vector2(.6f, 1.15f);
        magic.hitbox.offset = kind == Kind.Sigil ? new Vector2(0, 1.6f) : Vector2.zero;
        return magic;
    }

    void Update()
    {
        if (owner == null || owner.CurrentHealth <= 0 || Time.time >= expiresAt)
        { Destroy(gameObject); return; }
        if (PauseMenu.IsPaused) return;
        if (kind == Kind.Sigil)
        {
            if (!burst && Time.time >= activeAt)
            {
                burst = true;
                visual.sprite = BurstSprite();
                hitbox.enabled = true;
                Physics2D.SyncTransforms();
                TryHitPlayer();
            }
            else if (!burst)
                visual.color = new Color(1f, 1f, 1f, .82f + .15f * Mathf.Sin(Time.time * 16f));
        }
        else if (Time.time < activeAt)
            visual.color = new Color(1f, 1f, 1f, .78f + .17f * Mathf.Sin(Time.time * 18f));
        else if (!hitbox.enabled) { hitbox.enabled = true; visual.color = Color.white; }
    }

    void FixedUpdate()
    {
        if (PauseMenu.IsPaused || Time.time < activeAt || owner == null || owner.CurrentHealth <= 0) return;
        if (kind == Kind.Shard)
        {
            transform.position += (Vector3)(deflected ? velocity : Vector2.down * 12f) * Time.fixedDeltaTime;
            Physics2D.SyncTransforms();
            if (deflected)
            {
                if (Vector2.Distance(transform.position, (Vector2)owner.transform.position + Vector2.up) < 1.2f)
                { owner.TryTakeDamage(1); Destroy(gameObject); }
                return;
            }
            if (transform.position.y <= groundY) { Destroy(gameObject); return; }
        }
        TryHitPlayer();
    }

    void TryHitPlayer()
    {
        if (attempted || !hitbox.enabled || deflected) return;
        foreach (var collider in Physics2D.OverlapBoxAll(hitbox.bounds.center, hitbox.bounds.size, 0f))
        {
            var player = collider.GetComponentInParent<playercontroler>();
            if (player == null) continue;
            var dash = player.GetComponent<PlayerDash>();
            if (dash != null && dash.IsDashing) continue;
            var health = player.GetComponent<AshHealth>();
            if (health == null) continue;
            attempted = true;
            if (health.TakeDamage(damage))
            {
                health.GrantInvulnerability(.7f);
                if (!health.IsDead) PlayerHitReaction.Apply(player.gameObject, transform.position, new Vector2(3f, 2f));
            }
            return;
        }
    }

    static Sprite SigilSprite()
    {
        if (sigilSprite != null) return sigilSprite;
        var pixels = new Color32[28 * 8];
        for (int x = 1; x < 27; x++)
        for (int y = 0; y < 8; y++)
        {
            float ring = Mathf.Pow((x - 13.5f) / 13f, 2) + Mathf.Pow((y - 3.5f) / 3.2f, 2);
            if (ring > .65f && ring < 1.18f && (x + y) % 7 != 0)
                pixels[y * 28 + x] = (x + y) % 4 == 0 ? Hot : Red;
        }
        pixels[3 * 28 + 13] = Bone;
        pixels[4 * 28 + 14] = Bone;
        return sigilSprite = MakeSprite(pixels, 28, 8, new Vector2(.5f, .1f), "Crimson sigil warning");
    }

    static Sprite BurstSprite()
    {
        if (burstSprite != null) return burstSprite;
        var pixels = new Color32[24 * 54];
        for (int y = 0; y < 54; y++)
        {
            int half = 3 + (y * 7 % 11) / 2;
            for (int x = 12 - half; x <= 12 + half; x++)
            {
                if (x < 0 || x >= 24 || (x * 3 + y * 5) % 9 == 0) continue;
                pixels[y * 24 + x] = Mathf.Abs(x - 12) > half - 2 ? Hot : Dark;
            }
        }
        return burstSprite = MakeSprite(pixels, 24, 54, new Vector2(.5f, 0f), "Crimson sigil burst");
    }

    static Sprite ShardSprite()
    {
        if (shardSprite != null) return shardSprite;
        var pixels = new Color32[10 * 22];
        for (int y = 0; y < 22; y++)
        {
            int half = y < 8 ? 1 : y < 17 ? 3 : 2;
            for (int x = 5 - half; x <= 5 + half; x++)
                pixels[y * 10 + x] = x == 5 ? Hot : x == 5 - half ? Red : Dark;
        }
        return shardSprite = MakeSprite(pixels, 10, 22, new Vector2(.5f, .5f), "Scarlet falling shard");
    }

    static Sprite MakeSprite(Color32[] pixels, int width, int height, Vector2 pivot, string name)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply(false);
        var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), pivot, 16f);
        sprite.name = name;
        return sprite;
    }

    public static void ClearFor(BossArenaCombat boss)
    {
        foreach (var magic in FindObjectsByType<BossScarletMagic>(FindObjectsSortMode.None))
            if (magic.owner == boss) Destroy(magic.gameObject);
    }
}
