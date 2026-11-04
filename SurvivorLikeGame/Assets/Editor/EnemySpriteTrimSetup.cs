using System.IO;
using UnityEditor;
using UnityEngine;

// 分析敌人图集里每一帧的 alpha 包围盒，把“单帧内容矩形”写回 EnemyTypes.asset。
// 运行时 EnemySimRenderer 用它收紧 billboard quad，裁掉四周透明留白，
// 减少 alpha clip 之前仍会执行的片元着色（overdraw）。
//
// 图集本身是单张大图（spriteMode=1），所以这里直接读源 PNG 解码，
// 不依赖贴图的 Read/Write 设置，也不会在运行时保留 CPU 副本。
public static class EnemySpriteTrimSetup
{
    // 低于该 alpha 的像素视为透明（1/255 级别，只过滤边缘噪点）
    const byte AlphaThreshold = 8;

    // 内容矩形向外扩的像素数：要盖住 shader 的 1.25 texel 描边
    const int PaddingTexels = 2;

    [MenuItem("Tools/HD2D/9. Analyze Enemy Sprite Trim")]
    public static void Analyze()
    {
        var guids = AssetDatabase.FindAssets("t:EnemyTypeTable");
        if (guids.Length == 0)
        {
            Debug.LogError("[EnemyTrim] 找不到 EnemyTypeTable 资产");
            return;
        }

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var table = AssetDatabase.LoadAssetAtPath<EnemyTypeTable>(path);
            if (table == null) continue;

            int analyzed = 0;
            float oldArea = 0f;
            float newArea = 0f;

            for (int i = 0; i < table.types.Count; i++)
            {
                var t = table.types[i];
                var full = new Vector4(0f, 0f, 1f, 1f);

                if (t.sprite == null || t.sprite.texture == null)
                {
                    t.contentRect = full;
                    continue;
                }

                if (!TryAnalyzeSprite(t, out var rect, out float ratio))
                {
                    t.contentRect = full;
                    continue;
                }

                t.contentRect = rect;
                analyzed++;
                oldArea += 1f;
                newArea += ratio;

                Debug.Log($"[EnemyTrim] {t.typeName}: rect=({rect.x:F3}, {rect.y:F3}, {rect.z:F3}, {rect.w:F3}) " +
                          $"保留面积 {ratio * 100f:F1}%");
            }

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            float saved = oldArea > 0f ? (1f - newArea / oldArea) * 100f : 0f;
            Debug.Log($"[EnemyTrim] {path}: 已分析 {analyzed}/{table.types.Count} 个类型，" +
                      $"理论片元面积减少约 {saved:F1}%");
        }
    }

    static bool TryAnalyzeSprite(EnemyTypeData t, out Vector4 rect, out float ratio)
    {
        rect = new Vector4(0f, 0f, 1f, 1f);
        ratio = 1f;

        var texture = t.sprite.texture;
        string path = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Debug.LogWarning($"[EnemyTrim] {t.typeName}: 找不到源贴图 {path}");
            return false;
        }

        var temp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!temp.LoadImage(File.ReadAllBytes(path)))
            {
                Debug.LogWarning($"[EnemyTrim] {t.typeName}: 无法解码 {path}");
                return false;
            }

            int frames = Mathf.Max(1, t.animFrames);
            int rows = t.hasDeathAnim ? 2 : 1;
            float cellW = temp.width / (float)frames;
            float cellH = temp.height / (float)rows;

            var pixels = temp.GetPixels32();
            float minU = 1f, minV = 1f, maxU = 0f, maxV = 0f;
            bool any = false;

            for (int y = 0; y < temp.height; y++)
            {
                int row = Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) / cellH), 0, rows - 1);
                float v = (y + 0.5f - row * cellH) / cellH;

                int yOffset = y * temp.width;
                for (int x = 0; x < temp.width; x++)
                {
                    if (pixels[yOffset + x].a <= AlphaThreshold) continue;

                    int col = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) / cellW), 0, frames - 1);
                    float u = (x + 0.5f - col * cellW) / cellW;

                    if (u < minU) minU = u;
                    if (u > maxU) maxU = u;
                    if (v < minV) minV = v;
                    if (v > maxV) maxV = v;
                    any = true;
                }
            }

            if (!any)
            {
                Debug.LogWarning($"[EnemyTrim] {t.typeName}: 整张图都是透明的，保持整帧");
                return false;
            }

            // 向外扩一点，保证描边与 alpha clip 边缘不会被 quad 切掉
            float padU = Mathf.Min(PaddingTexels / cellW, 0.25f);
            float padV = Mathf.Min(PaddingTexels / cellH, 0.25f);
            minU = Mathf.Max(0f, minU - padU);
            minV = Mathf.Max(0f, minV - padV);
            maxU = Mathf.Min(1f, maxU + padU);
            maxV = Mathf.Min(1f, maxV + padV);

            float w = Mathf.Max(0.002f, maxU - minU);
            float h = Mathf.Max(0.002f, maxV - minV);
            rect = new Vector4(minU, minV, w, h);
            ratio = w * h;
            return true;
        }
        finally
        {
            Object.DestroyImmediate(temp);
        }
    }
}
