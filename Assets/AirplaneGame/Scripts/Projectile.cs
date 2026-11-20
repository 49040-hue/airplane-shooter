using UnityEngine;

/// <summary>Bullets and missiles (player and enemy). Missiles can home in on enemies.</summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    public bool fromPlayer = true;
    public int damage = 1;
    public float speed = 14f;
    public bool homing = false;
    public float turnRateDegPerSec = 200f;
    public GameObject hitEffect;
    public float hitEffectScale = 0.5f;

    Rigidbody2D rb;
    Vector2 direction = Vector2.up;
    bool used;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Launch(Vector2 dir)
    {
        direction = dir.normalized;
        transform.rotation = Quaternion.FromToRotation(Vector3.up, new Vector3(direction.x, direction.y, 0f));
        rb.linearVelocity = direction * speed;
    }

    void FixedUpdate()
    {
        if (homing && fromPlayer)
        {
            Enemy target = FindTarget();
            if (target != null)
            {
                Vector2 desired = ((Vector2)target.transform.position - rb.position).normalized;
                float maxRad = turnRateDegPerSec * Mathf.Deg2Rad * Time.fixedDeltaTime;
                Vector3 newDir = Vector3.RotateTowards(new Vector3(direction.x, direction.y, 0f),
                                                       new Vector3(desired.x, desired.y, 0f), maxRad, 0f);
                Launch(new Vector2(newDir.x, newDir.y));
            }
        }

        if (GameBounds.IsOutside(rb.position, 2f)) Destroy(gameObject);
    }

    Enemy FindTarget()
    {
        Enemy best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < Enemy.All.Count; i++)
        {
            Enemy e = Enemy.All[i];
            if (e == null) continue;
            if (e.transform.position.y > GameBounds.HalfHeight) continue;   // not on screen yet
            float d = ((Vector2)e.transform.position - rb.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = e; }
        }
        return best;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (used) return;

        if (fromPlayer)
        {
            Enemy e = other.GetComponent<Enemy>();
            if (e == null) return;
            e.TakeDamage(damage);
        }
        else
        {
            PlayerController p = other.GetComponent<PlayerController>();
            if (p == null) return;
            p.TakeDamage(damage);
        }

        used = true;
        if (hitEffect != null)
        {
            GameObject fx = Instantiate(hitEffect, transform.position, Quaternion.identity);
            fx.transform.localScale = Vector3.one * hitEffectScale;
        }
        Destroy(gameObject);
    }
}
