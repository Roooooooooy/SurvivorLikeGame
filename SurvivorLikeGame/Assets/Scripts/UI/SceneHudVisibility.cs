using UnityEngine;

// HUD 显隐驱动：标题页与死亡结算期间隐藏，开局后恢复。
// 挂在 Canvas 上而不是 HUD 自身，否则隐藏 HUD 后自身 Update 也会停止。
internal class SceneHudVisibility : MonoBehaviour
{
    public GameObject hud;
    public GameObject timer;
    public GameObject kills;

    // HUD v2 新增的顶部经验条等元素挂在 Canvas 下，需要一并隐藏
    public GameObject[] extras;

    bool? visible;

    void Update()
    {
        var gm = GameManager.Instance;
        bool title = gm != null && gm.titleScreen != null && gm.titleScreen.activeInHierarchy;
        bool over = gm != null && gm.isGameOver;
        bool show = !title && !over;

        if (visible == show) return;
        visible = show;

        if (hud != null) hud.SetActive(show);
        if (timer != null) timer.SetActive(show);
        if (kills != null) kills.SetActive(show);

        if (extras != null)
        {
            for (int i = 0; i < extras.Length; i++)
                if (extras[i] != null) extras[i].SetActive(show);
        }
    }
}