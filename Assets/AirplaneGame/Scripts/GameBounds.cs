using UnityEngine;

/// <summary>Size of the visible play area (based on the Main Camera).</summary>
public static class GameBounds
{
    public static float HalfHeight
    {
        get { Camera c = Camera.main; return c != null ? c.orthographicSize : 6f; }
    }

    public static float HalfWidth
    {
        get { Camera c = Camera.main; return c != null ? c.orthographicSize * c.aspect : 10f; }
    }

    public static bool IsOutside(Vector2 p, float margin)
    {
        return Mathf.Abs(p.x) > HalfWidth + margin || Mathf.Abs(p.y) > HalfHeight + margin;
    }
}
