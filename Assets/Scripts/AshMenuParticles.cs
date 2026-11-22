using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One UI mesh for every ash mote, including motes born on dissolving glyphs.
public sealed class AshMenuParticles : Graphic
{
    struct Mote
    {
        public Vector2 position, velocity;
        public float age, life, size;
        public Color color;
        public bool falling;
    }
    readonly List<Mote> motes = new List<Mote>(256);
    readonly Vector3[] corners = new Vector3[4];
    Texture2D noise;
    const int MaxMotes = 320;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }
    public void SetNoise(Texture2D value) { noise = value; }
    public void Clear() { motes.Clear(); SetVerticesDirty(); }

    public void EmitFromRect(RectTransform source, int count, float threshold,
        Vector2 drift, bool edgeOnly = true)
    {
        if (source == null) return;
        source.GetWorldCorners(corners);
        for (int i = 0; i < count && motes.Count < MaxMotes; i++)
        {
            Vector2 uv = new Vector2(Random.value, Random.value);
            if (edgeOnly)
            {
                int side = i % 4;
                if (side == 0) uv.x = Random.Range(0f, .08f);
                if (side == 1) uv.x = Random.Range(.92f, 1f);
                if (side == 2) uv.y = Random.Range(0f, .08f);
                if (side == 3) uv.y = Random.Range(.92f, 1f);
            }
            if (noise != null)
            {
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    if (Mathf.Abs(noise.GetPixelBilinear(uv.x, uv.y).r - threshold) < .16f)
                        break;
                    uv = new Vector2(Random.value, Random.value);
                }
            }
            Vector3 world = Vector3.Lerp(Vector3.Lerp(corners[0], corners[3], uv.y),
                Vector3.Lerp(corners[1], corners[2], uv.y), uv.x);
            Add(rectTransform.InverseTransformPoint(world), drift);
        }
    }

    public void EmitFromText(TextMeshProUGUI text, int perGlyph, Vector2 drift)
    {
        if (text == null) return;
        text.ForceMeshUpdate();
        var info = text.textInfo;
        var atlas = text.font != null ? text.font.atlasTexture : null;
        for (int i = 0; i < info.characterCount && motes.Count < MaxMotes; i++)
        {
            var glyph = info.characterInfo[i];
            if (!glyph.isVisible) continue;
            for (int j = 0; j < perGlyph && motes.Count < MaxMotes; j++)
            {
                Vector3 point = GlyphEdge(glyph, atlas);
                Add(rectTransform.InverseTransformPoint(
                    text.transform.TransformPoint(point)), drift);
            }
        }
    }

    public void EmitFromTextEdge(TextMeshProUGUI text, int count,
        Vector2 drift, bool falling)
    {
        if (text == null) return;
        text.ForceMeshUpdate();
        var info = text.textInfo;
        var atlas = text.font != null ? text.font.atlasTexture : null;
        if (info.characterCount == 0) return;
        for (int i = 0; i < count && motes.Count < MaxMotes; i++)
        {
            int index = Random.Range(0, info.characterCount);
            var glyph = info.characterInfo[index];
            if (!glyph.isVisible) continue;
            Vector3 point = GlyphEdge(glyph, atlas);
            Add(rectTransform.InverseTransformPoint(
                text.transform.TransformPoint(point)), drift, falling);
        }
    }

    static Vector3 GlyphEdge(TMP_CharacterInfo glyph, Texture2D atlas)
    {
        // SDF alpha identifies the visible contour; fall back to the glyph quad
        // when a platform keeps its font atlas GPU-only.
        if (atlas != null && atlas.isReadable)
        {
            for (int attempt = 0; attempt < 18; attempt++)
            {
                float x = Random.value, y = Random.value;
                Vector2 uv = Vector2.Lerp(glyph.vertex_BL.uv,
                    glyph.vertex_TR.uv, x);
                uv.y = Mathf.Lerp(glyph.vertex_BL.uv.y,
                    glyph.vertex_TR.uv.y, y);
                float alpha = atlas.GetPixelBilinear(uv.x, uv.y).a;
                if (alpha > .44f && alpha < .65f)
                    return new Vector3(Mathf.Lerp(glyph.bottomLeft.x,
                        glyph.topRight.x, x),
                        Mathf.Lerp(glyph.bottomLeft.y,
                        glyph.topRight.y, y), 0f);
            }
        }
        float t = Random.value;
        switch (Random.Range(0, 4))
        {
            case 0: return Vector3.Lerp(glyph.bottomLeft, glyph.bottomRight, t);
            case 1: return Vector3.Lerp(glyph.bottomRight, glyph.topRight, t);
            case 2: return Vector3.Lerp(glyph.topLeft, glyph.topRight, t);
            default: return Vector3.Lerp(glyph.bottomLeft, glyph.topLeft, t);
        }
    }

    void Add(Vector2 position, Vector2 drift, bool falling = false)
    {
        motes.Add(new Mote {
            position = position,
            velocity = drift + new Vector2(Random.Range(-45f, 45f),
                falling ? Random.Range(-40f, -5f) : Random.Range(-30f, 85f)),
            age = 0f,
            life = Random.Range(.23f, .7f),
            size = Random.Range(2f, 6f),
            color = Random.value < .26f ? new Color(1f, .48f, .18f, 1f) :
                new Color(.67f, .61f, .56f, .82f),
            falling = falling
        });
        SetVerticesDirty();
    }

    void Update()
    {
        if (motes.Count == 0) return;
        float dt = Time.unscaledDeltaTime;
        for (int i = motes.Count - 1; i >= 0; i--)
        {
            var mote = motes[i];
            mote.age += dt;
            if (mote.age >= mote.life) { motes.RemoveAt(i); continue; }
            mote.position += mote.velocity * dt;
            mote.velocity = Vector2.Lerp(mote.velocity,
                new Vector2(mote.velocity.x * .65f,
                    mote.falling ? -95f : 65f), dt * 2f);
            motes[i] = mote;
        }
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        for (int i = 0; i < motes.Count; i++)
        {
            var mote = motes[i];
            float fade = 1f - mote.age / mote.life;
            Color color = mote.color;
            color.a *= fade * fade;
            float half = mote.size * (.4f + fade * .6f) * .5f;
            int index = mesh.currentVertCount;
            mesh.AddVert(mote.position + new Vector2(-half, -half), color, Vector2.zero);
            mesh.AddVert(mote.position + new Vector2(-half, half), color, Vector2.up);
            mesh.AddVert(mote.position + new Vector2(half, half), color, Vector2.one);
            mesh.AddVert(mote.position + new Vector2(half, -half), color, Vector2.right);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
