using UnityEngine;

// Placed only inside an open street chasm, below its visible rim.
public sealed class VillageFallPit : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        var respawn = other.GetComponent<PlayerRespawn>();
        if (respawn != null) respawn.Respawn();
    }
}
