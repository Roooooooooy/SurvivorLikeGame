using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RpgGame;

namespace RpgGameEditor
{
    // Phase 0 一键搭建：区域资产、章节资产、Boot/MainMenu/Region_Test 场景、Build Settings。
    // 可重复执行；场景会被覆盖重建，区域/章节资产会原地更新。
    public static class Phase0SkeletonBuilder
    {
        const string SceneFolder = "Assets/Scenes";
        const string RegionFolder = "Assets/Resources/Regions";
        const string ChapterFolder = "Assets/Resources/Chapters";

        // 宝箱：Treasure Pack（CC0）。箱体/盖子/锁是三个独立网格，盖子可以绕合页旋转打开。
        const string ChestBodyPath = "Assets/Resources/Props/CC0/TreasurePack/chest_body.obj";
        const string ChestLidPath = "Assets/Resources/Props/CC0/TreasurePack/chest_lid.obj";
        const string ChestLockPath = "Assets/Resources/Props/CC0/TreasurePack/chest_lock.obj";
        const string ChestCoinStarPath = "Assets/Resources/Props/CC0/TreasurePack/coin-star.obj";
        const string ChestCoinCrownPath = "Assets/Resources/Props/CC0/TreasurePack/coin-crown.obj";
        const string ChestCoinDiamondPath = "Assets/Resources/Props/CC0/TreasurePack/coin-diamond.obj";
        const string ChestWoodMatPath = "Assets/Settings/HD2D_ChestWood.mat";
        const string ChestWoodTexPath = "Assets/Settings/HD2D_ChestWoodGrain.png";
        const string ChestMetalMatPath = "Assets/Settings/HD2D_ChestMetal.mat";
        const string ChestGlowMatPath = "Assets/Settings/HD2D_ChestGlow.mat";
        const string PortalStoneMatPath = "Assets/Settings/HD2D_PortalStone.mat";

        // 传送门：LowPolyPortal 圆环 + 双层螺旋漩涡 + 地面辉光。
        const string PortalRingModelPath = "Assets/Resources/Props/CC0/LowPolyPortal/Portal.obj";
        const string PortalRingMatPath = "Assets/Settings/HD2D_PortalRingRock.mat";
        const string PortalVortexMatPath = "Assets/Settings/HD2D_PortalVortex.mat";
        const string PortalCoreMatPath = "Assets/Settings/HD2D_PortalVortexCore.mat";
        const string PortalGroundGlowMatPath = "Assets/Settings/HD2D_PortalGroundGlow.mat";
        const string PortalRuneMatPath = "Assets/Settings/HD2D_PortalRune.mat";

        [MenuItem("工具/角色扮演/构建 Phase 0 骨架")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder("Assets/Resources");
            EnsureFolder(RegionFolder);
            EnsureFolder(ChapterFolder);

            var region = BuildRegionAsset();
            BuildChapterAsset(region);

            BuildBootScene();
            BuildMainMenuScene();
            BuildRegionScene(region);

            RegisterScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[RPG] Phase 0 骨架已生成。打开 Assets/Scenes/Boot.unity 进入 Play。");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
                AssetDatabase.CreateFolder(parent, name);
        }

        static RegionDefinition BuildRegionAsset()
        {
            const string path = RegionFolder + "/Region_Test.asset";
            var region = AssetDatabase.LoadAssetAtPath<RegionDefinition>(path);

            if (region == null)
            {
                region = ScriptableObject.CreateInstance<RegionDefinition>();
                AssetDatabase.CreateAsset(region, path);
            }

            region.id = "region_test";
            region.displayName = "Test Region";
            region.sceneName = "Region_Test";
            region.mode = RegionMode.ActionRPG;
            region.bounds = new Bounds(Vector3.zero, new Vector3(40f, 22f, 0f));
            region.defaultSpawnPointId = "default";

            EditorUtility.SetDirty(region);
            return region;
        }

        static void BuildChapterAsset(RegionDefinition region)
        {
            const string path = ChapterFolder + "/Chapter_01.asset";
            var chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(path);

            if (chapter == null)
            {
                chapter = ScriptableObject.CreateInstance<ChapterDefinition>();
                AssetDatabase.CreateAsset(chapter, path);
            }

            chapter.id = "chapter_01";
            chapter.displayName = "Chapter 1";
            chapter.order = 1;
            chapter.regions = new List<RegionDefinition> { region };

            EditorUtility.SetDirty(chapter);
        }

