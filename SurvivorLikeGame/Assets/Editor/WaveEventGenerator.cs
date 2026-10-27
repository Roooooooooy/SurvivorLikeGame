using UnityEditor;
using UnityEngine;

// W5 波次资产生成器：追加精英 / Boss 占位敌人类型，并生成 Resources/WaveEvents 的节奏资产。
// 只走 AssetDatabase，不手写 YAML；可重复执行（同名资产覆盖数值，已存在的类型不重复追加）。
public static class WaveEventGenerator
{
    const string EnemyTypesPath = "Assets/Resources/EnemyTypes.asset";
    const string WaveDir = "Assets/Resources/WaveEvents";
    const string EliteSpritePath = "Assets/Textures/enemy_slime_red.png";
    const string BossSpritePath = "Assets/Textures/enemy_slime_green.png";

    [MenuItem("Tools/Survivor/Generate W5 Wave Assets")]
    public static void Generate()
    {
        if (!EnsureWaveFolder()) return;

        var table = AssetDatabase.LoadAssetAtPath<EnemyTypeTable>(EnemyTypesPath);
        var eliteSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EliteSpritePath);
        var bossSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BossSpritePath);
        if (table == null || eliteSprite == null || bossSprite == null)
        {
            Debug.LogError("[W5][GEN] 缺少 EnemyTypes.asset 或精英贴图，生成中止");
            return;
        }

        EnsureEliteType(table, "Elite", eliteSprite, new Color(1f, 0.55f, 0.4f),
            scale: 2.2f, moveSpeed: 2.2f, maxHealth: 320, damage: 18, expValue: 20, animFps: 8f);
        EnsureEliteType(table, "BossPlaceholder", bossSprite, new Color(0.85f, 0.45f, 1f),
            scale: 3f, moveSpeed: 1.6f, maxHealth: 1200, damage: 30, expValue: 50, animFps: 6f);

        EditorUtility.SetDirty(table);

        // ---------------- 节奏资产 ----------------

        CreateEvent("wave_30_elite", WaveEventType.Elite, 30f, 6, 1, 12f,
            "精英来袭", "重装敌人接近", 3f);
        CreateEvent("wave_60_tide", WaveEventType.SurroundTide, 60f, 0, 20, 12f,
            "包围潮", "四面八方都是敌人", 3f);
        CreateEvent("wave_90_elite", WaveEventType.Elite, 90f, 6, 2, 12f,
            "精英来袭", "两只精英逼近", 3f);
        CreateEvent("wave_120_tide", WaveEventType.SurroundTide, 120f, 0, 28, 12f,
            "包围潮", "敌人从四周涌来", 3f);
        CreateEvent("wave_150_elite", WaveEventType.Elite, 150f, 6, 2, 12f,
            "精英来袭", "防线压力上升", 3f);
        CreateEvent("wave_180_tide", WaveEventType.SurroundTide, 180f, 0, 36, 12f,
            "包围潮", "不要被围住", 3f);
        CreateEvent("wave_210_elite", WaveEventType.Elite, 210f, 6, 3, 12f,
            "精英来袭", "三只精英出现", 3f);
        CreateEvent("wave_240_tide", WaveEventType.SurroundTide, 240f, 0, 44, 12f,
            "包围潮", "最后的包围", 3f);
        // duration 同时作为限时窗口：10 秒内击杀 3 只
        CreateEvent("wave_240_timed_elite", WaveEventType.TimedElite, 240f, 6, 3, 12f,
            "限时精英", "10 秒内击杀 3 只", 10f, bonusExp: 30, requiredKills: 3);
        CreateEvent("wave_270_elite", WaveEventType.Elite, 270f, 6, 3, 12f,
            "精英来袭", "精英成群出现", 3f);
        CreateEvent("wave_300_boss", WaveEventType.Boss, 300f, 7, 1, 14f,
            "BOSS", "最终试炼", 5f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate(table);
    }

    static bool EnsureWaveFolder()
    {
        if (AssetDatabase.IsValidFolder(WaveDir)) return true;
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            Debug.LogError("[W5][GEN] 缺少 Assets/Resources 目录");
            return false;
        }
        AssetDatabase.CreateFolder("Assets/Resources", "WaveEvents");
        return AssetDatabase.IsValidFolder(WaveDir);
    }

    // 已存在同名类型则复用其下标（不改变已有条目），否则追加到末尾。
    // 精英类型沿用普通敌人的贴图与动画参数，只调大尺寸/血量并用颜色区分。
    static void EnsureEliteType(EnemyTypeTable table, string typeName, Sprite sprite, Color color,
        float scale, float moveSpeed, int maxHealth, int damage, int expValue, float animFps)
    {
        var t = table.types.Find(x => x != null && x.typeName == typeName);
        if (t == null)
        {
            t = new EnemyTypeData { typeName = typeName };
            table.types.Add(t);
        }

        t.sprite = sprite;
        t.color = color;
        t.scale = scale;
        t.moveSpeed = moveSpeed;
        t.maxHealth = maxHealth;
        t.damage = damage;
        t.expValue = expValue;
        t.behavior = EnemyBehavior.Chase;
        t.explodeTriggerRange = 1.5f;
        t.explodeRadius = 2.5f;
        t.fuseTime = 0.6f;
        t.attackRange = 7f;
        t.fireInterval = 2f;
        t.projectileSpeed = 6f;
        t.projectileDamage = 8;
        t.animFrames = 10;
        t.animFps = animFps;
        t.hasDeathAnim = true;
    }

    static void CreateEvent(string fileName, WaveEventType type, float triggerTime, int enemyTypeIndex,
        int count, float radius, string title, string subtitle, float duration,
        int bonusExp = 0, int requiredKills = 0)
    {
        string path = WaveDir + "/" + fileName + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<WaveEventData>(path);
        bool isNew = asset == null;
        if (isNew) asset = ScriptableObject.CreateInstance<WaveEventData>();

        asset.eventType = type;
        asset.triggerTime = triggerTime;
        asset.enemyTypeIndex = enemyTypeIndex;
        asset.count = count;
        asset.radius = radius;
        asset.bannerTitle = title;
        asset.bannerSubtitle = subtitle;
        asset.duration = duration;
        asset.bonusExp = bonusExp;
        asset.requiredKills = requiredKills;

        if (isNew) AssetDatabase.CreateAsset(asset, path);
        else EditorUtility.SetDirty(asset);
    }

    static void Validate(EnemyTypeTable table)
    {
        int pass = 0;
        var errors = new System.Collections.Generic.List<string>();

        void Check(bool ok, string msg)
        {
            if (ok) { pass++; Debug.Log("[W5][GEN][PASS] " + msg); }
            else { errors.Add(msg); Debug.LogError("[W5][GEN][FAIL] " + msg); }
        }

        Check(table.types.Count >= 8, "EnemyTypes 至少 8 个类型（现有 0~5 + 精英 6/7）");
        int eliteIndex = table.types.FindIndex(t => t.typeName == "Elite");
        int bossIndex = table.types.FindIndex(t => t.typeName == "BossPlaceholder");
        Check(eliteIndex >= 0 && table.types[eliteIndex].sprite != null,
            "Elite 类型已配置贴图，下标 " + eliteIndex);
        Check(bossIndex >= 0 && table.types[bossIndex].sprite != null,
            "BossPlaceholder 类型已配置贴图，下标 " + bossIndex);
        if (eliteIndex >= 0)
        {
            var e = table.types[eliteIndex];
            Check(e.scale >= 2f && e.scale <= 2.4f && e.maxHealth >= 250 && e.maxHealth <= 400
                && e.expValue >= 15 && e.expValue <= 25 && e.behavior == EnemyBehavior.Chase,
                "Elite 数值符合预期（scale 2.0~2.4 / HP 250~400 / exp 15~25 / Chase）");
        }
        if (bossIndex >= 0)
        {
            var b = table.types[bossIndex];
            Check(b.scale == 3f && b.maxHealth >= 1000 && b.expValue == 50
                && b.behavior == EnemyBehavior.Chase,
                "BossPlaceholder 数值符合预期（scale 3 / HP 1000+ / exp 50 / Chase）");
        }

        var events = Resources.LoadAll<WaveEventData>("WaveEvents");
        Check(events.Length == 11, "Resources/WaveEvents 共 11 个事件资产，实际 " + events.Length);

        int elite = 0, tide = 0, timed = 0, boss = 0;
        foreach (var e in events)
        {
            switch (e.eventType)
            {
                case WaveEventType.Elite: elite++; break;
                case WaveEventType.SurroundTide: tide++; break;
                case WaveEventType.TimedElite: timed++; break;
                case WaveEventType.Boss: boss++; break;
            }
        }
        Check(elite == 5, "Elite 事件 5 个（30/90/150/210/270 秒），实际 " + elite);
        Check(tide == 4, "SurroundTide 事件 4 个（60/120/180/240 秒），实际 " + tide);
        Check(timed == 1, "TimedElite 事件 1 个，实际 " + timed);
        Check(boss == 1, "Boss 事件 1 个，实际 " + boss);

        var timedAsset = System.Array.Find(events, e => e.eventType == WaveEventType.TimedElite);
        Check(timedAsset != null && timedAsset.requiredKills == 3 && timedAsset.bonusExp == 30
            && timedAsset.triggerTime == 240f, "TimedElite：240 秒 / 3 杀 / 30 经验");

        var bossAsset = System.Array.Find(events, e => e.eventType == WaveEventType.Boss);
        Check(bossAsset != null && bossAsset.triggerTime == 300f && bossAsset.bannerTitle == "BOSS"
            && bossAsset.enemyTypeIndex == bossIndex, "Boss：300 秒 / 横幅 BOSS / 指向 BossPlaceholder");

        if (errors.Count == 0)
            Debug.Log("[W5][GEN] 生成完成并通过校验：" + pass + " 项（精英类型 2 个，事件资产 11 个）");
        else
            Debug.LogError("[W5][GEN] 生成结束，但校验失败 " + errors.Count + " 项："
                + string.Join("；", errors));
    }
}
