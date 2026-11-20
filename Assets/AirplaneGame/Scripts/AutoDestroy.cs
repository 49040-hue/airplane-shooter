using UnityEngine;

/// <summary>Destroys the object after a short time (used by explosions).</summary>
public class AutoDestroy : MonoBehaviour
{
    public float lifetime = 0.55f;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
