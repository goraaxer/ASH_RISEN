using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
public sealed class AshHeartsHUD : MonoBehaviour
{
    [SerializeField] private AshHealth health;
    [SerializeField] private Sprite fullHeart;
    [SerializeField] private Sprite emptyHeart;
    [SerializeField] private Sprite[] damageFrames;
    [SerializeField, Min(0.01f)] private float dissolveDuration = 0.64f;
    [SerializeField, Min(8f)] private float cellSize = 32f;
    [SerializeField, Min(0f)] private float spacing = 2f;

    private AshHealth subscribedHealth;
    private Image[] hearts = new Image[0];
    private Coroutine[] animations = new Coroutine[0];
    public AshHealth HealthSource => health;

    public void Configure(AshHealth source, Sprite full, Sprite empty, Sprite[] frames)
    {
        Unsubscribe();
        health = source;
        fullHeart = full;
        emptyHeart = empty;
        damageFrames = frames;
        if (Application.isPlaying && isActiveAndEnabled) Bind();
        else if (!Application.isPlaying && health != null)
        {
            Build(health.MaxHealth);
            ShowInstant(health.MaxHealth);
        }
    }

    private void OnEnable()
    {
        if (Application.isPlaying) Bind();
    }

    private void OnDisable()
    {
        Unsubscribe();
        StopAllCoroutines();
        for (int i = 0; i < animations.Length; i++) animations[i] = null;
    }

    private void Unsubscribe()
    {
        if (subscribedHealth != null) subscribedHealth.HealthChanged -= OnHealthChanged;
        subscribedHealth = null;
    }

    private void Bind()
    {
        Unsubscribe();
        if (health == null || fullHeart == null || emptyHeart == null)
        {
            Debug.LogWarning("AshHeartsHUD: assign Health, Full Heart and Empty Heart.", this);
            return;
        }
        Build(health.MaxHealth);
        ShowInstant(health.CurrentHealth);
        subscribedHealth = health;
        subscribedHealth.HealthChanged += OnHealthChanged;
    }

    private void Build(int count)
    {
        StopAllCoroutines();
        // This component owns only its dedicated HeartSlots container.
        Transform old = transform.Find("HeartSlots");
        if (old != null)
        {
            old.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }
        RectTransform root = (RectTransform)transform;
        root.sizeDelta = new Vector2(count * (cellSize + spacing) - spacing, cellSize);
        RectTransform slots = NewRect("HeartSlots", root);
        slots.anchorMin = Vector2.zero;
        slots.anchorMax = Vector2.one;
        slots.offsetMin = slots.offsetMax = Vector2.zero;
        hearts = new Image[count];
        animations = new Coroutine[count];
        for (int i = 0; i < count; i++)
        {
            RectTransform slot = NewRect("HP_" + (i + 1), slots);
            slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(0f, 1f);
            slot.sizeDelta = new Vector2(cellSize, cellSize);
            slot.anchoredPosition = new Vector2(i * (cellSize + spacing), 0f);
            MakeImage("Empty", slot, emptyHeart);
            hearts[i] = MakeImage("Heart", slot, fullHeart);
        }
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image MakeImage(string name, Transform parent, Sprite sprite)
    {
        RectTransform rect = NewRect(name, parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private void ShowInstant(int hp)
    {
        StopAllCoroutines();
        for (int i = 0; i < hearts.Length; i++)
        {
            animations[i] = null;
            hearts[i].sprite = fullHeart;
            hearts[i].enabled = i < hp;
        }
    }

    private void OnHealthChanged(int oldHP, int newHP, int maxHP)
    {
        // Other listeners can trigger a respawn while an older damage event is
        // still being dispatched. Never redraw a stale zero-HP state afterward.
        if (health != null && health.CurrentHealth != newHP)
        {
            ShowInstant(health.CurrentHealth);
            return;
        }
        if (hearts.Length != maxHP)
        {
            Build(maxHP);
            ShowInstant(newHP);
            return;
        }
        for (int i = 0; i < hearts.Length; i++)
        {
            if (i < newHP)
            {
                // Healing immediately cancels this slot's older damage effect.
                if (animations[i] != null) StopCoroutine(animations[i]);
                animations[i] = null;
                hearts[i].sprite = fullHeart;
                hearts[i].enabled = true;
            }
            else if (i < oldHP)
            {
                if (animations[i] != null) StopCoroutine(animations[i]);
                if (damageFrames == null || damageFrames.Length == 0)
                {
                    hearts[i].enabled = false;
                    animations[i] = null;
                }
                else animations[i] = StartCoroutine(Dissolve(i));
            }
            else if (animations[i] == null) hearts[i].enabled = false;
        }
    }

    private IEnumerator Dissolve(int index)
    {
        Image image = hearts[index];
        image.enabled = true;
        float frameDuration = Mathf.Max(0.01f, dissolveDuration) / damageFrames.Length;
        for (int frame = 0; frame < damageFrames.Length; frame++)
        {
            while (PauseMenu.IsPaused) yield return null;
            image.sprite = damageFrames[frame];
            // Show every ash frame, even after a hitch or an Inspector damage call.
            // Realtime also lets the effect finish when a death handler pauses time.
            yield return new WaitForSecondsRealtime(frameDuration);
        }
        image.enabled = false;
        animations[index] = null;
    }
}
