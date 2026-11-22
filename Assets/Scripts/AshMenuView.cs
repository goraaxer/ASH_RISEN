using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Shared animated UI for the pause screen and the title scene.
public sealed class AshMenuView : MonoBehaviour
{
    struct Entry
    {
        public Button button;
        public TextMeshProUGUI text;
        public AshGlyphDissolve glyph;
        public AshMenuItem item;
    }

    AshMenuTheme theme;
    PauseMenu pause;
    bool titleMode;
    bool visible;
    bool closing;
    int page;
    int pendingPage = -1;
    AshMenuItem activeItem;
    float portraitTime;
    float statusUntil;
    Action closeAction;
    Coroutine transition;
    Canvas canvas;
    CanvasScaler canvasScaler;
    RectTransform canvasRect, pageRoot;
    CanvasGroup canvasGroup;
    Image shade, cracks, frame, portrait;
    RawImage city, worldCapture;
    RenderTexture capturedWorld;
    int lastScreenWidth, lastScreenHeight;
    TextMeshProUGUI heading, status;
    AshGlyphDissolve headingGlyph;
    AshMenuParticles ash;
    AudioSource audioSource;
    ParticleSystem ambient;
    readonly List<Entry> entries = new List<Entry>();
    readonly List<Image> pageImages = new List<Image>();
    readonly List<AshGlyphDissolve> pageGlyphs = new List<AshGlyphDissolve>();
    readonly List<Material> materials = new List<Material>();

    public bool IsTransitioning => transition != null || activeItem != null;
    public bool CanInteract => visible && !closing && transition == null && activeItem == null;

    public bool BeginItemAction(AshMenuItem item)
    {
        if (!CanInteract) return false;
        activeItem = item;
        return true;
    }

    public void EndItemAction(AshMenuItem item)
    {
        if (activeItem == item) activeItem = null;
    }

    public void Configure(PauseMenu owner, bool isTitle)
    {
        pause = owner;
        titleMode = isTitle;
        theme = Resources.Load<AshMenuTheme>("AshMenuTheme");
        Build();
        if (titleMode) Open();
        else canvas.gameObject.SetActive(false);
    }

