using UnityEngine;

// The original monster is visible from entry; no alternate form or sprite deformation.
[DefaultExecutionOrder(-100)]
public sealed class BossArrival : MonoBehaviour
{
    public BossArenaCombat boss;
    public Collider2D combatArea;
    [Range(4f, 6f)] public float duration = 5f;
    public float approachDistance = 4f;
    public bool ReadyForCombat { get; private set; }
    public bool IsPlaying { get; private set; }
    Animator animator;
    SpriteRenderer bossSprite;
    camerafollow follow;
    Camera sceneCamera;
    playercontroler player;
    Vector3 entry, heroView, bossView;
    float elapsed, originalSpeed;
    bool bellPlayed, followWasEnabled;
    AudioSource bell;
    AudioClip bellClip;
    Material ashMaterial;

    void Start()
    {
        if (boss == null || BossArenaProgress.Defeated) { ReadyForCombat = true; return; }
        player = FindFirstObjectByType<playercontroler>();
        if (player == null) return;
        entry = player.transform.position;
        animator = boss.GetComponent<Animator>();
        bossSprite = boss.GetComponent<SpriteRenderer>();
        originalSpeed = animator.speed;
        animator.Play("BossDyn_Idle", 0, 0);
        animator.Update(0);
        animator.speed = 0;
        boss.SuspendArenaCombat(true);
        bell = gameObject.AddComponent<AudioSource>();
        bell.playOnAwake = false; bell.spatialBlend = 0; bell.volume = .32f;
        const int rate = 22050;
        var samples = new float[rate * 3];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / rate;
            samples[i] = (Mathf.Sin(2 * Mathf.PI * 110 * t) * Mathf.Exp(-t * 1.5f)
                + .35f * Mathf.Sin(2 * Mathf.PI * 231 * t) * Mathf.Exp(-t * 2.3f)
                + .15f * Mathf.Sin(2 * Mathf.PI * 319 * t) * Mathf.Exp(-t * 3f))
                * Mathf.Clamp01(t / .025f) * .6f * Mathf.Clamp01((3 - t) / .15f);
        }
        bellClip = AudioClip.Create("Distant church toll", samples.Length, 1, rate, false);
        bellClip.SetData(samples, 0); bell.clip = bellClip;
        var ashObject = new GameObject("Falling altar ash");
        ashObject.transform.SetParent(transform, false);
        ashObject.transform.position = boss.transform.position + Vector3.up * 7;
        var ash = ashObject.AddComponent<ParticleSystem>();
        ash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ash.main;
        main.startLifetime = 12; main.startSpeed = .4f; main.startSize = .055f;
        main.startColor = new Color(.6f, .52f, .46f, .45f);
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 100;
        var emission = ash.emission; emission.rateOverTime = 5;
        var shape = ash.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(12, 1, 0);
        var velocity = ash.velocityOverLifetime; velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World; velocity.y = -.7f; velocity.x = -.1f;
        ashMaterial = new Material(Shader.Find("Sprites/Default"));
        var renderer = ash.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = ashMaterial; renderer.sortingOrder = 3;
        ash.Play();
    }

    void Update()
    {
        if (ReadyForCombat || boss == null || player == null || PauseMenu.IsPaused ||
            (!IsPlaying && PauseMenu.InputBlocked)) return;
        if (!IsPlaying)
        {
            if (player.transform.position.x < entry.x + approachDistance) return;
            IsPlaying = true;
            PlayerControlLock.Acquire(this, player.gameObject, true);
            animator.Play("BossDyn_IntroAwakening", 0, 0f);
            animator.speed = 5f / Mathf.Clamp(duration, 4f, 6f);
            sceneCamera = Camera.main;
            if (sceneCamera != null)
            {
                follow = sceneCamera.GetComponent<camerafollow>();
                followWasEnabled = follow != null && follow.enabled;
                if (follow != null) follow.enabled = false;
                heroView = sceneCamera.transform.position;
                float bossX = boss.transform.position.x - 2;
                var bounds = sceneCamera.GetComponent<AshenSanctumCamera>();
                if (bounds != null)
                {
                    float halfWidth = sceneCamera.orthographicSize * sceneCamera.aspect;
                    bossX = Mathf.Clamp(bossX, bounds.left + halfWidth, bounds.right - halfWidth);
                }
                bossView = new Vector3(bossX, heroView.y, heroView.z);
            }
        }
        elapsed += Time.deltaTime;
        float phase = Mathf.Clamp01(elapsed / Mathf.Clamp(duration, 4, 6));
        if (phase >= .55f && !bellPlayed)
        {
            bellPlayed = true;
            bossSprite.flipX = player.transform.position.x > boss.transform.position.x;
            bell.Play();
            FindFirstObjectByType<AshenSanctumArena>()?.RingIntroBell();
        }
        float pan = phase < .3f ? Mathf.SmoothStep(0, 1, phase / .3f)
            : phase < .65f ? 1 : Mathf.SmoothStep(1, 0, (phase - .65f) / .35f);
        if (sceneCamera != null) sceneCamera.transform.position = Vector3.Lerp(heroView, bossView, pan);
        if (phase < 1) return;
        ReadyForCombat = true;
        Restore();
    }

    void Restore()
    {
        if (animator != null)
        {
            animator.enabled = true;
            animator.speed = originalSpeed;
            if (IsPlaying) animator.Play("BossDyn_Idle", 0, 0f);
        }
        if (IsPlaying)
        {
            if (follow != null) follow.enabled = followWasEnabled;
            PlayerControlLock.Release(this);
        }
        IsPlaying = false;
    }
    void OnDisable() { Restore(); }
    void OnDestroy()
    {
        if (bellClip != null) Destroy(bellClip);
        if (ashMaterial != null) Destroy(ashMaterial);
    }

    void OnGUI()
    {
        if (boss == null || !boss.IsFighting || boss.IsDefeated || !ReadyForCombat) return;
        float width = Mathf.Min(600, Screen.width * .65f);
        var bar = new Rect((Screen.width - width) / 2, Screen.height - 55, width, 12);
        Color old = GUI.color;
        GUI.color = new Color(.15f, .1f, .09f); GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = new Color(.55f, .08f, .06f);
        GUI.DrawTexture(new Rect(bar.x + 2, bar.y + 2, (bar.width - 4) * boss.CurrentHealth / boss.maxHealth, 8), Texture2D.whiteTexture);
        GUI.color = old;
        GUI.Label(new Rect(bar.x, bar.y - 26, width, 24),
            boss.WendigoForm ? "The Ashen Wendigo" : "The Ashen Warden");
    }
}
