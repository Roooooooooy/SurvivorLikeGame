using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// W1 武器资产生成器：批量创建 4 把武器的 prefab、弹体/特效 prefab 与升级资产。
// 只用 AssetDatabase / PrefabUtility 生成，不手写 YAML；可重复执行（覆盖同名资产）。
public static class WeaponAssetGenerator
{
    private const string PrefabDir = "Assets/Prefabs";
    private const string UpgradeDir = "Assets/Resources/Upgrades";

    private static readonly string[] WeaponKeys = { "pierce", "lightning", "nova", "boomerang" };

    // ---------------- 生成入口 ----------------

    [MenuItem("Tools/Survivor/Generate W1 Weapon Assets")]
    public static void Generate()
    {
        var dagger = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/weapon_dagger.png");
        var hammer = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/weapon_hammer.png");
        var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        if (dagger == null || hammer == null || knob == null)
        {
            Debug.LogError("[W1][GEN] 缺少精灵资源（weapon_dagger / weapon_hammer / Knob），生成中止");
            return;
        }

        // 弹体与特效 prefab
        var arrowPrefab = CreateArrowPrefab(dagger);
        var boomerangPrefab = CreateBoomerangPrefab(hammer);
        var ringPrefab = CreateBurstPrefab("Weapon_NovaRing", knob,
            new Color(0.65f, 0.5f, 1f, 0.85f), 0.2f, 7f, 0.35f, 15);
        var sparkPrefab = CreateBurstPrefab("Weapon_ArcSpark", knob,
            new Color(0.6f, 0.9f, 1f, 0.95f), 0.3f, 1.1f, 0.16f, 21);

        // 4 把武器本体 prefab（根节点挂对应武器脚本）
        CreateWeaponPrefab<PierceWeapon>("Weapon_Pierce", WeaponTarget.Pierce, w =>
        {
            w.damage = 12;
            w.attackInterval = 0.9f;
            w.arrowPrefab = arrowPrefab;
            w.arrowSpeed = 18f;
            w.baseArrowCount = 1;
            w.spreadAngle = 12f;
            w.arrowLifetime = 1.5f;
        });

        CreateWeaponPrefab<LightningWeapon>("Weapon_Lightning", WeaponTarget.Lightning, w =>
        {
            w.damage = 9;
            w.attackInterval = 1.4f;
            w.baseChainCount = 3;
            w.maxChainCount = 5;
            w.chainRange = 6f;
            w.jumpRange = 5f;
            w.impactRadius = 0.9f;
            w.sparkPrefab = sparkPrefab;
        });

        CreateWeaponPrefab<NovaWeapon>("Weapon_Nova", WeaponTarget.Nova, w =>
        {
            w.damage = 14;
            w.attackInterval = 2.5f;
            w.radius = 3.5f;
            w.baseNovaCount = 1;
            w.ringPrefab = ringPrefab;
        });

        CreateWeaponPrefab<BoomerangWeapon>("Weapon_Boomerang", WeaponTarget.Boomerang, w =>
        {
            w.damage = 8;
            w.attackInterval = 1.2f;
            w.boomerangPrefab = boomerangPrefab;
            w.baseBoomerangCount = 1;
            w.spreadAngle = 20f;
            w.outSpeed = 11f;
            w.outTime = 0.5f;
            w.returnSpeed = 15f;
            w.maxLifetime = 3f;
        });

        // 每把武器：1 个“获得” + 伤害 / 数量 / 攻击间隔 3 个升级
        var pierced = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Weapon_Pierce.prefab");
        var lightning = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Weapon_Lightning.prefab");
        var nova = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Weapon_Nova.prefab");
        var boomerang = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Weapon_Boomerang.prefab");

        CreateWeaponUpgrades("pierce", "Pierce Arrow", WeaponTarget.Pierce, pierced);
        CreateWeaponUpgrades("lightning", "Chain Lightning", WeaponTarget.Lightning, lightning);
        CreateWeaponUpgrades("nova", "Nova Burst", WeaponTarget.Nova, nova);
        CreateWeaponUpgrades("boomerang", "Boomerang", WeaponTarget.Boomerang, boomerang);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ValidateAll(pierced, lightning, nova, boomerang);
    }

