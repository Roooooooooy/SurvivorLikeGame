using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 程序化生成 HD-2D 场景用的低模地牢模块，替代 Cube/Cylinder 拼接。
// 生成结果直接存成 Mesh 资产，场景脚本通过路径加载。
// 设计原则：不做逐砖几何（避免和贴图砖缝打架），只做倒角、收分、破损轮廓和表面微起伏。
public static class HD2DLowPolyKit
{
    const string Dir = "Assets/Meshes/HD2D";
    const float UvPerMeter = 1f / 6f; // 6 米一次贴图重复，与墙面砖块尺寸接近

    [MenuItem("Tools/HD2D/6. Build Low-Poly Dungeon Kit")]
    public static void Build()
    {
        EnsureFolder();

        Save("HD2D_Wall", Wall(new Vector3(0.8f, 2.4f, 9f), 1337));
        Save("HD2D_Column", Column(0.5f, 5.2f, 8, 4242));

        Save("HD2D_Dais", ChamferBox(new Vector3(9f, 0.8f, 5f), 0.07f, 9001, 0.06f, 0.012f));
        Save("HD2D_Step1", ChamferBox(new Vector3(5.5f, 0.3f, 1.2f), 0.035f, 9010, 0.025f, 0.01f));
        Save("HD2D_Step2", ChamferBox(new Vector3(5.5f, 0.7f, 1.2f), 0.04f, 9011, 0.035f, 0.01f));

        Save("HD2D_BrazierBase", Prism(0.175f, 0.155f, 1.0f, 8, 9002, 0f, 0f));
        Save("HD2D_BrazierBowl", Prism(0.24f, 0.38f, 0.3f, 16, 9003, 0f, 0.02f));

        Save("HD2D_RubbleA", ChamferBox(new Vector3(0.55f, 0.3f, 0.45f), 0.06f, 32, 0f, 0.10f));
        Save("HD2D_RubbleB", ChamferBox(new Vector3(0.4f, 0.24f, 0.6f), 0.05f, 33, 0f, 0.12f));
        Save("HD2D_RubbleC", ChamferBox(new Vector3(0.7f, 0.36f, 0.5f), 0.07f, 34, 0f, 0.09f));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[HD2D] 低模地牢模块已生成: " + Dir);
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Meshes"))
            AssetDatabase.CreateFolder("Assets", "Meshes");
        if (!AssetDatabase.IsValidFolder(Dir))
            AssetDatabase.CreateFolder("Assets/Meshes", "HD2D");
    }

