using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 敌人渲染：全部敌人共享一个 quad 网格，按贴图分批，
// DrawMeshInstanced 每 1023 个一批，逐实例颜色/翻转/动画帧由 instanced shader 完成。
public partial class EnemySimulation
{
    class RenderBatch
    {
        public Material material;
        public Material xrayMaterial;
        public Matrix4x4[] matrices;
        public Vector4[] colors;
        public float[] frames;   // 逐实例动画帧索引
        public float[] rows;     // 逐实例图集行（0=行走 1=死亡）
        public float[] flashes;  // 逐实例受击闪红强度
        public float[] outlines;  // 逐实例描边强度（精英/Boss = 1，其余 0）
        public float[] dissolves; // 逐实例死亡溶解进度（0=不溶解，1=完全溶解）
        public float[] sortKeys;  // 逐实例排序键（HD-2D：-世界 Z，先画远端）
        public Vector4 trimRect;  // 单帧不透明内容矩形（HD-2D 裁剪 overdraw 用）
        public int count;
    }

    const int ChunkSize = 1023; // DrawMeshInstanced 单次上限

    Mesh quad;
    RenderBatch[] batches;
    int[] typeBatchIndex;      // 类型 -> 渲染批次
    Vector2[] typeFrameSize;   // 类型 -> 单帧世界尺寸
    int[] typeAnimFrames;      // 类型 -> 帧数
    float[] typeAnimFps;       // 类型 -> 帧率
    bool[] typeHasDeathAnim;   // 类型 -> 是否有死亡动画行
    float[] typeDeathDuration; // 类型 -> 死亡动画时长
    bool[] typeOutline;        // 类型 -> 是否精英/Boss（需要描边）
    MaterialPropertyBlock mpb;
    Matrix4x4[] chunkMats;
    Vector4[] chunkCols;
    float[] chunkFrames;
    float[] chunkRows;
    float[] chunkFlashes;
    float[] chunkOutlines;
    float[] chunkDissolves;

    // ---- HD-2D / URP 路径 ----
    // hd2d=true 时：逻辑 (x, y) 映射到世界 (x, 0, y)，quad 改为脚底 pivot，
    // 使用 URP alpha clip shader + 贴地椭圆阴影；否则保持旧的内置管线 2D 行为。
    bool hd2d;
    Mesh shadowQuad;
    Material shadowMaterial;
    Camera cachedCamera;
    int[] sortOrder;
    Matrix4x4[] shadowMats;
    Matrix4x4[] chunkShadowMats;

    // 供压测/调试读取：本帧敌人实际提交的 draw call 与实例数
    public int LastEnemyDrawCalls { get; private set; }
    public int LastShadowDrawCalls { get; private set; }
    public int LastXRayDrawCalls { get; private set; }
    public int LastRenderedInstances { get; private set; }

    [Header("HD-2D")]
    [Tooltip("开启后逻辑 (x,y) 映射到世界 (x,0,y)，并使用 URP alpha-clip 精灵。默认关闭，避免影响未迁移的 SampleScene。")]
    public bool hd2dRendering = false;

    [Header("敌人遮挡 XRay")]
    public bool enemyXray = true;
    public Color enemyXrayColor = new Color(1f, 0.42f, 0.12f, 0.40f);

