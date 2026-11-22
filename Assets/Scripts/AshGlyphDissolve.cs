using TMPro;
using UnityEngine;

// Dissolves the actual TMP glyph quads; no duplicate text or fade-only overlay.
[RequireComponent(typeof(TextMeshProUGUI))]
public sealed class AshGlyphDissolve : MonoBehaviour
{
    public float reveal = 1f;
    public bool selected;
    public float heat;
    public float charAmount;
    public float clickWave;
    public bool disabled;
    TextMeshProUGUI label;
    float pulse;
    float lastReveal = -1f;
    float lastPulse = -1f;
    float lastHeat = -1f;
    float lastChar = -1f;
    float lastWave = -1f;
    bool lastDisabled;
    bool lastSelected;

    void Awake() { label = GetComponent<TextMeshProUGUI>(); }
    public void Ignite() { pulse = 1f; }
    public void SetReveal(float amount) { reveal = Mathf.Clamp01(amount); }

    void LateUpdate()
    {
        if (pulse > 0f) pulse = Mathf.Max(0f, pulse - Time.unscaledDeltaTime / .21f);
        if (Mathf.Abs(lastReveal - reveal) < .001f &&
            Mathf.Abs(lastPulse - pulse) < .001f &&
            Mathf.Abs(lastHeat - heat) < .001f &&
            Mathf.Abs(lastChar - charAmount) < .001f &&
            Mathf.Abs(lastWave - clickWave) < .001f &&
            lastSelected == selected && lastDisabled == disabled &&
            heat < .01f) return;
        lastReveal = reveal;
        lastPulse = pulse;
        lastHeat = heat;
        lastChar = charAmount;
        lastWave = clickWave;
        lastSelected = selected;
        lastDisabled = disabled;
        label.ForceMeshUpdate();
        var info = label.textInfo;
        float burn = pulse > 0f ? Mathf.Sin((1f - pulse) * Mathf.PI) : 0f;
        for (int i = 0; i < info.characterCount; i++)
        {
            var character = info.characterInfo[i];
            if (!character.isVisible) continue;
            int materialIndex = character.materialReferenceIndex;
            int vertexIndex = character.vertexIndex;
            var colors = info.meshInfo[materialIndex].colors32;
            float noise = Mathf.Repeat(Mathf.Sin(i * 73.17f + 19.61f) * 43758.545f, 1f);
            float flicker = .65f + .35f *
                Mathf.Sin(Time.unscaledTime * 10f + i * 2.13f);
            float centerDistance = info.characterCount < 2 ? 0f :
                Mathf.Abs(i - (info.characterCount - 1) * .5f) /
                ((info.characterCount - 1) * .5f);
            float wave = clickWave > centerDistance ?
                Mathf.Clamp01((clickWave - centerDistance) * 5f) : 0f;
            Color baseColor = Color.Lerp(new Color(.78f, .75f, .72f),
                new Color(.98f, .72f, .43f), heat);
            if (disabled) baseColor *= .48f;
            Color hot = new Color(1f, .38f, .12f);
            for (int j = 0; j < 4; j++)
            {
                float corner = Mathf.Repeat(Mathf.Sin(i * 31.7f + j * 51.3f)
                    * 19247.73f, 1f);
                float ash = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01((reveal - noise * .54f - corner * .23f) * 4.1f));
                float erosion = charAmount * (.22f + noise * .48f) +
                    wave * charAmount * .7f;
                ash *= 1f - Mathf.Clamp01(erosion + burn * (.35f + noise * .48f));
                float ember = Mathf.Clamp01(heat * flicker *
                    (.22f + corner * .38f) + wave * .8f + burn * .85f);
                Color mixed = Color.Lerp(baseColor, hot,
                    Mathf.Clamp01(ember + (1f - ash) * .24f));
                colors[vertexIndex + j] = (Color32)new Color(mixed.r,
                    mixed.g, mixed.b, Mathf.Clamp01(ash));
            }
        }
        label.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }
}
