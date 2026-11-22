using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class VillageDistrictBackdrop : MonoBehaviour
{
    public float districtCenter;
    public float parallax = .07f;
    public float baseAlpha = .96f;
    SpriteRenderer sprite;
    Vector3 origin;
    Camera sceneCamera;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        sceneCamera = Camera.main;
        origin = transform.position;
    }

    void LateUpdate()
    {
        if (sceneCamera == null) return;
        float distance = sceneCamera.transform.position.x - districtCenter;
        transform.position = origin + new Vector3(distance * parallax,
            (sceneCamera.transform.position.y - 4.7f) * .85f, 0f);
        // The material feathers each painted layer at fixed world-space edges.
        // Camera-driven alpha fades used to reveal a new district as a whole
        // and made the scenery look like a scene replacement.
        var color = sprite.color;
        color.a = baseAlpha;
        sprite.color = color;
    }
}