    void InitRenderer()
    {
        // 只有显式开启 HD-2D 的敌人实例才切到 URP + XZ 映射；
        // 其他场景（尤其未迁移的 SampleScene）完全保持旧的内置管线 2D 路径。
        bool urpActive = GraphicsSettings.currentRenderPipeline != null;
        Shader shader = hd2dRendering && urpActive
            ? Shader.Find("Survivor/URPInstancedSprite")
            : null;
        if (shader == null) shader = Shader.Find("Survivor/InstancedSprite");
        if (shader == null)
        {
            Debug.LogError("EnemySimulation: 找不到 Survivor/URPInstancedSprite 或 Survivor/InstancedSprite shader");
            return;
        }

        hd2d = shader.name == "Survivor/URPInstancedSprite";
        if (hd2dRendering && !hd2d)
            Debug.LogWarning("EnemySimulation: 已开启 hd2dRendering，但 URP 未启用或找不到 Survivor/URPInstancedSprite，回退到内置管线路径");

        Shader xrayShader = null;
        if (hd2d && enemyXray)
        {
            xrayShader = Shader.Find("Survivor/URPInstancedSpriteXRay");
            if (xrayShader == null)
                Debug.LogWarning("EnemySimulation: 找不到 Survivor/URPInstancedSpriteXRay，敌人 XRay 已跳过");
        }

        quad = hd2d ? CreateHd2dQuad() : CreateQuad();
        sortOrder = new int[maxEnemies];
        if (hd2d)
        {
            shadowQuad = CreateGroundQuad();
            shadowMats = new Matrix4x4[maxEnemies];
            chunkShadowMats = new Matrix4x4[ChunkSize];
            cachedCamera = Camera.main;

            var shadowShader = Shader.Find("Survivor/URPInstancedShadow");
            if (shadowShader != null)
            {
                shadowMaterial = new Material(shadowShader) { name = "HD2D_EnemyShadow" };
                shadowMaterial.SetColor("_Color", new Color(0.02f, 0.015f, 0.025f, 0.55f));
                shadowMaterial.SetFloat("_Softness", 0.45f);
                shadowMaterial.enableInstancing = true;
                // 排在普通 Transparent 之前，避免盖住体积光等后加的透明效果
                shadowMaterial.renderQueue = 2900;
            }
            else
            {
                Debug.LogWarning("EnemySimulation: 找不到 Survivor/URPInstancedShadow，跳过贴地阴影");
            }
        }
        mpb = new MaterialPropertyBlock();
        chunkMats = new Matrix4x4[ChunkSize];
        chunkCols = new Vector4[ChunkSize];
        chunkFrames = new float[ChunkSize];
        chunkRows = new float[ChunkSize];
        chunkFlashes = new float[ChunkSize];
        chunkOutlines = new float[ChunkSize];
        chunkDissolves = new float[ChunkSize];

        // 同贴图的类型共享一个批次（一次 draw call 系列）
        var batchByTexture = new Dictionary<Texture, RenderBatch>();
        var batchList = new List<RenderBatch>();
        typeBatchIndex = new int[types.Count];
        typeFrameSize = new Vector2[types.Count];
        typeAnimFrames = new int[types.Count];
        typeAnimFps = new float[types.Count];
        typeHasDeathAnim = new bool[types.Count];
        typeDeathDuration = new float[types.Count];
        typeOutline = new bool[types.Count];

        for (int i = 0; i < types.Count; i++)
        {
            var t = types[i];
            var sprite = t.sprite;
            typeAnimFrames[i] = Mathf.Max(1, t.animFrames);
            typeAnimFps[i] = t.animFps;
            typeHasDeathAnim[i] = t.hasDeathAnim;
            typeDeathDuration[i] = t.hasDeathAnim ? t.animFrames / t.animFps : 0.15f;
            // 精英/Boss 描边判定：高血量或 Boss 行为。体型放大本身不再触发描边，
            // 否则 16×16 的大怪贴图会被 1.25 texel 的粗边包成一块色块。
            typeOutline[i] = t.behavior == EnemyBehavior.Boss || t.maxHealth >= 200;

            if (sprite == null)
            {
                Debug.LogError($"EnemySimulation: 类型 {t.typeName} 没有配置贴图");
                typeBatchIndex[i] = -1;
                continue;
            }

            // 图集横向按帧等分、纵向按行等分（行走/死亡两行时高度减半）
            int rows = t.hasDeathAnim ? 2 : 1;
            typeFrameSize[i] = new Vector2(sprite.bounds.size.x / typeAnimFrames[i], sprite.bounds.size.y / rows);

            // 单帧内容矩形：把四周透明留白裁掉，减少 alpha clip 之前跑的无用片元。
            // 编辑器工具 Tools/HD2D/9 会把实测值写回 EnemyTypes.asset；未分析过时为全帧。
            Vector4 trim = hd2d ? NormalizeTrim(t.contentRect) : new Vector4(0f, 0f, 1f, 1f);

            if (!batchByTexture.TryGetValue(sprite.texture, out var batch))
            {
                var mat = new Material(shader) { mainTexture = sprite.texture, enableInstancing = true };
                mat.SetFloat("_Frames", typeAnimFrames[i]);
                mat.SetFloat("_Rows", t.hasDeathAnim ? 2f : 1f);
                // 描边宽约 1.25 个图集像素；16×16 的小贴图按比例收细，避免糊成一圈色块
                float outlineWidth = sprite.texture.width < 64 ? 0.6f : 1.25f;
                mat.SetColor("_OutlineColor", new Color(0.78f, 0.58f, 0.16f, 1f));
                mat.SetFloat("_OutlineWidth", outlineWidth);
                mat.SetColor("_DissolveEdge", new Color(1f, 0.55f, 0.18f, 1f));
                if (hd2d)
                {
                    // URP alpha clip + 假光照参数；_MainTex_TexelSize 手动写入，
                    // 因为描边采样依赖它，不能指望 SRP 自动填充。
                    mat.SetFloat("_Cutoff", 0.5f);
                    mat.SetVector("_MainTex_TexelSize", new Vector4(
                        1f / sprite.texture.width, 1f / sprite.texture.height,
                        sprite.texture.width, sprite.texture.height));
                    mat.SetColor("_FakeLightColor", new Color(1f, 0.94f, 0.86f, 1f));
                    mat.SetColor("_AmbientColor", new Color(0.42f, 0.46f, 0.55f, 1f));
                    mat.SetVector("_FakeLightDir", new Vector4(0.35f, 0.7f, 0f, 0f));
                    mat.SetVector("_TrimRect", trim);
                }

                Material xrayMat = null;
                if (xrayShader != null)
                {
                    xrayMat = new Material(xrayShader)
                    {
                        mainTexture = sprite.texture,
                        enableInstancing = true,
                    };
                    xrayMat.SetFloat("_Frames", typeAnimFrames[i]);
                    xrayMat.SetFloat("_Rows", t.hasDeathAnim ? 2f : 1f);
                    xrayMat.SetVector("_TrimRect", trim);
                    xrayMat.SetFloat("_Cutoff", 0.5f);
                    xrayMat.SetColor("_XRayColor", enemyXrayColor);
                    xrayMat.renderQueue = 2602;
                }

                batch = new RenderBatch
                {
                    material = mat,
                    xrayMaterial = xrayMat,
                    trimRect = trim,
                    matrices = new Matrix4x4[maxEnemies],
                    colors = new Vector4[maxEnemies],
                    frames = new float[maxEnemies],
                    rows = new float[maxEnemies],
                    flashes = new float[maxEnemies],
                    outlines = new float[maxEnemies],
                    dissolves = new float[maxEnemies],
                    sortKeys = new float[maxEnemies],
                };
                batchByTexture[sprite.texture] = batch;
                batchList.Add(batch);
            }
            else if (hd2d && (batch.trimRect - trim).sqrMagnitude > 1e-8f)
            {
                // 同贴图的类型共享一个 quad/材质，裁剪矩形必须一致
                Debug.LogWarning($"EnemySimulation: 类型 {t.typeName} 与共享贴图的其他类型裁剪矩形不一致，沿用首个类型");
            }
            typeBatchIndex[i] = batchList.IndexOf(batch);
        }
        batches = batchList.ToArray();
    }

