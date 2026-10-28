using UnityEngine;

// 全局 UI 主题：颜色、字体、九宫格 Sprite 的唯一来源。
// 运行时 UI 通过 UITheme.Current 读取（Resources/UITheme.asset），
// 编辑器里由 Tools/Survivor/Generate UI Theme 生成/更新。
[CreateAssetMenu(fileName = "UITheme", menuName = "Survivor/UI Theme")]
public class UITheme : ScriptableObject
{
    [Header("字体")]
    public Font displayFont;   // Cinzel：英文标题、Boss 名、结算标题
    public Font bodyFont;      // VT323：数字、英文 HUD 标签、按键提示
    public Font cjkFont;       // Noto Serif SC 子集：中文标题/正文（含 CJK 时自动切换）

    [Header("颜色")]
    public Color ink = new Color(0.024f, 0.031f, 0.043f, 1f);        // #06080B
    public Color panel = new Color(0.059f, 0.082f, 0.114f, 1f);      // #0F151D
    public Color panel2 = new Color(0.082f, 0.114f, 0.153f, 1f);     // #151D27
    public Color gold = new Color(0.788f, 0.659f, 0.416f, 1f);       // #C9A86A
    public Color goldHi = new Color(0.906f, 0.827f, 0.627f, 1f);     // #E7D3A0
    public Color parchment = new Color(0.851f, 0.824f, 0.761f, 1f); // #D9D2C2
    public Color muted = new Color(0.561f, 0.545f, 0.502f, 1f);     // #8F8B80
    public Color blood = new Color(0.549f, 0.184f, 0.204f, 1f);     // #8C2F34
    public Color bloodHi = new Color(0.788f, 0.318f, 0.341f, 1f);   // #C95157
    public Color teal = new Color(0.373f, 0.561f, 0.541f, 1f);      // #5F8F8A
    public Color tealHi = new Color(0.561f, 0.753f, 0.722f, 1f);    // #8FC0B8
    public Color line = new Color(0.149f, 0.192f, 0.239f, 1f);      // #26313D

    [Header("九宫格 Sprite")]
    public Sprite panelSprite;        // 主面板（panel_tan）
    public Sprite panelInlaySprite;   // 内衬/行底（panel_inlay）
    public Sprite buttonSprite;       // 普通按钮（panel_grey）
    public Sprite buttonPressedSprite;// 按下按钮（panel_grey_pressed）
    public Sprite barSprite;          // 纯白填充（bar_white）
    public Sprite barFrameSprite;     // 血条外框（frame_list）
    public Sprite frameSprite;        // 装饰边框（border_010）

    static UITheme current;
    static bool loaded;

    // 运行时唯一入口；资源缺失时返回 null，调用方需做兜底
    public static UITheme Current
    {
        get
        {
            if (!loaded)
            {
                current = Resources.Load<UITheme>("UITheme");
                loaded = true;
            }
            return current;
        }
    }

    // 关闭 Domain Reload 时清掉缓存，重新从 Resources 读取
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        current = null;
        loaded = false;
    }

    // 字体兜底：主题资产尚未生成时仍然可编译/运行
    public Font DisplayFont => displayFont != null ? displayFont : FallbackFont();
    public Font BodyFont => bodyFont != null ? bodyFont : FallbackFont();
    public Font CjkFont => cjkFont != null ? cjkFont : FallbackFont();

    // 根据文本内容选字体：含中日韩字符时用 CJK 字体，否则用主题的 Display/Body。
    // 旧版 Text 不会自动回退，所以调用方必须走这里（UIFactory 已封装）。
    public Font ResolveFont(string text, bool display)
    {
        if (!string.IsNullOrEmpty(text) && ContainsCjk(text))
            return CjkFont;
        return display ? DisplayFont : BodyFont;
    }

    static bool ContainsCjk(string text)
    {
        foreach (char c in text)
        {
            // 常用 CJK、扩展 A、兼容表意、全角标点/字母
            if ((c >= 0x2E80 && c <= 0x9FFF) ||
                (c >= 0xF900 && c <= 0xFAFF) ||
                (c >= 0xFE30 && c <= 0xFE4F) ||
                (c >= 0xFF00 && c <= 0xFFEF))
                return true;
        }
        return false;
    }

    static Font FallbackFont()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    public Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
