using UnityEngine;

/// <summary>Scrolls two sky tiles downwards to make the plane look like it is flying.</summary>
public class ScrollingBackground : MonoBehaviour
{
    public Transform tileA;
    public Transform tileB;
    public float speed = 1.5f;

    float height;

    void Start()
    {
        float h = GameBounds.HalfHeight * 2f;
        float w = GameBounds.HalfWidth * 2f;
        height = h;
        Fit(tileA, w, h, 0f);
        Fit(tileB, w, h, h);
    }

    void Fit(Transform t, float w, float h, float y)
    {
        if (t == null) return;
        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        Vector2 size = sr.sprite.bounds.size;
        t.localScale = new Vector3(w / size.x * 1.02f, h / size.y * 1.01f, 1f);
        t.position = new Vector3(0f, y, 0f);
    }

    void Update()
    {
        Move(tileA);
        Move(tileB);
    }

    void Move(Transform t)
    {
        if (t == null) return;
        t.position += Vector3.down * speed * Time.deltaTime;
        if (t.position.y <= -height) t.position += Vector3.up * height * 2f;
    }
}