    void DisposeRenderer()
    {
        if (batches != null)
            foreach (var b in batches)
            {
                if (b.material != null) Destroy(b.material);
                if (b.xrayMaterial != null) Destroy(b.xrayMaterial);
            }
        if (quad != null) Destroy(quad);
        if (shadowQuad != null) Destroy(shadowQuad);
        if (shadowMaterial != null) Destroy(shadowMaterial);
    }

    void LateUpdate()
    {
        if (!initialized || batches == null) return;

        foreach (var b in batches) b.count = 0;
        float time = Time.time;
        int shadowCount = 0;
        LastEnemyDrawCalls = 0;
        LastShadowDrawCalls = 0;
        LastXRayDrawCalls = 0;
        LastRenderedInstances = 0;
        if (hd2d && cachedCamera == null) cachedCamera = Camera.main;

        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];

            // 活体或死亡动画期的尸体都要渲染
            bool dying = (e.flags & FLAG_DYING) != 0;
            if (e.alive == 0 && !dying) continue;

            int ti = e.typeIndex;
            int bi = typeBatchIndex[ti];
            if (bi < 0) continue;
            var batch = batches[bi];
            if (batch.count >= maxEnemies) continue;

            var t = types[ti];
            Color c = t.color;
            float flip = (e.flags & FLAG_FLIPX) != 0 ? -1f : 1f;
            Vector2 size = typeFrameSize[ti] * t.scale;
            int frames = typeAnimFrames[ti];
            float frame = 0f;
            float row = 0f;
            float flash = 0f;
            float outline = typeOutline[ti] ? 1f : 0f;
            float dissolve = 0f;

