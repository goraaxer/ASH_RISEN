using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EdgeCollider2D))]
public sealed class VisiblePlatformCollision : MonoBehaviour
{
    EdgeCollider2D topEdge;
    readonly List<Collider2D> actors = new List<Collider2D>();

    void Awake() { topEdge = GetComponent<EdgeCollider2D>(); }

    void Start()
    {
        var player = FindAnyObjectByType<playercontroler>();
        if (player != null) actors.Add(player.GetComponent<Collider2D>());
        RefreshCollisions();
    }

    void FixedUpdate() { RefreshCollisions(); }

    public void RefreshCollisions()
    {
        if (topEdge == null) return;
        float surfaceY = topEdge.bounds.max.y;
        foreach (var actor in actors)
        {
            if (actor == null || !actor.enabled) continue;
            var body = actor.attachedRigidbody;
            bool fallingOntoTop = body != null &&
                body.linearVelocity.y <= .05f &&
                actor.bounds.min.y >= surfaceY - .08f;
            Physics2D.IgnoreCollision(topEdge, actor, !fallingOntoTop);
        }
    }
}
