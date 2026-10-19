using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

// 刷怪表条目：到点解锁，按权重混入刷怪池
[System.Serializable]
public class SpawnEntry
{
    public GameObject prefab;
    public float unlockTime = 0f; // 开局多少秒后加入刷怪池
    public int weight = 10;
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Game State")]
    public bool isGameOver = false;
    public float gameTime = 0f;

    [Header("Enemy Spawning")]
    public List<SpawnEntry> spawnTable = new List<SpawnEntry>();
    public float spawnRadius = 15f;
    public float baseSpawnInterval = 2f;
    public float minSpawnInterval = 0.3f;

    [Header("UI References")]
    public Text timerText;
    public Text killCountText;
    public GameObject gameOverPanel;
    public GameObject levelUpPanel;
    public Button[] upgradeButtons;
    public TMP_Text[] upgradeButtonTexts;

    private int killCount = 0;
    private float spawnTimer = 0f;
    private float currentSpawnInterval;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        currentSpawnInterval = baseSpawnInterval;
        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (levelUpPanel)
        {
            levelUpPanel.SetActive(false);
        }
    }

    void Update()
    {
        if (isGameOver) return;

        gameTime += Time.deltaTime;
        UpdateSpawnInterval();
        HandleSpawning();
        UpdateUI();
    }

    void UpdateSpawnInterval()
    {
        // Difficulty ramps up over time
        currentSpawnInterval = Mathf.Max(minSpawnInterval, baseSpawnInterval - gameTime * 0.01f);
    }

    void HandleSpawning()
    {
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= currentSpawnInterval)
        {
            spawnTimer = 0f;
            SpawnEnemy();
            // Spawn more enemies as time goes on
            if (gameTime > 60f) SpawnEnemy();
            if (gameTime > 120f) SpawnEnemy();
            if (gameTime > 180f) SpawnEnemy();
        }
    }

    void SpawnEnemy()
    {
        if (spawnTable == null || spawnTable.Count == 0) return;
        var player = PlayerController.Instance;
        if (player == null) return;

        // 已解锁的敌人按权重随机
        int total = 0;
        foreach (var e in spawnTable)
            if (e.prefab != null && gameTime >= e.unlockTime) total += e.weight;
        if (total == 0) return;

        int roll = Random.Range(0, total);
        GameObject chosen = null;
        foreach (var e in spawnTable)
        {
            if (e.prefab == null || gameTime < e.unlockTime) continue;
            roll -= e.weight;
            if (roll < 0) { chosen = e.prefab; break; }
        }
        if (chosen == null) return;

        Vector2 spawnDir = Random.insideUnitCircle.normalized;
        Vector3 spawnPos = player.transform.position + new Vector3(spawnDir.x, spawnDir.y, 0) * spawnRadius;
        Instantiate(chosen, spawnPos, Quaternion.identity);
    }

    public void AddKill()
    {
        killCount++;
    }

    void UpdateUI()
    {
        if (timerText)
        {
            int minutes = (int)(gameTime / 60);
            int seconds = (int)(gameTime % 60);
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
        if (killCountText) killCountText.text = "Kills: " + killCount;
    }

    public void GameOver()
    {
        isGameOver = true;
        if (gameOverPanel) gameOverPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void ShowLevelUp(System.Collections.Generic.List<UpgradeOption> options)
    {
        Time.timeScale = 0f;
        if (levelUpPanel) levelUpPanel.SetActive(true);
        for (int i = 0; i < upgradeButtons.Length; i++)
        {
            if (i < options.Count)
            {
                upgradeButtons[i].gameObject.SetActive(true);
                if (upgradeButtonTexts[i]) upgradeButtonTexts[i].text = options[i].displayName;
            }
            else
            {
                upgradeButtons[i].gameObject.SetActive(false);
            }
        }
    }

    public void HideLevelUp()
    {
        Time.timeScale = 1f;
        if (levelUpPanel) levelUpPanel.SetActive(false);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}
