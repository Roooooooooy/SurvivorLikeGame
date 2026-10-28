using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

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
    public GameObject titleScreen;
    public Text timerText;
    public Text killCountText;
    public GameObject gameOverPanel;
    public GameObject levelUpPanel;
    public Button[] upgradeButtons;
    public Text[] upgradeButtonTexts;

    private int killCount = 0;
    private float spawnTimer = 0f;
    private float currentSpawnInterval;
    private bool isTitleScreen = false;

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

        // 游戏首页：暂停游戏逻辑，任意键开始
        if (titleScreen != null)
        {
            isTitleScreen = true;
            titleScreen.SetActive(true);
            Time.timeScale = 0f;
            ShowOverlayHintsOnTitle();
        }
    }

    // 标题页补充浮层入口提示（TAB SHOP / C CHARACTER / D DIFFICULTY）；缺哪条补哪条，避免重复叠加。
    // 标题字体是 Cinzel（仅拉丁字形），因此这里用英文提示，中文说明放在浮层内部。
    void ShowOverlayHintsOnTitle()
    {
        if (titleScreen == null) return;
        var controller = titleScreen.GetComponent<TitleScreenController>();
        if (controller == null || controller.subtitle == null) return;

        var subtitle = controller.subtitle;
        string hint = subtitle.text;
        if (!hint.Contains("TAB")) hint += "  ·  TAB SHOP";
        if (!hint.Contains("C CHARACTER")) hint += "  ·  C CHARACTER";
        if (!hint.Contains("D DIFFICULTY")) hint += "  ·  D DIFFICULTY";
        subtitle.text = hint;
    }

    void StartGame()
    {
        isTitleScreen = false;
        if (titleScreen) titleScreen.SetActive(false);
        Time.timeScale = 1f;
    }

    void Update()
    {
        // 快捷键：升级界面按 1/2/3 选择；结算界面按 R 重开（timeScale=0 时 Update 照常运行）
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (isTitleScreen)
        {
            // 选角界面打开时吞掉所有按键：C / ESC 关闭，其余键不会开始游戏
            if (CharacterSelectScreen.IsOpen)
            {
                if (kb != null && (kb.cKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                    CharacterSelectScreen.Hide();
                return;
            }

            // 商店打开时吞掉所有按键：TAB / ESC 关闭，其余键不会开始游戏
            if (MetaShopScreen.IsOpen)
            {
                if (kb != null && (kb.tabKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                    MetaShopScreen.Hide();
                return;
            }

            // 难度界面打开时吞掉所有按键：D / ESC 关闭，其余键不会开始游戏
            if (DifficultySelectScreen.IsOpen)
            {
                if (kb != null && (kb.dKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                    DifficultySelectScreen.Hide();
                return;
            }

            if (kb != null)
            {
                // TAB / C / D 优先于「任意键开始」：打开浮层的那一帧不能同时开局。
                // 三个浮层互斥，打开前先关掉另外两个。
                if (kb.tabKey.wasPressedThisFrame)
                {
                    CharacterSelectScreen.Hide();
                    DifficultySelectScreen.Hide();
                    MetaShopScreen.Show();
                    return;
                }
                if (kb.cKey.wasPressedThisFrame)
                {
                    MetaShopScreen.Hide();
                    DifficultySelectScreen.Hide();
                    CharacterSelectScreen.Show();
                    return;
                }
                if (kb.dKey.wasPressedThisFrame)
                {
                    MetaShopScreen.Hide();
                    CharacterSelectScreen.Hide();
                    DifficultySelectScreen.Show();
                    return;
                }
                if (kb.anyKey.wasPressedThisFrame) StartGame();
            }
            return;
        }
        if (kb != null)
        {
#if UNITY_EDITOR
            // 调试：F10 立即死亡（测试死亡演出）
            if (kb.f10Key.wasPressedThisFrame && PlayerController.Instance != null)
                PlayerController.Instance.DebugKill();
#endif
            if (levelUpPanel != null && levelUpPanel.activeSelf)
            {
                var wm = PlayerController.Instance != null
                    ? PlayerController.Instance.GetComponent<WeaponManager>() : null;
                if (wm != null)
                {
                    if (kb.digit1Key.wasPressedThisFrame) wm.SelectUpgrade(0);
                    else if (kb.digit2Key.wasPressedThisFrame) wm.SelectUpgrade(1);
                    else if (kb.digit3Key.wasPressedThisFrame) wm.SelectUpgrade(2);
                }
            }
            if (isGameOver && kb.rKey.wasPressedThisFrame) RestartGame();
        }

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
        Debug.Log("[GameManager] GameOver 触发，gameOverPanel=" + (gameOverPanel != null));
        // 立即结算本局：保证玩家在死亡演出期间就按 R 重开也不会漏记金币。
        // RunStats.FinalizeRun 幂等，结算界面再次调用不会重复入账。
        RunStats.FinalizeRun();
        isGameOver = true;
        // 黑魂式死亡：不暂停时间，怪继续移动；YOU DIED 演出结束后进入本局结算
        Time.timeScale = 1f;
        if (levelUpPanel) levelUpPanel.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(true);
    }

    public void ShowLevelUp(System.Collections.Generic.List<UpgradeOption> options)
    {
        if (isGameOver) return;
        Time.timeScale = 0f;
        if (levelUpPanel) levelUpPanel.SetActive(true);
        if (upgradeButtons == null) return;
        for (int i = 0; i < upgradeButtons.Length; i++)
        {
            if (i < options.Count)
            {
                upgradeButtons[i].gameObject.SetActive(true);
                var label = ButtonLabel(i);
                if (label != null) label.text = options[i].displayName;
            }
            else
            {
                upgradeButtons[i].gameObject.SetActive(false);
            }
        }
    }

    // 兼容旧场景：序列化的 upgradeButtonTexts 可能为空（TMP → legacy Text 迁移遗留），
    // 此时直接取按钮子节点上的 Text，避免升级选项文字不显示。
    Text ButtonLabel(int i)
    {
        if (upgradeButtonTexts != null && i < upgradeButtonTexts.Length && upgradeButtonTexts[i] != null)
            return upgradeButtonTexts[i];
        if (upgradeButtons != null && i < upgradeButtons.Length && upgradeButtons[i] != null)
            return upgradeButtons[i].GetComponentInChildren<Text>(true);
        return null;
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
