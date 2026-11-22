using UnityEngine;

// The arena uses a higher, bounded framing while leaving the intro pan in charge.
[DefaultExecutionOrder(200)]
[RequireComponent(typeof(Camera))]
public sealed class AshenSanctumCamera : MonoBehaviour
{
    public BossArrival arrival;
    public float left = 0f, right = 26f, restingHeight = 5.2f;
    float impulse;
    Vector2 noise;

    public void Shake(float strength) { impulse = Mathf.Max(impulse, strength); }

    void LateUpdate()
    {
        if (arrival != null && arrival.IsPlaying) return;
        var cam = GetComponent<Camera>();
        float halfWidth = cam.orthographicSize * cam.aspect;
        var position = transform.position;
        position.x = Mathf.Clamp(position.x, left + halfWidth, right - halfWidth);
        position.y = restingHeight;
        if (impulse > .005f)
        {
            noise = Random.insideUnitCircle * impulse;
            impulse = Mathf.MoveTowards(impulse, 0, Time.deltaTime * 1.6f);
        }
        else noise = Vector2.zero;
        transform.position = position + new Vector3(noise.x, noise.y, 0);
    }
}
