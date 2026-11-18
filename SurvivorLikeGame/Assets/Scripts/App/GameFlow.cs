using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RpgGame
{
    // 应用层场景/模式路由。常驻 DontDestroyOnLoad，负责区域加载、幸存者 Run 进入/退出和存档。
    [DisallowMultipleComponent]
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }

        public RegionDefinition CurrentRegion { get; private set; }
        public RunConfig ActiveRun { get; private set; }
        public bool IsInRun => ActiveRun != null;

        string pendingSpawnPointId;
        bool respawning;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SaveSystem.Load();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }

        public void LoadRegion(string regionId, string spawnPointId = null)
        {
            var region = RegionCatalog.Find(regionId);
            if (region == null)
            {
                Debug.LogError("[RPG] 找不到区域资产: " + regionId);
                return;
            }

            SaveNow();
            CurrentRegion = region;
            ActiveRun = null;
            pendingSpawnPointId = spawnPointId;
            Time.timeScale = 1f;

            // 在场景加载前设置，确保新场景的 Awake/Start 就能读到正确模式。
            RegionRuntime.SetFromRegion(region.mode);
            SceneManager.LoadScene(region.sceneName);
        }

        public void LoadMainMenu()
        {
            ActiveRun = null;
            CurrentRegion = null;
            pendingSpawnPointId = null;
            Time.timeScale = 1f;
            RunSession.Clear();
            RegionRuntime.Set(RuntimeMode.Menu);
            SceneManager.LoadScene("MainMenu");
        }

        public void EnterSurvivorRun(RunConfig config, string originRegionId, string returnSpawnPointId)
        {
            if (config == null)
            {
                Debug.LogError("[RPG] EnterSurvivorRun 需要 RunConfig");
                return;
            }

            config.originRegionId = originRegionId;
            config.returnSpawnPointId = returnSpawnPointId;
            if (string.IsNullOrEmpty(config.sceneName)) config.sceneName = "SampleScene";

            SaveNow();
            CurrentRegion = null;
            ActiveRun = config;
            RunSession.Begin(config);
            pendingSpawnPointId = null;
            Time.timeScale = 1f;
            RegionRuntime.Set(RuntimeMode.SurvivorRun);
            SceneManager.LoadScene(config.sceneName);
        }

        public void ExitSurvivorRun(RunResult result)
        {
            if (ActiveRun == null) return;

            string originRegionId = ActiveRun.originRegionId;
            string returnSpawnPointId = ActiveRun.returnSpawnPointId;

            ApplyRunResult(result);
            ActiveRun = null;
            RunSession.Clear();

            if (!string.IsNullOrEmpty(originRegionId) && RegionCatalog.Find(originRegionId) != null)
                LoadRegion(originRegionId, returnSpawnPointId);
            else
                LoadMainMenu();
        }

        public void NotifyPlayerDied(float delay)
        {
            if (respawning) return;
            respawning = true;
            StartCoroutine(RespawnRoutine(delay));
        }

        IEnumerator RespawnRoutine(float delay)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, delay));
            respawning = false;

            if (CurrentRegion != null)
                LoadRegion(CurrentRegion.id, pendingSpawnPointId);
            else
                LoadMainMenu();
        }

        void ApplyRunResult(RunResult result)
        {
            if (result == null) return;

            var character = SaveSystem.Current.character;
            character.gold += Mathf.Max(0, result.coins);
            character.AddExp(result.exp);
            SaveNow();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var root = FindFirstObjectByType<RegionRoot>();
            if (root != null)
            {
                RegionRuntime.SetFromRegion(root.mode);

                // 误加载的幸存者 GameManager 可能把 timeScale 设成 0；动作区强制恢复。
                if (root.mode == RegionMode.ActionRPG) Time.timeScale = 1f;

                CurrentRegion = RegionCatalog.Find(root.regionId);

                var world = SaveSystem.Current.world;
                world.regionId = root.regionId;

                var spawn = root.FindSpawn(pendingSpawnPointId);
                if (spawn != null)
                    world.spawnPointId = string.IsNullOrEmpty(pendingSpawnPointId)
                        ? root.defaultSpawnPointId
                        : pendingSpawnPointId;

                world.GetRegion(root.regionId).visited = true;

                var rig = FindFirstObjectByType<PlayerModeRig>();
                if (rig != null)
                {
                    rig.ApplyMode(root.mode);
                    if (spawn != null) rig.TeleportTo(spawn.position);
                }
            }

            if (ActiveRun != null)
            {
                RegionRuntime.Set(RuntimeMode.SurvivorRun);
                var bridge = FindFirstObjectByType<SurvivorRunBridge>();
                if (bridge == null)
                    bridge = new GameObject("[SurvivorRunBridge]").AddComponent<SurvivorRunBridge>();
                bridge.Bind(ActiveRun);
            }

            SaveNow();
        }

        public void SaveNow()
        {
            SaveSystem.Save();
        }
    }
}