    static void Save(string name, Mesh generated)
    {
        string path = Dir + "/" + name + ".asset";
        // 原地更新，保留 GUID：单独重跑本菜单时，已打开场景里的 Mesh 引用不会断
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            existing.Clear();
            existing.SetVertices(generated.vertices);
            existing.SetNormals(generated.normals);
            existing.SetUVs(0, new List<Vector2>(generated.uv));
            existing.SetTriangles(generated.triangles, 0);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(generated);
        }
        else
        {
            generated.name = name;
            AssetDatabase.CreateAsset(generated, path);
        }
    }

    // 断墙：沿 Z 长、沿 Y 分 6 层、沿 Z 分 18 列，顶边高度按列随机，形成参差断口。
    static Mesh Wall(Vector3 size, int seed)
    {
        var mb = new MeshBuilder();
        float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
        const int ny = 6;
        const int nz = 18;

        float[] top = new float[nz + 1];
        float maxBreak = Mathf.Min(0.38f, size.y * 0.16f);
        for (int i = 0; i <= nz; i++)
            top[i] = hy - 0.04f - Hash01(i, 0, 0, seed) * maxBreak;

        // 正反两面
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * hx;
            for (int i = 0; i < nz; i++)
            {
                float z0 = -hz + size.z * i / nz;
                float z1 = -hz + size.z * (i + 1) / nz;
                for (int j = 0; j < ny; j++)
                {
                    float y0 = -hy + size.y * j / ny;
                    float y1 = -hy + size.y * (j + 1) / ny;
                    float yt0 = (j == ny - 1) ? top[i] : y1;
                    float yt1 = (j == ny - 1) ? top[i + 1] : y1;
                    mb.AddPoly(
                        new Vector3(x, y0, z0),
                        new Vector3(x, y0, z1),
                        new Vector3(x, yt1, z1),
                        new Vector3(x, yt0, z0));
                }
            }
        }

        // 顶部随断口起伏
        for (int i = 0; i < nz; i++)
        {
            float z0 = -hz + size.z * i / nz;
            float z1 = -hz + size.z * (i + 1) / nz;
            mb.AddPoly(
                new Vector3(-hx, top[i], z0),
                new Vector3(hx, top[i], z0),
                new Vector3(hx, top[i + 1], z1),
                new Vector3(-hx, top[i + 1], z1));
        }

        // 两端封口与底面
        mb.AddPoly(
            new Vector3(-hx, -hy, -hz),
            new Vector3(hx, -hy, -hz),
            new Vector3(hx, top[0], -hz),
            new Vector3(-hx, top[0], -hz));
        mb.AddPoly(
            new Vector3(-hx, -hy, hz),
            new Vector3(-hx, top[nz], hz),
            new Vector3(hx, top[nz], hz),
            new Vector3(hx, -hy, hz));
        mb.AddPoly(
            new Vector3(-hx, -hy, -hz),
            new Vector3(-hx, -hy, hz),
            new Vector3(hx, -hy, hz),
            new Vector3(hx, -hy, -hz));

        return mb.ToMesh("HD2D_Wall");
    }

    // 石柱：八棱底座 + 收分柱身 + 外扩柱头，柱头顶面随机压低形成破损。
    static Mesh Column(float radius, float height, int sides, int seed)
    {
        var mb = new MeshBuilder();
        float hy = height * 0.5f;
        const float baseH = 0.35f;
        const float capH = 0.38f;
        float shaftH = height - baseH - capH;

        PrismInto(mb, new Vector3(0f, -hy + baseH * 0.5f, 0f),
            radius * 1.28f, radius * 1.16f, baseH, sides, seed + 1, 0f, 0f);
        PrismInto(mb, new Vector3(0f, -hy + baseH + shaftH * 0.5f, 0f),
            radius * 0.96f, radius * 0.82f, shaftH, sides, seed + 2, 0.5f, 0f);
        PrismInto(mb, new Vector3(0f, hy - capH * 0.5f, 0f),
            radius * 0.86f, radius * 1.24f, capH, sides, seed + 3, 0f, 0.22f);

        return mb.ToMesh("HD2D_Column");
    }

    static Mesh Prism(float rBottom, float rTop, float height, int sides,
        int seed, float angleOffset, float topDrop)
    {
        var mb = new MeshBuilder();
        PrismInto(mb, Vector3.zero, rBottom, rTop, height, sides, seed, angleOffset, topDrop);
        return mb.ToMesh("HD2D_Prism");
    }

    static void PrismInto(MeshBuilder mb, Vector3 center, float rBottom, float rTop,
        float height, int sides, int seed, float angleOffset, float topDrop)
    {
        float y0 = center.y - height * 0.5f;
        float y1 = center.y + height * 0.5f;

        var bottom = new Vector3[sides];
        var top = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = (i + angleOffset) * Mathf.PI * 2f / sides;
            float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            // 半径微抖动，避免机械的正多边形感
            float rb = 1f + (Hash01(i, seed, 3, 77) - 0.5f) * 0.045f;
            float rt = 1f + (Hash01(i, seed, 4, 78) - 0.5f) * 0.045f;
            float drop = topDrop > 0f ? Hash01(i, seed, 9, 88) * topDrop : 0f;

            bottom[i] = new Vector3(center.x + cos * rBottom * rb, y0, center.z + sin * rBottom * rb);
            top[i] = new Vector3(center.x + cos * rTop * rt, y1 - drop, center.z + sin * rTop * rt);
        }

        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            mb.AddPoly(bottom[i], bottom[j], top[j], top[i]);
        }

        for (int i = 1; i < sides - 1; i++)
            mb.AddPoly(bottom[0], bottom[i + 1], bottom[i]);
        for (int i = 1; i < sides - 1; i++)
            mb.AddPoly(top[0], top[i], top[i + 1]);
    }

    // 带倒角的盒子：6 个内缩面 + 12 条倒角边 + 8 个角三角面。
    // damage 会压低顶面附近顶点（形成破损边缘），roughness 给全表面加微起伏。
    static Mesh ChamferBox(Vector3 size, float bevel, int seed, float damage, float roughness)
    {
        var mb = new MeshBuilder();
        float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
        float[] h = { hx, hy, hz };
        float b = Mathf.Min(bevel, Mathf.Min(hx, Mathf.Min(hy, hz)) * 0.45f);
        float minDim = Mathf.Min(hx, Mathf.Min(hy, hz)) * 2f;

        mb.Deform = p =>
        {
            int ix = Mathf.RoundToInt(p.x * 256f);
            int iy = Mathf.RoundToInt(p.y * 256f);
            int iz = Mathf.RoundToInt(p.z * 256f);
            float nx = Hash01(ix, iy, iz, seed + 11) - 0.5f;
            float ny = Hash01(ix, iy, iz, seed + 23) - 0.5f;
            float nz = Hash01(ix, iy, iz, seed + 37) - 0.5f;

            Vector3 q = p + new Vector3(nx, ny, nz) * (roughness * minDim);
            if (damage > 0f && p.y > hy - b - 0.001f)
                q.y -= Mathf.Abs(Hash01(ix, iz, 5, seed + 41)) * damage;
            return q;
        };

        // 6 个面
        for (int axis = 0; axis < 3; axis++)
        {
            int j = (axis + 1) % 3;
            int k = (axis + 2) % 3;
            for (int s = -1; s <= 1; s += 2)
            {
                float av = s * h[axis];
                float jm = -(h[j] - b), jp = h[j] - b;
                float km = -(h[k] - b), kp = h[k] - b;
                mb.AddPoly(
                    AxisPoint(axis, av, j, jm, k, km),
                    AxisPoint(axis, av, j, jp, k, km),
                    AxisPoint(axis, av, j, jp, k, kp),
                    AxisPoint(axis, av, j, jm, k, kp));
            }
        }

        // 12 条倒角边
        for (int i = 0; i < 3; i++)
        {
            for (int j = i + 1; j < 3; j++)
            {
                int k = 3 - i - j;
                for (int si = -1; si <= 1; si += 2)
                {
                    for (int sj = -1; sj <= 1; sj += 2)
                    {
                        float km = -(h[k] - b), kp = h[k] - b;
                        mb.AddPoly(
                            AxisPoint(i, si * h[i], j, sj * (h[j] - b), k, km),
                            AxisPoint(i, si * h[i], j, sj * (h[j] - b), k, kp),
                            AxisPoint(i, si * (h[i] - b), j, sj * h[j], k, kp),
                            AxisPoint(i, si * (h[i] - b), j, sj * h[j], k, km));
                    }
                }
            }
        }

        // 8 个角三角面
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sy = -1; sy <= 1; sy += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    mb.AddPoly(
                        new Vector3(sx * hx, sy * (hy - b), sz * (hz - b)),
                        new Vector3(sx * (hx - b), sy * hy, sz * (hz - b)),
                        new Vector3(sx * (hx - b), sy * (hy - b), sz * hz));
                }
            }
        }

        return mb.ToMesh("HD2D_ChamferBox");
    }

    static Vector3 AxisPoint(int a, float av, int b, float bv, int c, float cv)
    {
        Vector3 p = Vector3.zero;
        p[a] = av;
        p[b] = bv;
        p[c] = cv;
        return p;
    }

    // 稳定的位置哈希：同一位置的重复顶点会得到同一偏移，不会裂开。
    static float Hash01(int x, int y, int z, int seed)
    {
        unchecked
        {
            uint h = (uint)seed * 374761393u;
            h += (uint)x * 668265263u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h += (uint)y * 2246822519u;
            h = (h ^ (h >> 15)) * 3266489917u;
            h += (uint)z * 374761393u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / 16777215f;
        }
    }

    sealed class MeshBuilder
    {
        public System.Func<Vector3, Vector3> Deform;

        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> tris = new List<int>();

        public void AddPoly(params Vector3[] points)
        {
            if (points.Length < 3)
                return;

            if (Deform != null)
            {
                for (int i = 0; i < points.Length; i++)
                    points[i] = Deform(points[i]);
            }

            Vector3 normal = Vector3.Cross(
                points[1] - points[0],
                points[2] - points[0]).normalized;

            Vector3 center = Vector3.zero;
            for (int i = 0; i < points.Length; i++)
                center += points[i];
            center /= points.Length;

            // 模块都以原点为中心（凸形），用重心方向保证法线朝外
            if (Vector3.Dot(normal, center) < 0f)
            {
                System.Array.Reverse(points);
                normal = -normal;
            }

            int start = verts.Count;
            for (int i = 0; i < points.Length; i++)
            {
                verts.Add(points[i]);
                normals.Add(normal);
                uvs.Add(ProjectUV(points[i], normal));
            }

            for (int i = 1; i < points.Length - 1; i++)
            {
                tris.Add(start);
                tris.Add(start + i);
                tris.Add(start + i + 1);
            }
        }

        static Vector2 ProjectUV(Vector3 p, Vector3 n)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            if (ay >= ax && ay >= az)
                return new Vector2(p.x, p.z) * UvPerMeter;
            if (ax >= az)
                return new Vector2(p.z, p.y) * UvPerMeter;
            return new Vector2(p.x, p.y) * UvPerMeter;
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
