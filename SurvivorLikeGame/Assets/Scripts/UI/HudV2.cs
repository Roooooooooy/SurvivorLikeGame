using UnityEngine;
using UnityEngine.UI;

// HUD 改版，参考 Vampire Survivors 的信息层级：
// - 顶部通栏细经验条（整屏宽度），左侧挂等级徽章
// - 血条下移，左侧加心形图标
// - 计时器与击杀数各配一枚贴着文字左侧的图标
// 全部在运行时搭建，不改场景结构；HUDController 的引用会被重定向到新元素。
public static class HudV2
{
    public static void Apply(Canvas canvas, UITheme theme, HUDController hud)
    {
        if (canvas == null || theme == null) return;
        var root = canvas.transform;

        // ---------- 顶部通栏经验条 ----------
        var strip = EnsureRect(root, "__XPStrip", new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 18f));
        var stripBg = strip.GetComponent<Image>();
        stripBg.color = new Color(0.06f, 0.08f, 0.11f, 0.96f);
        stripBg.raycastTarget = false;

        var fill = EnsureRect(strip, "XPFill", Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var fillRt = fill;
        fillRt.offsetMin = new Vector2(0f, 3f);
        fillRt.offsetMax = new Vector2(0f, -3f);

        var fillImage = fill.GetComponent<Image>();
        fillImage.sprite = theme.barSprite != null ? theme.barSprite : UIFactory.WhiteSprite;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.color = theme.teal;
        fillImage.raycastTarget = false;

        // 顶部金线，给整屏信息区收口
        var rule = EnsureRect(root, "__XPRule", new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, 2f), new Vector2(0f, -18f));
        var ruleImage = rule.GetComponent<Image>();
        ruleImage.color = theme.WithAlpha(theme.gold, 0.5f);
        ruleImage.raycastTarget = false;

        // 等级徽章：压在经验条左端
        var badge = EnsureRect(strip, "__LevelBadge", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(74f, 14f));
        var badgeImage = badge.GetComponent<Image>();
        badgeImage.sprite = theme.panelSprite != null ? theme.panelSprite : UIFactory.WhiteSprite;
        badgeImage.type = Image.Type.Sliced;
        badgeImage.color = theme.panel;
        badgeImage.raycastTarget = false;

        var levelText = badge.GetComponentInChildren<Text>(true);
        if (levelText == null)
        {
            levelText = UIFactory.CreateText(badge, "Text", "Lv.1", 13, theme.goldHi, TextAnchor.MiddleCenter);
            var rt = levelText.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(2f, 0f);
            rt.offsetMax = new Vector2(-2f, 0f);
        }
        else
        {
            levelText.color = theme.goldHi;
        }

        // 升级时等级徽章闪一下
        var levelFlash = badge.GetComponent<HudLevelFlash>();
        if (levelFlash == null) levelFlash = badge.gameObject.AddComponent<HudLevelFlash>();
        levelFlash.badge = badgeImage;
        levelFlash.baseColor = theme.panel;
        levelFlash.flashColor = theme.goldHi;

        if (hud != null)
        {
            hud.expBarFill = fillImage;
            hud.levelText = levelText;
        }

        // 隐藏场景里原有的第二条经验条
        var oldExp = root.Find("HUD/bg (1)");
        if (oldExp != null) oldExp.gameObject.SetActive(false);

        // ---------- 血条 ----------
        var hpBar = root.Find("HUD/bg") as RectTransform;
        if (hpBar != null)
        {
            hpBar.localPosition = new Vector3(40f, -46f, 0f);
            var heart = EnsureImage(hpBar, "HeartIcon", new Vector2(0f, 0.5f),
                new Vector2(-22f, 0f), new Vector2(26f, 26f));
            heart.sprite = UpgradeIcons.Load("ic_health");
            heart.preserveAspect = true;
            heart.color = Color.white;

            // 25/50/75% 刻度，血条再长也能一眼看出剩余比例
            for (int i = 1; i <= 3; i++)
            {
                float a = i * 0.25f;
                var tick = EnsureRect(hpBar, "__HpTick" + i, new Vector2(a, 0f), new Vector2(a, 1f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1f, 0f));
                var tickImage = tick.GetComponent<Image>();
                tickImage.color = theme.WithAlpha(theme.gold, 0.35f);
                tickImage.raycastTarget = false;
                if (hpBar.childCount > 1) tick.SetSiblingIndex(1);
            }

            // 低血量脉冲
            var hpFillTransform = hpBar.Find("hp");
            var hpFillImage = hpFillTransform != null
                ? hpFillTransform.GetComponent<Image>()
                : (hud != null ? hud.healthBarFill : null);
            var pulse = hpBar.GetComponent<HudHealthPulse>();
            if (pulse == null) pulse = hpBar.gameObject.AddComponent<HudHealthPulse>();
            pulse.fill = hpFillImage;
            pulse.heart = heart;
            pulse.baseColor = theme.blood;
            pulse.hiColor = theme.bloodHi;
        }

        // ---------- 计时器 / 击杀数图标（贴着文字左缘） ----------
        AttachIcon(root.Find("TimerText"), "Icon", "ic_time");
        AttachIcon(root.Find("KillCountText"), "Icon", "ic_skull");

        var kills = root.Find("KillCountText");
        if (kills != null && kills.GetComponent<HudTextPunch>() == null)
            kills.gameObject.AddComponent<HudTextPunch>();

        // ---------- 金币计数 ----------
        var existingCoins = root.Find("CoinsText");
        var coins = existingCoins != null ? existingCoins.GetComponent<Text>() : null;
        if (coins == null)
            coins = UIFactory.CreateText(root, "CoinsText", "0", 18, theme.goldHi, TextAnchor.MiddleCenter);
        var coinRt = coins.rectTransform;
        coinRt.anchorMin = coinRt.anchorMax = new Vector2(1f, 1f);
        coinRt.pivot = new Vector2(1f, 1f);
        coinRt.anchoredPosition = new Vector2(-78f, -46f);
        coinRt.sizeDelta = new Vector2(120f, 24f);
        AttachIcon(coins.transform, "Icon", "ic_coin");
        if (coins.GetComponent<HudTextPunch>() == null)
            coins.gameObject.AddComponent<HudTextPunch>();
        if (hud != null) hud.coinText = coins;
    }

    static void AttachIcon(Transform textTransform, string name, string iconName)
    {
        var text = textTransform != null ? textTransform.GetComponent<Text>() : null;
        if (text == null) return;

        var image = EnsureImage(textTransform, name, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(26f, 26f));
        image.sprite = UpgradeIcons.Load(iconName);
        image.preserveAspect = true;

        var follower = image.GetComponent<TextIconFollower>();
        if (follower == null) follower = image.gameObject.AddComponent<TextIconFollower>();
        follower.target = text;
        follower.gap = 10f;
    }

    static RectTransform EnsureRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 anchoredPos, Vector2 size)
    {
        var existing = parent.Find(name) as RectTransform;
        RectTransform rt;
        if (existing != null)
        {
            rt = existing;
        }
        else
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.SetAsLastSibling();
        }

        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
        return rt;
    }

    static Image EnsureImage(Transform parent, string name, Vector2 anchor,
        Vector2 anchoredPos, Vector2 size)
    {
        var existing = parent.Find(name) as RectTransform;
        RectTransform rt;
        if (existing != null)
        {
            rt = existing;
        }
        else
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
        }

        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var image = rt.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }
}