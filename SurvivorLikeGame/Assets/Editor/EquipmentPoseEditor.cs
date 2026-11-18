using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RpgGame;

// 装备姿态编辑器：按 动作 × 方向 × 帧 预览身体和装备，拖动装备微调像素偏移。
//
// 打开：工具 / 角色扮演 / 装备姿态编辑器
// 数据：写入 EquipItemData.frameTracks；每个 动作×方向 都有一条完整帧表。
// 预览画布与运行时一致：64×64，装备图集按方向取帧。
public class EquipmentPoseEditor : EditorWindow
{
    const int FrameSize = 64;
    const int GearFrameCount = 8;

    static readonly string[] DirNames =
    {
        "west", "north-west", "north", "north-east",
        "east", "south-east", "south", "south-west",
    };

    static readonly string[] DirLabels =
    {
        "西", "西北", "北", "东北", "东", "东南", "南", "西南",
    };

    static readonly string[] MotionLabels = { "待机", "跑步", "攻击" };

    [SerializeField] EquipItemData item;
    [SerializeField] GearMotion motion = GearMotion.Idle;
    [SerializeField] int direction = 6;   // 默认南（正面）
    [SerializeField] int frame;
    [SerializeField] float zoom = 6f;
    [SerializeField] bool onionSkin = true;
    [SerializeField] bool showGear = true;

    readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();

    bool dragging;
    Vector2 dragStartMouse;
    Vector2 dragStartOffset;

    [MenuItem("工具/角色扮演/装备姿态编辑器")]
    static void Open()
    {
        var window = GetWindow<EquipmentPoseEditor>("装备姿态编辑器");
        window.minSize = new Vector2(980f, 680f);
    }

    void OnEnable()
    {
        titleContent = new GUIContent("装备姿态编辑器");
        Selection.selectionChanged += OnSelectionChanged;

        // 打开窗口时自动选第一件装备，避免空白页。
        if (item == null)
        {
            var all = EquipDatabase.All;
            if (all != null && all.Length > 0) item = all[0];
        }

        EnsureFullTracks();
    }

