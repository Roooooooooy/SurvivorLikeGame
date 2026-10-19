using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Game State")]
    public bool isGameOver = false;
    public float gameTime = 0f;

    [Header("Enemy Spawning")]
    public GameObject[] enemyPrefabs;
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
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return;
        var player = PlayerController.Instance;
        if (player == null) return;

        Vector2 spawnDir = Random.insideUnitCircle.normalized;
        Vector3 spawnPos = player.transform.position + new Vector3(spawnDir.x, spawnDir.y, 0) * spawnRadius;

        int index = 0;
        // Spawn stronger enemies over time
        if (gameTime > 120f && enemyPrefabs.Length > 1) index = Random.Range(0, Mathf.Min(2, enemyPrefabs.Length));
        if (gameTime > 240f && enemyPrefabs.Length > 2) index = Random.Range(0, enemyPrefabs.Length);

        Instantiate(enemyPrefabs[index], spawnPos, Quaternion.identity);
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
