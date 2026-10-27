using UnityEditor;
using UnityEngine;

// 一次性编辑器工具：生成金币预制体并写入 EnemyTypes.asset。
// 预制体的精灵/碰撞体/脚本全部由编辑器创建，避免手写 YAML 与 GUID。
public static class CoinPrefabGenerator
{
    const string PrefabPath = "Assets/Prefabs/CoinPrefab.prefab";
    const string SpritePath = "Assets/Textures/ExpGemSprite.png";
    const string EnemyTypesPath = "Assets/Resources/EnemyTypes.asset";

    [MenuItem("Tools/Survivor/Generate Coin Prefab")]
    public static void Generate()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (sprite == null)
        {
            Debug.LogError($"[CoinPrefabGenerator] 找不到精灵：{SpritePath}");
            return;
        }

        var root = new GameObject("CoinPrefab");
        try
        {
            // 与 ExpGemPrefab 保持同样的缩放，贴图复用经验宝石并染成金色
            root.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(1f, 0.84f, 0.25f, 1f);
            sr.sortingOrder = 1;

            var col = root.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;

            var pickup = root.AddComponent<CoinPickup>();
            pickup.value = 1;
            pickup.moveSpeed = 8f;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        AssignToEnemyTypes(prefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CoinPrefabGenerator] 已生成 {PrefabPath} 并写入 EnemyTypes.coinPrefab");
    }

    static void AssignToEnemyTypes(GameObject prefab)
    {
        var table = AssetDatabase.LoadAssetAtPath<EnemyTypeTable>(EnemyTypesPath);
        if (table == null)
        {
            Debug.LogError($"[CoinPrefabGenerator] 找不到敌人表：{EnemyTypesPath}");
            return;
        }

        var so = new SerializedObject(table);
        var prop = so.FindProperty("coinPrefab");
        if (prop == null)
        {
            Debug.LogError("[CoinPrefabGenerator] EnemyTypeTable 缺少 coinPrefab 字段");
            return;
        }

        prop.objectReferenceValue = prefab;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(table);
    }
}