    void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
    }

    void OnSelectionChanged()
    {
        if (Selection.activeObject is EquipItemData data)
        {
            item = data;
            frame = 0;
            textureCache.Clear();
            EnsureFullTracks();
            Repaint();
        }
    }

    void OnGUI()
    {
        DrawToolbar();
        if (item == null)
        {
            EditorGUILayout.HelpBox(
                "选择 Resources/Equipment 下的装备资产，或在 Project 里选中一个 EquipItemData。",
                MessageType.Info);
            return;
        }

        EditorGUILayout.BeginHorizontal();

        var previewRect = GUILayoutUtility.GetRect(100f, 100f,
            GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        DrawPreview(previewRect);

        EditorGUILayout.BeginVertical(GUILayout.Width(330f));
        DrawSidePanel();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal();
        item = (EquipItemData)EditorGUILayout.ObjectField(
            "装备资产", item, typeof(EquipItemData), false);

        if (GUILayout.Button("定位", GUILayout.Width(60f)) && item != null)
            EditorGUIUtility.PingObject(item);

        EditorGUILayout.EndHorizontal();

        if (item == null) return;

        int newMotion = GUILayout.Toolbar((int)motion, MotionLabels);
        if (newMotion != (int)motion)
        {
            motion = (GearMotion)newMotion;
            frame = Mathf.Clamp(frame, 0, FrameCount(motion) - 1);
            Repaint();
        }

        int newDirection = GUILayout.Toolbar(direction, DirLabels);
        if (newDirection != direction)
        {
            direction = newDirection;
            Repaint();
        }

        frame = EditorGUILayout.IntSlider("帧", frame, 0, FrameCount(motion) - 1);

        using (new EditorGUILayout.HorizontalScope())
        {
            zoom = EditorGUILayout.Slider("缩放", zoom, 3f, 12f);
            onionSkin = GUILayout.Toggle(onionSkin, "洋葱皮", GUILayout.Width(70f));
            showGear = GUILayout.Toggle(showGear, "显示装备", GUILayout.Width(80f));
        }
    }

    void DrawPreview(Rect rect)
    {
        EditorGUI.DrawRect(rect, new Color(0.08f, 0.10f, 0.13f));

        float canvasSize = FrameSize * zoom;
        float centerX = rect.x + rect.width * 0.5f;
        float centerY = rect.y + rect.height * 0.5f;
        var bodyRect = new Rect(
            centerX - canvasSize * 0.5f,
            centerY - canvasSize * 0.5f,
            canvasSize, canvasSize);

        DrawGrid(bodyRect);

        if (onionSkin)
        {
            if (frame > 0) DrawBody(bodyRect, frame - 1, 0.16f);
            if (frame + 1 < FrameCount(motion)) DrawBody(bodyRect, frame + 1, 0.16f);
        }

        DrawBody(bodyRect, frame, 1f);
        if (showGear) DrawGear(bodyRect);

        HandleDrag(rect);

        var style = EditorStyles.whiteLabel;
        GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, 460f, 20f),
            DirLabels[direction] + "  /  " + MotionLabels[(int)motion] +
            "  /  第 " + frame + " 帧", style);
        var offset = CurrentOffset;
        GUI.Label(new Rect(rect.x + 10f, rect.y + 30f, 460f, 20f),
            "偏移 (" + offset.x.ToString("0") + ", " + offset.y.ToString("0") + ") 像素", style);

        if (GetBodyTexture() == null)
        {
            GUI.Label(new Rect(rect.x + 10f, rect.y + rect.height - 28f, rect.width - 20f, 20f),
                "当前方向没有该动作素材", EditorStyles.whiteLabel);
        }
    }

    void DrawGrid(Rect rect)
    {
        var line = new Color(1f, 1f, 1f, 0.08f);
        var border = new Color(0.78f, 0.66f, 0.42f, 0.85f);

        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), border);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), border);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), border);
        EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), border);

        // 每 8 像素一条参考线，方便判断相对位置。
        for (int i = 1; i < 8; i++)
        {
            float t = i / 8f;
            EditorGUI.DrawRect(new Rect(rect.x + rect.width * t, rect.y, 1f, rect.height), line);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height * t, rect.width, 1f), line);
        }

        // 画布中心十字
        var cross = new Color(0.4f, 0.8f, 1f, 0.7f);
        float cx = rect.x + rect.width * 0.5f;
        float cy = rect.y + rect.height * 0.5f;
        EditorGUI.DrawRect(new Rect(cx - 5f, cy, 11f, 1f), cross);
        EditorGUI.DrawRect(new Rect(cx, cy - 5f, 1f, 11f), cross);
    }

    void DrawBody(Rect rect, int frameIndex, float alpha)
    {
        var texture = GetBodyTexture();
        if (texture == null) return;

        int count = Mathf.Min(FrameCount(motion), texture.width / FrameSize);
        if (count <= 0) return;

        int index = Mathf.Clamp(frameIndex, 0, count - 1);
        var coords = new Rect(
            index * (float)FrameSize / texture.width, 0f,
            (float)FrameSize / texture.width, 1f);

        var old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTextureWithTexCoords(rect, texture, coords, true);
        GUI.color = old;
    }

    void DrawGear(Rect bodyRect)
    {
        var texture = GetGearTexture();
        if (texture == null) return;

        int count = Mathf.Min(GearFrameCount, texture.width / FrameSize);
        if (count <= 0) return;

        int index = direction % count;
        var coords = new Rect(
            index * (float)FrameSize / texture.width, 0f,
            (float)FrameSize / texture.width, 1f);

        var offset = CurrentOffset;
        // GUI 的 y 向下，装备数据的 y 向上。
        var gearRect = new Rect(
            bodyRect.x + offset.x * zoom,
            bodyRect.y - offset.y * zoom,
            bodyRect.width,
            bodyRect.height);

        var old = GUI.color;
        GUI.color = item != null ? item.tint : Color.white;
        GUI.DrawTextureWithTexCoords(gearRect, texture, coords, true);
        GUI.color = old;
    }

    void HandleDrag(Rect previewRect)
    {
        var evt = Event.current;

        if (evt.type == EventType.MouseDown && evt.button == 0 &&
            previewRect.Contains(evt.mousePosition))
        {
            dragging = true;
            dragStartMouse = evt.mousePosition;
            dragStartOffset = CurrentOffset;
            evt.Use();
            return;
        }

        if (!dragging) return;

        if (evt.type == EventType.MouseDrag && evt.button == 0)
        {
            var delta = evt.mousePosition - dragStartMouse;
            var offset = dragStartOffset + new Vector2(delta.x / zoom, -delta.y / zoom);
            SetCurrentOffset(offset, false);
            Repaint();
            evt.Use();
            return;
        }

        if (evt.type == EventType.MouseUp)
        {
            dragging = false;
            SaveAsset();
            evt.Use();
        }
    }

    void DrawSidePanel()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("当前帧偏移（像素）", EditorStyles.boldLabel);

        var current = CurrentOffset;
        EditorGUI.BeginChangeCheck();
        var edited = EditorGUILayout.Vector2Field("偏移", current);
        if (EditorGUI.EndChangeCheck())
            SetCurrentOffset(edited, true);

        EditorGUILayout.Space(4f);
        if (GUILayout.Button("按身体帧自动生成默认偏移"))
            AutoFillFromBody();

        if (GUILayout.Button("重置当前帧为默认偏移"))
            ResetCurrentFrame();

        if (GUILayout.Button("把当前偏移设为默认值"))
            SetDefaultToCurrent();

        EditorGUILayout.Space(4f);
        if (GUILayout.Button("应用到本动作的所有帧"))
            ApplyToAction();

        if (GUILayout.Button("应用到所有方向 / 当前帧"))
            ApplyToAllDirections();

        if (GUILayout.Button("应用到所有方向 / 所有帧"))
            ApplyToEverything();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("资产设置", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        var defaultOffset = EditorGUILayout.Vector2Field("默认偏移", item.offset);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(item, "修改装备默认偏移");
            item.offset = new Vector2(
                Mathf.Round(defaultOffset.x), Mathf.Round(defaultOffset.y));
            EditorUtility.SetDirty(item);
            SaveAsset();
        }

        EditorGUI.BeginChangeCheck();
        var visualRoot = EditorGUILayout.TextField("外观路径", item.visualRoot);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(item, "修改装备外观路径");
            item.visualRoot = visualRoot;
            EditorUtility.SetDirty(item);
            textureCache.Clear();
            SaveAsset();
        }

        EditorGUI.BeginChangeCheck();
        var tint = EditorGUILayout.ColorField("色调", item.tint);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(item, "修改装备色调");
            item.tint = tint;
            EditorUtility.SetDirty(item);
            SaveAsset();
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("逐帧条目",
            item.CountFrameOffsets() + " / " + EquipItemData.TotalFrameOffsetCount);

        if (GUILayout.Button("所有帧恢复为默认偏移"))
            ResetAllTracks();

        if (GUILayout.Button("保存资产"))
            SaveAsset();

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "在左侧预览里按住左键拖动装备即可调整当前帧。\n" +
            "每个动作/方向/帧都有独立默认值，直接拖动并保存即可。\n" +
            "「按身体帧自动生成默认偏移」会覆盖所有帧，适合先铺一版基准。",
            MessageType.Info);

        if (item.HasVisual && GetGearTexture() == null)
        {
            EditorGUILayout.HelpBox(
                "找不到装备图集 Resources/" + item.visualRoot +
                "\n请检查 visualRoot 和文件是否已导入。",
                MessageType.Warning);
        }
    }

    int FrameCount(GearMotion m)
    {
        return EquipItemData.FrameCount(m);
    }

    // 打开或切换装备时补齐 3 种动作 × 8 个方向的完整帧表。
    void EnsureFullTracks()
    {
        if (item == null) return;

        Undo.RecordObject(item, "补齐装备逐帧轨道");
        if (!item.EnsureAllFrameTracks()) return;

        EditorUtility.SetDirty(item);
        AssetDatabase.SaveAssets();
    }

    Vector2 CurrentOffset
    {
        get
        {
            if (item == null) return Vector2.zero;
            return item.ResolveOffset(motion, direction, frame, item.offset);
        }
    }

    void SetCurrentOffset(Vector2 offset, bool save)
    {
        if (item == null) return;

        offset = new Vector2(Mathf.Round(offset.x), Mathf.Round(offset.y));
        Undo.RecordObject(item, "修改装备当前帧偏移");
        item.SetFrameOffset(motion, direction, frame, offset);
        EditorUtility.SetDirty(item);

        if (save) SaveAsset();
        Repaint();
    }

    void AutoFillFromBody()
    {
        if (item == null) return;

        Undo.RecordObject(item, "按身体帧自动填充装备偏移");
        EquipmentOffsetBaker.Fill(item);
        EditorUtility.SetDirty(item);
        SaveAsset();
        Repaint();
    }

    void ResetCurrentFrame()
    {
        if (item == null) return;

        Undo.RecordObject(item, "重置装备当前帧偏移");
        item.ResetFrameOffset(motion, direction, frame);
        EditorUtility.SetDirty(item);
        SaveAsset();
        Repaint();
    }

    void SetDefaultToCurrent()
    {
        if (item == null) return;

        var current = CurrentOffset;
        Undo.RecordObject(item, "设置装备默认偏移");
        item.offset = current;
        item.SetFrameOffset(motion, direction, frame, current);
        EditorUtility.SetDirty(item);
        SaveAsset();
        Repaint();
    }

    void ApplyToAction()
    {
        if (item == null) return;

        var current = CurrentOffset;
        int count = FrameCount(motion);
        Undo.RecordObject(item, "应用装备偏移到本动作");
        for (int f = 0; f < count; f++)
            item.SetFrameOffset(motion, direction, f, current);
        EditorUtility.SetDirty(item);
        SaveAsset();
        Repaint();
    }

    void ApplyToAllDirections()
    {
        if (item == null) return;

        var current = CurrentOffset;
        Undo.RecordObject(item, "应用装备偏移到所有方向");
        for (int d = 0; d < 8; d++)
            item.SetFrameOffset(motion, d, frame, current);
        EditorUtility.SetDirty(item);
        SaveAsset();
        Repaint();
    }

    void ApplyToEverything()
    {
        if (item == null) return;

        var current = CurrentOffset;
        int count = FrameCount(motion);
        Undo.RecordObject(item, "应用装备偏移到所有帧");
        for (int d = 0; d < 8; d++)
        {
            for (int f = 0; f < count; f++)
                item.SetFrameOffset(motion, d, f, current);
        }
        EditorUtility.SetDirty(item);
        SaveAsset();
        Repaint();
    }

    void ResetAllTracks()
    {
        if (item == null) return;

        Undo.RecordObject(item, "重置所有装备逐帧偏移");
        item.ResetAllFrameOffsets();
        EditorUtility.SetDirty(item);
        SaveAsset();
        Repaint();
    }

    void SaveAsset()
    {
        if (item != null) EditorUtility.SetDirty(item);
        AssetDatabase.SaveAssets();
    }

    Texture2D GetBodyTexture()
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

        return LoadTexture(root + "/" + prefix + "_" + DirNames[direction]);
    }

    Texture2D GetGearTexture()
    {
        if (item == null || !item.HasVisual) return null;
        return LoadTexture(item.visualRoot);
    }

    Texture2D LoadTexture(string path)
    {
        if (textureCache.TryGetValue(path, out var cached))
            return cached;

        var texture = Resources.Load<Texture2D>(path);
        textureCache[path] = texture;
        return texture;
    }
}