        static void BuildBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(new Color(0.05f, 0.05f, 0.07f));

            var boot = new GameObject("BootEntry");
            boot.AddComponent<BootEntry>();

            EditorSceneManager.SaveScene(scene, SceneFolder + "/Boot.unity");
        }

        static void BuildMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(new Color(0.04f, 0.04f, 0.06f));

            var menu = new GameObject("MainMenu");
            menu.AddComponent<MainMenuController>();

            EditorSceneManager.SaveScene(scene, SceneFolder + "/MainMenu.unity");
        }

        static void BuildRegionScene(RegionDefinition region)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 动作区直接复用 SampleScene 的 HD-2D 视角：
            // 透视俯角 FOV 35 + 偏移 (0,9,-14) + 旋转 (30,0,0)。
            CreateHd2dCamera();
            BuildDirectionalLight();

            var regionGo = new GameObject("RegionRoot");
            var root = regionGo.AddComponent<RegionRoot>();
            root.regionId = region.id;
            root.mode = region.mode;
            root.defaultSpawnPointId = region.defaultSpawnPointId;
            root.bounds = region.bounds;

            var spawnGo = new GameObject("Spawn_default");
            spawnGo.transform.SetParent(regionGo.transform, false);
            spawnGo.transform.position = Vector3.zero;
            root.spawnPoints.Add(new SpawnPoint { id = "default", point = spawnGo.transform });

            BuildHd2dGround();
            BuildWallColliders(region.bounds);
            BuildVisualWalls(region.bounds);

            BuildPlayer();
            BuildPixelPlayer();

            BuildEnemy(new Vector3(3.5f, 0.5f, 0f));
            BuildEnemy(new Vector3(-4f, 3f, 0f));

            // 哥布林战士（8 方向、待机/奔跑/受击/攻击/死亡）。
            // 预制体由 工具/角色扮演/哥布林战士/2. 生成预制体 创建；缺失时跳过。
            BuildGoblinPrefab(new Vector3(6.5f, 3.5f, 0f), "Enemy_GoblinWarrior_A");
            BuildGoblinPrefab(new Vector3(-7f, -3.5f, 0f), "Enemy_GoblinWarrior_B");

            BuildChest(new Vector3(-3f, -1.5f, 0f));
            BuildRiftPortal(new Vector3(0f, 4.5f, 0f));

            BuildHd2dVolume();
            BuildHd2dBridge();

            // 冷色补光 + 雾/环境光；运行时 Awake 负责创建子光源。
            var atmosphere = new GameObject("Hd2dAtmosphere");
            atmosphere.AddComponent<Hd2dAtmosphere>();

            var hud = new GameObject("DebugHud");
            hud.AddComponent<DebugHud>();

            EditorSceneManager.SaveScene(scene, SceneFolder + "/Region_Test.unity");
        }

        static Camera CreateHd2dCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";

            var cam = go.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = 35f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 150f;
            cam.allowHDR = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f);
            cam.transform.SetPositionAndRotation(
                new Vector3(0f, 9f, -14f), Quaternion.Euler(30f, 0f, 0f));

            var data = go.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.renderShadows = true;

            var follow = go.AddComponent<CameraFollow>();
            follow.hd2d = true;
            follow.hd2dOffset = new Vector3(0f, 9f, -14f);
            follow.hd2dEuler = new Vector3(30f, 0f, 0f);

            go.AddComponent<AudioListener>();
            return cam;
        }

        static void BuildDirectionalLight()
        {
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.90f, 0.76f);
            light.intensity = 2.6f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;
            light.shadowBias = 0.02f;
            light.shadowNormalBias = 0.3f;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.008f;
            RenderSettings.fogColor = new Color(0.08f, 0.09f, 0.12f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.48f, 0.55f);
        }

        static Camera CreateCamera(Color background)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.transform.position = new Vector3(0f, 0f, -10f);

            go.AddComponent<AudioListener>();
            return cam;
        }

        static Sprite LoadSprite(string path, string fallbackPath = null)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null && !string.IsNullOrEmpty(fallbackPath))
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(fallbackPath);

            if (sprite == null)
                Debug.LogWarning("[RPG] 找不到 Sprite: " + path);

            return sprite;
        }

        static void BuildHd2dGround()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "HD2D_Ground";

            var meshCollider = go.GetComponent<MeshCollider>();
            if (meshCollider != null) Object.DestroyImmediate(meshCollider);

            go.transform.localScale = new Vector3(20f, 1f, 20f); // 200 x 200

            var renderer = go.GetComponent<MeshRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/HD2D_GroundGame.mat");
            if (mat != null) renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;

            var follow = go.AddComponent<Hd2dGroundFollow>();
            follow.target = null;

            // 跟随玩家的暖色点光，和 SampleScene 一致
            var torch = new GameObject("GroundTorch");
            torch.transform.SetParent(go.transform, false);
            torch.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            var light = torch.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.30f);
            light.intensity = 1.1f;
            light.range = 7f;
            light.shadows = LightShadows.None;
        }

        // 2D 碰撞边界：玩法仍走 XY 平面。
        static void BuildWallColliders(Bounds bounds)
        {
            var root = new GameObject("WallColliders");

            CreateWallCollider(root.transform, "Top",
                new Vector2(0f, bounds.max.y + 0.5f),
                new Vector2(bounds.size.x + 2f, 1f));
            CreateWallCollider(root.transform, "Bottom",
                new Vector2(0f, bounds.min.y - 0.5f),
                new Vector2(bounds.size.x + 2f, 1f));
            CreateWallCollider(root.transform, "Left",
                new Vector2(bounds.min.x - 0.5f, 0f),
                new Vector2(1f, bounds.size.y + 2f));
            CreateWallCollider(root.transform, "Right",
                new Vector2(bounds.max.x + 0.5f, 0f),
                new Vector2(1f, bounds.size.y + 2f));
        }

        static void CreateWallCollider(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject("Wall_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        // 3D 可见墙面：只负责 HD-2D 画面，不参与 2D 碰撞。
        static void BuildVisualWalls(Bounds bounds)
        {
            var root = new GameObject("Walls");
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/HD2D_DarkStone.mat");

            CreateVisualWall(root.transform, "Top",
                new Vector3(0f, 1.25f, bounds.max.y + 0.5f),
                new Vector3(bounds.size.x + 2f, 2.5f, 1f), mat);
            CreateVisualWall(root.transform, "Bottom",
                new Vector3(0f, 1.25f, bounds.min.y - 0.5f),
                new Vector3(bounds.size.x + 2f, 2.5f, 1f), mat);
            CreateVisualWall(root.transform, "Left",
                new Vector3(bounds.min.x - 0.5f, 1.25f, 0f),
                new Vector3(1f, 2.5f, bounds.size.y + 2f), mat);
            CreateVisualWall(root.transform, "Right",
                new Vector3(bounds.max.x + 0.5f, 1.25f, 0f),
                new Vector3(1f, 2.5f, bounds.size.y + 2f), mat);
        }

        static void CreateVisualWall(Transform parent, string name, Vector3 position, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Wall_" + name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = size;

            var collider = go.GetComponent<BoxCollider>();
            if (collider != null) Object.DestroyImmediate(collider);

            var renderer = go.GetComponent<MeshRenderer>();
            if (mat != null) renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        static void BuildHd2dVolume()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/HD2D_Profile.asset");
            if (profile != null)
            {
                var go = new GameObject("HD2D_GlobalVolume");
                var volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profile;
            }

            var tuning = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/HD2D_GameTuning.asset");
            if (tuning != null)
            {
                var go = new GameObject("HD2D_GameTuning");
                var volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10f;
                volume.sharedProfile = tuning;
            }
        }

        static void BuildHd2dBridge()
        {
            var go = new GameObject("HD2D_Bridge");
            var bridge = go.AddComponent<Hd2dSpriteBridge>();
            bridge.playerAmbient = 0.75f;
            bridge.playerTint = new Color(0.96f, 0.98f, 1f, 1f);
            bridge.playerScale = 1.30f;
            bridge.propLibrary = AssetDatabase.LoadAssetAtPath<Hd2dPropLibrary>(
                "Assets/Resources/HD2D/PropLibrary.asset");
        }

        static GameObject BuildPlayer()
        {
            var go = new GameObject("Player");
            go.transform.position = Vector3.zero;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.32f;

            // 视觉由 HD2D_PixelPlayer 接管，逻辑物体不再挂 SpriteRenderer。

            var motor = go.AddComponent<PlayerMotor>();
            motor.moveSpeed = 5f;

            var health = go.AddComponent<PlayerHealth>();
            health.maxHealth = 100;

            var combat = go.AddComponent<ActionCombatController>();
            combat.damage = 10;
            combat.ResetCombo();

            var interactor = go.AddComponent<WorldInteractor>();
            interactor.radius = 1.8f;

            var rig = go.AddComponent<PlayerModeRig>();
            rig.motor = motor;
            rig.health = health;
            rig.actionCombat = combat;
            rig.interactor = interactor;

            var hitboxGo = new GameObject("AttackHitbox");
            hitboxGo.transform.SetParent(go.transform, false);
            hitboxGo.transform.localPosition = Vector3.zero;

            var hitboxCollider = hitboxGo.AddComponent<BoxCollider2D>();
            hitboxCollider.isTrigger = true;
            hitboxCollider.size = new Vector2(1.1f, 0.9f);

            var hitbox = hitboxGo.AddComponent<Hitbox>();
            hitbox.damage = 10;
            hitbox.knockback = 1.0f;
            hitbox.owner = go;
            combat.hitbox = hitbox;
            hitboxGo.SetActive(false);

            return go;
        }

        // 与 SampleScene 完全一致的 8 方向像素猫视图；它会在运行时跟随 PlayerHealth。
        static void BuildPixelPlayer()
        {
            var go = new GameObject("HD2D_PixelPlayer");
            var view = go.AddComponent<Hd2dPixelPlayer>();
            view.viewScale = 1.6f;
            view.worldYOffset = 0f;
            view.startDirection = 6; // 南 / 正面
            view.runSpeedReference = 5f;
            view.maxRunFps = 16f;
            view.dust = true;
            view.dustColor = new Color(0.85f, 0.80f, 0.70f, 0.40f);
            view.dustSize = 0.45f;
            view.dustRate = 24f;
            view.turnDust = true;
            view.turnDustCount = 3;
            view.stopDustCount = 5;
            view.turnDustCooldown = 0.12f;
            view.shadowDiameter = 0.85f;
            view.shadowDepth = 0.50f;
            view.shadowOpacity = 0.42f;
            view.litAmbient = 0.85f;
            view.xrayColor = new Color(0.35f, 0.82f, 1f, 0.45f);
        }

        static void BuildEnemy(Vector3 position)
        {
            var go = new GameObject("Enemy_Slime");
            go.transform.position = position;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            // 更重、更强阻尼，避免被玩家碰撞的惯性推着一直滑走。
            body.mass = 20f;
            body.linearDamping = 10f;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.35f;

            var view = go.AddComponent<SpriteRenderer>();
            view.sprite = LoadSprite(
                "Assets/Textures/enemy_slime_green.png",
                "Assets/Textures/gray.png");
            view.sortingOrder = 5;

            // enemy_slime_green 是 320x64 的 10 帧 x 2 行图集，直接当 Sprite 会把所有帧铺开。
            var sheet = go.AddComponent<SpriteSheetFrameView>();
            sheet.frameWidth = 32;
            sheet.frameHeight = 32;
            sheet.framesPerRow = 10;
            sheet.row = 0;
            sheet.pixelsPerUnit = 16f;
            sheet.animate = true;
            sheet.fps = 10f;

            var enemy = go.AddComponent<ActionEnemy>();
            enemy.maxHealth = 30;
            enemy.knockbackResistance = 0.75f;

            var brain = go.AddComponent<EnemyBrain>();
            brain.moveSpeed = 2.2f;
            brain.attackRange = 1.1f;
            brain.attackDamage = 8;
        }

        // 哥布林战士预制体：缺失时只记录警告，不阻断场景搭建。
        static void BuildGoblinPrefab(Vector3 position, string name)
        {
            const string path = "Assets/Prefabs/Enemy_GoblinWarrior.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("[RPG] 找不到 " + path +
                                 "，跳过哥布林。先执行 工具/角色扮演/哥布林战士/2. 生成预制体");
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.position = position;
        }

        // 可开盖宝箱：Treasure Pack（CC0）。
        // 箱体/锁/盖子分属三个网格，盖子挂在后侧合页上；逻辑碰撞体放在 XY 且为实体。
        internal static void BuildChest(Vector3 position)
        {
            var visual = new GameObject("Chest_Visual");
            visual.transform.position = new Vector3(position.x, 0f, position.y);

            var modelRoot = new GameObject("Chest_Model");
            modelRoot.transform.SetParent(visual.transform, false);
            // 模型正面（锁）朝 +X，转 +90° 后面向相机（-Z）；
            // 合页留在背面（+Z），盖子向远离相机的方向翻开。
            modelRoot.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            modelRoot.transform.localScale = Vector3.one * 1.20f;

            var woodTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ChestWoodTexPath);
            var wood = LoadOrCreateLitMaterial(ChestWoodMatPath, woodTex,
                new Color(0.85f, 0.80f, 0.75f));
            if (wood != null)
            {
                if (woodTex != null)
                {
                    wood.SetTexture("_BaseMap", woodTex);
                    wood.SetTexture("_MainTex", woodTex);
                    wood.SetTextureScale("_BaseMap", new Vector2(2f, 2f));
                }
                wood.SetFloat("_Smoothness", 0.20f);
                wood.SetFloat("_Metallic", 0f);
                EditorUtility.SetDirty(wood);
            }

            var metal = LoadOrCreateLitMaterial(ChestMetalMatPath, null, new Color(0.95f, 0.68f, 0.18f));
            if (metal != null)
            {
                metal.SetFloat("_Smoothness", 0.72f);
                metal.SetFloat("_Metallic", 0.8f);
                EditorUtility.SetDirty(metal);
            }

            bool built = InstantiateModel(ChestBodyPath, modelRoot.transform, Vector3.zero, wood) != null;
            InstantiateModel(ChestLockPath, modelRoot.transform, Vector3.zero, metal);

            var hinge = new GameObject("ChestLidHinge");
            hinge.transform.SetParent(modelRoot.transform, false);
            hinge.transform.localPosition = new Vector3(-0.3214f, 0.40f, 0f);
            InstantiateModel(ChestLidPath, hinge.transform,
                new Vector3(0.3214f, -0.40f, 0f), wood);

            if (!built)
            {
                Object.DestroyImmediate(visual);
                BuildSpriteChest(position);
                return;
            }

            // 金币堆：开盖后才显示。
            var lootRoot = new GameObject("ChestLoot");
            lootRoot.transform.SetParent(modelRoot.transform, false);
            // 箱体顶面是封闭的，金币/金光要放在箱口上方才看得见。
            lootRoot.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            CreateCoin(ChestCoinStarPath, lootRoot.transform, new Vector3(0f, 0.03f, 0f), 0.014f, 20f, metal);
            CreateCoin(ChestCoinCrownPath, lootRoot.transform, new Vector3(-0.10f, 0.01f, 0.06f), 0.012f, -35f, metal);
            CreateCoin(ChestCoinDiamondPath, lootRoot.transform, new Vector3(0.11f, 0.02f, -0.05f), 0.011f, 60f, metal);

            // 箱内金光：开盖后才出现，让“已打开”有明确的视觉反馈。
            var chestGlow = LoadOrCreateShaderMaterial(ChestGlowMatPath, "HD2D/GroundGlow");
            if (chestGlow != null)
            {
                chestGlow.SetColor("_Color", new Color(1f, 0.55f, 0.12f, 1f));
                chestGlow.SetFloat("_Intensity", 1.35f);
                chestGlow.SetFloat("_Falloff", 1.5f);
                chestGlow.SetFloat("_Pulse", 0.6f);
                EditorUtility.SetDirty(chestGlow);
            }
            CreateGroundGlow("Chest_InnerGlow", lootRoot.transform,
                new Vector3(0f, 0.01f, 0f), new Vector3(0.62f, 0.78f, 1f), chestGlow);

            lootRoot.SetActive(false);

            var lightGo = new GameObject("ChestGlow");
            lightGo.transform.SetParent(modelRoot.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.70f, 0.28f);
            light.intensity = 0f;
            light.range = 2.6f;
            light.shadows = LightShadows.None;

            var view = visual.AddComponent<ChestView>();
            view.lidHinge = hinge.transform;
            view.openAngle = 105f;
            view.openDuration = 0.42f;
            view.glowLight = light;
            view.closedLightIntensity = 0f;
            view.openLightIntensity = 0.9f;
            view.lootRoot = lootRoot;

            // 逻辑体：实体碰撞（玩家不能穿过），交互由 WorldInteractor 的 Overlap 检测。
            // 碰撞体比视觉轮廓略大，并向背面偏移，覆盖打开后的盖子，避免猫贴身穿模。
            var logic = new GameObject("Chest");
            logic.transform.position = position;

            var collider2D = logic.AddComponent<BoxCollider2D>();
            collider2D.isTrigger = false;
            collider2D.size = new Vector2(1.60f, 1.15f);
            collider2D.offset = new Vector2(0f, 0.12f);

            var chest = logic.AddComponent<Chest>();
            chest.chestId = "chest_01";
            chest.displayName = "Chest";
            chest.gold = 15;
            chest.exp = 25;
            chest.view = view;
        }

        // 裂隙传送门：LowPolyPortal 圆环 + 双层螺旋漩涡 + 地面辉光 + 点光 + 内吸光点。
        internal static void BuildRiftPortal(Vector3 position)
        {
            var visual = new GameObject("Portal_Visual");
            visual.transform.position = new Vector3(position.x, 0f, position.y);

            var stone = LoadOrCreateLitMaterial(PortalStoneMatPath, null,
                new Color(0.17f, 0.16f, 0.21f));
            if (stone != null)
            {
                stone.SetFloat("_Smoothness", 0.24f);
                stone.SetFloat("_Metallic", 0.08f);
                EditorUtility.SetDirty(stone);
            }

            // 只保留基座；主体改用 LowPolyPortal 圆环。
            CreateStoneBlock(visual.transform, "Portal_Base",
                new Vector3(0f, 0.08f, 0f), new Vector3(2.35f, 0.16f, 0.72f),
                stone, Vector3.zero);

            // LowPolyPortal 模型本身在 YZ 平面（法线朝 X），旧版没转 90°，所以是侧着的。
            // 这里绕 Y 转 90° 让圆环正对相机，再压成椭圆外框。
            // 材质改用和墙面同系的岩石贴图，避免旧 Portal 贴图在点光下发白。
            var ringMat = LoadOrCreateLitMaterial(PortalRingMatPath, null,
                new Color(0.78f, 0.76f, 0.84f));
            var darkStone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/HD2D_DarkStone.mat");
            if (ringMat != null)
            {
                var stoneTex = darkStone != null ? darkStone.GetTexture("_BaseMap") : null;
                if (stoneTex != null)
                {
                    ringMat.SetTexture("_BaseMap", stoneTex);
                    ringMat.SetTexture("_MainTex", stoneTex);
                }
                ringMat.SetFloat("_Smoothness", 0.18f);
                ringMat.SetFloat("_Metallic", 0.05f);
                EditorUtility.SetDirty(ringMat);
            }
            if (ringMat == null) ringMat = stone;

            var ring = InstantiateModel(PortalRingModelPath, visual.transform,
                new Vector3(0f, 0.197f, 0f), ringMat, new Vector3(0f, 90f, 0f));
            if (ring != null)
                ring.transform.localScale = new Vector3(0.45f, 0.46f, 0.38f);

            // 柱内侧的发光符文条。
            var rune = LoadOrCreateEmissiveMaterial(PortalRuneMatPath,
                new Color(0.20f, 0.08f, 0.32f), new Color(0.55f, 0.28f, 1f) * 1.2f);
            for (int i = 0; i < 3; i++)
            {
                float y = 0.62f + i * 0.56f;
                CreateStoneBlock(visual.transform, "Portal_Rune_L" + i,
                    new Vector3(-0.815f, y, -0.32f), new Vector3(0.045f, 0.30f, 0.05f),
                    rune, Vector3.zero);
                CreateStoneBlock(visual.transform, "Portal_Rune_R" + i,
                    new Vector3(0.815f, y, -0.32f), new Vector3(0.045f, 0.30f, 0.05f),
                    rune, Vector3.zero);
            }

            // 两层反向旋转的螺旋面，叠出纵深。
            var vortex = LoadOrCreateShaderMaterial(PortalVortexMatPath, "HD2D/PortalVortex");
            if (vortex != null)
            {
                vortex.SetColor("_ColorInner", new Color(0.48f, 0.16f, 1f, 1f));
                vortex.SetColor("_ColorOuter", new Color(0.07f, 0.34f, 1f, 1f));
                vortex.SetFloat("_Intensity", 0.48f);
                vortex.SetFloat("_Speed", 1.5f);
                vortex.SetFloat("_Twist", 2.4f);
                vortex.SetFloat("_NoiseScale", 6f);
                vortex.SetFloat("_RimPower", 0.30f);
                vortex.SetFloat("_Seed", 0f);
                EditorUtility.SetDirty(vortex);
            }
            CreateQuad("Portal_Vortex", visual.transform,
                new Vector3(0f, 1.20f, -0.06f), new Vector3(1.50f, 1.80f, 1f), vortex);

            var core = LoadOrCreateShaderMaterial(PortalCoreMatPath, "HD2D/PortalVortex");
            if (core != null)
            {
                core.SetColor("_ColorInner", new Color(0.72f, 0.52f, 1f, 1f));
                core.SetColor("_ColorOuter", new Color(0.22f, 0.52f, 1f, 1f));
                core.SetFloat("_Intensity", 0.28f);
                core.SetFloat("_Speed", -2.3f);
                core.SetFloat("_Twist", 4.6f);
                core.SetFloat("_NoiseScale", 9f);
                core.SetFloat("_RimPower", 0.20f);
                core.SetFloat("_Seed", 4.7f);
                EditorUtility.SetDirty(core);
            }
            CreateQuad("Portal_VortexCore", visual.transform,
                new Vector3(0f, 1.20f, -0.10f), new Vector3(1.06f, 1.38f, 1f), core);

            var groundGlow = LoadOrCreateShaderMaterial(PortalGroundGlowMatPath, "HD2D/GroundGlow");
            if (groundGlow != null)
            {
                groundGlow.SetColor("_Color", new Color(0.42f, 0.18f, 1f, 1f));
                groundGlow.SetFloat("_Intensity", 0.45f);
                groundGlow.SetFloat("_Falloff", 2.2f);
                groundGlow.SetFloat("_Pulse", 1f);
                EditorUtility.SetDirty(groundGlow);
            }
            CreateGroundGlow("Portal_GroundGlow", visual.transform,
                new Vector3(0f, 0.025f, 0.05f), new Vector3(3.6f, 2.5f, 1f), groundGlow);

            var lightGo = new GameObject("PortalLight");
            lightGo.transform.SetParent(visual.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.20f, -0.65f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.55f, 0.34f, 0.95f);
            light.intensity = 1.1f;
            light.range = 5.0f;
            light.shadows = LightShadows.None;

            var view = visual.AddComponent<PortalVortexView>();
            view.glowLight = light;
            view.baseLightIntensity = 1.1f;
            view.vortexSize = new Vector2(1.50f, 1.80f);
            view.vortexCenter = new Vector3(0f, 1.20f, -0.12f);

            // 逻辑体：实体碰撞（挡在门前），按 E 交互进入裂隙。
            var logic = new GameObject("Portal_Rift");
            logic.transform.position = position;

            var collider2D = logic.AddComponent<BoxCollider2D>();
            collider2D.isTrigger = false;
            collider2D.size = new Vector2(1.90f, 0.70f);

            var portal = logic.AddComponent<Portal>();
            portal.displayName = "Survivor Rift";
            portal.survivorRift = true;
            portal.survivorSceneName = "SampleScene";
            portal.difficultyId = "normal";
            portal.targetDuration = 120f;
        }

        static GameObject InstantiateModel(string path, Transform parent, Vector3 localPosition,
            Material material, Vector3? localEuler = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("[RPG] 找不到模型: " + path);
                return null;
            }

            var go = Object.Instantiate(prefab);
            go.name = Path.GetFileNameWithoutExtension(path);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localEuler.HasValue
                ? Quaternion.Euler(localEuler.Value)
                : Quaternion.identity;
            go.transform.localScale = Vector3.one;

            foreach (var collider in go.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            if (material != null)
            {
                foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                {
                    int count = Mathf.Max(1, renderer.sharedMaterials.Length);
                    var mats = new Material[count];
                    for (int i = 0; i < count; i++) mats[i] = material;
                    renderer.sharedMaterials = mats;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
            }

            return go;
        }

        static void CreateCoin(string path, Transform parent, Vector3 localPosition,
            float scale, float yaw, Material material)
        {
            var coin = InstantiateModel(path, parent, localPosition, material,
                new Vector3(0f, yaw, 0f));
            if (coin != null) coin.transform.localScale = Vector3.one * scale;
        }

        static GameObject CreateStoneBlock(Transform parent, string name, Vector3 localPosition,
            Vector3 localScale, Material material, Vector3 localEuler)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;

            var renderer = go.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go;
        }

        static void CreateQuad(string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;

            var renderer = go.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static void CreateGroundGlow(string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = localScale;

            var renderer = go.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Material LoadOrCreateShaderMaterial(string path, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning("[RPG] 找不到 Shader: " + shaderName);
                return null;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material LoadOrCreateEmissiveMaterial(string path, Color baseColor, Color emission)
        {
            var mat = LoadOrCreateLitMaterial(path, null, baseColor);
            if (mat == null) return null;

            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", emission);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void BuildSpriteChest(Vector3 position)
        {
            var go = new GameObject("Chest");
            go.transform.position = position;

            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1f, 1f);

            var view = go.AddComponent<SpriteRenderer>();
            view.sprite = LoadSprite(
                "Assets/Textures/enemy_slime_yellow.png",
                "Assets/Textures/gray.png");
            view.color = new Color(1f, 0.85f, 0.35f, 1f);
            view.sortingOrder = 4;

            var sheet = go.AddComponent<SpriteSheetFrameView>();
            sheet.frameWidth = 32;
            sheet.frameHeight = 32;
            sheet.framesPerRow = 10;
            sheet.row = 0;
            sheet.pixelsPerUnit = 16f;
            sheet.animate = false;

            var chest = go.AddComponent<Chest>();
            chest.chestId = "chest_01";
            chest.displayName = "Chest";
            chest.gold = 15;
            chest.exp = 25;
        }

        static void BuildSpritePortal(Vector3 position)
        {
            var go = new GameObject("Portal_Rift");
            go.transform.position = position;

            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.5f, 1.5f);

            var view = go.AddComponent<SpriteRenderer>();
            view.sprite = LoadSprite(
                "Assets/Textures/enemy_slime_blue.png",
                "Assets/Textures/gray.png");
            view.color = new Color(0.7f, 0.5f, 1f, 0.9f);
            view.sortingOrder = 4;

            var sheet = go.AddComponent<SpriteSheetFrameView>();
            sheet.frameWidth = 32;
            sheet.frameHeight = 32;
            sheet.framesPerRow = 10;
            sheet.row = 0;
            sheet.pixelsPerUnit = 16f;
            sheet.animate = false;

            var portal = go.AddComponent<Portal>();
            portal.displayName = "Survivor Rift";
            portal.survivorRift = true;
            portal.survivorSceneName = "SampleScene";
            portal.difficultyId = "normal";
            portal.targetDuration = 120f;
        }

        static bool GetRendererBounds(GameObject go, out Bounds bounds)
        {
            bounds = new Bounds();
            bool has = false;

            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                if (!has)
                {
                    bounds = renderer.bounds;
                    has = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return has;
        }

        static Material LoadOrCreateLitMaterial(string path, Texture2D texture, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            if (texture != null) mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material LoadOrCreateSpiralMaterial(string path, Texture2D texture, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            if (texture != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);     // alpha
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);      // 双面，避免 Quad 朝向问题
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;

            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void RegisterScenes()
        {
            var paths = new[]
            {
                SceneFolder + "/Boot.unity",
                SceneFolder + "/MainMenu.unity",
                SceneFolder + "/Region_Test.unity",
                "Assets/Scenes/SampleScene.unity",
            };

            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var path in paths)
            {
                if (File.Exists(path))
                    scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