    static RectTransform Box(Transform parent, string name,
        Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    Image Picture(Transform parent, string name, Vector2 position,
        Vector2 size, Color color, Sprite sprite = null, bool dissolve = true)
    {
        var rect = Box(parent, name, position, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        if (dissolve)
        {
            var shader = Shader.Find("Ashrisen/UI Ash Dissolve");
            if (shader != null)
            {
                var material = new Material(shader);
                material.name = "Ash dissolve — " + name;
                material.SetTexture("_NoiseTex", theme != null ? theme.noise : null);
                material.SetFloat("_NoiseScale", name == "World charring" ? 4f : 1f);
                material.SetFloat("_Dissolve", 0f);
                image.material = material;
                materials.Add(material);
            }
        }
        return image;
    }

    TextMeshProUGUI Text(Transform parent, string name, string value,
        Vector2 position, Vector2 size, int fontSize,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left,
        bool animate = true)
    {
        var rect = Box(parent, name, position, size);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = theme != null && theme.font != null ? theme.font :
            TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(.83f, .77f, .7f);
        text.raycastTarget = false;
        if (animate) rect.gameObject.AddComponent<AshGlyphDissolve>();
        return text;
    }

    void Build()
    {
        var root = new GameObject("Ash menu canvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
            typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        canvasRect = root.GetComponent<RectTransform>();
        Stretch(canvasRect);
        canvasGroup = root.GetComponent<CanvasGroup>();
        canvasScaler = root.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        FitScreen();

        if (EventSystem.current == null)
            new GameObject("Ash menu event system", typeof(EventSystem),
                typeof(StandaloneInputModule)).transform.SetParent(transform);

        if (titleMode && theme != null && theme.city != null)
        {
            var cityRect = Box(canvasRect, "Gothic city at dusk",
                Vector2.zero, new Vector2(1920, 1080));
            city = cityRect.gameObject.AddComponent<RawImage>();
            city.texture = theme.city.texture;
            city.raycastTarget = false;
            Stretch(city.rectTransform);
            FitScreen();
        }
        if (!titleMode)
        {
            var snapshotRect = Box(canvasRect, "World before charring",
                Vector2.zero, new Vector2(1920, 1080));
            worldCapture = snapshotRect.gameObject.AddComponent<RawImage>();
            worldCapture.raycastTarget = false;
            Stretch(snapshotRect);
            var shader = Shader.Find("Ashrisen/UI Ash Dissolve");
            if (shader != null)
            {
                var material = new Material(shader);
                material.name = "Captured world burning to ash";
                material.SetTexture("_NoiseTex", theme != null ? theme.noise : null);
                material.SetFloat("_NoiseScale", 4f);
                worldCapture.material = material;
                materials.Add(material);
            }
        }

        shade = Picture(canvasRect, "World charring", Vector2.zero,
            new Vector2(1920, 1080), new Color(.07f, .035f, .045f,
                titleMode ? .62f : .87f));
        Stretch(shade.rectTransform);
        cracks = Picture(canvasRect, "Edge embers and cracks", Vector2.zero,
            new Vector2(1920, 1080), new Color(1f, .43f, .18f, .28f),
            theme != null ? theme.cracks : null);
        Stretch(cracks.rectTransform);
        frame = Picture(canvasRect, "Charred menu stone", new Vector2(440, 0),
            new Vector2(960, 960), Color.white,
            theme != null ? theme.panel : null);
        Picture(canvasRect, "Amber menu seam", new Vector2(-45, 0),
            new Vector2(3, 760), new Color(.64f, .32f, .17f, .67f), null, false);

        var portraitBack = Picture(canvasRect, "Portrait niche", new Vector2(-505, -20),
            new Vector2(565, 790), new Color(.11f, .08f, .09f, .5f));
        portraitBack.material.SetFloat("_NoiseScale", 2f);
        Sprite portraitSprite = titleMode && theme != null && theme.emblem != null ?
            theme.emblem : theme != null && theme.heroineIdle != null && theme.heroineIdle.Length > 0 ?
                theme.heroineIdle[0] : null;
        portrait = Picture(canvasRect, titleMode ? "Ashrisen game emblem" : "Original heroine in ash",
            new Vector2(-500, -10), new Vector2(530, 690),
            titleMode ? Color.white : new Color(.88f, .84f, .83f, .96f), portraitSprite);
        portrait.preserveAspect = true;

        Text(canvasRect, "World inscription", GameText.Get("menu.inscription"),
            new Vector2(-490, -410), new Vector2(620, 50), 23,
            TextAlignmentOptions.Center);
        heading = Text(canvasRect, "Menu heading", "ASHRISEN",
            new Vector2(440, 394), new Vector2(840, 112), 77,
            TextAlignmentOptions.Center);
        heading.fontStyle = FontStyles.Bold;
        headingGlyph = heading.GetComponent<AshGlyphDissolve>();
        Text(canvasRect, "Menu subtitle", GameText.Get("menu.subtitle"),
            new Vector2(440, 318), new Vector2(820, 48), 25,
            TextAlignmentOptions.Center);
        pageRoot = Box(canvasRect, "Active menu section",
            new Vector2(440, -80), new Vector2(870, 690));
        status = Text(canvasRect, "Menu status", "",
            new Vector2(440, -455), new Vector2(840, 52), 23,
            TextAlignmentOptions.Center, false);
        status.color = new Color(.98f, .59f, .33f);

        var ashRect = Box(canvasRect, "Ash from dissolving elements",
            Vector2.zero, new Vector2(1920, 1080));
        Stretch(ashRect);
        ash = ashRect.gameObject.AddComponent<AshMenuParticles>();
        ash.SetNoise(theme != null ? theme.noise : null);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.ignoreListenerPause = true;
        audioSource.volume = .42f;
        BuildAmbientAsh();
        BuildPage(0);
    }

    void BuildAmbientAsh()
    {
        var camera = Camera.main;
        if (camera == null) return;
        var go = new GameObject("Menu ambient ash particle system",
            typeof(ParticleSystem));
        go.transform.SetParent(camera.transform, false);
        go.transform.localPosition = new Vector3(0, 0, 4);
        ambient = go.GetComponent<ParticleSystem>();
        var mainModule = ambient.main;
        mainModule.loop = true;
        mainModule.startLifetime = 4f;
        mainModule.startSpeed = .23f;
        mainModule.startSize = .045f;
        mainModule.maxParticles = 160;
        mainModule.useUnscaledTime = true;
        mainModule.startColor = new Color(.72f, .52f, .4f, .48f);
        var emission = ambient.emission;
        emission.rateOverTime = 24f;
        var shape = ambient.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(21f, 11f, .1f);
        var velocity = ambient.velocityOverLifetime;
        velocity.enabled = true;
        velocity.y = .06f;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        materials.Add(renderer.material);
        ambient.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void Sound(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }

    void SetDissolve(Image image, float amount)
    {
        if (image != null && image.material != null &&
            image.material.HasProperty("_Dissolve"))
            image.material.SetFloat("_Dissolve", Mathf.Clamp01(amount));
    }

    void SetWhole(float amount)
    {
        amount = Mathf.Clamp01(amount);
        canvasGroup.alpha = amount;
        if (worldCapture != null && worldCapture.material != null &&
            worldCapture.material.HasProperty("_Dissolve"))
            worldCapture.material.SetFloat("_Dissolve", amount * .98f);
        SetDissolve(shade, 1f - amount);
        SetDissolve(cracks, 1f - amount);
        SetDissolve(frame, 1f - amount);
        SetDissolve(portrait, 1f - amount);
        headingGlyph.SetReveal(amount);
        foreach (var glyph in pageGlyphs) glyph.SetReveal(amount);
        foreach (var image in pageImages) SetDissolve(image, 1f - amount);
    }

    public void Open()
    {
        if (visible) return;
        if (!titleMode) CaptureWorld();
        visible = true;
        closing = false;
        foreach (var entry in entries) entry.glyph.selected = false;
        headingGlyph.heat = headingGlyph.charAmount = 0f;
        foreach (var glyph in pageGlyphs)
        {
            glyph.heat = 0f;
            glyph.charAmount = 0f;
            glyph.clickWave = 0f;
        }
        canvas.gameObject.SetActive(true);
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        SetWhole(0f);
        if (ambient != null) ambient.Play();
        Sound(theme != null ? theme.open : null);
        transition = StartCoroutine(OpenRoutine());
    }

    IEnumerator OpenRoutine()
    {
        float time = 0f;
        while (time < .5f)
        {
            time += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(time / .5f);
            SetWhole(Mathf.SmoothStep(0f, 1f, progress));
            if (progress > .17f && progress < .65f && Time.frameCount % 3 == 0)
            {
                ash.EmitFromRect(frame.rectTransform, 4, 1f - progress,
                    new Vector2(95, 45));
                ash.EmitFromText(heading, 1, new Vector2(100, 25));
            }
            yield return null;
        }
        SetWhole(1f);
        transition = null;
        SelectFirst();
    }

    public void Close(Action after)
    {
        if (!visible) { after?.Invoke(); return; }
        if (activeItem != null) activeItem.FinishImmediately();
        if (closing) return;
        if (transition != null) StopCoroutine(transition);
        pendingPage = -1;
        closeAction = after;
        closing = true;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        Sound(theme != null ? theme.close : null);
        ash.EmitFromText(heading, 5, new Vector2(240, 80));
        ash.EmitFromRect(frame.rectTransform, 90, .5f,
            new Vector2(260, 90), false);
        transition = StartCoroutine(CloseRoutine());
    }

    IEnumerator CloseRoutine()
    {
        float time = 0f;
        while (time < .4f)
        {
            time += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(time / .4f);
            float ember = Mathf.Sin(progress * Mathf.PI) * .85f;
            headingGlyph.heat = ember;
            headingGlyph.charAmount = progress;
            foreach (var glyph in pageGlyphs)
            {
                glyph.heat = ember;
                glyph.charAmount = progress;
            }
            SetWhole(1f - Mathf.SmoothStep(0f, 1f, progress));
            yield return null;
        }
        CompleteClose();
    }

    void CompleteClose()
    {
        transition = null;
        visible = false;
        closing = false;
        if (ambient != null)
            ambient.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ash.Clear();
        canvas.gameObject.SetActive(false);
        ReleaseWorld();
        var action = closeAction;
        closeAction = null;
        action?.Invoke();
    }

    public void Skip()
    {
        if (activeItem != null)
        {
            activeItem.FinishImmediately();
            return;
        }
        if (transition == null) return;
        StopCoroutine(transition);
        transition = null;
        if (closing) CompleteClose();
        else
        {
            if (pendingPage >= 0)
            {
                BuildPage(pendingPage);
                pendingPage = -1;
            }
            SetWhole(1f);
            SelectFirst();
        }
    }

    public void ShowPage(int target)
    {
        if (!visible) { page = target; BuildPage(target); return; }
        if (transition != null) Skip();
        if (page == target) return;
        pendingPage = target;
        transition = StartCoroutine(PageRoutine(target));
    }

    IEnumerator PageRoutine(int target)
    {
        Sound(theme != null ? theme.ash : null);
        Vector2 drift = target == 4 ? new Vector2(170, 0) :
            target == 5 ? new Vector2(0, 100) :
            target == 6 ? new Vector2(-120, 55) : new Vector2(135, 55);
        foreach (var entry in entries)
            ash.EmitFromText(entry.text, 3, drift);
        ash.EmitFromRect(frame.rectTransform, 24, .45f, drift);
        if (target == 5)
            ash.EmitFromRect(portrait.rectTransform, 36, .5f,
                new Vector2(-35, 70), false);
        float time = 0f;
        while (time < .13f)
        {
            time += Time.unscaledDeltaTime;
            float value = 1f - Mathf.Clamp01(time / .13f);
            headingGlyph.SetReveal(value);
            headingGlyph.heat = 1f - value;
            headingGlyph.charAmount = 1f - value;
            foreach (var glyph in pageGlyphs)
            {
                glyph.SetReveal(value);
                glyph.heat = 1f - value;
                glyph.charAmount = 1f - value;
            }
            foreach (var image in pageImages) SetDissolve(image, 1f - value);
            yield return null;
        }
        page = target;
        BuildPage(target);
        if (target == 5) SetDissolve(portrait, 1f);
        time = 0f;
        while (time < .19f)
        {
            time += Time.unscaledDeltaTime;
            float value = Mathf.Clamp01(time / .19f);
            headingGlyph.SetReveal(value);
            headingGlyph.heat = 1f - value;
            headingGlyph.charAmount = 1f - value;
            foreach (var glyph in pageGlyphs)
            {
                glyph.SetReveal(value);
                glyph.heat = 1f - value;
                glyph.charAmount = 1f - value;
            }
            foreach (var image in pageImages) SetDissolve(image, 1f - value);
            if (target == 5) SetDissolve(portrait, 1f - value);
            yield return null;
        }
        transition = null;
        pendingPage = -1;
        SelectFirst();
    }

    void BuildPage(int newPage)
    {
        foreach (Transform child in pageRoot) Destroy(child.gameObject);
        entries.Clear();
        pageImages.Clear();
        pageGlyphs.Clear();
        page = newPage;
        heading.text = titleMode && newPage == 0 ? "ASHRISEN" :
            newPage == 0 ? GameText.Get("menu.pause") :
            newPage == 1 ? GameText.Get("menu.settings") :
            newPage == 2 ? GameText.Get("menu.exit_confirm") :
            newPage == 3 ? GameText.Get("menu.inventory") :
            newPage == 4 ? GameText.Get("menu.map") :
            newPage == 5 ? GameText.Get("menu.character") :
            GameText.Get("menu.equipment");
        if (titleMode) BuildTitlePage(newPage);
        else BuildPausePage(newPage);
    }

    void AddEntry(string text, float y, Action action, int fontSize = 52)
    {
        var rect = Box(pageRoot, "Ash action — " + text,
            new Vector2(0, y), new Vector2(745, 76));
        var hit = rect.gameObject.AddComponent<Image>();
        hit.color = new Color(.2f, .1f, .08f, .04f);
        var button = rect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        var label = Text(rect, "Letters forming from embers", text,
            Vector2.zero, new Vector2(730, 74), fontSize,
            TextAlignmentOptions.MidlineLeft);
        var glyph = label.GetComponent<AshGlyphDissolve>();
        var item = rect.gameObject.AddComponent<AshMenuItem>();
        item.Configure(this, label, ash, theme, audioSource, action);
        pageGlyphs.Add(glyph);
        entries.Add(new Entry { button = button, text = label, glyph = glyph, item = item });
    }

    void SectionLine(string text, float y, int size = 27)
    {
        var label = Text(pageRoot, "Section inscription", text,
            new Vector2(0, y), new Vector2(750, 72), size,
            TextAlignmentOptions.Center);
        pageGlyphs.Add(label.GetComponent<AshGlyphDissolve>());
    }

    void BuildTitlePage(int value)
    {
        if (value == 1) { BuildSettings(); return; }
        AddEntry(GameText.Get("menu.new_game"), 230, NewGame);
        AddEntry(GameText.Get("menu.continue"), 145, ContinueGame);
        entries[1].item.SetDisabled(!GameSaveSystem.HasSave);
        AddEntry(GameText.Get("menu.settings"), 60, () => ShowPage(1));
        AddEntry(GameText.Get("menu.quit"), -25, () => Close(QuitApplication));
        SectionLine(GameText.Get("menu.title_tagline"), -255, 22);
    }

    void BuildPausePage(int value)
    {
        if (value == 0)
        {
            AddEntry(GameText.Get("menu.resume"), 245, () => pause.Resume(), 64);
            AddEntry(GameText.Get("menu.character"), 155, () => pause.OpenPage(5), 56);
            AddEntry(GameText.Get("menu.inventory"), 65, () => pause.OpenPage(3), 54);
            AddEntry(GameText.Get("menu.equipment"), -25, () => pause.OpenPage(6), 54);
            AddEntry(GameText.Get("menu.map"), -115, () => pause.OpenPage(4), 58);
            AddEntry(GameText.Get("menu.settings"), -205, () => pause.OpenPage(1), 52);
            AddEntry(GameText.Get("menu.return_main"), -295, () => pause.OpenPage(2), 42);
            return;
        }
        if (value == 1) { BuildSettings(); return; }
        if (value == 2)
        {
            SectionLine(GameText.Get("menu.unsaved_warning"), 160);
            AddEntry(GameText.Get("menu.save_and_return"), 30,
                () => pause.ReturnToMainMenu(true));
            AddEntry(GameText.Get("menu.return_without_save"), -55,
                () => pause.ReturnToMainMenu(false));
            AddEntry(GameText.Get("menu.back"), -160, () => pause.OpenPage(0));
            return;
        }
        if (value == 3)
        {
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 5; col++)
                pageImages.Add(Picture(pageRoot, "Inventory ash cell",
                    new Vector2(-280 + col * 140, 170 - row * 130),
                    new Vector2(105, 105), new Color(.29f, .23f, .21f, .68f)));
            SectionLine(GameText.Get("menu.empty_inventory"), -220, 25);
        }
        else if (value == 4) BuildMap();
        else if (value == 5)
        {
            var hp = FindFirstObjectByType<AshHealth>();
            SectionLine(GameText.Get("menu.wanderer"), 225, 31);
            SectionLine(hp == null ? GameText.Get("menu.health_missing") :
                GameText.Format("menu.health", hp.CurrentHealth, hp.MaxHealth), 110);
            var hero = FindFirstObjectByType<playercontroler>();
            SectionLine(hero == null ? "" :
                GameText.Format("menu.stats", hero.speed, hero.jumpForce), 30, 24);
        }
        else if (value == 6)
        {
            for (int slot = 0; slot < 4; slot++)
            {
                pageImages.Add(Picture(pageRoot, "Equipment ember slot",
                    new Vector2(-245 + slot * 160, 70), new Vector2(120, 145),
                    new Color(.3f, .19f, .15f, .7f)));
            }
            SectionLine(GameText.Get("menu.empty_equipment"), -130, 25);
        }
        AddEntry(GameText.Get("menu.back"), -275, () => pause.OpenPage(0));
    }

    void BuildMap()
    {
        var hero = FindFirstObjectByType<playercontroler>();
        float reached = hero == null ? 0f : hero.transform.position.x;
        string[] names = { GameText.Get("location.graveyard"),
            GameText.Get("location.outskirts"), GameText.Get("location.lower_village"),
            GameText.Get("location.square"), GameText.Get("location.pier"),
            GameText.Get("location.church") };
        float[] starts = { 0, 79, 129, 181, 217, 310 };
        for (int i = 0; i < names.Length; i++)
        {
            float x = -320 + i * 128;
            if (i < names.Length - 1)
                pageImages.Add(Picture(pageRoot, "Ash route line",
                    new Vector2(x + 64, 45), new Vector2(110, 4),
                    new Color(.72f, .36f, .2f, reached >= starts[i] ? .82f : .25f)));
            pageImages.Add(Picture(pageRoot, "Revealed location",
                new Vector2(x, 45), new Vector2(20, 20),
                reached >= starts[i] ? new Color(.95f, .55f, .25f) :
                    new Color(.35f, .33f, .32f)));
            var label = Text(pageRoot, "Location name", names[i],
                new Vector2(x, -20), new Vector2(130, 75), 18,
                TextAlignmentOptions.Center);
            pageGlyphs.Add(label.GetComponent<AshGlyphDissolve>());
        }
        SectionLine(GameText.Get("menu.map_hint"), -155, 24);
    }

    void BuildSettings()
    {
        SectionLine(GameText.Format("menu.volume", Mathf.RoundToInt(AudioListener.volume * 100)), 225);
        AddEntry(GameText.Get("menu.quieter"), 125, () => { SetVolume(AudioListener.volume - .1f); });
        AddEntry(GameText.Get("menu.louder"), 55, () => { SetVolume(AudioListener.volume + .1f); });
        AddEntry(GameText.Format("menu.fullscreen", Screen.fullScreen ?
            GameText.Get("menu.yes") : GameText.Get("menu.no")), -15,
            () => { SetFullscreen(!Screen.fullScreen); BuildPage(1); });
        AddEntry(GameText.Format("menu.quality", QualitySettings.names[QualitySettings.GetQualityLevel()]),
            -85, () => { SetQuality((QualitySettings.GetQualityLevel() + 1) %
                QualitySettings.names.Length); BuildPage(1); });
        AddEntry(GameText.Get("menu.back"), -245, () => {
            PlayerPrefs.Save();
            if (titleMode) ShowPage(0); else pause.OpenPage(0);
        });
    }

    void SetVolume(float value)
    {
        if (pause != null) pause.SetVolume(value);
        else { AudioListener.volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat("settings.volume", AudioListener.volume); }
        BuildPage(1);
    }
    void SetFullscreen(bool value)
    {
        if (pause != null) pause.SetFullscreen(value);
        else { Screen.fullScreen = value;
            PlayerPrefs.SetInt("settings.fullscreen", value ? 1 : 0); }
    }
    void SetQuality(int value)
    {
        if (pause != null) pause.SetQuality(value);
        else { QualitySettings.SetQualityLevel(value);
            PlayerPrefs.SetInt("settings.quality", value); }
    }

    void SelectFirst()
    {
        EventSystem.current?.SetSelectedGameObject(null);
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].button.interactable)
            {
                EventSystem.current?.SetSelectedGameObject(entries[i].button.gameObject);
                break;
            }
    }
    void Update()
    {
        if (lastScreenWidth != Screen.width || lastScreenHeight != Screen.height)
            FitScreen();
        if (!visible) return;
        portraitTime += Time.unscaledDeltaTime;
        if (portrait != null)
        {
            if (!titleMode && theme != null && theme.heroineIdle != null && theme.heroineIdle.Length > 0)
                portrait.sprite = theme.heroineIdle[
                    Mathf.FloorToInt(portraitTime * 7f) % theme.heroineIdle.Length];
            portrait.rectTransform.localScale = Vector3.one *
                (1f + Mathf.Sin(portraitTime * 1.7f) * .0015f);
        }
        if (statusUntil > 0f && Time.unscaledTime > statusUntil)
        { status.text = ""; statusUntil = 0f; }
    }

    void FitScreen()
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        if (canvasScaler != null && Screen.height > 0)
            canvasScaler.matchWidthOrHeight =
                Screen.width / (float)Screen.height < 1920f / 1080f ? 0f : 1f;
        if (city == null || city.texture == null || Screen.height == 0) return;
        float sourceAspect = city.texture.width / (float)city.texture.height;
        float screenAspect = Screen.width / (float)Screen.height;
        if (screenAspect < sourceAspect)
        {
            float width = screenAspect / sourceAspect;
            city.uvRect = new Rect((1f - width) * .5f, 0f, width, 1f);
        }
        else
        {
            float height = sourceAspect / screenAspect;
            city.uvRect = new Rect(0f, (1f - height) * .5f, 1f, height);
        }
    }

