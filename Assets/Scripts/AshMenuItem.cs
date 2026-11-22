using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A reusable animated text action. One TMP mesh and one shared ash mesh serve the whole item.
[RequireComponent(typeof(Button))]
public sealed class AshMenuItem : MonoBehaviour, IPointerEnterHandler,
    IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public enum ItemState { Idle, Hover, Selected, Click, Disabled }
    public ItemState State { get; private set; }
    public float Heat => glyph != null ? glyph.heat : 0f;

    AshMenuView owner;
    AshMenuParticles ash;
    AshMenuTheme theme;
    AudioSource audioSource;
    TextMeshProUGUI label;
    AshGlyphDissolve glyph;
    Button button;
    Material emberMaterial;
    Outline emberEdge;
    Action action;
    Coroutine clickRoutine;
    bool pointerInside;
    bool keyboardSelected;
    bool pointerAway;
    float ashTimer;
    float crackleTimer;

    public void Configure(AshMenuView menu, TextMeshProUGUI text,
        AshMenuParticles particles, AshMenuTheme menuTheme,
        AudioSource sound, Action onClick)
    {
        owner = menu;
        label = text;
        glyph = text.GetComponent<AshGlyphDissolve>();
        ash = particles;
        theme = menuTheme;
        audioSource = sound;
        action = onClick;
        button = GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(Activate);
        if (label.fontSharedMaterial != null)
        {
            emberMaterial = new Material(label.fontSharedMaterial);
            emberMaterial.name = "Ash menu glyph ember";
            label.fontSharedMaterial = emberMaterial;
        }
        emberEdge = label.gameObject.AddComponent<Outline>();
        emberEdge.effectDistance = new Vector2(2.3f, -2.3f);
        emberEdge.effectColor = new Color(1f, .27f, .08f, 0f);
        emberEdge.useGraphicAlpha = true;
        State = ItemState.Idle;
    }

    public void SetDisabled(bool disabled)
    {
        if (button == null) button = GetComponent<Button>();
        button.interactable = !disabled;
        if (glyph != null) glyph.disabled = disabled;
        State = disabled ? ItemState.Disabled : ItemState.Idle;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!Available()) return;
        pointerInside = true;
        pointerAway = false;
        EventSystem.current?.SetSelectedGameObject(gameObject);
        BeginWarmth();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        pointerAway = true;
        if (State != ItemState.Click && State != ItemState.Disabled)
            State = ItemState.Idle;
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!Available()) return;
        keyboardSelected = true;
        pointerAway = false;
        BeginWarmth();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        keyboardSelected = false;
        pointerAway = false;
        if (!pointerInside && State != ItemState.Click && State != ItemState.Disabled)
            State = ItemState.Idle;
    }

    void BeginWarmth()
    {
        if (State == ItemState.Click || State == ItemState.Disabled) return;
        bool wasCold = State == ItemState.Idle;
        State = pointerInside ? ItemState.Hover : ItemState.Selected;
        if (wasCold && theme != null && theme.ember != null && audioSource != null)
            audioSource.PlayOneShot(theme.ember, .25f);
    }

    bool Available() => button != null && button.interactable && owner != null && owner.CanInteract;

    void Update()
    {
        if (glyph == null) return;
        if (State != ItemState.Click)
        {
            bool active = button != null && button.interactable && owner != null &&
                owner.CanInteract && !pointerAway && (pointerInside || keyboardSelected);
            State = button != null && !button.interactable ? ItemState.Disabled :
                active ? (pointerInside ? ItemState.Hover : ItemState.Selected) : ItemState.Idle;
            float target = active ? 1f : 0f;
            glyph.heat = Mathf.MoveTowards(glyph.heat, target,
                Time.unscaledDeltaTime * (active ? 2.8f : 3.8f));
            // Erosion never erases the inscription while it is only highlighted.
            glyph.charAmount = glyph.heat * (.1f + .035f *
                Mathf.Sin(Time.unscaledTime * 4.5f));
            glyph.selected = active;
            if (active)
            {
                ashTimer -= Time.unscaledDeltaTime;
                if (ashTimer <= 0f)
                {
                    ashTimer = .065f;
                    ash?.EmitFromTextEdge(label, 3, new Vector2(12f, -4f), true);
                }
                crackleTimer -= Time.unscaledDeltaTime;
                if (crackleTimer <= 0f)
                {
                    crackleTimer = UnityEngine.Random.Range(1.3f, 2.3f);
                    if (theme != null && theme.ember != null && audioSource != null)
                        audioSource.PlayOneShot(theme.ember, .06f);
                }
            }
        }
        if (emberMaterial != null)
        {
            if (emberMaterial.HasProperty("_OutlineWidth"))
                emberMaterial.SetFloat("_OutlineWidth", glyph.heat * .06f);
            if (emberMaterial.HasProperty("_OutlineColor"))
                emberMaterial.SetColor("_OutlineColor",
                    new Color(1f, .24f + glyph.heat * .18f, .08f,
                        glyph.heat * .68f));
        }
        if (emberEdge != null)
            emberEdge.effectColor = new Color(1f, .29f, .07f,
                glyph.heat * (.22f + .06f *
                Mathf.Sin(Time.unscaledTime * 7f)));
    }

    public void Activate()
    {
        if (!Available() || State == ItemState.Click || !owner.BeginItemAction(this)) return;
        State = ItemState.Click;
        if (theme != null && theme.ember != null && audioSource != null)
            audioSource.PlayOneShot(theme.ember, .7f);
        clickRoutine = StartCoroutine(ClickRoutine());
    }

    IEnumerator ClickRoutine()
    {
        float time = 0f;
        while (time < .34f)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / .34f);
            glyph.heat = Mathf.Lerp(1f, .25f, t);
            glyph.charAmount = t;
            glyph.clickWave = t * 1.15f;
            glyph.SetReveal(1f - Mathf.SmoothStep(0f, 1f, t));
            if (Time.frameCount % 2 == 0)
                ash?.EmitFromTextEdge(label, 3,
                    new Vector2(80f * t, 15f), true);
            yield return null;
        }
        FinishClick();
    }

    public void FinishImmediately()
    {
        if (State != ItemState.Click) return;
        if (clickRoutine != null) StopCoroutine(clickRoutine);
        FinishClick();
    }

    void FinishClick()
    {
        clickRoutine = null;
        if (glyph != null)
        {
            glyph.SetReveal(0f);
            glyph.charAmount = 1f;
            glyph.clickWave = 1.15f;
        }
        ash?.EmitFromTextEdge(label, 12, new Vector2(135f, 20f), true);
        owner?.EndItemAction(this);
        State = ItemState.Idle;
        action?.Invoke();
        // Settings actions may keep the same page; restore the selected label then.
        if (glyph != null && gameObject.activeInHierarchy)
        {
            glyph.SetReveal(1f);
            glyph.charAmount = 0f;
            glyph.clickWave = 0f;
        }
    }

    void OnDestroy()
    {
        if (emberMaterial != null) Destroy(emberMaterial);
    }
}