            if (dying)
            {
                float prog = Mathf.Clamp01(1f - e.deathTimer / typeDeathDuration[ti]);
                if (typeHasDeathAnim[ti])
                {
                    // 播放死亡动画行（单向，不循环）；溶解保持 0，不干扰原死亡动画
                    int fi = Mathf.Min(frames - 1, Mathf.FloorToInt(prog * frames));
                    row = 1f;
                    frame = fi;
                    // 死亡行（shader row=1，采样图集上半张的压扁动画）每帧内容都贴到单元格底部，
                    // 与行走行脚底基线一致，不需要额外竖直偏移。曾按行走行的间隙做补偿，
                    // 会让死亡中段整体下沉 2~4 图集像素（body 看起来陷进地面）。
                }
                else
                {
                    // 无死亡动画的类型：保留原有的快速放大淡出，同时按进度溶解
                    size *= 1f + prog * 0.8f;
                    c.a = 1f - prog;
                    dissolve = prog;
                }
            }
            else
            {
                // 受击：shader 闪红（强度随计时衰减）+ 挤压放大
                if (e.flashTimer > 0f)
                {
                    flash = e.flashTimer / 0.2f;
                    size *= 1f + e.flashTimer * 1.5f;
                }
                else if (e.fuseTimer >= 0f)
                {
                    float pulse = Mathf.PingPong((t.fuseTime - e.fuseTimer) * 8f, 1f);
                    c = Color.Lerp(t.color, Color.red, pulse);
                }

                // 行走帧动画：相位随机错开，帧号取整避免采样到两帧之间
                frame = frames > 1
                    ? Mathf.Floor((time * typeAnimFps[ti] + e.animPhase * frames) % frames)
                    : 0f;
            }

            // HD-2D 走 alpha clip：形状由 clip 决定，tint alpha 必须保持 1，
            // 否则无死亡动画的类型会在淡出中途整块被裁掉。溶解仍由 _Dissolve 负责。
            if (hd2d) c.a = 1f;

