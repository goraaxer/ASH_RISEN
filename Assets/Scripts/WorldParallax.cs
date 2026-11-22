using UnityEngine;

public sealed class WorldParallax : MonoBehaviour
{
    public Transform cameraTransform;
    [Range(0f, 1f)] public float horizontalFactor = 0.08f;
    Vector3 origin;
    float cameraOrigin;

    void Start()
    {
        origin = transform.position;
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        if (cameraTransform != null) cameraOrigin = cameraTransform.position.x;
    }

    void LateUpdate()
    {
        if (cameraTransform == null) return;
        transform.position = origin + Vector3.right *
            ((cameraTransform.position.x - cameraOrigin) * horizontalFactor);
    }
}
