using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

// 刷怪表条目：到点解锁，按权重混入刷怪池
// typeIndex 对应 Resources/EnemyTypes.asset 中的类型下标
[System.Serializable]
public class SpawnEntry
{
    public int typeIndex;
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

#if UNITY_EDITOR
        // 压测：按 F9 在玩家周围爆刷 500 只随机敌人
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.f9Key.wasPressedThisFrame)
        {
            var p = PlayerController.Instance;
            if (p != null)
            {
                for (int i = 0; i < 500; i++)
                {
                    Vector2 d = Random.insideUnitCircle.normalized;
                    EnemySimulation.Instance.Spawn(
                        Random.Range(0, EnemySimulation.Instance.TypeCount),
                        (Vector2)p.transform.position + d * Random.Range(5f, 20f));
                }
            }
        }
#endif

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
            if (gameTime >= e.unlockTime) total += e.weight;
        if (total == 0) return;

        int roll = Random.Range(0, total);
        int typeIndex = 0;
        foreach (var e in spawnTable)
        {
            if (gameTime < e.unlockTime) continue;
            roll -= e.weight;
            if (roll < 0) { typeIndex = e.typeIndex; break; }
        }

        Vector2 spawnDir = Random.insideUnitCircle.normalized;
        Vector3 spawnPos = player.transform.position + new Vector3(spawnDir.x, spawnDir.y, 0) * spawnRadius;
        EnemySimulation.Instance.Spawn(typeIndex, spawnPos);
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
