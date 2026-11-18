using UnityEngine;
using UnityEngine.SceneManagement;

namespace RpgGame
{
    // Boot 场景入口：GameFlow 已由 AppBootstrap 创建，这里只负责进主菜单。
    public class BootEntry : MonoBehaviour
    {
        public string fallbackScene = "MainMenu";

        void Start()
        {
            if (GameFlow.Instance != null)
                GameFlow.Instance.LoadMainMenu();
            else
                SceneManager.LoadScene(fallbackScene);
        }
    }
}
