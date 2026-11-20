using System.Collections.Generic;
using UnityEngine;

/// <summary>One script for all enemy types (small, medium, fast) and the boss.</summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour
{
    public enum Pattern { Straight, ZigZag, Dive, Boss }

    [Header("Stats")]
    public Pattern pattern = Pattern.Straight;
    public int maxHealth = 1;
    public float speed = 2.5f;
    public int scoreValue = 10;
    public int contactDamage = 20;
    public bool isBoss = false;

    [Header("Shooting (0 = never)")]
    public GameObject bulletPrefab;
    public float fireInterval = 0f;
    public int bulletDamage = 10;
    public bool aimAtPlayer = false;

    [Header("Effects")]
    public GameObject explosionPrefab;
    public float explosionScale = 1f;

    public static readonly List<Enemy> All = new List<Enemy>();

    public int Health { get; private set; }
    public bool IsDead { get { return dead; } }

    Rigidbody2D rb;
    SpriteRenderer sr;
    Color baseColor = Color.white;
    float age;
    float phase;
    float fireTimer;
    float flashTimer;
    bool dead;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
        Health = maxHealth;
        phase = Random.value * 6.28f;
        fireTimer = Random.Range(0.6f, 0.6f + Mathf.Max(0.1f, fireInterval));
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Update()
    {
        if (sr != null)
        {
            if (flashTimer > 0f) { flashTimer -= Time.deltaTime; sr.color = new Color(1f, 0.5f, 0.5f, 1f); }
            else sr.color = baseColor;
        }

        if (dead || fireInterval <= 0f || bulletPrefab == null) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
        if (transform.position.y > GameBounds.HalfHeight - 0.3f) return;   // still above the screen

        fireTimer -= Time.deltaTime;
        if (fireTimer > 0f) return;
        fireTimer = IsAngry ? fireInterval * 0.6f : fireInterval;
        Fire();
    }

    bool IsAngry { get { return isBoss && Health < maxHealth / 2; } }

    void FixedUpdate()
    {
        if (dead) return;
        age += Time.fixedDeltaTime;
        Vector2 v = Vector2.zero;

        switch (pattern)
        {
            case Pattern.Straight:
                v = new Vector2(0f, -speed);
                break;

            case Pattern.ZigZag:
                v = new Vector2(Mathf.Cos(age * 2f + phase) * 2f, -speed);
                break;

            case Pattern.Dive:
            {
                float dx = 0f;
                PlayerController p = GetPlayer();
                if (p != null) dx = Mathf.Clamp(p.transform.position.x - rb.position.x, -1f, 1f) * speed * 0.6f;
                v = new Vector2(dx, -speed);
                break;
            }

            case Pattern.Boss:
            {
                float hoverY = GameBounds.HalfHeight - 2.8f;
                float range = Mathf.Max(1f, GameBounds.HalfWidth - 2.5f);
                float freq = IsAngry ? 1.1f : 0.7f;
                float targetX = Mathf.Sin(age * freq) * range;
                float vx = Mathf.Clamp((targetX - rb.position.x) * 3f, -speed * 2.5f, speed * 2.5f);
                float vy = Mathf.Clamp((hoverY - rb.position.y) * 1.5f, -speed, speed);
                v = new Vector2(vx, vy);
                break;
            }
        }

        rb.linearVelocity = v;

        if (!isBoss && rb.position.y < -GameBounds.HalfHeight - 2f) Destroy(gameObject);
    }

    PlayerController GetPlayer()
    {
        return GameManager.Instance != null ? GameManager.Instance.player : null;
    }

    void Fire()
    {
        Vector2 origin = (Vector2)transform.position + Vector2.down * 0.6f;

        // Always aim at the player (if the player is gone, shoot straight down)
        Vector2 baseDir = Vector2.down;
        PlayerController p = GetPlayer();
        if (p != null) baseDir = ((Vector2)p.transform.position - origin).normalized;

        if (isBoss)
        {
            // Boss fires a fan of bullets centred on the player
            int count = IsAngry ? 7 : 5;
            float spread = IsAngry ? 70f : 50f;
            for (int i = 0; i < count; i++)
            {
                float a = -spread * 0.5f + spread * i / (count - 1);
                Vector3 d3 = Quaternion.Euler(0f, 0f, a) * new Vector3(baseDir.x, baseDir.y, 0f);
                Shoot(origin, new Vector2(d3.x, d3.y));
            }
            return;
        }

        Shoot(origin, baseDir);
    }

    void Shoot(Vector2 origin, Vector2 dir)
    {
        GameObject go = Instantiate(bulletPrefab, origin, Quaternion.identity);
        Projectile pr = go.GetComponent<Projectile>();
        if (pr == null) return;
        pr.fromPlayer = false;
        pr.damage = bulletDamage;
        pr.Launch(dir);
    }

    public void TakeDamage(int amount)
    {
        if (dead) return;
        Health -= amount;
        flashTimer = 0.08f;
        if (Health <= 0) Die(true);
    }

    public void Die(bool killedByPlayer)
    {
        if (dead) return;
        dead = true;

        if (explosionPrefab != null)
        {
            GameObject fx = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            fx.transform.localScale = Vector3.one * explosionScale;
        }
        if (killedByPlayer && GameManager.Instance != null) GameManager.Instance.OnEnemyKilled(this);
        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (dead) return;
        PlayerController p = other.GetComponent<PlayerController>();
        if (p == null) return;
        p.TakeDamage(contactDamage);
        if (!isBoss) Die(true);
    }
}
