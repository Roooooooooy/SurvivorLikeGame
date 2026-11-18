using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RpgGame;
using RpgGameEditor.OpenFractureGeometry;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RpgGameEditor
{
    public static class KayKitDestructionBuilder
    {
        const string Art = "Assets/Art/KayKitDungeon";
        const string Folder = Art + "/Generated/Fractures";

        [MenuItem("工具/角色扮演/KayKit/4. 接入木箱与桌子破坏")]
        public static void UpgradeCurrentScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出 Play。");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != KayKitRegionBuilder.ScenePath || scene.isDirty)
                throw new InvalidOperationException("请打开并保存 KayKit 场景，再接入破坏。");
            var layout = JsonUtility.FromJson<KayKitRegionBuilder.Layout>(File.ReadAllText(Art + "/Layout.json"));
            PrepareGeometry(layout.variants.Where(v => v.destructible).Select(v => v.model));
            var hierarchy = scene.GetRootGameObjects().First(go => go.name == "KayKit_Layout").transform;
            var modules = hierarchy.Find("Modules"); var interactions = hierarchy.Find("InteractionsXY");
            var colliders = hierarchy.Find("CollisionXY"); var greybox = hierarchy.Find("Greybox");
            var fractureAssets = new Dictionary<string, GameObject>();

            foreach (var variant in layout.variants.Where(v => v.destructible))
            {
                string path = Art + "/Generated/Prefabs/" + variant.id + ".prefab";
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var fractured = Bake(contents, variant.id);
                    Configure(contents, "", variant.maxHealth, fractured);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    fractureAssets.Add(variant.id, fractured);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            int count = 0;
            foreach (var placement in layout.props)
            {
                var variant = layout.variants.First(v => v.id == placement.variant);
                if (!variant.destructible) continue;
                var go = hierarchy.GetComponentsInChildren<KayKitWorldAnchor>(true)
                    .First(a => a.gameObject.name == variant.id &&
                        Vector2.Distance(a.transform.position, new Vector2(placement.x, placement.y)) < 0.05f).gameObject;
                var bounds = BoundsOf(go.GetComponent<KayKitWorldAnchor>().visual.gameObject);
                var old = colliders.GetComponentsInChildren<BoxCollider2D>().FirstOrDefault(c => c.name == variant.id &&
                    Vector2.Distance(c.transform.position, new Vector2(bounds.center.x, bounds.center.z)) < 0.05f);
                var blocker = go.GetComponent<BoxCollider2D>();
                if (blocker == null) blocker = go.AddComponent<BoxCollider2D>();
                blocker.size = new Vector2(bounds.size.x, bounds.size.z);
                blocker.offset = new Vector2(bounds.center.x - go.transform.position.x, bounds.center.z - go.transform.position.y);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                go.transform.SetParent(interactions, true);
                var prop = Configure(go, placement.id, variant.maxHealth, fractureAssets[variant.id]);
                prop.greyboxVisual = greybox.Cast<Transform>().FirstOrDefault(t => t.name == variant.id &&
                    Vector2.Distance(new Vector2(t.position.x, t.position.z), new Vector2(bounds.center.x, bounds.center.z)) < 0.05f)?.gameObject;
                prop.SetPreviewMode(modules.gameObject.activeSelf);
                EditorUtility.SetDirty(prop);
                count++;
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("[KayKitDestruction] 已接入 " + count + " 组木箱/桌子；宝箱、门与角色布局保留。");
        }

        public static void PrepareGeometry(IEnumerable<string> models)
        {
            foreach (var model in models.Distinct())
            {
                var importer = AssetImporter.GetAtPath(Art + "/Models/" + model + ".fbx") as ModelImporter;
                if (importer == null) throw new InvalidOperationException("模型尚未导入：" + model);
                if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
            }
            if (!Directory.Exists(Folder)) { Directory.CreateDirectory(Folder); AssetDatabase.Refresh(); }
        }

        public static DestructibleProp Configure(GameObject go, string id, int health, GameObject fractured)
        {
            var prop = go.GetComponent<DestructibleProp>();
            if (prop == null) prop = go.AddComponent<DestructibleProp>();
            prop.propId = id; prop.maxHealth = Mathf.Max(1, health);
            prop.intactVisual = go.GetComponent<KayKitWorldAnchor>().visual.gameObject;
            prop.fracturedPrefab = fractured;
            return prop;
        }

        public static GameObject Bake(GameObject source, string variant)
        {
            var pieces = new List<Mesh>();
            var visual = source.GetComponent<KayKitWorldAnchor>().visual;
            foreach (var filter in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = Object.Instantiate(filter.sharedMesh);
                var transform = visual.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                mesh.vertices = mesh.vertices.Select(transform.MultiplyPoint3x4).ToArray();
                mesh.normals = mesh.normals.Select(n => transform.inverse.transpose.MultiplyVector(n).normalized).ToArray();
                mesh.RecalculateBounds();
                pieces.AddRange(Islands(mesh)); Object.DestroyImmediate(mesh);
            }

            int target = variant == "Table" ? 16 : variant == "CrateSingle" ? 10 : 22;
            var random = new System.Random(StableSeed(variant));
            int attempts = 0;
            while (pieces.Count < target && attempts++ < target * 3)
            {
                var candidates = pieces.Where(m => m.vertexCount > 12 && m.triangles.Length > 24)
                    .OrderByDescending(m => m.bounds.size.sqrMagnitude).ToArray();
                if (candidates.Length == 0) break;
                var mesh = candidates[attempts % candidates.Length];
                Vector3 normal = variant == "Table"
                    ? (mesh.bounds.size.x > mesh.bounds.size.z ? Vector3.forward : Vector3.right)
                    : (attempts % 2 == 0 ? Vector3.right : Vector3.forward);
                normal = (normal + new Vector3(0.07f, 0.02f, 0.11f)).normalized;
                float width = Mathf.Abs(Vector3.Dot(mesh.bounds.size, normal));
                if (width < 0.08f) continue;
                var data = new FragmentData(mesh);
                Vector3 origin = mesh.bounds.center + normal * width * ((float)random.NextDouble() - 0.5f) * 0.2f;
                MeshSlicer.Slice(data, normal, origin, Vector2.one, Vector2.zero, out var top, out var bottom);
                if (top.triangleCount == 0 || bottom.triangleCount == 0) continue;
                var a = top.ToMesh(); var b = bottom.ToMesh();
                pieces.Remove(mesh); Object.DestroyImmediate(mesh); pieces.Add(a); pieces.Add(b);
            }

            var root = new GameObject(variant + "_Fractured");
            root.AddComponent<PropFractureEffect>().seed = StableSeed(variant);
            var outer = AssetDatabase.LoadAssetAtPath<Material>(Art + "/Generated/DungeonAtlas.mat");
            var inner = InnerMaterial();
            int index = 0;
            foreach (var sourcePiece in pieces.Where(m => m.vertexCount >= 3 && m.triangles.Length >= 3))
            {
                var mesh = sourcePiece;
                Vector3 center = mesh.bounds.center;
                mesh.vertices = mesh.vertices.Select(v => v - center).ToArray(); mesh.RecalculateBounds();
                mesh.name = variant + "_Shard_" + index.ToString("D2");
                string path = Folder + "/" + mesh.name + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null) AssetDatabase.CreateAsset(mesh, path);
                else { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
                var child = new GameObject("Wood_" + index.ToString("D2")); child.transform.SetParent(root.transform, false);
                child.transform.localPosition = center;
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterials = new[] { outer, inner };
                renderer.shadowCastingMode = ShadowCastingMode.On;
                index++;
            }
            if (index < 2) { Object.DestroyImmediate(root); throw new InvalidOperationException("预切未生成有效木片：" + variant); }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + variant + ".prefab");
            Object.DestroyImmediate(root);
            Debug.Log("[KayKitDestruction] " + variant + " 预切为 " + index + " 个保留 UV 的网格片。");
            return prefab;
        }

        static List<Mesh> Islands(Mesh source)
        {
            var vertices = source.vertices; var triangles = source.triangles;
            var parent = Enumerable.Range(0, vertices.Length).ToArray();
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            void Union(int a, int b) { parent[Find(a)] = Find(b); }
            var coincident = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                var v = vertices[i]; var key = new Vector3Int(Mathf.RoundToInt(v.x * 100000f), Mathf.RoundToInt(v.y * 100000f), Mathf.RoundToInt(v.z * 100000f));
                if (coincident.TryGetValue(key, out int previous)) Union(previous, i); else coincident.Add(key, i);
            }
            for (int i = 0; i < triangles.Length; i += 3) { Union(triangles[i], triangles[i + 1]); Union(triangles[i], triangles[i + 2]); }
            var groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int key = Find(triangles[i]);
                if (!groups.TryGetValue(key, out var list)) groups.Add(key, list = new List<int>());
                list.Add(triangles[i]); list.Add(triangles[i + 1]); list.Add(triangles[i + 2]);
            }
            var result = new List<Mesh>(); var normals = source.normals; var uv = source.uv;
            foreach (var group in groups.Values)
            {
                var map = new Dictionary<int, int>(); var vs = new List<Vector3>(); var ns = new List<Vector3>(); var us = new List<Vector2>(); var ts = new List<int>();
                foreach (int vertex in group)
                {
                    if (!map.TryGetValue(vertex, out int local))
                    {
                        local = vs.Count; map.Add(vertex, local); vs.Add(vertices[vertex]); ns.Add(normals[vertex]); us.Add(uv[vertex]);
                    }
                    ts.Add(local);
                }
                var mesh = new Mesh(); mesh.SetVertices(vs); mesh.SetNormals(ns); mesh.SetUVs(0, us); mesh.SetTriangles(ts, 0); mesh.RecalculateBounds();
                result.Add(mesh);
            }
            return result;
        }

        static Material InnerMaterial()
        {
            const string path = Folder + "/WoodInterior.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", new Color(0.45f, 0.19f, 0.045f)); material.SetFloat("_Smoothness", 0.04f);
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        public static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true); var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static int StableSeed(string text)
        {
            unchecked { int hash = 17; foreach (char c in text) hash = hash * 31 + c; return hash & int.MaxValue; }
        }
    }
}
