using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RpgGame
{
    // 装备位置编辑用的动作分类，与身体图集一一对应。
    public enum GearMotion
    {
        Idle = 0,
        Run = 1,
        Attack = 2,
    }

    // 旧版稀疏逐帧数据。保留字段名用于把旧资产迁移到 GearFrameTrack。
    [Serializable]
    public class GearFrameOffset
    {
        public GearMotion motion;
        public int direction;
        public int frame;
        public Vector2 offset;
    }

    // 一个“动作 × 方向”的完整帧偏移表。
    // frames.Length 固定为 FrameCount(motion)：待机/跑步 8，攻击 16。
    [Serializable]
    public class GearFrameTrack
    {
        public GearMotion motion;
        public int direction;
        public Vector2[] frames = new Vector2[0];
    }

    // 像素装备图层的取帧方式。
    // Static：每个方向只有一张静态图，适合帽子、背包。
    // BodyFrames：按身体当前帧取图，适合披风、尾巴等需要跟随动画的部件。
    public enum GearSyncMode
    {
        Static = 0,
        BodyFrames = 1,
    }

    // 一件装备的完整定义：存档 id、槽位、数值加成、像素外观路径。
    // 资源放在 Resources/Equipment 下，由 EquipDatabase 统一加载。
    [CreateAssetMenu(fileName = "EquipItem", menuName = "角色扮演/装备")]
    public class EquipItemData : ScriptableObject
    {
        [Header("标识")]
        public string id;
        public string displayName;
        [TextArea] public string description;
        public EquipSlot slot = EquipSlot.Head;

        [Header("UI")]
        public Sprite icon;
        public int goldValue = 5;      // 重复掉落时的折算金币

        [Header("属性加成")]
        public int attack;
        public int defense;
        public int maxHealth;
        public float moveSpeed;

        [Header("像素外观（Phase B）")]
        [Tooltip("Resources 路径，例如 Player/Cat8Dir/Equipment/hat_wizard")]
        public string visualRoot;
        public GearSyncMode syncMode = GearSyncMode.Static;
        [Tooltip("默认像素偏移（64×64 画布像素）；逐帧偏移会覆盖它")]
        public Vector2 offset;
        public Color tint = Color.white;

        [Header("逐帧位置（由装备姿态编辑器写入）")]
        [Tooltip("每个 动作×方向 一条完整轨道；每帧都有自己的默认偏移")]
        public List<GearFrameTrack> frameTracks = new List<GearFrameTrack>();

        // 旧版稀疏数据，工具第一次打开时会迁移到 frameTracks。
        [HideInInspector]
        public List<GearFrameOffset> frameOffsets = new List<GearFrameOffset>();

        public const int DirectionCount = 8;
        public const int TotalFrameOffsetCount = 256; // 8×8 + 8×8 + 8×16

        public bool HasVisual => !string.IsNullOrEmpty(visualRoot);

        public static int FrameCount(GearMotion motion)
        {
            return motion == GearMotion.Attack ? 16 : 8;
        }

        public GearFrameTrack FindTrack(GearMotion motion, int direction)
        {
            if (frameTracks == null) return null;

            for (int i = 0; i < frameTracks.Count; i++)
            {
                var track = frameTracks[i];
                if (track != null && track.motion == motion && track.direction == direction)
                    return track;
            }

            return null;
        }

        // 取当前动作/方向/帧的最终像素偏移。
        // 完整轨道优先；旧数据次之；最后回退到全局默认值。
        public Vector2 ResolveOffset(GearMotion motion, int direction, int frame, Vector2 fallback)
        {
            var track = FindTrack(motion, direction);
            if (track != null && track.frames != null &&
                frame >= 0 && frame < track.frames.Length)
                return track.frames[frame];

            if (frameOffsets != null)
            {
                for (int i = 0; i < frameOffsets.Count; i++)
                {
                    var entry = frameOffsets[i];
                    if (entry != null &&
                        entry.motion == motion &&
                        entry.direction == direction &&
                        entry.frame == frame)
                        return entry.offset;
                }
            }

            return fallback;
        }

        // 写入某一帧；轨道不存在或长度不足时自动补齐，新增帧用全局默认值。
        public void SetFrameOffset(GearMotion motion, int direction, int frame, Vector2 value)
        {
            int count = FrameCount(motion);
            if (frame < 0 || frame >= count) return;

            var track = GetOrCreateTrack(motion, direction, count);
            EnsureTrackLength(track, count);
            track.frames[frame] = value;

            // 旧数据已经迁移到轨道后就不再保留重复条目。
            RemoveLegacyEntry(motion, direction, frame);
        }

        // 把某一帧恢复成该装备的默认偏移。
        public void ResetFrameOffset(GearMotion motion, int direction, int frame)
        {
            SetFrameOffset(motion, direction, frame, offset);
        }

        // 把所有动作/方向/帧都恢复成默认偏移。
        public void ResetAllFrameOffsets()
        {
            EnsureAllFrameTracks();

            if (frameTracks != null)
            {
                for (int i = 0; i < frameTracks.Count; i++)
                {
                    var track = frameTracks[i];
                    if (track == null || track.frames == null) continue;

                    for (int f = 0; f < track.frames.Length; f++)
                        track.frames[f] = offset;
                }
            }

            if (frameOffsets != null) frameOffsets.Clear();
        }

        // 保证 3 种动作 × 8 个方向都有完整帧表，并把旧稀疏数据迁移进来。
        // 返回 true 表示资产内容发生了变化，调用方需要 SetDirty。
        public bool EnsureAllFrameTracks()
        {
            bool changed = false;

            if (frameTracks == null)
            {
                frameTracks = new List<GearFrameTrack>();
                changed = true;
            }

            for (int m = 0; m < 3; m++)
            {
                var motion = (GearMotion)m;
                int count = FrameCount(motion);

                for (int d = 0; d < DirectionCount; d++)
                {
                    var track = FindTrack(motion, d);
                    if (track == null)
                    {
                        frameTracks.Add(new GearFrameTrack
                        {
                            motion = motion,
                            direction = d,
                            frames = NewDefaultFrames(count),
                        });
                        changed = true;
                    }
                    else if (EnsureTrackLength(track, count))
                    {
                        changed = true;
                    }
                }
            }

            // 旧数据迁移：覆盖对应帧后清空旧列表。
            if (frameOffsets != null && frameOffsets.Count > 0)
            {
                for (int i = 0; i < frameOffsets.Count; i++)
                {
                    var entry = frameOffsets[i];
                    if (entry == null) continue;

                    var track = FindTrack(entry.motion, entry.direction);
                    if (track == null || track.frames == null) continue;
                    if (entry.frame >= 0 && entry.frame < track.frames.Length)
                        track.frames[entry.frame] = entry.offset;
                }

                frameOffsets.Clear();
                changed = true;
            }

            return changed;
        }

        public int CountFrameOffsets()
        {
            int count = frameOffsets != null ? frameOffsets.Count : 0;

            if (frameTracks != null)
            {
                for (int i = 0; i < frameTracks.Count; i++)
                {
                    var track = frameTracks[i];
                    if (track != null && track.frames != null)
                        count += track.frames.Length;
                }
            }

            return count;
        }

        public void ClearFrameOffsets()
        {
            if (frameTracks != null) frameTracks.Clear();
            if (frameOffsets != null) frameOffsets.Clear();
        }

        GearFrameTrack GetOrCreateTrack(GearMotion motion, int direction, int count)
        {
            var track = FindTrack(motion, direction);
            if (track != null) return track;

            track = new GearFrameTrack
            {
                motion = motion,
                direction = direction,
                frames = NewDefaultFrames(count),
            };

            if (frameTracks == null) frameTracks = new List<GearFrameTrack>();
            frameTracks.Add(track);
            return track;
        }

        bool EnsureTrackLength(GearFrameTrack track, int count)
        {
            if (track.frames != null && track.frames.Length == count) return false;

            var resized = NewDefaultFrames(count);
            if (track.frames != null)
            {
                int copy = Mathf.Min(track.frames.Length, count);
                for (int i = 0; i < copy; i++)
                    resized[i] = track.frames[i];
            }

            track.frames = resized;
            return true;
        }

        Vector2[] NewDefaultFrames(int count)
        {
            var frames = new Vector2[count];
            for (int i = 0; i < count; i++) frames[i] = offset;
            return frames;
        }

        void RemoveLegacyEntry(GearMotion motion, int direction, int frame)
        {
            if (frameOffsets == null) return;

            for (int i = frameOffsets.Count - 1; i >= 0; i--)
            {
                var entry = frameOffsets[i];
                if (entry != null &&
                    entry.motion == motion &&
                    entry.direction == direction &&
                    entry.frame == frame)
                    frameOffsets.RemoveAt(i);
            }
        }

        // 面板里的单行摘要，只列出非零属性。
        public string BuildStatSummary()
        {
            var sb = new StringBuilder();
            if (attack != 0) Append(sb, "攻击", attack);
            if (defense != 0) Append(sb, "防御", defense);
            if (maxHealth != 0) Append(sb, "生命", maxHealth);
            if (moveSpeed != 0f) Append(sb, "移速", moveSpeed);
            return sb.Length > 0 ? sb.ToString() : "无属性加成";
        }

        static void Append(StringBuilder sb, string label, int value)
        {
            if (sb.Length > 0) sb.Append("  ");
            sb.Append(label).Append(' ').Append(value >= 0 ? "+" : "").Append(value);
        }

        static void Append(StringBuilder sb, string label, float value)
        {
            if (sb.Length > 0) sb.Append("  ");
            sb.Append(label).Append(' ').Append(value >= 0f ? "+" : "").Append(value.ToString("0.0"));
        }

        void OnValidate()
        {
            if (string.IsNullOrEmpty(id)) id = name;
            if (string.IsNullOrEmpty(displayName)) displayName = id;
        }
    }
}