            int k = batch.count++;
            if (hd2d)
            {
                // 逻辑 (x, y) -> 世界 (x, 0, y)；脚底 pivot 的 quad 直接贴地。
                Vector3 groundPos = new Vector3(e.position.x, 0f, e.position.y);
                Quaternion rot = Quaternion.identity;
                if (cachedCamera != null)
                {
                    // 只绕 Y 轴朝向相机（HD-2D billboard），保持脚底始终贴地
                    Vector3 toCam = cachedCamera.transform.position - groundPos;
                    toCam.y = 0f;
                    if (toCam.sqrMagnitude > 1e-6f)
                        rot = Quaternion.LookRotation(toCam, Vector3.up);
                }

                // 按内容矩形收紧 quad：透明留白不再提交片元，同时保持角色脚底/相对位置不变
                Vector4 trim = batch.trimRect;
                Vector2 quadSize = new Vector2(size.x * trim.z, size.y * trim.w);
                Vector3 right = rot * Vector3.right;
                Vector3 worldPos = groundPos;
                worldPos += right * ((trim.x + trim.z * 0.5f - 0.5f) * size.x * flip);
                worldPos.y += trim.y * size.y;

                batch.matrices[k] = Matrix4x4.TRS(
                    worldPos, rot,
                    new Vector3(quadSize.x * flip, quadSize.y, 1f));

                // 世界 Z 越大离相机越远：先画远端，近端后画（配合 depth test 双保险）
                batch.sortKeys[k] = -e.position.y;

                if (shadowMaterial != null && shadowCount < shadowMats.Length)
                {
                    // 贴地椭圆：宽度跟内容走，纵深压扁；高度抬 2cm 防止和地面 z-fighting
                    shadowMats[shadowCount++] = Matrix4x4.TRS(
                        new Vector3(groundPos.x, 0.02f, groundPos.z),
                        Quaternion.identity,
                        new Vector3(quadSize.x * 0.46f, 1f, quadSize.y * 0.16f));
                }
            }
            else
            {
                // z=1：敌人画在玩家/宝石后面
                batch.matrices[k] = Matrix4x4.TRS(
                    new Vector3(e.position.x, e.position.y, 1f),
                    Quaternion.identity,
                    new Vector3(size.x * flip, size.y, 1f));
            }
            batch.colors[k] = c;
            batch.frames[k] = frame;
            batch.rows[k] = row;
            batch.flashes[k] = flash;
            batch.outlines[k] = outline;
            batch.dissolves[k] = dissolve;
        }

        foreach (var b in batches) LastRenderedInstances += b.count;

        foreach (var batch in batches)
        {
            int total = batch.count;
            bool sorted = hd2d && total > 1;
            if (sorted)
            {
                // 按世界 Z 从远到近排序。alpha clip + ZWrite 本身已能正确遮挡，
                // 排序主要保证透明/未来混合模式下的绘制顺序稳定。
                for (int i = 0; i < total; i++) sortOrder[i] = i;
                System.Array.Sort(batch.sortKeys, sortOrder, 0, total);
            }

            int drawn = 0;
            while (drawn < total)
            {
                int n = Mathf.Min(ChunkSize, total - drawn);
                if (sorted)
                {
                    for (int i = 0; i < n; i++)
                    {
                        int src = sortOrder[drawn + i];
                        chunkMats[i] = batch.matrices[src];
                        chunkCols[i] = batch.colors[src];
                        chunkFrames[i] = batch.frames[src];
                        chunkRows[i] = batch.rows[src];
                        chunkFlashes[i] = batch.flashes[src];
                        chunkOutlines[i] = batch.outlines[src];
                        chunkDissolves[i] = batch.dissolves[src];
                    }
                }
                else
                {
                    System.Array.Copy(batch.matrices, drawn, chunkMats, 0, n);
                    System.Array.Copy(batch.colors, drawn, chunkCols, 0, n);
                    System.Array.Copy(batch.frames, drawn, chunkFrames, 0, n);
                    System.Array.Copy(batch.rows, drawn, chunkRows, 0, n);
                    System.Array.Copy(batch.flashes, drawn, chunkFlashes, 0, n);
                    System.Array.Copy(batch.outlines, drawn, chunkOutlines, 0, n);
                    System.Array.Copy(batch.dissolves, drawn, chunkDissolves, 0, n);
                }
                mpb.SetVectorArray("_Color", chunkCols);
                mpb.SetFloatArray("_Frame", chunkFrames);
                mpb.SetFloatArray("_Row", chunkRows);
                mpb.SetFloatArray("_Flash", chunkFlashes);
                mpb.SetFloatArray("_Outline", chunkOutlines);
                mpb.SetFloatArray("_Dissolve", chunkDissolves);
                // 敌人是纯表现层，不投射/接收实时阴影；光照由 shader 内的假光照完成
                Graphics.DrawMeshInstanced(quad, 0, batch.material, chunkMats, n, mpb,
                    ShadowCastingMode.Off, false);
                LastEnemyDrawCalls++;

                // 同一批矩阵再画一遍 XRay pass：只在前景深度测试失败处出现剪影。
                if (enemyXray && batch.xrayMaterial != null)
                {
                    batch.xrayMaterial.SetColor("_XRayColor", enemyXrayColor);
                    Graphics.DrawMeshInstanced(quad, 0, batch.xrayMaterial, chunkMats, n, mpb,
                        ShadowCastingMode.Off, false);
                    LastXRayDrawCalls++;
                }

                drawn += n;
            }
        }

        if (hd2d && shadowMaterial != null && shadowCount > 0)
        {
            int drawn = 0;
            while (drawn < shadowCount)
            {
                int n = Mathf.Min(ChunkSize, shadowCount - drawn);
                System.Array.Copy(shadowMats, drawn, chunkShadowMats, 0, n);
                Graphics.DrawMeshInstanced(shadowQuad, 0, shadowMaterial, chunkShadowMats, n,
                    null, ShadowCastingMode.Off, false);
                LastShadowDrawCalls++;
                drawn += n;
            }
        }
    }

    // 兜底：旧资产没有裁剪数据时按整帧处理，避免 quad 缩成 0
    static Vector4 NormalizeTrim(Vector4 r)
    {
        if (r.z <= 1e-4f || r.w <= 1e-4f) return new Vector4(0f, 0f, 1f, 1f);
        float x = Mathf.Clamp(r.x, 0f, 0.998f);
        float y = Mathf.Clamp(r.y, 0f, 0.998f);
        float w = Mathf.Clamp(r.z, 0.002f, 1f - x);
        float h = Mathf.Clamp(r.w, 0.002f, 1f - y);
        return new Vector4(x, y, w, h);
    }

    static Mesh CreateQuad()
    {
        var m = new Mesh
        {
            vertices = new[]
            {
                new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f),
            },
            uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one },
            triangles = new[] { 0, 2, 1, 1, 2, 3 },
        };
        // 实例化绘制没有逐实例剔除，放大包围盒防止整批被误剔除
        m.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        return m;
    }

    // HD-2D 用：pivot 在脚底中心（y=0..1），矩阵平移到 (x, 0, y) 时脚底正好贴地。
    static Mesh CreateHd2dQuad()
    {
        var m = new Mesh
        {
            vertices = new[]
            {
                new Vector3(-0.5f, 0f), new Vector3(0.5f, 0f),
                new Vector3(-0.5f, 1f), new Vector3(0.5f, 1f),
            },
            uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one },
            triangles = new[] { 0, 2, 1, 1, 2, 3 },
        };
        m.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        return m;
    }

    // 贴地阴影用：XZ 平面上的单位 quad
    static Mesh CreateGroundQuad()
    {
        var m = new Mesh
        {
            vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f),
            },
            uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one },
            triangles = new[] { 0, 2, 1, 1, 2, 3 },
        };
        m.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        return m;
    }
}
