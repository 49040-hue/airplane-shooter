using UnityEngine;

/// <summary>The player's fighter: moves in all directions, fires bullets and homing missiles.</summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 7f;

    [Header("Health")]
    public int maxHealth = 100;
    public float invulnerableSeconds = 1f;

    [Header("Weapons")]
    public GameObject bulletPrefab;
    public GameObject missilePrefab;
    public GameObject explosionPrefab;
    public float fireInterval = 0.18f;
    public float bulletSpacing = 0.28f;
    public float muzzleOffsetY = 0.7f;
    public int startMissiles = 5;
    public int maxMissiles = 8;
    public float missileCooldown = 0.8f;
    public float missileRegenSeconds = 6f;

    public int Health { get; private set; }
    public int MaxHealth { get { return maxHealth; } }
    public int Missiles { get; private set; }

    Rigidbody2D rb;
    SpriteRenderer sr;
    Vector2 moveInput;
    float fireTimer;
    float missileTimer;
    float regenTimer;
    float invulTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        Health = maxHealth;
        Missiles = startMissiles;
    }

    void Update()
    {
        bool playing = GameManager.Instance == null || GameManager.Instance.IsPlaying;
        moveInput = playing ? GameInput.Move : Vector2.zero;

        fireTimer -= Time.deltaTime;
        missileTimer -= Time.deltaTime;

        if (playing)
        {
            if (GameInput.FireHeld && fireTimer <= 0f) { fireTimer = fireInterval; FireBullets(); }
            if (GameInput.MissilePressed && Missiles > 0 && missileTimer <= 0f) { missileTimer = missileCooldown; FireMissile(); }

            if (Missiles < maxMissiles)
            {
                regenTimer += Time.deltaTime;
                if (regenTimer >= missileRegenSeconds) { regenTimer = 0f; Missiles++; }
            }
        }

        // blink while invulnerable
        if (sr != null)
        {
            Color c = sr.color;
            if (invulTimer > 0f)
            {
                invulTimer -= Time.deltaTime;
                c.a = ((int)(Time.time * 20f)) % 2 == 0 ? 0.35f : 1f;
            }
            else c.a = 1f;
            sr.color = c;
        }
    }

    void FixedUpdate()
    {
        rb.linearVelocity = moveInput * moveSpeed;

        Vector2 p = rb.position;
        p.x = Mathf.Clamp(p.x, -GameBounds.HalfWidth + 0.7f, GameBounds.HalfWidth - 0.7f);
        p.y = Mathf.Clamp(p.y, -GameBounds.HalfHeight + 0.8f, GameBounds.HalfHeight - 0.8f);
        rb.position = p;

        rb.rotation = Mathf.LerpAngle(rb.rotation, -moveInput.x * 12f, 0.2f);
    }

    void FireBullets()
    {
        if (bulletPrefab == null) return;
        for (int i = -1; i <= 1; i += 2)
        {
            Vector3 pos = transform.position + new Vector3(i * bulletSpacing, muzzleOffsetY, 0f);
            GameObject go = Instantiate(bulletPrefab, pos, Quaternion.identity);
            Projectile pr = go.GetComponent<Projectile>();
            if (pr != null) pr.Launch(Vector2.up);
        }
    }

    void FireMissile()
    {
        if (missilePrefab == null) return;
        Missiles--;
        Vector3 pos = transform.position + new Vector3(0f, muzzleOffsetY, 0f);
        GameObject go = Instantiate(missilePrefab, pos, Quaternion.identity);
        Projectile pr = go.GetComponent<Projectile>();
        if (pr != null) pr.Launch(Vector2.up);
    }

    public void Heal(int amount)
    {
        Health = Mathf.Min(maxHealth, Health + amount);
    }

    public void TakeDamage(int amount)
    {
        if (invulTimer > 0f || Health <= 0) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

        Health -= amount;
        invulTimer = invulnerableSeconds;

        if (Health <= 0)
        {
            Health = 0;
            if (explosionPrefab != null)
            {
                GameObject fx = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
                fx.transform.localScale = Vector3.one * 1.5f;
            }
            if (GameManager.Instance != null) GameManager.Instance.GameOver();
            Destroy(gameObject);
        }
    }
}