    public void SetStatus(string message)
    {
        status.text = message ?? "";
        statusUntil = Time.unscaledTime + 3f;
    }
    void NewGame() => Close(() => {
        GameSaveSystem.ClearProgress();
        SceneManager.LoadScene("AshenVillage");
    });
    void ContinueGame()
    {
        if (!GameSaveSystem.HasSave) { SetStatus(GameText.Get("menu.no_save")); return; }
        Close(() => {
            if (!GameSaveSystem.Continue("AshenVillage", out var error))
                SetStatus(error);
        });
    }
    void QuitApplication()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
    void OnDestroy()
    {
        ReleaseWorld();
        if (ambient != null) Destroy(ambient.gameObject);
        foreach (var material in materials) if (material != null) Destroy(material);
    }

    void CaptureWorld()
    {
        ReleaseWorld();
        var camera = Camera.main;
        if (camera == null || worldCapture == null) return;
        capturedWorld = RenderTexture.GetTemporary(Mathf.Max(1, Screen.width),
            Mathf.Max(1, Screen.height), 0, RenderTextureFormat.ARGB32);
        capturedWorld.name = "Paused world ash snapshot";
        var previous = camera.targetTexture;
        try
        {
            camera.targetTexture = capturedWorld;
            camera.Render();
            worldCapture.texture = capturedWorld;
        }
        finally { camera.targetTexture = previous; }
    }

    void ReleaseWorld()
    {
        if (worldCapture != null) worldCapture.texture = null;
        if (capturedWorld == null) return;
        RenderTexture.ReleaseTemporary(capturedWorld);
        capturedWorld = null;
    }
}
