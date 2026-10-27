using UnityEngine;

// 波次事件横幅：屏幕顶部大字提示（"精英来袭" / "包围潮" / "BOSS"）。
// 契约：玩法层只调用静态 Show，不关心 UI 如何创建。
// 实现：首次 Show 时由 WaveBannerView 运行时搭建 Canvas，同一时间只存在一套横幅。
public static class WaveBanner
{
    // title: 主标题；subtitle: 副标题（可为 null）；duration: 停留秒数（unscaled）
    public static void Show(string title, string subtitle = null, float duration = 3f)
    {
        // 空标题忽略，避免闪出一个只有背景的空横幅
        if (string.IsNullOrWhiteSpace(title)) return;

        // 只在运行时创建 UI，避免编辑模式下往场景里塞临时对象
        if (!Application.isPlaying) return;

        WaveBannerView.Instance.Enqueue(title, subtitle, duration);
    }
}
