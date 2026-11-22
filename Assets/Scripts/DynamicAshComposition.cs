using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Drives the experimental menu as one stage while hit targets remain separated.
public sealed class DynamicAshComposition : MonoBehaviour
{
    sealed class EntryMotion
    {
        public DynamicAshMenuItem item;
        public RectTransform hit;
        public RectTransform label;
        public CanvasGroup visibility;
        public AshMenuShape focus, focusEdge;
        public Vector2 home;
        public float x, xVelocity, scale = 1f, scaleVelocity;
        public float angle, angleVelocity;
        public float emphasis, emphasisVelocity;
    }

    readonly List<EntryMotion> entries = new List<EntryMotion>();
    RectTransform stage, pageRoot, frame, portraitBack, portrait, seam;
    RectTransform heading, subtitle, inscription, status;
    Image cracks;
    RawImage city;
    Rect cityUv;
    Vector2 pageHome;
    Vector2 portraitHome;
    bool titleMode;
    int page;
    int selected = -1;
    float spin, spinVelocity, moveX, moveXVelocity, moveY, moveYVelocity;
    float heroX, heroXVelocity, heroY, heroYVelocity;
    float click, pageShift;
    float open = 1f, close;

    public int SelectedIndex => selected;
    public float StageAngle => spin;

    static RectTransform Find(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child as RectTransform;
            var nested = Find(child, name);
            if (nested != null) return nested;
        }
        return null;
    }

    static void MoveInto(RectTransform child, RectTransform parent)
    {
        if (child == null) return;
        Vector2 position = child.anchoredPosition;
        child.SetParent(parent, false);
        child.anchoredPosition = position;
    }

    public void Configure(RectTransform canvas, RectTransform section,
        Image oldFrame, Image niche, Image heroine, Image oldSeam,
        Image edgeCracks, TextMeshProUGUI title,
        TextMeshProUGUI statusText, RawImage cityImage,
        AshMenuTheme theme, bool isTitle)
    {
        titleMode = isTitle;
        pageRoot = section;
        frame = oldFrame.rectTransform;
        portraitBack = niche.rectTransform;
        portrait = heroine.rectTransform;
        seam = oldSeam.rectTransform;
        heading = title.rectTransform;
        status = statusText.rectTransform;
        subtitle = Find(canvas, "Menu subtitle");
        inscription = Find(canvas, "World inscription");
        cracks = edgeCracks;
        city = cityImage;
        if (city != null) cityUv = city.uvRect;

        var go = new GameObject("Dynamic ash composition", typeof(RectTransform));
        stage = go.GetComponent<RectTransform>();
        stage.SetParent(canvas, false);
        stage.anchorMin = stage.anchorMax = new Vector2(.5f, .5f);
        stage.pivot = new Vector2(.5f, .5f);
        stage.sizeDelta = new Vector2(1920, 1080);
        stage.anchoredPosition = Vector2.zero;
        stage.SetSiblingIndex(edgeCracks.transform.GetSiblingIndex() + 1);

        MoveInto(frame, stage);
        oldFrame.enabled = false;
        MakeShape(stage, "One angled ash field", new[] {
            new Vector2(-1300, -760), new Vector2(280, -760),
            new Vector2(55, 760), new Vector2(-1300, 760)
        }, new[] {
            new Color(.14f, .055f, .065f, .81f),
            new Color(.25f, .07f, .075f, .64f),
            new Color(.26f, .085f, .085f, .56f),
            new Color(.15f, .055f, .065f, .75f)
        });

        MoveInto(portraitBack, stage);
        niche.color = new Color(.08f, .045f, .055f, .1f);
        portraitBack.sizeDelta = new Vector2(650, 855);
        MoveInto(portrait, stage);
        portrait.sizeDelta = titleMode && theme != null && theme.emblem != null ?
            new Vector2(760, 800) : new Vector2(590, 800);
        portraitHome = new Vector2(635, -34);
        portrait.anchoredPosition = portraitHome;
        portraitBack.anchoredPosition = portraitHome;

        MoveInto(seam, stage);
        seam.anchoredPosition = new Vector2(170, 0);
        seam.sizeDelta = new Vector2(5, 1500);
        seam.localRotation = Quaternion.Euler(0, 0, 8.4f);
        oldSeam.color = new Color(.96f, .42f, .18f, .72f);
        MoveInto(inscription, stage);
        if (inscription != null)
        {
            inscription.anchoredPosition = new Vector2(595, -455);
            inscription.sizeDelta = new Vector2(600, 48);
            inscription.gameObject.SetActive(false);
        }
        MoveInto(heading, stage);
        heading.anchoredPosition = new Vector2(-425, 392);
        heading.sizeDelta = new Vector2(925, 130);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.fontSize = 98;
        MoveInto(subtitle, stage);
        if (subtitle != null)
        {
            subtitle.anchoredPosition = new Vector2(-425, 308);
            subtitle.sizeDelta = new Vector2(850, 52);
            subtitle.GetComponent<TextMeshProUGUI>().alignment =
                TextAlignmentOptions.MidlineLeft;
        }
        MoveInto(pageRoot, stage);
        pageHome = new Vector2(-405, -70);
        pageRoot.anchoredPosition = pageHome;
        pageRoot.sizeDelta = new Vector2(890, 720);
        MoveInto(status, stage);
        status.anchoredPosition = new Vector2(-405, -465);
        status.sizeDelta = new Vector2(900, 55);
    }

    static AshMenuShape MakeShape(RectTransform parent, string name,
        Vector2[] points, Color[] colors)
    {
        var go = new GameObject(name, typeof(RectTransform),
            typeof(AshMenuShape));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = parent.sizeDelta;
        var shape = go.GetComponent<AshMenuShape>();
        shape.raycastTarget = false;
        shape.SetGeometry(points, colors);
        return shape;
    }

    public void Rebuild(int section, List<DynamicAshMenuItem> items)
    {
        page = section;
        var titleLabel = heading != null ? heading.GetComponent<TextMeshProUGUI>() : null;
        if (titleLabel != null) titleLabel.fontSize = page == 2 ? 77 : 98;
        entries.Clear();
        selected = -1;
        if (titleMode && page == 0) pageHome = new Vector2(-405, -70);
        else if (page == 5) pageHome = new Vector2(-430, -55);
        else if (page == 3 || page == 6) pageHome = new Vector2(-415, -75);
        else pageHome = new Vector2(-405, -70);

        int count = items != null ? items.Count : 0;
        for (int i = 0; i < count; i++)
        {
            var item = items[i];
            if (item == null) continue;
            var hit = item.GetComponent<RectTransform>();
            var text = item.GetComponentInChildren<TextMeshProUGUI>();
            if (hit == null || text == null) continue;
            var visibility = hit.GetComponent<CanvasGroup>();
            if (visibility == null)
                visibility = hit.gameObject.AddComponent<CanvasGroup>();
            var motion = new EntryMotion { item = item, hit = hit,
                label = text.rectTransform, visibility = visibility };
            float y;
            if (page == 0 && titleMode) y = 228 - i * 116;
            else if (page == 0) y = 278 - i * 94;
            else if (page == 1) y = 155 - i * 88;
            else if (page == 2) y = 65 - i * 110;
            else if (page == 7) y = 20 - i * 170;
            else y = -275;
            float x = page == 0 ? -55f : page == 1 ? -35f : 0f;
            motion.home = new Vector2(x, y);
            hit.anchoredPosition = motion.home;
            hit.sizeDelta = new Vector2(760, 76);
            motion.focus = MakeShape(hit, "Active ember plate", new[] {
                new Vector2(-390, -37), new Vector2(310, -37),
                new Vector2(350, 37), new Vector2(-365, 37)
            }, new[] {
                new Color(.55f, .15f, .13f, 1f),
                new Color(.4f, .1f, .11f, .85f),
                new Color(.55f, .15f, .13f, .85f),
                new Color(.71f, .2f, .13f, 1f)
            });
            motion.focus.transform.SetAsFirstSibling();
            motion.focus.color = new Color(1f, 1f, 1f, 0f);
            motion.focusEdge = MakeShape(hit, "Ember plate edge", new[] {
                new Vector2(-392, -37), new Vector2(-382, -37),
                new Vector2(-358, 37), new Vector2(-368, 37)
            }, new[] {
                new Color(.99f, .43f, .18f, 1f),
                new Color(1f, .62f, .25f, 1f),
                new Color(.99f, .43f, .18f, 1f),
                new Color(.99f, .43f, .18f, 1f)
            });
            motion.focusEdge.transform.SetSiblingIndex(1);
            motion.focusEdge.color = new Color(1f, 1f, 1f, 0f);
            motion.label.anchoredPosition = Vector2.zero;
            motion.label.localScale = Vector3.one;
            motion.label.localRotation = Quaternion.identity;
            if (page == 0)
                text.fontSize = titleMode ?
                    Mathf.Max(46, 72 - i * 5) :
                    Mathf.Max(43, 70 - i * 4);
            entries.Add(motion);
        }
        portraitHome = page == 5 ? new Vector2(590, -5) :
            page == 3 ? new Vector2(760, -100) :
            page == 4 ? new Vector2(790, -160) :
            page == 6 ? new Vector2(645, -28) :
            page == 1 ? new Vector2(730, -85) :
            new Vector2(635, -34);
    }

    public void ResetForOpen()
    {
        open = 0f;
        close = 0f;
        pageShift = 0f;
        click = 0f;
    }
    public void SetOpenProgress(float value) { open = Mathf.Clamp01(value); }
    public void SetCloseProgress(float value) { close = Mathf.Clamp01(value); }
    public void SetPageShift(float value) { pageShift = value; }
    public void ResetPageShift() { pageShift = 0f; }

    static float Spring(float current, ref float velocity,
        float target, float dt, float strength = 180f, float damping = 24f)
    {
        velocity += (target - current) * strength * dt;
        velocity *= Mathf.Exp(-damping * dt);
        return current + velocity * dt;
    }

    void Update()
    {
        if (stage == null) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, .04f);
        if (dt <= 0f) return;
        int active = -1, clicking = -1;
        for (int i = 0; i < entries.Count; i++)
        {
            var item = entries[i].item;
            if (item == null) continue;
            if (item.State == DynamicAshMenuItem.ItemState.Click) clicking = i;
            else if (item.State == DynamicAshMenuItem.ItemState.Hover ||
                     item.State == DynamicAshMenuItem.ItemState.Selected)
                active = i;
        }
        if (clicking >= 0) active = clicking;
        selected = active;
        click = Mathf.MoveTowards(click, clicking >= 0 ? 1f : 0f,
            dt * (clicking >= 0 ? 5f : 2.8f));

        float fraction = active < 0 || entries.Count < 2 ? .5f :
            active / (float)(entries.Count - 1);
        float targetAngle = active < 0 ? 0f : Mathf.Lerp(-.7f, .7f, fraction);
        if (clicking >= 0) targetAngle += (fraction < .5f ? -1f : 1f) * click * .35f;
        spin = Spring(spin, ref spinVelocity, targetAngle, dt, 210f, 24f);
        moveX = Spring(moveX, ref moveXVelocity,
            active < 0 ? 0f : -4f + fraction * 7f, dt);
        moveY = Spring(moveY, ref moveYVelocity,
            active < 0 ? 0f : 3f - fraction * 6f, dt);

        float opening = 1f - Mathf.SmoothStep(0f, 1f, open);
        float closing = Mathf.SmoothStep(0f, 1f, close);
        stage.localRotation = Quaternion.Euler(0, 0,
            spin + closing * (fraction < .5f ? -1.2f : 1.2f));
        stage.anchoredPosition = new Vector2(moveX - 165f * opening +
            115f * closing, moveY - 35f * opening + 55f * closing);
        pageRoot.anchoredPosition = pageHome +
            new Vector2(pageShift, -Mathf.Abs(pageShift) * .2f);

        heroX = Spring(heroX, ref heroXVelocity,
            portraitHome.x + (fraction - .5f) * 8f, dt, 115f, 22f);
        heroY = Spring(heroY, ref heroYVelocity,
            portraitHome.y - (fraction - .5f) * 4f, dt, 115f, 22f);
        portrait.anchoredPosition = new Vector2(heroX + 175f * opening +
            205f * closing, heroY + Mathf.Sin(Time.unscaledTime * 1.4f) * .6f);
        portraitBack.anchoredPosition = portrait.anchoredPosition +
            new Vector2(15f, -3f);
        portrait.localRotation = Quaternion.Euler(0, 0,
            -spin * .15f + Mathf.Sin(Time.unscaledTime * 1.1f) * .08f);
        if (city != null)
        {
            var uv = cityUv;
            uv.x += (fraction - .5f) * .002f +
                Mathf.Sin(Time.unscaledTime * .25f) * .0003f;
            city.uvRect = uv;
        }
        if (cracks != null)
        {
            var color = cracks.color;
            color.a = .1f + .01f * Mathf.Sin(Time.unscaledTime * 2.2f);
            cracks.color = color;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var motion = entries[i];
            if (motion.label == null) continue;
            float arrival = Mathf.Clamp01((open - i * .055f) / .63f);
            arrival = Mathf.SmoothStep(0f, 1f, arrival);
            if (motion.visibility != null)
            {
                motion.visibility.alpha = arrival * (1f - closing);
                motion.visibility.blocksRaycasts = open >= .98f &&
                    close < .02f && Mathf.Abs(pageShift) < 1f &&
                    clicking < 0;
            }
            bool isActive = i == active;
            float targetScale = isActive ? 1.045f : active >= 0 ? .99f : 1f;
            float targetX = isActive ? 9f : 0f;
            if (clicking >= 0 && i != clicking)
                targetX += (i < clicking ? -1f : 1f) * click * 15f;
            motion.scale = Spring(motion.scale, ref motion.scaleVelocity,
                targetScale, dt, 260f, 26f);
            motion.x = Spring(motion.x, ref motion.xVelocity,
                targetX, dt, 240f, 25f);
            motion.angle = Spring(motion.angle, ref motion.angleVelocity,
                isActive ? -.15f : 0f,
                dt, 210f, 24f);
            motion.emphasis = Spring(motion.emphasis,
                ref motion.emphasisVelocity, isActive ? 1f : 0f,
                dt, 250f, 20f);
            float focusAlpha = motion.emphasis * arrival * (1f - closing);
            if (motion.focus != null)
            {
                motion.focus.color = new Color(1f, 1f, 1f,
                    focusAlpha * .75f);
                motion.focus.rectTransform.anchoredPosition =
                    new Vector2(motion.x, 0f);
            }
            if (motion.focusEdge != null)
            {
                motion.focusEdge.color = new Color(1f, 1f, 1f,
                    focusAlpha);
                motion.focusEdge.rectTransform.anchoredPosition =
                    new Vector2(motion.x, 0f);
            }
            motion.label.localScale = Vector3.one * motion.scale;
            motion.label.anchoredPosition = new Vector2(motion.x -
                (1f - arrival) * 84f,
                Mathf.Sin(Time.unscaledTime * 1.1f + i) * .2f);
            motion.label.localRotation = Quaternion.Euler(0, 0, motion.angle);
        }
    }

}

// A crisp, resolution-independent graphic field without overlapping UI panels.
public sealed class AshMenuShape : MaskableGraphic
{
    Vector2[] points;
    Color[] vertexColors;

    public void SetGeometry(Vector2[] vertices, Color[] colors)
    {
        points = vertices;
        vertexColors = colors;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (points == null || vertexColors == null ||
            points.Length != vertexColors.Length || points.Length < 3) return;
        for (int i = 0; i < points.Length; i++)
            mesh.AddVert(points[i], vertexColors[i] * color, Vector2.zero);
        for (int i = 1; i < points.Length - 1; i++)
            mesh.AddTriangle(0, i, i + 1);
    }
}
