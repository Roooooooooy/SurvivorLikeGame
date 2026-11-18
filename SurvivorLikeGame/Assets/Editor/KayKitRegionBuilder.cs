using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RpgGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RpgGameEditor
{
    // 只生成独立 KayKit 样板；原 Region_Test 与其手工调整作为角色/相机模板保留。
    public static class KayKitRegionBuilder
    {
        public const string ScenePath = "Assets/Scenes/Region_KayKit.unity";
        const string ArtFolder = "Assets/Art/KayKitDungeon";
        const string GeneratedFolder = ArtFolder + "/Generated";
        const string PrefabFolder = GeneratedFolder + "/Prefabs";
        const string RegionId = "region_kaykit";
        const float Cell = 2f;

        [Serializable] public class Room { public int minX, maxX, minY, maxY; }
        [Serializable] public class Variant
        {
            public string id, model;
            public float scale = 1f;
            public string detailModel;
            public float detailScale = 1f;
            public float[] detailOffset;
            public bool destructible;
            public int maxHealth = 20;
        }
        [Serializable] public class Placement { public string id, variant; public float x, y, yaw; }
        [Serializable] public class Layout
        {
            public int seed;
            public Room[] rooms;
            public Variant[] variants;
            public Placement[] props;
        }

        [MenuItem("工具/角色扮演/KayKit/1. 构建模块样板")]
        public static void BuildModules() => Build(true);

        [MenuItem("工具/角色扮演/KayKit/2. 构建灰盒样板")]
        public static void BuildGreybox() => Build(false);

        [MenuItem("工具/角色扮演/KayKit/3. 切换灰盒与模块")]
        public static void ToggleGreybox()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlaying) return;
            var root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "KayKit_Layout");
            if (root == null) return;
            var modules = root.transform.Find("Modules").gameObject;
            var greybox = root.transform.Find("Greybox").gameObject;
            bool showModules = !modules.activeSelf;
            modules.SetActive(showModules);
            greybox.SetActive(!showModules);
            foreach (var prop in root.GetComponentsInChildren<DestructibleProp>(true)) prop.SetPreviewMode(showModules);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void Build(bool showModules)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出 Play 再构建 KayKit 样板。");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("当前场景有未保存修改，请先保存后再构建样板。");

            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(ArtFolder + "/Layout.json"));
            ValidateLayout(layout);
            PrepareImports();
            KayKitDestructionBuilder.PrepareGeometry(layout.variants.Where(v => v.destructible).Select(v => v.model));
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh();
            var material = CreateMaterial("DungeonAtlas", Color.white, true);
            var greyMaterial = CreateMaterial("GreyboxStone", new Color(0.37f, 0.43f, 0.51f), false);
            var baseMaterial = CreateMaterial("Foundation", new Color(0.055f, 0.075f, 0.10f), false);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null &&
                !AssetDatabase.CopyAsset("Assets/Scenes/Region_Test.unity", ScenePath))
                throw new InvalidOperationException("无法复制 Region_Test 作为样板基础。");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (string name in new[] { "KayKit_Layout", "Walls", "WallColliders", "HD2D_Ground", "Chest", "Chest_Visual" })
            {
                var old = scene.GetRootGameObjects().FirstOrDefault(go => go.name == name);
                if (old != null) Object.DestroyImmediate(old);
            }

            var root = new GameObject("KayKit_Layout");
            var modules = Child(root.transform, "Modules");
            var greybox = Child(root.transform, "Greybox");
            var collisions = Child(root.transform, "CollisionXY");
            var interactions = Child(root.transform, "InteractionsXY");
            var cells = Cells(layout);
            var rng = new System.Random(layout.seed);
            var floorNames = new[]
            {
                "floor_tile_small", "floor_tile_small", "floor_tile_small", "floor_tile_small",
                "floor_tile_small", "floor_tile_small", "floor_tile_small", "floor_tile_small",
                "floor_tile_small_weeds_A", "floor_tile_small_weeds_A", "floor_tile_small_decorated"
            };

            // 保证房间与门之间只有真实可行走的地面，不用隐形大地板填掉布局空洞。
            foreach (var cell in cells.OrderBy(p => p.y).ThenBy(p => p.x))
            {
                Vector3 at = new Vector3(cell.x * Cell, 0f, cell.y * Cell);
                var floor = Model(floorNames[rng.Next(floorNames.Length)], material, modules, 1f, at, rng.Next(4) * 90f);
                // 作者地砖的基准面为 Y=0.05；装饰物的最高点不能用于地面对齐。
                floor.transform.position += Vector3.down * 0.05f;
                Cube(greybox, "Floor", at - Vector3.up * 0.12f, new Vector3(Cell, 0.24f, Cell), greyMaterial);
                Cube(modules, "TileBase", at - Vector3.up * 0.16f, new Vector3(Cell, 0.2f, Cell), baseMaterial);

                foreach (var step in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                {
                    if (cells.Contains(cell + step)) continue;
                    Vector2 edge = new Vector2(at.x + step.x, at.z + step.y);
                    bool horizontal = step.y != 0;
                    bool low = step.y < 0;
                    string wall = low ? "wall_half" : rng.Next(4) == 0 ? "wall_cracked" : "wall";
                    Model(wall, material, modules, 0.5f, new Vector3(edge.x, 0f, edge.y), horizontal ? 0f : 90f);
                    Vector2 size = horizontal ? new Vector2(Cell, 0.5f) : new Vector2(0.5f, Cell);
                    Collider(collisions, "Boundary", edge, size);
                    Cube(greybox, "Wall", new Vector3(edge.x, low ? 0.5f : 1f, edge.y),
                        new Vector3(size.x, low ? 1f : 2f, size.y), greyMaterial);
                }
            }

            // 门框侧柱和补缝使用同一 XY 坐标；门板打开后不撤掉门框的实体碰撞。
            var door = BuildDoor(material);
            SavePrefab(door, "SideRoomDoor");
            Place(door, interactions, new Vector2(1f, -1f));
            Collider(collisions, "DoorFrame_North", new Vector2(1f, 0.34f), new Vector2(0.7f, 1.32f));
            Collider(collisions, "DoorFrame_South", new Vector2(1f, -2.34f), new Vector2(0.7f, 1.32f));

            var prefabs = new Dictionary<string, GameObject>();
            foreach (var variant in layout.variants)
            {
                var go = AnchoredModel(variant.id, variant.model, material, variant.scale);
                if (!string.IsNullOrEmpty(variant.detailModel))
                {
                    var visual = go.GetComponent<KayKitWorldAnchor>().visual;
                    var detail = Model(variant.detailModel, material, visual, variant.detailScale, Vector3.zero, 0f);
                    detail.transform.localPosition = new Vector3(variant.detailOffset[0], variant.detailOffset[1], variant.detailOffset[2]);
                }
                var bounds = VisualBounds(go);
                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(bounds.size.x, bounds.size.z);
                collider.offset = new Vector2(bounds.center.x, bounds.center.z);
                if (variant.destructible)
                    KayKitDestructionBuilder.Configure(go, "", variant.maxHealth, KayKitDestructionBuilder.Bake(go, variant.id));
                prefabs.Add(variant.id, SavePrefab(go, variant.id));
                Object.DestroyImmediate(go);
            }
            foreach (var prop in layout.props)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[prop.variant], scene);
                var definition = layout.variants.First(v => v.id == prop.variant);
                Place(go, definition.destructible ? interactions : modules, new Vector2(prop.x, prop.y));
                go.GetComponent<KayKitWorldAnchor>().visual.rotation = Quaternion.Euler(0f, prop.yaw, 0f);
                // 放置/旋转组合后重新取实际 XZ 占地，转换为玩法 XY 碰撞。
                var c = go.GetComponent<BoxCollider2D>();
                var bounds = VisualBounds(go);
                var grey = Cube(greybox, prop.variant, bounds.center, bounds.size, greyMaterial);
                if (definition.destructible)
                {
                    c.size = new Vector2(bounds.size.x, bounds.size.z);
                    c.offset = new Vector2(bounds.center.x - prop.x, bounds.center.z - prop.y);
                    var destructible = go.GetComponent<DestructibleProp>(); destructible.propId = prop.id;
                    destructible.greyboxVisual = grey; destructible.SetPreviewMode(showModules);
                }
                else
                {
                    Collider(collisions, prop.variant, new Vector2(bounds.center.x, bounds.center.z), new Vector2(bounds.size.x, bounds.size.z));
                    Object.DestroyImmediate(c);
                }
            }

            var chest = BuildChest(material);
            SavePrefab(chest, "TreasureChest");
            Place(chest, interactions, new Vector2(7f, 1.5f));
            var foundationBase = Cube(modules, "DioramaBase", new Vector3(-1f, -0.65f, 0f), new Vector3(24f, 0.3f, 20f), baseMaterial);
            foundationBase.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var region = Object.FindFirstObjectByType<RegionRoot>();
            region.regionId = RegionId;
            region.bounds = new Bounds(new Vector3(-1f, 0f, 0f), new Vector3(20f, 18f, 0f));
            var spawn = region.FindSpawn("default");
            spawn.position = new Vector3(-4f, -5f, 0f);
            GameObject.Find("Player").transform.position = spawn.position;
            MoveRoot("Portal_Rift", new Vector3(-4f, 5f, 0f));
            MoveRoot("Portal_Visual", new Vector3(-4f, 0f, 5f));
            var slimes = Object.FindObjectsByType<ActionEnemy>(FindObjectsSortMode.None);
            int enemyIndex = 0;
            foreach (var enemy in slimes)
            {
                enemy.transform.position = new Vector3(-6f + (enemyIndex++ % 2) * 3f, 1f + enemyIndex, 0f);
                var brain = enemy.GetComponent<EnemyBrain>();
                if (brain != null) { brain.aggroRange = 3f; brain.loseTargetRange = 5f; }
            }
            var light = GameObject.Find("Directional Light").GetComponent<Light>();
            light.intensity = 1.5f;
            var atmosphere = Object.FindFirstObjectByType<Hd2dAtmosphere>();
            if (atmosphere != null) { atmosphere.fogDensity = 0.004f; atmosphere.ambient = new Color(0.5f, 0.54f, 0.61f); }
            var follow = Camera.main.GetComponent<CameraFollow>();
            follow.hd2dOffset = new Vector3(0f, 10f, -15f);
            follow.hd2dEuler = new Vector3(33f, 0f, 0f);
            Camera.main.transform.SetPositionAndRotation(new Vector3(-4f, 10f, -20f), Quaternion.Euler(follow.hd2dEuler));

            modules.gameObject.SetActive(showModules);
            greybox.gameObject.SetActive(!showModules);
            RegisterRegion(region.bounds);
            if (!EditorBuildSettings.scenes.Any(item => item.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            File.WriteAllText(GeneratedFolder + "/BuildReport.json", JsonUtility.ToJson(new BuildReport
            {
                seed = layout.seed, floorCells = cells.Count, propVariants = layout.variants.Length,
                props = layout.props.Length, colliders = root.GetComponentsInChildren<Collider2D>(true).Length,
                modulesVisible = showModules
            }, true));
            Debug.Log($"[KayKit] 样板构建完成：{cells.Count} 地面模块，{layout.variants.Length} 道具变体，{layout.props.Length} 道具，灰盒与模块共用碰撞。场景：{ScenePath}");
        }

        [Serializable] class BuildReport { public int seed, floorCells, propVariants, props, colliders; public bool modulesVisible; }

        static HashSet<Vector2Int> Cells(Layout layout)
        {
            var result = new HashSet<Vector2Int>();
            foreach (var room in layout.rooms)
                for (int y = room.minY; y <= room.maxY; y++)
                    for (int x = room.minX; x <= room.maxX; x++) result.Add(new Vector2Int(x, y));
            return result;
        }

        static void ValidateLayout(Layout layout)
        {
            if (layout == null || layout.rooms == null || layout.variants == null || layout.props == null)
                throw new InvalidDataException("Layout.json 缺少布局字段。");
            if (layout.variants.Select(v => v.id).Distinct().Count() != layout.variants.Length)
                throw new InvalidDataException("道具变体 id 重复。");
            var cells = Cells(layout);
            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>(); queue.Enqueue(cells.First());
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue(); if (!visited.Add(cell)) continue;
                foreach (var dir in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    if (cells.Contains(cell + dir) && !visited.Contains(cell + dir)) queue.Enqueue(cell + dir);
            }
            if (visited.Count != cells.Count) throw new InvalidDataException("地面布局不连通。");
            foreach (var variant in layout.variants)
                if (variant.scale <= 0f || (!string.IsNullOrEmpty(variant.detailModel) &&
                    (variant.detailOffset == null || variant.detailOffset.Length != 3 || variant.detailScale <= 0f)))
                    throw new InvalidDataException("道具尺寸无效：" + variant.id);
            foreach (var prop in layout.props)
                if (!layout.variants.Any(v => v.id == prop.variant) ||
                    !cells.Contains(new Vector2Int(Mathf.RoundToInt(prop.x / Cell), Mathf.RoundToInt(prop.y / Cell))))
                    throw new InvalidDataException("道具位置或变体无效：" + prop.variant);
            var destructibles = layout.props.Where(p => layout.variants.First(v => v.id == p.variant).destructible).ToArray();
            if (destructibles.Any(p => string.IsNullOrEmpty(p.id)) || destructibles.Select(p => p.id).Distinct().Count() != destructibles.Length)
                throw new InvalidDataException("可破坏物需要唯一且稳定的 id。");
        }

        static void PrepareImports()
        {
            foreach (string path in Directory.GetFiles(ArtFolder + "/Models", "*.fbx"))
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("请先完成 FBX 导入：" + path);
                if (importer.materialImportMode == ModelImporterMaterialImportMode.None) continue;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = false;
                importer.SaveAndReimport();
            }
            var textureImporter = AssetImporter.GetAtPath(ArtFolder + "/dungeon_texture.png") as TextureImporter;
            if (textureImporter != null && textureImporter.maxTextureSize != 1024)
            {
                textureImporter.maxTextureSize = 1024;
                textureImporter.SaveAndReimport();
            }
        }

        static Material CreateMaterial(string name, Color color, bool textured)
        {
            string path = GeneratedFolder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("缺少 URP Lit shader。");
                mat = new Material(shader); AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.05f);
            mat.SetFloat("_Metallic", 0f);
            if (textured) mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtFolder + "/dungeon_texture.png"));
            EditorUtility.SetDirty(mat); return mat;
        }

        static GameObject Model(string name, Material material, Transform parent, float scale, Vector3 position, float yaw)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ArtFolder + "/Models/" + name + ".fbx");
            if (asset == null) throw new InvalidOperationException("模型未导入：" + name);
            var go = Object.Instantiate(asset, parent);
            go.name = name;
            go.transform.localScale = Vector3.one * scale;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.position = position;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                renderer.receiveShadows = true;
            }
            return go;
        }

        static GameObject AnchoredModel(string name, string model, Material material, float scale)
        {
            var go = new GameObject(name);
            var visual = Model(model, material, go.transform, scale, Vector3.zero, 0f);
            var anchor = go.AddComponent<KayKitWorldAnchor>(); anchor.visual = visual.transform; anchor.Sync();
            return go;
        }

        static GameObject BuildDoor(Material material)
        {
            var go = AnchoredModel("SideRoomDoor", "wall_doorway", material, 0.75f);
            var anchor = go.GetComponent<KayKitWorldAnchor>(); anchor.visual.rotation = Quaternion.Euler(0f, 90f, 0f);
            var leaf = anchor.visual.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "wall_doorway_door");
            if (leaf == null) throw new InvalidOperationException("KayKit 门板节点未找到。");
            var collider = go.AddComponent<BoxCollider2D>(); collider.size = new Vector2(0.65f, 1.25f);
            var door = go.AddComponent<KayKitDoor>(); door.doorLeaf = leaf; door.passageBlocker = collider;
            return go;
        }

        static GameObject BuildChest(Material material)
        {
            var go = AnchoredModel("KayKitChest", "chest", material, 0.95f);
            var anchor = go.GetComponent<KayKitWorldAnchor>();
            anchor.visual.rotation = Quaternion.Euler(0f, 180f, 0f);
            var lid = anchor.visual.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "chest_lid");
            if (lid == null) throw new InvalidOperationException("KayKit 宝箱盖节点未找到。");
            var axis = Child(lid.parent, "LidAxis"); axis.localPosition = lid.localPosition;
            axis.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var hinge = Child(axis, "LidHinge"); lid.SetParent(hinge, true);
            var view = go.AddComponent<ChestView>(); view.lidHinge = hinge; view.openAngle = -105f;
            go.AddComponent<BoxCollider2D>().size = new Vector2(1.52f, 1.16f);
            var chest = go.AddComponent<Chest>(); chest.chestId = "kaykit_side_room_chest";
            chest.gold = 10; chest.exp = 0; chest.lootItemIds = new string[0]; chest.view = view;
            return go;
        }

        static GameObject SavePrefab(GameObject go, string name)
            => PrefabUtility.SaveAsPrefabAsset(go, PrefabFolder + "/" + name + ".prefab");

        static void Place(GameObject go, Transform parent, Vector2 position)
        {
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(position.x, position.y, 0f);
            go.GetComponent<KayKitWorldAnchor>().Sync();
        }

        static void RegisterRegion(Bounds bounds)
        {
            const string path = "Assets/Resources/Regions/Region_KayKit.asset";
            var region = AssetDatabase.LoadAssetAtPath<RegionDefinition>(path);
            if (region == null) { region = ScriptableObject.CreateInstance<RegionDefinition>(); AssetDatabase.CreateAsset(region, path); }
            region.id = RegionId; region.displayName = "遗迹庭院 · KayKit 样板";
            region.sceneName = "Region_KayKit"; region.mode = RegionMode.ActionRPG;
            region.bounds = bounds; region.defaultSpawnPointId = "default"; EditorUtility.SetDirty(region);
        }

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
        }

        static void Collider(Transform parent, string name, Vector2 at, Vector2 size)
        {
            var go = Child(parent, name).gameObject; go.transform.position = new Vector3(at.x, at.y, 0f);
            go.AddComponent<BoxCollider2D>().size = size;
        }

        static GameObject Cube(Transform parent, string name, Vector3 at, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent, false); go.transform.position = at; go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static Bounds VisualBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        static void MoveRoot(string name, Vector3 at)
        {
            var go = GameObject.Find(name); if (go != null) go.transform.position = at;
        }
    }
}
