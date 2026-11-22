using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class VillageBranchBackdrop : MonoBehaviour
{
    public bool forest;
    public float centerX;
    SpriteRenderer sprite;
    Camera sceneCamera;
    playercontroler player;
    Vector3 origin;
    float alpha;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        sceneCamera = Camera.main;
        player = FindAnyObjectByType<playercontroler>();
        origin = transform.position;
        var color = sprite.color; color.a = 0f; sprite.color = color;
    }

    void LateUpdate()
    {
        if (sceneCamera == null || player == null) return;
        float x = player.transform.position.x;
        float y = player.transform.position.y;
        float enter = forest ? SmoothBand(x, 276f, 284f) *
            SmoothBand(y, 1f, 4f) :
            SmoothBand(x, 214f, 222f) *
            (1f - SmoothBand(y, -3f, 0f));
        float leave = forest ? 1f - SmoothBand(x, 299f, 309f) :
            1f - SmoothBand(x, 245f, 255f);
        alpha = .95f * enter * leave;
        transform.position = origin + new Vector3(
            (sceneCamera.transform.position.x - centerX) * .035f,
            (sceneCamera.transform.position.y - 4.7f) * .85f, 0f);
        var color = sprite.color; color.a = alpha; sprite.color = color;
    }

    static float SmoothBand(float value, float from, float to)
    { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value)); }
}
