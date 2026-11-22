using UnityEngine;

// Small readable hazards. Each wave owns one short active collider and never tracks the player.
public sealed class BossAshWave : MonoBehaviour
{
    static Sprite waveSprite;
    BossArenaCombat owner;
    float direction, speed, expires;
    int damage;
    BoxCollider2D box;
    bool attempted, deflected;
    public bool IsDeflected => deflected;

    public bool Deflect()
    {
        if (deflected || attempted || owner == null) return false;
        deflected = true;
        direction = -direction;
        speed *= 1.2f;
        expires = Time.time + 1.5f;
        var visual = GetComponent<SpriteRenderer>();
        visual.flipX = direction < 0f;
        visual.color = new Color(.9f, .83f, .74f);
        CombatAudio.Play(CombatCue.ProjectileParry, .55f);
        return true;
    }

    public static BossAshWave Spawn(BossArenaCombat owner, Vector2 origin, float direction,
        float speed, int damage)
    {
        var go = new GameObject("Boss ash wave");
        go.transform.position = origin;
        var visual = go.AddComponent<SpriteRenderer>();
        visual.sprite = WaveSprite();
        visual.sortingOrder = 25;
        visual.flipX = direction < 0f;
        var wave = go.AddComponent<BossAshWave>();
        wave.owner = owner;
        wave.direction = Mathf.Sign(direction);
        wave.speed = speed;
        wave.damage = damage;
        wave.expires = Time.time + 2.6f;
        wave.box = go.AddComponent<BoxCollider2D>();
        wave.box.isTrigger = true;
        wave.box.size = new Vector2(1.45f, .45f);
        return wave;
    }
    static Sprite WaveSprite()
    {
        if (waveSprite != null) return waveSprite;
        var texture = new Texture2D(24, 8, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[24 * 8];
        for (int x = 0; x < 24; x++)
        for (int y = 0; y < 8; y++)
        {
            int height = x < 4 ? 2 : x < 9 ? 4 : x < 15 ? 6 : x < 20 ? 4 : 2;
            if (y >= height) continue;
            pixels[y * 24 + x] = y >= height - 2
                ? new Color32(124, 108, 104, 205)
                : new Color32(71, 65, 68, 225);
        }
        texture.SetPixels32(pixels); texture.Apply(false);
        waveSprite = Sprite.Create(texture, new Rect(0, 0, 24, 8), new Vector2(.5f, 0f), 16f);
        waveSprite.name = "Boss ash wave";
        return waveSprite;
    }
    void FixedUpdate()
    {
        if (owner == null || owner.CurrentHealth <= 0 || Time.time >= expires ||
            transform.position.x < 1f || transform.position.x > 25f)
        { Destroy(gameObject); return; }
        if (PauseMenu.IsPaused || SceneTransitionManager.IsBusy) return;
        transform.position += Vector3.right * (direction * speed * Time.fixedDeltaTime);
        Physics2D.SyncTransforms();
        if (deflected)
        {
            if (Mathf.Abs(transform.position.x - owner.transform.position.x) < 1.05f)
            {
                if (owner.TryTakeDamage(1)) CombatAudio.Play(CombatCue.SwordImpact, .5f);
                Destroy(gameObject);
            }
            return;
        }
        if (attempted) return;
        foreach (var hit in Physics2D.OverlapBoxAll(box.bounds.center, box.bounds.size, 0f))
        {
            var player = hit.GetComponentInParent<playercontroler>();
            if (player == null) continue;
            var dash = player.GetComponent<PlayerDash>();
            if (dash != null && dash.IsDashing) return;
            attempted = true;
            var health = player.GetComponent<AshHealth>();
            if (health != null && health.TakeDamage(damage) && !health.IsDead)
                PlayerHitReaction.Apply(player.gameObject, owner.transform.position, new Vector2(3f, 2f));
            return;
        }
    }
    public static void ClearFor(BossArenaCombat boss)
    {
        foreach (var wave in FindObjectsByType<BossAshWave>(FindObjectsSortMode.None))
            if (wave.owner == boss) Destroy(wave.gameObject);
    }
}

public sealed class BossLandingMarker : MonoBehaviour
{
    static Sprite markerSprite;
    float expires;
    SpriteRenderer visual;
    public static BossLandingMarker Show(Vector2 point, float seconds)
    {
        var go = new GameObject("Boss landing warning");
        go.transform.position = point;
        var marker = go.AddComponent<BossLandingMarker>();
        marker.visual = go.AddComponent<SpriteRenderer>();
        marker.visual.sprite = SpriteAsset();
        marker.visual.sortingOrder = 24;
        marker.expires = Time.time + seconds;
        return marker;
    }
    static Sprite SpriteAsset()
    {
        if (markerSprite != null) return markerSprite;
        var texture = new Texture2D(48, 3, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[48 * 3];
        for (int x = 0; x < 48; x++)
        for (int y = 0; y < 3; y++)
            if (x < 3 || x > 44 || y == 0 || (x % 4) < 2)
                pixels[y * 48 + x] = new Color32(134, 61, 59, 180);
        texture.SetPixels32(pixels); texture.Apply(false);
        markerSprite = Sprite.Create(texture, new Rect(0, 0, 48, 3),
            new Vector2(.5f, 0f), 16f);
        markerSprite.name = "Boss landing warning";
        return markerSprite;
    }
    void Update()
    {
        if (Time.time >= expires) { Hide(); return; }
        if (visual != null)
        {
            Color color = visual.color;
            color.a = .5f + .35f * Mathf.Sin(Time.time * 25f);
            visual.color = color;
        }
    }
    public void Hide() { if (this != null) Destroy(gameObject); }
}

public static class BossFightEffects
{
    public static void Bell(float volume)
    {
        const int sampleRate = 16000;
        var samples = new float[sampleRate];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)sampleRate;
            samples[i] = (Mathf.Sin(2 * Mathf.PI * 110f * t) +
                .26f * Mathf.Sin(2 * Mathf.PI * 223f * t)) * Mathf.Exp(-4f * t) * .35f;
        }
        var go = new GameObject("Distant boss bell");
        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false; audio.spatialBlend = 0; audio.volume = volume;
        var clip = AudioClip.Create("Distant boss bell", samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        audio.clip = clip; audio.Play();
        Object.Destroy(go, 1.2f);
        Object.Destroy(clip, 1.2f);
    }
    public static void VictoryBanner()
    {
        new GameObject("Boss defeated announcement").AddComponent<BossVictoryBanner>();
    }
}

public sealed class BossVictoryBanner : MonoBehaviour
{
    float until;
    GUIStyle style;
    void Awake() { until = Time.unscaledTime + 3.5f; }
    void Update() { if (Time.unscaledTime >= until) Destroy(gameObject); }
    void OnGUI()
    {
        if (Time.unscaledTime >= until) return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter,
                fontSize = 26, fontStyle = FontStyle.Bold };
            style.normal.textColor = new Color(.75f, .7f, .68f);
        }
        GUI.Label(new Rect(Screen.width * .25f, Screen.height * .27f,
            Screen.width * .5f, 50f), "BOSS DEFEATED", style);
    }
}
