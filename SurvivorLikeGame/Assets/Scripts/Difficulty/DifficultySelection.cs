using System.Collections.Generic;
using UnityEngine;

// 难度选择与解锁。
// 契约：选中难度存 PlayerPrefs（跨局保留）；解锁键约定为 "difficulty_<id>"。
// 资产统一放在 Resources/Difficulties 下，All 按 id 升序返回，保证 UI 顺序稳定。
public static class DifficultySelection
{
    // 选中难度 id 的 PlayerPrefs 键
    public const string SelectedKey = "survivor.difficulty.selected";

    // 难度资产所在目录（Resources 相对路径）
    const string ResourcesPath = "Difficulties";

    static DifficultyData[] all;

    // 全部难度资产：懒加载一次，按 id 升序排序
    public static DifficultyData[] All
    {
        get
        {
            if (all == null) LoadAll();
            return all;
        }
    }

    // 当前难度：优先取存档里的选择；存档缺失或资产已被移除时回退到默认难度。
    // 默认难度取 id 升序中第一个 unlockedByDefault 的档位（即“普通”），
    // 全部档位都需要解锁时才回退到排序后的第一个，避免一上来就选中未解锁难度。
    public static DifficultyData Selected
    {
        get
        {
            var list = All;
            if (list.Length == 0) return null;

            string savedId = PlayerPrefs.GetString(SelectedKey, string.Empty);
            if (!string.IsNullOrEmpty(savedId))
            {
                foreach (var data in list)
                {
                    if (data != null && data.id == savedId) return data;
                }
            }

            foreach (var data in list)
            {
                if (data != null && data.unlockedByDefault) return data;
            }
            return list[0];
        }
    }

    public static string SelectedId => Selected != null ? Selected.id : string.Empty;

    // 选择并持久化：仅接受非空 id；具体是否解锁由 UI 层调用 IsUnlocked 判断。
    public static void Select(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        PlayerPrefs.SetString(SelectedKey, id);
        PlayerPrefs.Save(); // 立即落盘，避免异常退出丢失选择
    }

    // 是否已解锁：默认开放的档位直接可用，其余读 MetaProgress 的通用解锁键。
    public static bool IsUnlocked(DifficultyData data)
    {
        if (data == null) return false;
        if (data.unlockedByDefault) return true;
        if (string.IsNullOrEmpty(data.id)) return false;
        return MetaProgress.IsUnlocked("difficulty_" + data.id);
    }

    // 关闭域重载时静态数组会跨 Play 保留；清空缓存，下次访问重新从 Resources 读取。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        all = null;
    }

    static void LoadAll()
    {
        var assets = Resources.LoadAll<DifficultyData>(ResourcesPath);
        var list = new List<DifficultyData>(assets.Length);
        foreach (var data in assets)
        {
            if (data == null || string.IsNullOrEmpty(data.id)) continue;
            list.Add(data);
        }
        // 按 id 字典序排序，保证 Resources.LoadAll 的返回顺序变化不影响显示顺序
        list.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
        all = list.ToArray();
    }
}
