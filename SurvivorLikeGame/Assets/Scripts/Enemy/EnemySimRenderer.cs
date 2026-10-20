using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 敌人渲染：全部敌人共享一个 quad 网格，按贴图分批，
// DrawMeshInstanced 每 1023 个一批，逐实例颜色/翻转由 instanced shader 完成。
public partial class EnemySimulation
{
    class RenderBatch
    {
        public Material material;
        public Matrix4x4[] matrices;
        public Vector4[] colors;
        public int count;
    }

    const int ChunkSize = 1023; // DrawMeshInstanced 单次上限

    Mesh quad;
    RenderBatch[] batches;
    int[] typeBatchIndex;      // 类型 -> 渲染批次
    Vector2[] typeSpriteSize;  // 类型 -> 精灵世界尺寸
    MaterialPropertyBlock mpb;
    Matrix4x4[] chunkMats;
    Vector4[] chunkCols;

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

        // 同贴图的类型共享一个批次（一次 draw call 系列）
        var batchByTexture = new Dictionary<Texture, RenderBatch>();
        var batchList = new List<RenderBatch>();
        typeBatchIndex = new int[types.Count];
        typeSpriteSize = new Vector2[types.Count];

        for (int i = 0; i < types.Count; i++)
        {
            var sprite = types[i].sprite;
            if (sprite == null)
            {
                Debug.LogError($"EnemySimulation: 类型 {types[i].typeName} 没有配置贴图");
                typeBatchIndex[i] = -1;
                continue;
            }

            typeSpriteSize[i] = sprite.bounds.size;

            if (!batchByTexture.TryGetValue(sprite.texture, out var batch))
            {
                batch = new RenderBatch
                {
                    material = new Material(shader) { mainTexture = sprite.texture, enableInstancing = true },
                    matrices = new Matrix4x4[maxEnemies],
                    colors = new Vector4[maxEnemies],
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

        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if (e.alive == 0) continue;

            int bi = typeBatchIndex[e.typeIndex];
            if (bi < 0) continue;
            var batch = batches[bi];
            if (batch.count >= maxEnemies) continue;

            var t = types[e.typeIndex];

            // 受击闪白 > 自爆红闪 > 类型染色
            Color c = t.color;
            if (e.flashTimer > 0f) c = Color.white;
            else if (e.fuseTimer >= 0f)
            {
                float pulse = Mathf.PingPong((t.fuseTime - e.fuseTimer) * 8f, 1f);
                c = Color.Lerp(t.color, Color.red, pulse);
            }

            float flip = (e.flags & FLAG_FLIPX) != 0 ? -1f : 1f;
            Vector2 size = typeSpriteSize[e.typeIndex] * t.scale;

            int k = batch.count++;
            // z=1：敌人画在玩家/宝石后面
            batch.matrices[k] = Matrix4x4.TRS(
                new Vector3(e.position.x, e.position.y, 1f),
                Quaternion.identity,
                new Vector3(size.x * flip, size.y, 1f));
            batch.colors[k] = c;
        }

        foreach (var batch in batches)
        {
            int drawn = 0;
            while (drawn < batch.count)
            {
                int n = Mathf.Min(ChunkSize, batch.count - drawn);
                System.Array.Copy(batch.matrices, drawn, chunkMats, 0, n);
                System.Array.Copy(batch.colors, drawn, chunkCols, 0, n);
                mpb.SetVectorArray("_Color", chunkCols);
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