    // ---------------- prefab 构建 ----------------

    static GameObject CreateArrowPrefab(Sprite sprite)
    {
        var go = new GameObject("Weapon_PierceArrow");
        go.transform.localScale = new Vector3(0.45f, 0.45f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(0.75f, 1f, 0.85f, 1f);
        sr.sortingOrder = 5;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var dealer = go.AddComponent<DamageDealer>();
        dealer.isContinuous = true;   // 穿透箭：持续判定、同一敌人限频
        dealer.destroyOnHit = false;  // 命中不销毁

        go.AddComponent<PierceProjectile>();
        return SavePrefab(go, PrefabDir + "/Weapon_PierceArrow.prefab");
    }

    static GameObject CreateBoomerangPrefab(Sprite sprite)
    {
        var go = new GameObject("Weapon_BoomerangProjectile");
        go.transform.localScale = new Vector3(0.6f, 0.6f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(1f, 0.85f, 0.4f, 1f);
        sr.sortingOrder = 6;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.angularDamping = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var dealer = go.AddComponent<DamageDealer>();
        dealer.isContinuous = true;   // 往返持续伤害
        dealer.destroyOnHit = false;

        go.AddComponent<BoomerangProjectile>();
        return SavePrefab(go, PrefabDir + "/Weapon_BoomerangProjectile.prefab");
    }

    static GameObject CreateBurstPrefab(string name, Sprite sprite, Color color,
        float startScale, float endScale, float duration, int sortingOrder)
    {
        var go = new GameObject(name);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;

        var burst = go.AddComponent<SpriteBurstEffect>();
        burst.startScale = startScale;
        burst.endScale = endScale;
        burst.duration = duration;

        return SavePrefab(go, PrefabDir + "/" + name + ".prefab");
    }

    static GameObject CreateWeaponPrefab<T>(string name, WeaponTarget target, System.Action<T> setup)
        where T : AdvancedWeaponBase
    {
        var go = new GameObject(name);
        var weapon = go.AddComponent<T>();
        weapon.target = target; // prefab 里也写一份，作为运行时 Awake 之前的兜底
        setup?.Invoke(weapon);
        return SavePrefab(go, PrefabDir + "/" + name + ".prefab");
    }

    static GameObject SavePrefab(GameObject go, string path)
    {
        var asset = PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        return asset;
    }

    // ---------------- 升级资产 ----------------

    static void CreateWeaponUpgrades(string key, string displayName, WeaponTarget target, GameObject weaponPrefab)
    {
        // 获得武器：maxStack=1，weight≈5
        CreateUpgrade("up_" + key + "_grant", key + "_grant",
            "Acquire " + displayName, "Unlock " + displayName + " as a new weapon",
            5, 1, UpgradeStat.WeaponGrant, 0f, target, weaponPrefab);

        CreateUpgrade("up_" + key + "_damage", key + "_damage",
            displayName + " Damage +20%", displayName + " deals 20% more damage",
            10, 4, UpgradeStat.WeaponDamagePct, 0.2f, target, null);

        CreateUpgrade("up_" + key + "_count", key + "_count",
            displayName + " Count +1", "Improves " + displayName + " count",
            8, 2, UpgradeStat.WeaponCountFlat, 1f, target, null);

        CreateUpgrade("up_" + key + "_rate", key + "_rate",
            displayName + " Rate +15%", displayName + " attacks 15% faster",
            10, 3, UpgradeStat.WeaponIntervalPct, 0.15f, target, null);
    }

    static UpgradeData CreateUpgrade(string fileName, string id, string displayName, string description,
        int weight, int maxStack, UpgradeStat stat, float value, WeaponTarget weapon, GameObject weaponPrefab)
    {
        string path = UpgradeDir + "/" + fileName + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<UpgradeData>(path);
        bool isNew = asset == null;
        if (isNew) asset = ScriptableObject.CreateInstance<UpgradeData>();

        asset.id = id;
        asset.displayName = displayName;
        asset.description = description;
        asset.weight = weight;
        asset.maxStack = maxStack;
        asset.effects.Clear();
        asset.effects.Add(new UpgradeEffect
        {
            stat = stat,
            value = value,
            weapon = weapon,
            weaponPrefab = weaponPrefab,
        });

        if (isNew) AssetDatabase.CreateAsset(asset, path);
        else EditorUtility.SetDirty(asset);

        return asset;
    }

    // ---------------- 生成后校验 ----------------

    static void ValidateAll(GameObject pierce, GameObject lightning, GameObject nova, GameObject boomerang)
    {
        int pass = 0;
        var errors = new List<string>();

        void Check(bool ok, string msg)
        {
            if (ok) { pass++; Debug.Log("[W1][GEN][PASS] " + msg); }
            else { errors.Add(msg); Debug.LogError("[W1][GEN][FAIL] " + msg); }
        }

        var pw = pierce != null ? pierce.GetComponent<PierceWeapon>() : null;
        var lw = lightning != null ? lightning.GetComponent<LightningWeapon>() : null;
        var nw = nova != null ? nova.GetComponent<NovaWeapon>() : null;
        var bw = boomerang != null ? boomerang.GetComponent<BoomerangWeapon>() : null;

        Check(pw != null && pw.target == WeaponTarget.Pierce && pw.arrowPrefab != null,
            "Weapon_Pierce.prefab：PierceWeapon / target=Pierce / arrowPrefab 已赋值");
        Check(lw != null && lw.target == WeaponTarget.Lightning && lw.sparkPrefab != null,
            "Weapon_Lightning.prefab：LightningWeapon / target=Lightning / sparkPrefab 已赋值");
        Check(nw != null && nw.target == WeaponTarget.Nova && nw.ringPrefab != null,
            "Weapon_Nova.prefab：NovaWeapon / target=Nova / ringPrefab 已赋值");
        Check(bw != null && bw.target == WeaponTarget.Boomerang && bw.boomerangPrefab != null,
            "Weapon_Boomerang.prefab：BoomerangWeapon / target=Boomerang / boomerangPrefab 已赋值");

        Check(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Weapon_PierceArrow.prefab")
            ?.GetComponent<DamageDealer>()?.isContinuous == true, "Weapon_PierceArrow.prefab：DamageDealer 为持续判定");
        Check(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Weapon_BoomerangProjectile.prefab")
            ?.GetComponent<BoomerangProjectile>() != null, "Weapon_BoomerangProjectile.prefab：BoomerangProjectile 存在");

        var targets = new Dictionary<string, WeaponTarget>
        {
            { "pierce", WeaponTarget.Pierce },
            { "lightning", WeaponTarget.Lightning },
            { "nova", WeaponTarget.Nova },
            { "boomerang", WeaponTarget.Boomerang },
        };
        var prefabs = new Dictionary<string, GameObject>
        {
            { "pierce", pierce }, { "lightning", lightning }, { "nova", nova }, { "boomerang", boomerang },
        };

        foreach (var key in WeaponKeys)
        {
            var target = targets[key];
            var prefab = prefabs[key];

            CheckUpgrade("up_" + key + "_grant", key + "_grant", UpgradeStat.WeaponGrant, target, prefab, Check);
            CheckUpgrade("up_" + key + "_damage", key + "_damage", UpgradeStat.WeaponDamagePct, target, null, Check);
            CheckUpgrade("up_" + key + "_count", key + "_count", UpgradeStat.WeaponCountFlat, target, null, Check);
            CheckUpgrade("up_" + key + "_rate", key + "_rate", UpgradeStat.WeaponIntervalPct, target, null, Check);
        }

        if (errors.Count == 0)
            Debug.Log("[W1][GEN] 生成完成并通过校验：" + pass + " 项。"
                + "武器 prefab 4 个，弹体/特效 prefab 4 个，升级资产 16 个（Resources/Upgrades）");
        else
            Debug.LogError("[W1][GEN] 生成结束，但校验失败 " + errors.Count + " 项：" + string.Join("；", errors));
    }

    static void CheckUpgrade(string fileName, string id, UpgradeStat stat, WeaponTarget weapon,
        GameObject expectedPrefab, System.Action<bool, string> check)
    {
        string path = UpgradeDir + "/" + fileName + ".asset";
        var data = AssetDatabase.LoadAssetAtPath<UpgradeData>(path);

        check(data != null, "资产存在 " + path);
        if (data == null) return;

        check(data.id == id, fileName + "：id = " + id);
        check(data.effects.Count == 1 && data.effects[0].stat == stat, fileName + "：效果类型 = " + stat);
        if (data.effects.Count == 0) return;

        if (stat == UpgradeStat.WeaponGrant)
        {
            check(data.effects[0].weaponPrefab == expectedPrefab, fileName + "：weaponPrefab 指向对应武器 prefab");
            check(data.maxStack == 1, fileName + "：maxStack = 1");
            check(data.weight >= 4 && data.weight <= 6, fileName + "：weight ≈ 5");
        }
        else
        {
            check(data.effects[0].weapon == weapon, fileName + "：effects[].weapon = " + weapon);
        }
    }

    // ---------------- 运行期验证（Play Mode） ----------------

    private static int verifyDelayFrames;

    [MenuItem("Tools/Survivor/Verify W1 Weapons (Play Mode)")]
    public static void VerifyInPlayMode()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[W1][VERIFY] 请先进入 Play Mode 再执行该菜单");
            return;
        }

        Time.timeScale = 1f;

        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (player == null)
        {
            Debug.LogError("[W1][VERIFY][FAIL] 场景中找不到 PlayerController");
            return;
        }

        // 先在玩家周围铺敌人；等两帧让 EnemySimulation 重建空间网格后再做伤害校验
        var sim = EnemySimulation.Instance;
        if (sim != null)
        {
            Vector2 pp = player.transform.position;
            int typeCount = Mathf.Max(1, sim.TypeCount);
            for (int i = 0; i < 48; i++)
            {
                float angle = i * 2.39996f; // 黄金角，避免落点重叠
                float dist = 1.2f + (i % 8) * 0.45f;
                sim.Spawn(i % typeCount, pp + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist);
            }
        }

        verifyDelayFrames = 2;
        EditorApplication.update -= TickVerify;
        EditorApplication.update += TickVerify;
    }

