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
        public Matrix4x4[] matrices;
        public Vector4[] colors;
        public float[] frames;   // 逐实例动画帧索引
        public float[] rows;     // 逐实例图集行（0=行走 1=死亡）
        public float[] flashes;  // 逐实例受击闪红强度
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
    MaterialPropertyBlock mpb;
    Matrix4x4[] chunkMats;
    Vector4[] chunkCols;
    float[] chunkFrames;
    float[] chunkRows;
    float[] chunkFlashes;

    void InitRenderer()
    {
        var shader = Shader.Find("Survivor/InstancedSprite");
        if (shader == null)
        {
            Debug.LogError("EnemySimulation: 找不到 Survivor/InstancedSprite shader");
            return;
        }

        quad = CreateQuad();
        mpb = new MaterialPropertyBlock();
        chunkMats = new Matrix4x4[ChunkSize];
        chunkCols = new Vector4[ChunkSize];
        chunkFrames = new float[ChunkSize];
        chunkRows = new float[ChunkSize];
        chunkFlashes = new float[ChunkSize];

        // 同贴图的类型共享一个批次（一次 draw call 系列）
        var batchByTexture = new Dictionary<Texture, RenderBatch>();
        var batchList = new List<RenderBatch>();
        typeBatchIndex = new int[types.Count];
        typeFrameSize = new Vector2[types.Count];
        typeAnimFrames = new int[types.Count];
        typeAnimFps = new float[types.Count];
        typeHasDeathAnim = new bool[types.Count];
        typeDeathDuration = new float[types.Count];

        for (int i = 0; i < types.Count; i++)
        {
            var t = types[i];
            var sprite = t.sprite;
            typeAnimFrames[i] = Mathf.Max(1, t.animFrames);
            typeAnimFps[i] = t.animFps;
            typeHasDeathAnim[i] = t.hasDeathAnim;
            typeDeathDuration[i] = t.hasDeathAnim ? t.animFrames / t.animFps : 0.15f;

            if (sprite == null)
            {
                Debug.LogError($"EnemySimulation: 类型 {t.typeName} 没有配置贴图");
                typeBatchIndex[i] = -1;
                continue;
            }

            // 图集横向按帧等分、纵向按行等分（行走/死亡两行时高度减半）
            int rows = t.hasDeathAnim ? 2 : 1;
            typeFrameSize[i] = new Vector2(sprite.bounds.size.x / typeAnimFrames[i], sprite.bounds.size.y / rows);

            if (!batchByTexture.TryGetValue(sprite.texture, out var batch))
            {
                var mat = new Material(shader) { mainTexture = sprite.texture, enableInstancing = true };
                mat.SetFloat("_Frames", typeAnimFrames[i]);
                mat.SetFloat("_Rows", t.hasDeathAnim ? 2f : 1f);
                batch = new RenderBatch
                {
                    material = mat,
                    matrices = new Matrix4x4[maxEnemies],
                    colors = new Vector4[maxEnemies],
                    frames = new float[maxEnemies],
                    rows = new float[maxEnemies],
                    flashes = new float[maxEnemies],
                };
                batchByTexture[sprite.texture] = batch;
                batchList.Add(batch);
            }
            typeBatchIndex[i] = batchList.IndexOf(batch);
        }
        batches = batchList.ToArray();
    }

    void DisposeRenderer()
    {
        if (batches != null)
            foreach (var b in batches)
                if (b.material != null) Destroy(b.material);
        if (quad != null) Destroy(quad);
    }

    void LateUpdate()
    {
        if (!initialized || batches == null) return;

        foreach (var b in batches) b.count = 0;
        float time = Time.time;

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

            if (dying)
            {
                float prog = Mathf.Clamp01(1f - e.deathTimer / typeDeathDuration[ti]);
                if (typeHasDeathAnim[ti])
                {
                    // 播放死亡动画行（单向，不循环）
                    row = 1f;
                    frame = Mathf.Min(frames - 1, Mathf.Floor(prog * frames));
                }
                else
                {
                    // 无死亡动画的类型：快速放大淡出
                    size *= 1f + prog * 0.8f;
                    c.a = 1f - prog;
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

            int k = batch.count++;
            // z=1：敌人画在玩家/宝石后面
            batch.matrices[k] = Matrix4x4.TRS(
                new Vector3(e.position.x, e.position.y, 1f),
                Quaternion.identity,
                new Vector3(size.x * flip, size.y, 1f));
            batch.colors[k] = c;
            batch.frames[k] = frame;
            batch.rows[k] = row;
            batch.flashes[k] = flash;
        }

        foreach (var batch in batches)
        {
            int drawn = 0;
            while (drawn < batch.count)
            {
                int n = Mathf.Min(ChunkSize, batch.count - drawn);
                System.Array.Copy(batch.matrices, drawn, chunkMats, 0, n);
                System.Array.Copy(batch.colors, drawn, chunkCols, 0, n);
                System.Array.Copy(batch.frames, drawn, chunkFrames, 0, n);
                System.Array.Copy(batch.rows, drawn, chunkRows, 0, n);
                System.Array.Copy(batch.flashes, drawn, chunkFlashes, 0, n);
                mpb.SetVectorArray("_Color", chunkCols);
                mpb.SetFloatArray("_Frame", chunkFrames);
                mpb.SetFloatArray("_Row", chunkRows);
                mpb.SetFloatArray("_Flash", chunkFlashes);
                Graphics.DrawMeshInstanced(quad, 0, batch.material, chunkMats, n, mpb);
                drawn += n;
            }
        }
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
}
