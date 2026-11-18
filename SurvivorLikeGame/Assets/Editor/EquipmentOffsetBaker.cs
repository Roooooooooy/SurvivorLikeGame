using UnityEditor;
using UnityEngine;
using RpgGame;

// 按身体帧自动生成装备的逐帧默认偏移。
//
// 思路：装备图集是按 idle 第 0 帧烘焙的；对每个动作/方向/帧，
// 计算该帧头部锚点相对 idle 第 0 帧的位移，叠加到装备的默认偏移上。
// 这样帽子会跟着跑步的上下起伏、攻击的前倾一起动。
//
// 菜单：工具 / 角色扮演 / 自动填充装备逐帧偏移
// 也可从装备姿态编辑器里的按钮调用。
public static class EquipmentOffsetBaker
{
    const int FrameSize = 64;

    static readonly string[] DirNames =
    {
        "west", "north-west", "north", "north-east",
        "east", "south-east", "south", "south-west",
    };

    [MenuItem("工具/角色扮演/自动填充装备逐帧偏移")]
    static void AutoFillAllEquipment()
    {
        var all = EquipDatabase.All;
        int filled = 0;

        for (int i = 0; i < all.Length; i++)
        {
            var item = all[i];
            if (item == null || !item.HasVisual) continue;

            Undo.RecordObject(item, "自动填充装备逐帧偏移");
            Fill(item);
            EditorUtility.SetDirty(item);
            filled++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Equip] 已按身体帧自动填充 " + filled + " 件装备的逐帧默认偏移");
    }

    // 为一件装备写入完整的 3 动作 × 8 方向 × N 帧默认偏移。
    public static void Fill(EquipItemData item)
    {
        if (item == null) return;

        item.EnsureAllFrameTracks();
        Vector2 baseOffset = item.offset;

        for (int d = 0; d < EquipItemData.DirectionCount; d++)
        {
            // 烘焙基准：idle 第 0 帧的头部锚点。
            var idle = LoadBody(GearMotion.Idle, d);
            if (idle == null) continue;

            var idlePixels = idle.GetPixels32();
            if (!TryGetHeadAnchor(idlePixels, idle.width, 0, out var bakedAnchor))
                continue;

            for (int m = 0; m < 3; m++)
            {
                var motion = (GearMotion)m;
                int frameCount = EquipItemData.FrameCount(motion);
                var body = LoadBody(motion, d);

                if (body == null)
                {
                    // 该方向没有这个动作的素材（例如攻击的北/东北/西北），先给默认值。
                    for (int f = 0; f < frameCount; f++)
                        item.SetFrameOffset(motion, d, f, baseOffset);
                    continue;
                }

                var pixels = body.GetPixels32();
                for (int f = 0; f < frameCount; f++)
                {
                    if (!TryGetHeadAnchor(pixels, body.width, f, out var anchor))
                    {
                        item.SetFrameOffset(motion, d, f, baseOffset);
                        continue;
                    }

                    // 身体头部相对烘焙帧往上移动多少，装备就往上移动多少。
                    var offset = new Vector2(
                        Mathf.Round(baseOffset.x + anchor.x - bakedAnchor.x),
                        Mathf.Round(baseOffset.y + anchor.y - bakedAnchor.y));

                    item.SetFrameOffset(motion, d, f, offset);
                }
            }
        }
    }

    // 头部锚点：取整帧 alpha 包围盒上半部分的中心 x 和顶部 y。
    // 纹理坐标 y 向上（Unity 的 GetPixels32 原点在左下）。
    static bool TryGetHeadAnchor(Color32[] pixels, int textureWidth, int frameIndex,
                                 out Vector2 anchor)
    {
        anchor = Vector2.zero;

        int x0 = frameIndex * FrameSize;
        if (x0 + FrameSize > textureWidth) return false;

        int minX = FrameSize, maxX = -1, minY = FrameSize, maxY = -1;
        for (int y = 0; y < FrameSize; y++)
        {
            int row = y * textureWidth + x0;
            for (int x = 0; x < FrameSize; x++)
            {
                if (pixels[row + x].a <= 8) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < 0 || maxY < 0) return false;

        // 上半部分（从包围盒顶部往下 55%），排除尾巴和下半身。
        int headMinY = minY + Mathf.FloorToInt((maxY - minY + 1) * 0.45f);
        int headMinX = FrameSize, headMaxX = -1, headMaxY = -1;

        for (int y = headMinY; y <= maxY; y++)
        {
            int row = y * textureWidth + x0;
            for (int x = minX; x <= maxX; x++)
            {
                if (pixels[row + x].a <= 8) continue;
                if (x < headMinX) headMinX = x;
                if (x > headMaxX) headMaxX = x;
                if (y > headMaxY) headMaxY = y;
            }
        }

        if (headMaxX < 0)
        {
            anchor = new Vector2((minX + maxX + 1) * 0.5f, maxY + 1f);
            return true;
        }

        anchor = new Vector2(
            (headMinX + headMaxX + 1) * 0.5f,
            headMaxY + 1f);
        return true;
    }

    static Texture2D LoadBody(GearMotion motion, int direction)
    {
        string root;
        string prefix;

        switch (motion)
        {
            case GearMotion.Run:
                root = "Player/Cat8Dir/Running";
                prefix = "run";
                break;
            case GearMotion.Attack:
                root = "Player/Cat8Dir/Attack";
                prefix = "attack";
                break;
            default:
                root = "Player/Cat8Dir/Idle";
                prefix = "idle";
                break;
        }

        if (direction < 0 || direction >= DirNames.Length) return null;
        return Resources.Load<Texture2D>(root + "/" + prefix + "_" + DirNames[direction]);
    }
}