    static void TickVerify()
    {
        if (!Application.isPlaying)
        {
            EditorApplication.update -= TickVerify;
            return;
        }

        if (verifyDelayFrames > 0)
        {
            verifyDelayFrames--;
            return;
        }

        EditorApplication.update -= TickVerify;
        RunVerification();
    }

    static void RunVerification()
    {
        int pass = 0, fail = 0;

        void Check(bool ok, string msg)
        {
            if (ok) { pass++; Debug.Log("[W1][VERIFY][PASS] " + msg); }
            else { fail++; Debug.LogError("[W1][VERIFY][FAIL] " + msg); }
        }

        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        Check(player != null, "场景中存在 PlayerController");
        if (player == null) { FinishVerify(pass, fail); return; }

        var wm = player.GetComponent<WeaponManager>();
        Check(wm != null, "玩家身上有 WeaponManager");
        if (wm == null) { FinishVerify(pass, fail); return; }

        var sim = EnemySimulation.Instance;
        Check(sim != null && sim.TypeCount > 0, "EnemySimulation 已初始化");

        var buildOptions = typeof(WeaponManager).GetMethod("BuildOptions",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var applyEffect = typeof(WeaponManager).GetMethod("ApplyEffect",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var applyUpgrade = typeof(WeaponManager).GetMethod("ApplyUpgrade",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Check(buildOptions != null && applyEffect != null && applyUpgrade != null,
            "可反射 WeaponManager 的升级构造/应用方法");

        if (buildOptions == null || applyEffect == null || applyUpgrade == null)
        {
            FinishVerify(pass, fail);
            return;
        }

        // 1) 尚未获得时：4 个“获得”资产都能进入升级选项
        var drawnBefore = DrawIds(buildOptions, wm, 400);
        foreach (var key in WeaponKeys)
            Check(drawnBefore.Contains(key + "_grant"), "升级池可抽出获得项: " + key + "_grant");

        // 2) 通过真实 WeaponGrant 效果授予武器（等价于玩家在升级界面选中）
        foreach (var key in WeaponKeys)
        {
            var grant = LoadUpgrade("up_" + key + "_grant");
            bool ok = grant != null && grant.effects.Count > 0 && grant.effects[0].weaponPrefab != null;
            Check(ok, "加载获得资产且 weaponPrefab 有效: " + key);
            if (ok) applyEffect.Invoke(wm, new object[] { grant.effects[0] });
        }

        var pierce = wm.activeWeapons.OfType<PierceWeapon>().FirstOrDefault();
        var lightning = wm.activeWeapons.OfType<LightningWeapon>().FirstOrDefault();
        var nova = wm.activeWeapons.OfType<NovaWeapon>().FirstOrDefault();
        var boomerang = wm.activeWeapons.OfType<BoomerangWeapon>().FirstOrDefault();

        Check(pierce != null && pierce.target == WeaponTarget.Pierce, "穿透箭已挂到玩家，target=Pierce");
        Check(lightning != null && lightning.target == WeaponTarget.Lightning, "闪电链已挂到玩家，target=Lightning");
        Check(nova != null && nova.target == WeaponTarget.Nova, "新星爆发已挂到玩家，target=Nova");
        Check(boomerang != null && boomerang.target == WeaponTarget.Boomerang, "回旋镖已挂到玩家，target=Boomerang");

        Check(pierce != null && pierce.transform.parent == wm.transform, "穿透箭实例是玩家子物体");
        Check(lightning != null && lightning.transform.parent == wm.transform, "闪电链实例是玩家子物体");
        Check(nova != null && nova.transform.parent == wm.transform, "新星爆发实例是玩家子物体");
        Check(boomerang != null && boomerang.transform.parent == wm.transform, "回旋镖实例是玩家子物体");

        // 重复获得不会挂第二把（HasWeaponType 判重）
        var grantAgain = LoadUpgrade("up_pierce_grant");
        if (grantAgain != null && grantAgain.effects.Count > 0)
            applyEffect.Invoke(wm, new object[] { grantAgain.effects[0] });
        Check(wm.activeWeapons.OfType<PierceWeapon>().Count() == 1, "重复获得不会挂第二把穿透箭");

        // 3) 持有武器后：12 个武器升级资产都能进入升级选项
        var drawnAfter = DrawIds(buildOptions, wm, 600);
        foreach (var key in WeaponKeys)
        {
            Check(drawnAfter.Contains(key + "_damage"), "升级池可抽出: " + key + "_damage");
            Check(drawnAfter.Contains(key + "_count"), "升级池可抽出: " + key + "_count");
            Check(drawnAfter.Contains(key + "_rate"), "升级池可抽出: " + key + "_rate");
        }

        // 4) 数量升级真实生效（覆盖 4 把武器）
        CheckCountUpgrade(pierce, "pierce", applyUpgrade, wm, Check);
        CheckCountUpgrade(lightning, "lightning", applyUpgrade, wm, Check);
        CheckCountUpgrade(nova, "nova", applyUpgrade, wm, Check);
        CheckCountUpgrade(boomerang, "boomerang", applyUpgrade, wm, Check);

        // 5) 伤害 / 攻击间隔升级（以穿透箭为例）
        if (pierce != null)
        {
            var damageUp = LoadUpgrade("up_pierce_damage");
            if (damageUp != null)
            {
                int beforeDamage = pierce.damage;
                applyUpgrade.Invoke(wm, new object[] { damageUp });
                Check(pierce.damage > beforeDamage,
                    "穿透箭伤害升级生效: " + beforeDamage + " -> " + pierce.damage);
            }

            var rateUp = LoadUpgrade("up_pierce_rate");
            if (rateUp != null)
            {
                float beforeInterval = pierce.attackInterval;
                applyUpgrade.Invoke(wm, new object[] { rateUp });
                Check(pierce.attackInterval < beforeInterval,
                    "穿透箭攻击间隔升级生效: " + beforeInterval + " -> " + pierce.attackInterval);
            }
        }

        // 6) 自动攻击链路：把计时器推满后调用 Update，确认会走 Attack
        var updateMethod = typeof(WeaponBase).GetMethod("Update",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var timerField = typeof(WeaponBase).GetField("attackTimer",
            BindingFlags.NonPublic | BindingFlags.Instance);

        void ForceAttack(AdvancedWeaponBase weapon)
        {
            if (weapon == null || updateMethod == null || timerField == null) return;
            weapon.attackInterval = 0.001f;
            timerField.SetValue(weapon, 999f);
            updateMethod.Invoke(weapon, null);
            Check(weapon.attackCount > 0, weapon.GetType().Name + " 自动攻击已触发（Update→Attack）");
        }

        // 攻击前清掉残留计数，便于观察本次攻击结果
        if (pierce != null) { pierce.lastHitCount = 0; pierce.attackCount = 0; }
        if (lightning != null) { lightning.lastHitCount = 0; lightning.attackCount = 0; }
        if (nova != null) { nova.lastHitCount = 0; nova.attackCount = 0; }
        if (boomerang != null) { boomerang.attackCount = 0; }

        ForceAttack(pierce);
        ForceAttack(lightning);
        ForceAttack(nova);
        ForceAttack(boomerang);

        // 7) 攻击结果：弹体生成 / 伤害命中
        int arrows = UnityEngine.Object.FindObjectsByType<PierceProjectile>(FindObjectsSortMode.None).Length;
        Check(arrows >= 2, "穿透箭一次射出至少 2 支（基础 1 + 数量升级 1），实际 " + arrows);

        int boomerangs = UnityEngine.Object.FindObjectsByType<BoomerangProjectile>(FindObjectsSortMode.None).Length;
        Check(boomerangs >= 2, "回旋镖一次掷出至少 2 个（基础 1 + 数量升级 1），实际 " + boomerangs);

        Check(lightning != null && lightning.lastHitCount > 0,
            "闪电链造成伤害（命中 " + (lightning != null ? lightning.lastHitCount : 0) + "）");
        Check(nova != null && nova.lastHitCount > 0,
            "新星爆发造成伤害（命中 " + (nova != null ? nova.lastHitCount : 0) + "）");

        FinishVerify(pass, fail);
    }

    static void CheckCountUpgrade(AdvancedWeaponBase weapon, string key,
        MethodInfo applyUpgrade, WeaponManager wm, System.Action<bool, string> check)
    {
        if (weapon == null)
        {
            check(false, key + "：数量升级校验时武器为空");
            return;
        }

        var up = LoadUpgrade("up_" + key + "_count");
        if (up == null)
        {
            check(false, key + "：找不到 up_" + key + "_count");
            return;
        }

        int before = weapon.CountUpgrades;
        applyUpgrade.Invoke(wm, new object[] { up });
        check(weapon.CountUpgrades == before + 1,
            key + " 数量升级生效：countUpgrades " + before + " -> " + weapon.CountUpgrades);
    }

    static HashSet<string> DrawIds(MethodInfo buildOptions, WeaponManager wm, int rolls)
    {
        var ids = new HashSet<string>();
        for (int i = 0; i < rolls; i++)
        {
            var options = buildOptions.Invoke(wm, new object[] { 3 }) as List<UpgradeOption>;
            if (options == null) break;
            foreach (var option in options)
                if (!option.IsEvolution && option.upgrade != null)
                    ids.Add(option.upgrade.id);
        }
        return ids;
    }

    static UpgradeData LoadUpgrade(string fileName)
        => AssetDatabase.LoadAssetAtPath<UpgradeData>(UpgradeDir + "/" + fileName + ".asset");

    static void FinishVerify(int pass, int fail)
    {
        if (fail == 0)
            Debug.Log("[W1][VERIFY] 全部通过：" + pass + " 项检查（4 把武器可获得、可升级、可自动攻击）");
        else
            Debug.LogError("[W1][VERIFY] 验证结束：PASS=" + pass + "，FAIL=" + fail + "（详见上方 FAIL 日志）");
    }
}
