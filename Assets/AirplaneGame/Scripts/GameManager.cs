using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Score, stages, enemy spawning, boss, HUD, game over / victory / restart.</summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Scene references")]
    public PlayerController player;
    public GameObject[] enemyPrefabs;   // 0 = small, 1 = medium, 2 = fast
    public GameObject bossPrefab;

    [Header("UI")]
    public Text scoreText;
    public Text stageText;
    public Text missileText;
    public Text messageText;
    public Image healthFill;
    public Image bossFill;
    public GameObject bossBarRoot;

    public enum State { Playing, GameOver, Victory }
    public State state = State.Playing;
    public bool IsPlaying { get { return state == State.Playing; } }
    public int Score { get; private set; }

    int stage = 1;
    int killsInStage;
    bool bossStage;
    Enemy boss;
    float spawnTimer = 1f;
    float pauseSpawnUntil;
    float messageTimer;
    int highScore;

    static readonly int[] killTargets = { 10, 18, 26 };
    static readonly float[] spawnIntervals = { 1.6f, 1.2f, 0.9f };
    static readonly float[][] weights =
    {
        new float[] { 1f, 0f, 0f },
        new float[] { 0.5f, 0.3f, 0.2f },
        new float[] { 0.3f, 0.4f, 0.3f }
    };
    static readonly float[] bossWeights = { 0.6f, 0.1f, 0.3f };

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        highScore = PlayerPrefs.GetInt("AirplaneHighScore", 0);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        pauseSpawnUntil = Time.time + 1.5f;
        ShowMessage("STAGE 1", 2f);
    }

    void Update()
    {
        if (state == State.Playing) HandleSpawning();
        else if (GameInput.RestartPressed) Restart();

        UpdateHud();
    }

    // ---------- spawning ----------
    void HandleSpawning()
    {
        if (Time.time < pauseSpawnUntil) return;
        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f) return;

        if (bossStage)
        {
            spawnTimer = 2.4f;
            SpawnEnemy(bossWeights);
        }
        else
        {
            spawnTimer = spawnIntervals[stage - 1];
            SpawnEnemy(weights[stage - 1]);
        }
    }

    void SpawnEnemy(float[] w)
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return;

        float total = 0f;
        for (int i = 0; i < w.Length; i++) total += w[i];
        float r = Random.value * total;
        int index = 0;
        for (int i = 0; i < w.Length; i++)
        {
            r -= w[i];
            if (r <= 0f) { index = i; break; }
        }
        index = Mathf.Clamp(index, 0, enemyPrefabs.Length - 1);

        float x = Random.Range(-GameBounds.HalfWidth + 0.8f, GameBounds.HalfWidth - 0.8f);
        Vector3 pos = new Vector3(x, GameBounds.HalfHeight + 1f, 0f);
        Instantiate(enemyPrefabs[index], pos, Quaternion.identity);
    }

    // ---------- progress ----------
    public void OnEnemyKilled(Enemy e)
    {
        if (state != State.Playing) return;
        Score += e.scoreValue;

        if (e.isBoss) { Victory(); return; }

        if (!bossStage)
        {
            killsInStage++;
            if (killsInStage >= killTargets[stage - 1]) NextStage();
        }
    }

    void NextStage()
    {
        stage++;
        killsInStage = 0;
        if (player != null) player.Heal(25);

        if (stage > killTargets.Length)
        {
            bossStage = true;
            StartCoroutine(BossIntro());
        }
        else
        {
            ShowMessage("STAGE " + stage, 2f);
            pauseSpawnUntil = Time.time + 2f;
        }
    }

    IEnumerator BossIntro()
    {
        ShowMessage("WARNING!\nBOSS INCOMING", 3f);
        pauseSpawnUntil = Time.time + 5f;
        yield return new WaitForSeconds(3f);
        if (state != State.Playing || bossPrefab == null) yield break;

        Vector3 pos = new Vector3(0f, GameBounds.HalfHeight + 2.5f, 0f);
        GameObject go = Instantiate(bossPrefab, pos, Quaternion.identity);
        boss = go.GetComponent<Enemy>();
    }

    public void GameOver()
    {
        if (state != State.Playing) return;
        state = State.GameOver;
        SaveHighScore();
        ShowMessage("GAME OVER\nScore: " + Score + "   Best: " + highScore + "\nPress R to restart", 99999f);
    }

    void Victory()
    {
        state = State.Victory;
        SaveHighScore();
        ShowMessage("YOU WIN!\nScore: " + Score + "   Best: " + highScore + "\nPress R to play again", 99999f);

        List<Enemy> copy = new List<Enemy>(Enemy.All);
        for (int i = 0; i < copy.Count; i++)
            if (copy[i] != null) copy[i].Die(false);
    }

    void SaveHighScore()
    {
        if (Score > highScore) highScore = Score;
        PlayerPrefs.SetInt("AirplaneHighScore", highScore);
        PlayerPrefs.Save();
    }

    void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ---------- HUD ----------
    void ShowMessage(string text, float seconds)
    {
        if (messageText == null) return;
        messageText.text = text;
        messageTimer = seconds;
    }

    void UpdateHud()
    {
        if (messageText != null && messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f) messageText.text = "";
        }

        if (scoreText != null) scoreText.text = "SCORE  " + Score;
        if (stageText != null) stageText.text = bossStage ? "BOSS STAGE" : "STAGE " + stage + "   (" + killsInStage + "/" + killTargets[Mathf.Min(stage, killTargets.Length) - 1] + ")";

        if (player != null)
        {
            if (healthFill != null) healthFill.fillAmount = player.Health / (float)player.MaxHealth;
            if (missileText != null) missileText.text = "MISSILES  " + player.Missiles + "  [M]";
        }
        else
        {
            if (healthFill != null) healthFill.fillAmount = 0f;
        }

        if (bossBarRoot != null)
        {
            bool show = boss != null && !boss.IsDead;
            bossBarRoot.SetActive(show);
            if (show && bossFill != null) bossFill.fillAmount = boss.Health / (float)boss.maxHealth;
        }
    }
}
