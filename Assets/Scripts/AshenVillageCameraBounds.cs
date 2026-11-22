using UnityEngine;
using UnityEngine.Rendering.Universal;

internal static class AshenCameraFraming
{
    public const float OrthographicSize = 7.59375f;
    public const int ReferenceWidth = 384;
    public const int ReferenceHeight = 216;

    public static void Apply(Camera camera)
    {
        if (camera == null) return;
        camera.orthographicSize = OrthographicSize;
        var pixelPerfect = camera.GetComponent<PixelPerfectCamera>();
        if (pixelPerfect == null) return;
        pixelPerfect.assetsPPU = 16;
        pixelPerfect.refResolutionX = ReferenceWidth;
        pixelPerfect.refResolutionY = ReferenceHeight;
    }
}

[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(Camera))]
public sealed class AshenVillageCameraBounds : MonoBehaviour
{
    public float minimumX = 4f;
    public float maximumX = 344f;
    public float minimumY = 4.7f;
    public float maximumY = 12f;

    void OnEnable() { AshenCameraFraming.Apply(GetComponent<Camera>()); }

    void LateUpdate()
    {
        var camera = GetComponent<Camera>();
        float x = Mathf.Clamp(transform.position.x, minimumX, maximumX);
        // Keep the churchward street visible beneath the optional forest roof.
        // Otherwise stepping off that roof looks like a blind fall.
        float localMax = x < 229f ? 6.2f : x < 267f ? 8f :
            x < 310f ? 5.4f : maximumY;
        var follow = GetComponent<camerafollow>();
        float floor = x > 215f && x < 251f && follow != null &&
            follow.target != null && follow.target.position.y < -2f ? .65f : minimumY;
        float y = Mathf.Clamp(transform.position.y, floor, localMax);
        transform.position = new Vector3(x, y, transform.position.z);
    }
}
