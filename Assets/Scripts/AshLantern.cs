using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class AshLantern : MonoBehaviour
{
    public float radius = 2f;
    public Vector3 spawnOffset = new Vector3(0, 1.5f, 0);
    public bool showPrompt = true;
    public bool IsLit => lit;
    SpriteRenderer sprite;
    SpriteRenderer flame;
    SpriteRenderer[] embers;
    Color baseColor;
    bool lit;
    float litAtTime = -10f;
    static Sprite[] flameFrames;
    static Sprite emberSprite;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        baseColor = sprite.color;
        CreateFire();
    }

    void Start()
    {
        var respawn = FindAnyObjectByType<playercontroler>()?.GetComponent<PlayerRespawn>();
        if (respawn != null &&
            Vector3.Distance(respawn.respawnPoint, transform.position + spawnOffset) < .1f)
            lit = true;
        UpdateFire();
    }

    playercontroler Nearby()
    {
        var player = FindAnyObjectByType<playercontroler>();
        return player != null && !PauseMenu.InputBlocked &&
            Vector2.Distance(player.transform.position, transform.position) < radius ? player : null;
    }

    void Update()
    {
        UpdateFire();
        if (Input.GetKeyDown(KeyCode.E)) TryActivate();
    }

    public bool TryActivate()
    {
        var player = Nearby();
        if (player == null) return false;
        player.GetComponent<PlayerRespawn>()?.SetCheckpoint(transform.position + spawnOffset);
        player.GetComponent<AshHealth>()?.ResetHealth();
        AshHound.ResetAll();
        GothicEnemy.ResetAll();
        GameSaveSystem.Save(player.gameObject, out _);
        lit = true;
        litAtTime = Time.unscaledTime;
        UpdateFire();
        return true;
    }

    static void EnsureFireSprites()
    {
        if (flameFrames != null && flameFrames[0] != null) return;
        flameFrames = new Sprite[4];
        for (int frame = 0; frame < flameFrames.Length; frame++)
        {
            var texture = new Texture2D(16, 20, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[16 * 20];
            for (int y = 0; y < 19; y++)
            {
                int half = y < 3 ? 3 : y < 8 ? 5 : y < 12 ? 4 : y < 16 ? 2 : 0;
                int center = 7 + (y > 8 ? (frame + y / 4) % 3 - 1 : 0);
                for (int x = center - half; x <= center + half; x++)
                {
                    if (x < 0 || x >= 16) continue;
                    int d = Mathf.Abs(x - center);
                    Color32 color = d >= half ? new Color32(151, 38, 28, 210) :
                        d >= half - 1 ? new Color32(230, 85, 30, 245) :
                        y < 10 ? new Color32(255, 209, 94, 255) : new Color32(255, 132, 43, 255);
                    pixels[y * 16 + x] = color;
                }
            }
            // Small side tongues keep the flame from looking like a single cone.
            int side = frame % 2 == 0 ? 2 : 12;
            for (int y = 5; y < 11 + frame % 2; y++)
                pixels[y * 16 + side + (y / 3) % 2] = new Color32(236, 90, 30, 230);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            flameFrames[frame] = Sprite.Create(texture, new Rect(0, 0, 16, 20),
                new Vector2(.5f, .35f), 40f);
        }

        var emberTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        emberTexture.filterMode = FilterMode.Point;
        emberTexture.SetPixels32(new [] {
            new Color32(238, 93, 38, 220), new Color32(0, 0, 0, 0),
            new Color32(255, 188, 77, 255), new Color32(244, 117, 42, 210)
        });
        emberTexture.Apply(false, true);
        emberSprite = Sprite.Create(emberTexture, new Rect(0, 0, 2, 2),
            new Vector2(.5f, .5f), 28f);
    }

    void CreateFire()
    {
        EnsureFireSprites();
        var fireObject = new GameObject("Lit flame");
        fireObject.transform.SetParent(transform, false);
        fireObject.transform.localPosition = new Vector3(0f, -.04f, -.01f);
        flame = fireObject.AddComponent<SpriteRenderer>();
        flame.sortingLayerID = sprite.sortingLayerID;
        flame.sortingOrder = sprite.sortingOrder + 1;
        flame.sprite = flameFrames[0];

        embers = new SpriteRenderer[3];
        for (int i = 0; i < embers.Length; i++)
        {
            var emberObject = new GameObject("Rising ember " + (i + 1));
            emberObject.transform.SetParent(transform, false);
            var renderer = emberObject.AddComponent<SpriteRenderer>();
            renderer.sprite = emberSprite;
            renderer.sortingLayerID = sprite.sortingLayerID;
            renderer.sortingOrder = sprite.sortingOrder + 2;
            embers[i] = renderer;
        }
    }

    void UpdateFire()
    {
        if (flame == null) return;
        if (flameFrames == null || flameFrames.Length == 0 || flameFrames[0] == null)
            EnsureFireSprites();
        flame.enabled = lit;
        if (!lit)
        {
            foreach (var ember in embers) ember.enabled = false;
            sprite.color = baseColor * new Color(.72f, .67f, .68f, 1f);
            return;
        }

        float time = Time.unscaledTime;
        flame.sprite = flameFrames[Mathf.FloorToInt(time * 9f) % flameFrames.Length];
        float ignition = Mathf.Clamp01((time - litAtTime) / .3f);
        float flare = .35f * Mathf.Sin(ignition * Mathf.PI);
        flame.transform.localScale = new Vector3(
            1f + .08f * Mathf.Sin(time * 12f) + flare,
            .2f + .8f * ignition + flare, 1f);
        float pulse = .93f + .07f * Mathf.Sin(time * 7f);
        sprite.color = baseColor * new Color(1f, pulse, pulse, 1f);
        for (int i = 0; i < embers.Length; i++)
        {
            var ember = embers[i];
            float phase = (time * (.8f + i * .12f) + i * .34f) % 1f;
            ember.enabled = true;
            ember.transform.localPosition = new Vector3(
                Mathf.Sin(time * 2.7f + i * 2.4f) * .14f,
                .16f + phase * .55f, -.01f);
            ember.color = new Color(1f, 1f, 1f, 1f - phase);
        }
    }

    void OnGUI()
    {
        if (!showPrompt || Nearby() == null) return;
        var style = new GUIStyle(GUI.skin.label) {
            alignment = TextAnchor.MiddleCenter, fontSize = 18
        };
        style.normal.textColor = new Color(1f, .78f, .55f);
        GUI.Label(new Rect(0, Screen.height - 90, Screen.width, 35),
            GameText.Get("interaction.rest_lantern"), style);
    }
}
