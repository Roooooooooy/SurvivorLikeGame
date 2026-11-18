using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RpgGame;

// 像素主角的装备图层。
// 身体仍由 Hd2dPixelPlayer 播放整张精灵表；这里只按当前槽位和朝向叠加一张 64×64 的装备图。
//
// 资源规范（Phase B）：
//   Resources/Player/Cat8Dir/Equipment/{visualRoot}.png
//   横向 8 帧 × 64×64，帧顺序与 Hd2dPixelPlayer.DirNames 一致。
//   画布必须与身体帧相同，枢轴直接复用身体 sprite 的脚底 pivot。
//
// 没有对应贴图时该槽位自动隐藏，不影响现有角色表现。
public class Hd2dEquipmentVisuals : MonoBehaviour
{
    const int LayerSortingOrder = 1;
    const float CameraOffset = 0.01f;

    static readonly EquipSlot[] Slots =
    {
        EquipSlot.Head, EquipSlot.Body, EquipSlot.Back, EquipSlot.Weapon,
    };

    Hd2dPixelPlayer owner;
    Shader litShader;
    bool initialized;

    readonly Dictionary<EquipSlot, SpriteRenderer> layers = new Dictionary<EquipSlot, SpriteRenderer>();
    readonly Dictionary<EquipSlot, EquipItemData> equipped = new Dictionary<EquipSlot, EquipItemData>();
    readonly Dictionary<EquipSlot, Sprite[]> slotFrames = new Dictionary<EquipSlot, Sprite[]>();

    readonly Dictionary<Texture2D, Sprite[]> spriteCache = new Dictionary<Texture2D, Sprite[]>();
    readonly Dictionary<Texture2D, Material> materialCache = new Dictionary<Texture2D, Material>();

    public void Initialize(Hd2dPixelPlayer player)
    {
        owner = player;
        litShader = Shader.Find("HD2D/SpriteLit");
        if (litShader == null)
            Debug.LogWarning("[Hd2dEquipmentVisuals] 找不到 HD2D/SpriteLit，装备层不会显示");

        for (int i = 0; i < Slots.Length; i++)
        {
            var slot = Slots[i];
            var go = new GameObject("Gear_" + slot);
            go.transform.SetParent(player.transform, false);
            // 已经跟随 Hd2dPixelPlayer 的 billboard，不要再被 Hd2dSpriteBridge
            // 当成普通精灵做第二次代理——否则会被额外抬升并盖掉原图层。
            go.AddComponent<Hd2dProxyMarker>();

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.sortingOrder = LayerSortingOrder;
            renderer.enabled = false;

            layers[slot] = renderer;
        }

        initialized = true;
        Refresh();
    }

    void OnEnable()
    {
        EquipmentService.Changed += Refresh;
    }

    void OnDisable()
    {
        EquipmentService.Changed -= Refresh;
    }

    // 穿脱装备后重新绑定贴图；贴图加载失败时隐藏该槽位。
    void Refresh()
    {
        if (!initialized || owner == null) return;

        for (int i = 0; i < Slots.Length; i++)
        {
            var slot = Slots[i];
            var item = EquipmentService.GetEquipped(slot);
            equipped[slot] = item;

            var layer = layers[slot];
            slotFrames[slot] = null;

            if (item == null || !item.HasVisual || litShader == null)
            {
                layer.enabled = false;
                continue;
            }

            var frames = LoadFrames(item.visualRoot);
            if (frames == null || frames.Length == 0)
            {
                layer.enabled = false;
                continue;
            }

            slotFrames[slot] = frames;
            layer.enabled = true;
            layer.sprite = frames[0];
            layer.sharedMaterial = MaterialFor(frames[0].texture);
            layer.color = item.tint;
        }
    }

    void LateUpdate()
    {
        if (!initialized || owner == null) return;

        int direction = owner.Direction;
        bool hide = owner.HideEquipment;

        for (int i = 0; i < Slots.Length; i++)
        {
            var slot = Slots[i];
            var layer = layers[slot];

            // 闪避/死亡没有装备偏移数据，先隐藏，动作结束后自动恢复。
            if (hide)
            {
                layer.enabled = false;
                continue;
            }

            var frames = slotFrames[slot];
            if (frames == null || frames.Length == 0) continue;
            if (!layer.enabled) layer.enabled = true;

            layer.sprite = frames[direction % frames.Length];

            var item = equipped[slot];
            // 逐帧偏移按 64×64 画布像素存储；换算成局部世界单位后跟随角色缩放。
            var offset = item != null
                ? item.ResolveOffset(owner.Motion, direction, owner.FrameIndex, item.offset)
                : Vector2.zero;
            float pixelsPerUnit = Mathf.Max(0.01f, owner.PixelsPerUnit);
            layer.transform.localPosition = new Vector3(
                offset.x / pixelsPerUnit,
                offset.y / pixelsPerUnit,
                CameraOffset);

            // 受击闪红/死亡变灰由身体颜色驱动，装备层跟着一起变。
            var tint = item != null ? item.tint : Color.white;
            layer.color = tint * owner.BodyColor;
        }
    }

    Sprite[] LoadFrames(string visualRoot)
    {
        var texture = Resources.Load<Texture2D>(visualRoot);
        if (texture == null) return null;

        if (spriteCache.TryGetValue(texture, out var cached))
            return cached;

        int frameWidth = owner.FramePixelWidth;
        int frameHeight = owner.FramePixelHeight;
        if (texture.width < frameWidth || texture.height < frameHeight)
        {
            Debug.LogWarning("[Hd2dEquipmentVisuals] 装备图集尺寸异常 " +
                             texture.width + "x" + texture.height + "：" + visualRoot);
            return null;
        }

        int count = Mathf.Min(8, texture.width / Mathf.Max(1, frameWidth));
        var pivot = owner.BodyPivotNormalized;
        var frames = new Sprite[count];

        for (int i = 0; i < count; i++)
        {
            var rect = new Rect(i * frameWidth, 0f, frameWidth, frameHeight);
            frames[i] = Sprite.Create(texture, rect, pivot, owner.PixelsPerUnit, 0,
                SpriteMeshType.FullRect);
            frames[i].name = visualRoot + "_" + i;
        }

        spriteCache[texture] = frames;
        return frames;
    }

    Material MaterialFor(Texture2D texture)
    {
        if (materialCache.TryGetValue(texture, out var cached))
            return cached;

        var material = new Material(litShader)
        {
            name = "GearLit (Runtime)",
            hideFlags = HideFlags.HideAndDontSave,
        };
        material.SetTexture("_MainTex", texture);
        material.SetFloat("_Cutoff", 0.5f);
        material.SetFloat("_AmbientBoost", 0.85f);
        material.SetFloat("_OutlineWidth", 0f);
        material.SetInt("_ZTest", 4);   // LEqual
        material.SetInt("_ZWrite", 1);
        material.renderQueue = 2600;

        materialCache[texture] = material;
        return material;
    }

    void OnDestroy()
    {
        foreach (var frames in spriteCache.Values)
        {
            for (int i = 0; i < frames.Length; i++)
                if (frames[i] != null) Destroy(frames[i]);
        }
        spriteCache.Clear();

        foreach (var material in materialCache.Values)
            if (material != null) Destroy(material);
        materialCache.Clear();
    }
}
