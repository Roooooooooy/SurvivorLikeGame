using UnityEditor;
using UnityEngine;

// UI 主题生成器：配置 Kenney 9-slice 的导入参数，并把字体/Sprite/配色写入 Resources/UITheme.asset。
// 可重复执行：覆盖现有资产，不删除其它资源。
public static class UIThemeGenerator
{
    const string TextureDir = "Assets/Textures/UI";
    const string ThemePath = "Assets/Resources/UITheme.asset";

    static readonly string[] NineSlicePanels =
    {
        TextureDir + "/panel_tan.png",
        TextureDir + "/panel_grey.png",
        TextureDir + "/panel_grey_pressed.png",
        TextureDir + "/frame_blue.png",
        TextureDir + "/frame_list.png",
        TextureDir + "/border_010.png",
        TextureDir + "/border_020.png",
    };

    [MenuItem("Tools/Survivor/Generate UI Theme")]
    public static void Generate()
    {
        foreach (var path in NineSlicePanels)
            ConfigureSprite(path, new Vector4(12f, 12f, 12f, 12f));

        ConfigureSprite(TextureDir + "/panel_inlay.png", Vector4.zero);
        ConfigureSprite(TextureDir + "/frame_blue_inlay.png", Vector4.zero);
        ConfigureSprite(TextureDir + "/bar_white.png", Vector4.zero);

        var theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<UITheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }

        theme.displayFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Cinzel-Bold.ttf");
        theme.bodyFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/VT323-Regular.ttf");
        theme.cjkFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSerifSC-Subset.ttf");

        theme.panelSprite = LoadSprite("panel_tan");
        theme.panelInlaySprite = LoadSprite("panel_inlay");
        theme.buttonSprite = LoadSprite("panel_grey");
        theme.buttonPressedSprite = LoadSprite("panel_grey_pressed");
        theme.barSprite = LoadSprite("bar_white");
        theme.barFrameSprite = LoadSprite("frame_list");
        theme.frameSprite = LoadSprite("border_010");

        // 配色与提案保持一致（显式写入，避免资产被旧默认值覆盖）
        theme.ink = Hex("06080B");
        theme.panel = Hex("0F151D");
        theme.panel2 = Hex("151D27");
        theme.gold = Hex("C9A86A");
        theme.goldHi = Hex("E7D3A0");
        theme.parchment = Hex("D9D2C2");
        theme.muted = Hex("8F8B80");
        theme.blood = Hex("8C2F34");
        theme.bloodHi = Hex("C95157");
        theme.teal = Hex("5F8F8A");
        theme.tealHi = Hex("8FC0B8");
        theme.line = Hex("26313D");

        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        bool ok = theme.displayFont != null && theme.bodyFont != null && theme.cjkFont != null
                  && theme.panelSprite != null && theme.buttonSprite != null && theme.barSprite != null;
        Debug.Log("[UITheme] 生成完成：" + ThemePath + " | 关键资源完整=" + ok);
    }

    static Sprite LoadSprite(string fileName)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TextureDir + "/" + fileName + ".png");
        if (sprite == null) Debug.LogError("[UITheme] 找不到 Sprite: " + fileName);
        return sprite;
    }

    static void ConfigureSprite(string path, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[UITheme] 找不到贴图导入器: " + path);
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = border;
        importer.SaveAndReimport();
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        return c;
    }
}
