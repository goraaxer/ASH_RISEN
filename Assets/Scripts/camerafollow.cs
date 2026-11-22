using UnityEngine;

public class camerafollow : MonoBehaviour
{
    public Transform target;
    public Vector2 offset = new Vector2(0f, 1.5f);
    [Min(0f)] public float horizontalSmoothTime = 0.16f;
    [Min(0f)] public float verticalSmoothTime = 0.22f;
    Vector3 followVelocity;

    public void SnapNow()
    {
        followVelocity = Vector3.zero;
        if (target == null) return;
        transform.position = new Vector3(target.position.x + offset.x,
            target.position.y + offset.y, -10f);
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        var desired = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            -10f
        );
        if (Time.deltaTime <= 0f) return;
        var current = transform.position;
        var position = new Vector3(
            Mathf.SmoothDamp(current.x, desired.x, ref followVelocity.x, horizontalSmoothTime),
            Mathf.SmoothDamp(current.y, desired.y, ref followVelocity.y, verticalSmoothTime),
            -10f
        );
        transform.position = position;
    }
}
