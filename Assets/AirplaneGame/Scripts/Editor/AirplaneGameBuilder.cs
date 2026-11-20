using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Menu: Tools > Build Airplane Game. Creates sprites, animation, prefabs, scene and UI automatically.</summary>
public static class AirplaneGameBuilder
{
    const string Root = "Assets/AirplaneGame";
    const string Gen = Root + "/Generated";
    const string SpriteDir = Gen + "/Sprites";
    const string PrefabDir = Gen + "/Prefabs";
    const string AnimDir = Gen + "/Animations";
    const string ScenePath = Gen + "/AirplaneShooter.unity";

    // ------------------------------------------------------------------ pixel canvas
    class Pix
    {
        public int w, h;
        public Color[] px;

        public Pix(int w, int h) { this.w = w; this.h = h; px = new Color[w * h]; }

        public void Blend(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            Color d = px[y * w + x];
            float a = c.a + d.a * (1f - c.a);
            if (a <= 0f) return;
            Color r = (c * c.a + d * d.a * (1f - c.a)) / a;
            r.a = a;
            px[y * w + x] = r;
        }

        public void Fill(Color c) { for (int i = 0; i < px.Length; i++) px[i] = c; }

        public void Rect(int x0, int y0, int x1, int y1, Color c)
        {
            for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) Blend(x, y, c);
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color c)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - rx - 1)), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(cx + rx + 1));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - ry - 1)), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(cy + ry + 1));
            float minR = Mathf.Min(rx, ry);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 1.3f) continue;
                    float cov = Mathf.Clamp01((1f - d) * minR + 0.5f);
                    if (cov <= 0f) continue;
                    Color cc = c; cc.a *= cov;
                    Blend(x, y, cc);
                }
            }
        }

        static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }

        public void Tri(Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
            int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
            int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
                    bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
                    bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
                    if (!(neg && pos)) Blend(x, y, col);
                }
            }
        }

        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col)
        {
            Tri(a, b, c, col);
            Tri(a, c, d, col);
        }

        public void AddOutline(Color col, int r)
        {
            float[] alpha = new float[w * h];
            for (int i = 0; i < alpha.Length; i++) alpha[i] = px[i].a;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (alpha[y * w + x] >= 0.5f) continue;
                    bool near = false;
                    for (int dy = -r; dy <= r && !near; dy++)
                    {
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            if (alpha[ny * w + nx] >= 0.5f) { near = true; break; }
                        }
                    }
                    if (near) { Color o = col; o.a = 1f; px[y * w + x] = o; }
                }
            }
        }

        public void FlipY()
        {
            for (int y = 0; y < h / 2; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int a = y * w + x, b = (h - 1 - y) * w + x;
                    Color t = px[a]; px[a] = px[b]; px[b] = t;
                }
            }
        }
    }

    // ------------------------------------------------------------------ main entry
    [MenuItem("Tools/Build Airplane Game")]
    public static void Build()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog("Airplane Game", "Pehle Play mode band karein (aur compile hone dein), phir dobara try karein.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        AssetDatabase.DeleteAsset(Gen);
        Directory.CreateDirectory(SpriteDir);
        Directory.CreateDirectory(PrefabDir);
        Directory.CreateDirectory(AnimDir);
        AssetDatabase.Refresh();

        // ---- sprites
        Sprite sPlayer = SaveSprite("Player", MakePlane(96, new Color(0.88f, 0.9f, 0.94f), new Color(0.58f, 0.63f, 0.72f), new Color(0.85f, 0.15f, 0.15f), new Color(0.3f, 0.75f, 1f), 0.46f, false, 0), 64f);
        Sprite sSmall = SaveSprite("EnemySmall", MakePlane(64, new Color(0.85f, 0.3f, 0.25f), new Color(0.95f, 0.55f, 0.2f), new Color(0.3f, 0.1f, 0.1f), new Color(1f, 0.9f, 0.4f), 0.44f, true, 0), 64f);
        Sprite sMedium = SaveSprite("EnemyMedium", MakePlane(80, new Color(0.35f, 0.65f, 0.35f), new Color(0.2f, 0.45f, 0.25f), new Color(0.95f, 0.85f, 0.2f), new Color(1f, 0.5f, 0.3f), 0.47f, true, 2), 64f);
        Sprite sFast = SaveSprite("EnemyFast", MakePlane(64, new Color(0.98f, 0.82f, 0.2f), new Color(0.95f, 0.5f, 0.1f), new Color(0.2f, 0.2f, 0.25f), new Color(0.2f, 0.2f, 0.3f), 0.34f, true, 0), 64f);
        Sprite sBoss = SaveSprite("Boss", MakePlane(192, new Color(0.45f, 0.25f, 0.6f), new Color(0.3f, 0.15f, 0.4f), new Color(0.9f, 0.2f, 0.3f), new Color(1f, 0.3f, 0.3f), 0.49f, true, 4), 64f);
        Sprite sBullet = SaveSprite("Bullet", MakeBullet(), 64f);
        Sprite sEnemyBullet = SaveSprite("EnemyBullet", MakeEnemyBullet(), 64f);
        Sprite sMissile = SaveSprite("Missile", MakeMissile(), 64f);
        Sprite sBg = SaveSprite("Sky", MakeSky(), 64f);
        Sprite sWhite = SaveSprite("White", MakeWhite(), 4f);
        Sprite[] frames = new Sprite[6];
        for (int i = 0; i < frames.Length; i++) frames[i] = SaveSprite("Explosion_" + i, MakeExplosionFrame(i), 64f);

        // ---- explosion animation
        AnimationClip clip = new AnimationClip();
        clip.frameRate = 12f;
        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++) { keys[i].time = i / 12f; keys[i].value = frames[i]; }
        keys[frames.Length].time = frames.Length / 12f;
        keys[frames.Length].value = frames[frames.Length - 1];
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
        AssetDatabase.CreateAsset(clip, AnimDir + "/Explosion.anim");
        UnityEditor.Animations.AnimatorController controller =
            UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(AnimDir + "/Explosion.controller", clip);

        // ---- prefabs
        GameObject explosionPrefab = MakeExplosionPrefab(frames[0], controller);
        GameObject bulletPrefab = MakeProjectile("Bullet", sBullet, true, 1, 14f, false, null, 0f, 0.4f);
        GameObject missilePrefab = MakeProjectile("Missile", sMissile, true, 10, 9f, true, explosionPrefab, 0.9f, 0.5f);
        GameObject enemyBulletPrefab = MakeProjectile("EnemyBullet", sEnemyBullet, false, 10, 5f, false, null, 0f, 0.6f);

        GameObject small = MakeEnemy("Enemy_Small", sSmall, Enemy.Pattern.Straight, 1, 2.5f, 10, 15, 3.0f, 8, false, false, explosionPrefab, enemyBulletPrefab, 1f);
        GameObject medium = MakeEnemy("Enemy_Medium", sMedium, Enemy.Pattern.ZigZag, 4, 1.8f, 25, 20, 2.2f, 10, true, false, explosionPrefab, enemyBulletPrefab, 1.2f);
        GameObject fast = MakeEnemy("Enemy_Fast", sFast, Enemy.Pattern.Dive, 2, 4.5f, 20, 20, 0f, 0, false, false, explosionPrefab, enemyBulletPrefab, 1f);
        GameObject boss = MakeEnemy("Boss", sBoss, Enemy.Pattern.Boss, 150, 2f, 500, 30, 1.1f, 12, false, true, explosionPrefab, enemyBulletPrefab, 3f);

        GameObject playerPrefab = MakePlayerPrefab(sPlayer, bulletPrefab, missilePrefab, explosionPrefab);

        // ---- scene
        GameObject light = GameObject.Find("Directional Light");
        if (light != null) Object.DestroyImmediate(light);

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.72f, 0.95f);
        }

        GameObject bg = new GameObject("Background");
        ScrollingBackground scroller = bg.AddComponent<ScrollingBackground>();
        scroller.tileA = MakeBgTile(bg.transform, "BG_A", sBg);
        scroller.tileB = MakeBgTile(bg.transform, "BG_B", sBg);

        GameObject playerObj = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        playerObj.transform.position = new Vector3(0f, -4.5f, 0f);

        GameObject gm = new GameObject("GameManager");
        GameManager manager = gm.AddComponent<GameManager>();
        manager.player = playerObj.GetComponent<PlayerController>();
        manager.enemyPrefabs = new GameObject[] { small, medium, fast };
        manager.bossPrefab = boss;

        BuildHud(manager, sWhite);

        EditorSceneManager.SaveScene(scene, ScenePath);

        List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool has = false;
        for (int i = 0; i < list.Count; i++) if (list[i].path == ScenePath) has = true;
        if (!has)
        {
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Airplane Game",
            "Game tayyar hai!\n\nAb upar Play button dabayein.\n\nW/A/S/D ya Arrows = move\nSpace = bullets\nM = missile\nR = restart (game over ke baad)", "OK");
    }

    // ------------------------------------------------------------------ sprite drawing
    static Sprite SaveSprite(string name, Pix pix, float ppu)
    {
        Texture2D tex = new Texture2D(pix.w, pix.h, TextureFormat.RGBA32, false);
        tex.SetPixels(pix.px);
        tex.Apply();
        string path = SpriteDir + "/" + name + ".png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = ppu;
        ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear;
        ti.alphaIsTransparency = true;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Pix MakePlane(int n, Color body, Color wing, Color accent, Color glass, float span, bool facingDown, int engines)
    {
        Pix p = new Pix(n, n);
        float N = n;
        float cx = N * 0.5f;
        Color dark = new Color(0.12f, 0.12f, 0.18f);

        for (int s = -1; s <= 1; s += 2)
        {
            Vector2 a = new Vector2(cx + s * 0.06f * N, 0.62f * N);
            Vector2 b = new Vector2(cx + s * span * N, 0.30f * N);
            Vector2 c = new Vector2(cx + s * span * N, 0.20f * N);
            Vector2 d = new Vector2(cx + s * 0.06f * N, 0.30f * N);
            p.Quad(a, b, c, d, wing);
            p.Quad(Vector2.Lerp(a, b, 0.7f), b, c, Vector2.Lerp(d, c, 0.7f), accent);

            Vector2 ta = new Vector2(cx + s * 0.05f * N, 0.20f * N);
            Vector2 tb = new Vector2(cx + s * 0.21f * N, 0.09f * N);
            Vector2 tc = new Vector2(cx + s * 0.21f * N, 0.04f * N);
            Vector2 td = new Vector2(cx + s * 0.05f * N, 0.10f * N);
            p.Quad(ta, tb, tc, td, wing);
        }

        if (engines >= 2)
        {
            for (int s = -1; s <= 1; s += 2) p.Ellipse(cx + s * 0.2f * N, 0.30f * N, 0.035f * N, 0.075f * N, dark);
        }
        if (engines >= 4)
        {
            for (int s = -1; s <= 1; s += 2) p.Ellipse(cx + s * 0.34f * N, 0.27f * N, 0.03f * N, 0.065f * N, dark);
        }

        p.Ellipse(cx, 0.5f * N, 0.085f * N, 0.44f * N, body);
        p.Ellipse(cx, 0.85f * N, 0.045f * N, 0.07f * N, accent);
        p.Ellipse(cx, 0.66f * N, 0.045f * N, 0.085f * N, glass);

        p.AddOutline(new Color(0.08f, 0.08f, 0.12f), n >= 128 ? 3 : 2);
        if (facingDown) p.FlipY();
        return p;
    }

    static Pix MakeBullet()
    {
        Pix p = new Pix(16, 32);
        p.Ellipse(8, 16, 6, 15, new Color(1f, 0.85f, 0.2f, 0.55f));
        p.Ellipse(8, 16, 4, 12, new Color(1f, 0.95f, 0.4f, 1f));
        p.Ellipse(8, 18, 2, 8, Color.white);
        return p;
    }

    static Pix MakeEnemyBullet()
    {
        Pix p = new Pix(24, 24);
        p.Ellipse(12, 12, 11, 11, new Color(1f, 0.3f, 0.1f, 0.5f));
        p.Ellipse(12, 12, 8, 8, new Color(1f, 0.4f, 0.2f, 1f));
        p.Ellipse(12, 12, 4, 4, new Color(1f, 0.92f, 0.75f, 1f));
        return p;
    }

    static Pix MakeMissile()
    {
        Pix p = new Pix(24, 64);
        p.Ellipse(12, 9, 4.5f, 10, new Color(1f, 0.6f, 0.1f, 0.9f));
        p.Ellipse(12, 10, 2.5f, 6, new Color(1f, 0.95f, 0.5f, 1f));
        p.Tri(new Vector2(9, 16), new Vector2(1, 5), new Vector2(9, 28), new Color(0.95f, 0.5f, 0.15f));
        p.Tri(new Vector2(15, 16), new Vector2(23, 5), new Vector2(15, 28), new Color(0.95f, 0.5f, 0.15f));
        p.Rect(9, 14, 15, 46, new Color(0.82f, 0.84f, 0.88f));
        p.Tri(new Vector2(9, 46), new Vector2(15, 46), new Vector2(12, 60), new Color(0.9f, 0.15f, 0.15f));
        p.Rect(9, 34, 15, 37, new Color(0.9f, 0.15f, 0.15f));
        p.AddOutline(new Color(0.1f, 0.1f, 0.12f), 1);
        return p;
    }

    static Pix MakeSky()
    {
        int W = 256, H = 512;
        Pix p = new Pix(W, H);
        for (int y = 0; y < H; y++)
        {
            float k = Mathf.Sin(y / (float)H * Mathf.PI * 2f) * 0.03f;
            Color c = new Color(0.45f + k, 0.72f + k, 0.95f, 1f);
            for (int x = 0; x < W; x++) p.px[y * W + x] = c;
        }

        System.Random rng = new System.Random(11);
        for (int k = 0; k < 9; k++)
        {
            int cx = rng.Next(30, 226), cy = rng.Next(0, H);
            for (int wrap = -1; wrap <= 1; wrap++)
            {
                int yy = cy + wrap * H;
                for (int j = 0; j < 5; j++)
                {
                    float px = cx + (j - 2) * 17f;
                    float r = 16f + (j == 2 ? 8f : (j % 2 == 0 ? 3f : 0f));
                    p.Ellipse(px, yy - 6f, r, r * 0.8f, new Color(0.82f, 0.9f, 1f, 0.9f));
                }
                for (int j = 0; j < 5; j++)
                {
                    float px = cx + (j - 2) * 17f;
                    float r = 16f + (j == 2 ? 8f : (j % 2 == 0 ? 3f : 0f));
                    p.Ellipse(px, yy, r, r * 0.8f, new Color(1f, 1f, 1f, 0.92f));
                }
            }
        }
        return p;
    }

    static Pix MakeWhite()
    {
        Pix p = new Pix(4, 4);
        p.Fill(Color.white);
        return p;
    }

    static Pix MakeExplosionFrame(int i)
    {
        Pix p = new Pix(128, 128);
        float t = i / 5f;
        float r = Mathf.Lerp(14f, 56f, Mathf.Pow(t, 0.7f));
        float a = 1f - t * 0.65f;
        float cx = 64f, cy = 64f;

        p.Ellipse(cx, cy, r * 1.1f, r * 1.1f, new Color(0.2f, 0.2f, 0.22f, 0.35f * t));
        p.Ellipse(cx, cy, r, r, new Color(1f, 0.45f, 0.1f, a));
        p.Ellipse(cx, cy, r * 0.7f, r * 0.7f, new Color(1f, 0.85f, 0.2f, a));
        p.Ellipse(cx, cy, r * 0.35f, r * 0.35f, new Color(1f, 1f, 0.9f, a * (1f - t)));

        for (int k = 0; k < 8; k++)
        {
            float ang = (k * 45f + i * 12f) * Mathf.Deg2Rad;
            float dist = r * (0.95f + 0.25f * t);
            float sr = Mathf.Max(2f, 7f * (1f - t * 0.6f));
            Color sc = k % 2 == 0 ? new Color(1f, 0.55f, 0.1f, a) : new Color(1f, 0.25f, 0.1f, a);
            p.Ellipse(cx + Mathf.Cos(ang) * dist, cy + Mathf.Sin(ang) * dist, sr, sr, sc);
        }
        return p;
    }

    // ------------------------------------------------------------------ prefabs
    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static Rigidbody2D AddBody(GameObject go, bool continuous)
    {
        Rigidbody2D rb = GetOrAdd<Rigidbody2D>(go);
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        if (continuous) rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        return rb;
    }

    static void AddTriggerBox(GameObject go, Sprite s, float k)
    {
        BoxCollider2D bc = go.AddComponent<BoxCollider2D>();
        bc.size = s.bounds.size * k;
        bc.isTrigger = true;
    }

    static GameObject SavePrefab(GameObject go, string name)
    {
        GameObject asset = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/" + name + ".prefab");
        Object.DestroyImmediate(go);
        return asset;
    }

    static GameObject MakeExplosionPrefab(Sprite first, UnityEditor.Animations.AnimatorController controller)
    {
        GameObject go = new GameObject("Explosion");
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = first;
        sr.sortingOrder = 20;
        Animator an = go.AddComponent<Animator>();
        an.runtimeAnimatorController = controller;
        AutoDestroy ad = go.AddComponent<AutoDestroy>();
        ad.lifetime = 0.55f;
        return SavePrefab(go, "Explosion");
    }

    static GameObject MakeProjectile(string name, Sprite s, bool fromPlayer, int damage, float speed, bool homing, GameObject hitFx, float hitScale, float colliderK)
    {
        GameObject go = new GameObject(name);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = 8;
        AddBody(go, true);
        AddTriggerBox(go, s, colliderK);
        Projectile pr = go.AddComponent<Projectile>();
        pr.fromPlayer = fromPlayer;
        pr.damage = damage;
        pr.speed = speed;
        pr.homing = homing;
        pr.turnRateDegPerSec = 220f;
        pr.hitEffect = hitFx;
        pr.hitEffectScale = hitScale;
        return SavePrefab(go, name);
    }

    static GameObject MakeEnemy(string name, Sprite s, Enemy.Pattern pattern, int hp, float speed, int score, int contact,
                                float fireInterval, int bulletDamage, bool aim, bool isBoss,
                                GameObject explosion, GameObject bullet, float expScale)
    {
        GameObject go = new GameObject(name);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = isBoss ? 6 : 5;
        AddBody(go, false);
        AddTriggerBox(go, s, 0.7f);
        Enemy e = go.AddComponent<Enemy>();
        e.pattern = pattern;
        e.maxHealth = hp;
        e.speed = speed;
        e.scoreValue = score;
        e.contactDamage = contact;
        e.fireInterval = fireInterval;
        e.bulletDamage = bulletDamage;
        e.aimAtPlayer = aim;
        e.isBoss = isBoss;
        e.bulletPrefab = bullet;
        e.explosionPrefab = explosion;
        e.explosionScale = expScale;
        return SavePrefab(go, name);
    }

    static GameObject MakePlayerPrefab(Sprite s, GameObject bullet, GameObject missile, GameObject explosion)
    {
        GameObject go = new GameObject("Player");
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = 10;
        AddBody(go, false);
        AddTriggerBox(go, s, 0.5f);
        PlayerController pc = go.AddComponent<PlayerController>();
        pc.bulletPrefab = bullet;
        pc.missilePrefab = missile;
        pc.explosionPrefab = explosion;
        return SavePrefab(go, "Player");
    }

    static Transform MakeBgTile(Transform parent, string name, Sprite s)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = -10;
        return go.transform;
    }

    // ------------------------------------------------------------------ HUD
    static Font GetFont()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    static GameObject MakeUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static Text MakeText(Transform parent, string name, string content, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 boxSize)
    {
        GameObject go = MakeUI(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = boxSize;

        Text t = go.AddComponent<Text>();
        t.font = GetFont();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;

        UnityEngine.UI.Outline ol = go.AddComponent<UnityEngine.UI.Outline>();
        ol.effectColor = Color.black;
        ol.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    static Image MakeBar(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 boxSize, Color fillColor, Sprite white, out GameObject root)
    {
        GameObject bgGo = MakeUI(name, parent);
        RectTransform rt = bgGo.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = boxSize;
        Image bg = bgGo.AddComponent<Image>();
        bg.sprite = white;
        bg.color = new Color(0f, 0f, 0f, 0.6f);
        bg.raycastTarget = false;

        GameObject fillGo = MakeUI("Fill", bgGo.transform);
        RectTransform fr = fillGo.GetComponent<RectTransform>();
        fr.anchorMin = Vector2.zero;
        fr.anchorMax = Vector2.one;
        fr.offsetMin = new Vector2(3f, 3f);
        fr.offsetMax = new Vector2(-3f, -3f);
        Image fill = fillGo.AddComponent<Image>();
        fill.sprite = white;
        fill.color = fillColor;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 1f;
        fill.raycastTarget = false;

        root = bgGo;
        return fill;
    }

    static void BuildHud(GameManager gm, Sprite white)
    {
        GameObject canvasGo = new GameObject("HUD", typeof(RectTransform));
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        Transform c = canvasGo.transform;

        gm.scoreText = MakeText(c, "ScoreText", "SCORE  0", 32, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(20f, -15f), new Vector2(400f, 40f));
        gm.stageText = MakeText(c, "StageText", "STAGE 1", 32, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -15f), new Vector2(500f, 40f));
        gm.missileText = MakeText(c, "MissileText", "MISSILES  5  [M]", 28, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-20f, -15f), new Vector2(400f, 40f));

        MakeText(c, "HealthLabel", "HEALTH", 22, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(20f, 52f), new Vector2(300f, 30f));
        GameObject healthRoot;
        gm.healthFill = MakeBar(c, "HealthBar", new Vector2(0f, 0f), new Vector2(20f, 20f), new Vector2(300f, 28f), new Color(0.2f, 0.85f, 0.3f), white, out healthRoot);

        GameObject bossRoot;
        gm.bossFill = MakeBar(c, "BossBar", new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(500f, 24f), new Color(0.9f, 0.15f, 0.25f), white, out bossRoot);
        MakeText(bossRoot.transform, "BossLabel", "BOSS", 18, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 24f));
        gm.bossBarRoot = bossRoot;
        bossRoot.SetActive(false);

        gm.messageText = MakeText(c, "MessageText", "", 60, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 300f));
        MakeText(c, "HintText", "WASD / Arrows = Move   |   Space = Bullets   |   M = Missile   |   R = Restart", 20, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(1200f, 30f));
    }
}
