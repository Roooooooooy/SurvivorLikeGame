using System;
using System.IO;
using UnityEngine;

namespace RpgGame
{
    // Phase 0 存档：版本化 JSON，原子写入并保留 .bak。
    // 旧的 PlayerPrefs（MetaProgress）暂不迁移，Phase 1 再做单向迁移。
    public static class SaveSystem
    {
        const string FileName = "rpg_save_0.json";
        static SaveData current;

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static SaveData Current
        {
            get
            {
                if (current == null) Load();
                return current;
            }
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    string json = File.ReadAllText(SavePath);
                    current = JsonUtility.FromJson<SaveData>(json);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RPG Save] 读取失败，使用新存档：" + e.Message);
                current = null;
            }

            if (current == null) current = new SaveData();
            if (current.character == null) current.character = new RpgCharacter();
            if (current.world == null) current.world = new WorldState();

            // JsonUtility 对缺失字段会留下 null 列表，这里统一补齐，避免旧档/手改档崩溃。
            if (current.character.inventory == null) current.character.inventory = new System.Collections.Generic.List<string>();
            if (current.character.equipped == null) current.character.equipped = new System.Collections.Generic.List<string>();
            if (current.world.regions == null) current.world.regions = new System.Collections.Generic.List<RegionStateEntry>();
            if (current.world.flags == null) current.world.flags = new System.Collections.Generic.List<string>();
        }

        public static void Save()
        {
            try
            {
                var data = Current;
                data.savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string json = JsonUtility.ToJson(data, true);

                string temp = SavePath + ".tmp";
                string backup = SavePath + ".bak";
                File.WriteAllText(temp, json);

                if (File.Exists(SavePath))
                    File.Replace(temp, SavePath, backup);
                else
                    File.Move(temp, SavePath);
            }
            catch (Exception e)
            {
                Debug.LogError("[RPG Save] 写入失败：" + e.Message);
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
                if (File.Exists(SavePath + ".bak")) File.Delete(SavePath + ".bak");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RPG Save] 删除失败：" + e.Message);
            }

            current = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;
    }
}
