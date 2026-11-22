using UnityEngine;

// Keeps the camera inside the painted western edge of this test scene.
[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Camera))]
public sealed class AshenGraveyardCameraBounds : MonoBehaviour
{
    public float minimumX = 4f;
    public float maximumX = 80f;
    public float minimumY = 4.7f;
    public float maximumY = 6.2f;
    public float skyMargin = .5f;
    public SpriteRenderer skyCoverage;

    Camera sceneCamera;

    void Awake()
    {
        sceneCamera = GetComponent<Camera>();
        AshenCameraFraming.Apply(sceneCamera);
    }

    void OnEnable()
    {
        if (sceneCamera == null) sceneCamera = GetComponent<Camera>();
        AshenCameraFraming.Apply(sceneCamera);
    }

    void LateUpdate()
    {
        float left = minimumX;
        float right = maximumX;
        if (skyCoverage != null)
        {
            float halfView = sceneCamera.orthographicSize * sceneCamera.aspect;
            left = Mathf.Max(left, skyCoverage.bounds.min.x + halfView + skyMargin);
            right = Mathf.Min(right, skyCoverage.bounds.max.x - halfView - skyMargin);
        }
        // Keep the viewport within the two visible soil rows when following the ground route.
        float floor = Mathf.Max(minimumY, sceneCamera.orthographicSize - 2.9f);
        transform.position = new Vector3(
            Mathf.Clamp(transform.position.x, left, Mathf.Max(left, right)),
            Mathf.Max(floor, Mathf.Min(maximumY, transform.position.y)),
            transform.position.z);
    }
}
