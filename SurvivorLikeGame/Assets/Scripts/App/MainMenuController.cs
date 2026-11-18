using UnityEngine;

namespace RpgGame
{
    // Phase 0 占位主菜单：OnGUI 绘制，不依赖场景 Canvas / TMP。
    public class MainMenuController : MonoBehaviour
    {
        public string firstRegionId = "region_test";
        public string firstSpawnPointId = "default";

        void OnGUI()
        {
            GUI.skin.label.fontSize = 22;
            GUI.Label(new Rect(40f, 32f, 700f, 40f), "RPG Phase 0 Skeleton");

            var save = SaveSystem.Current;
            GUI.Label(new Rect(40f, 78f, 900f, 30f),
                "Lv " + save.character.level +
                "   Gold " + save.character.gold +
                "   Region " + save.world.regionId);

            if (GUI.Button(new Rect(40f, 130f, 280f, 44f), "Start Chapter 1"))
            {
                if (GameFlow.Instance != null)
                    GameFlow.Instance.LoadRegion(firstRegionId, firstSpawnPointId);
            }

            if (GUI.Button(new Rect(40f, 186f, 280f, 44f), "Quit"))
                Application.Quit();

            GUI.Label(new Rect(40f, 250f, 1000f, 30f),
                "Region controls: WASD move / J or LMB attack / Space dodge / E interact");
        }
    }
}
