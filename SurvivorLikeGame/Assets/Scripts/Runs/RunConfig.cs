using System;
using System.Collections.Generic;

namespace RpgGame
{
    // 一次幸存者 Run 的完整配置：从哪个区域进入、打完回哪里、用什么难度/奖励表。
    [Serializable]
    public class RunConfig
    {
        public string runId = "survivor_test";
        public string sceneName = "SampleScene";
        public string originRegionId;
        public string returnSpawnPointId = "default";
        public string difficultyId = "normal";
        public string rewardTableId;
        public float targetDuration = 120f;
        public List<string> modifiers = new List<string>();
    }
}
