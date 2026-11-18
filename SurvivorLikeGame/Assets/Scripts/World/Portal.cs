using UnityEngine;

namespace RpgGame
{
    // 区域传送门；survivorRift = true 时进入幸存者子玩法。
    public class Portal : Interactable
    {
        [Header("普通区域传送")]
        public string targetRegionId;
        public string targetSpawnPointId = "default";

        [Header("幸存者裂隙")]
        public bool survivorRift;
        public string survivorSceneName = "SampleScene";
        public string difficultyId = "normal";
        public float targetDuration = 120f;

        public override string Prompt
        {
            get
            {
                if (survivorRift) return "Enter Survivor Rift";
                return string.IsNullOrEmpty(displayName) ? "Travel" : displayName;
            }
        }

        public override void Interact()
        {
            if (GameFlow.Instance == null)
            {
                Debug.LogError("[RPG] GameFlow 不存在，无法传送");
                return;
            }

            if (survivorRift)
            {
                var config = new RunConfig
                {
                    runId = "survivor_" + (string.IsNullOrEmpty(targetRegionId) ? "test" : targetRegionId),
                    sceneName = survivorSceneName,
                    difficultyId = difficultyId,
                    targetDuration = targetDuration,
                };
                GameFlow.Instance.EnterSurvivorRun(config, CurrentRegionId(), targetSpawnPointId);
            }
            else
            {
                GameFlow.Instance.LoadRegion(targetRegionId, targetSpawnPointId);
            }
        }

        string CurrentRegionId()
        {
            var root = FindFirstObjectByType<RegionRoot>();
            return root != null ? root.regionId : "";
        }
    }
}
