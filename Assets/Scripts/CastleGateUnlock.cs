using UnityEngine;

public sealed class CastleGateUnlock : MonoBehaviour
{
    public Collider2D blockingCollider;
    public float liftDistance = 9f;
    public float liftSpeed = 2f;
    Vector3 closedPosition;

    void Awake() { closedPosition = transform.position; }

    void Update()
    {
        bool open = BossArenaProgress.Defeated;
        if (blockingCollider != null) blockingCollider.enabled = !open;
        var target = closedPosition + (open ? Vector3.up * liftDistance : Vector3.zero);
        transform.position = Vector3.MoveTowards(transform.position, target,
            liftSpeed * Time.deltaTime);
    }
}
