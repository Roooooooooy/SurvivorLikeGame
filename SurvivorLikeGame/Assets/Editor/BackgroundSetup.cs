using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 一次性/可重复执行的背景美术接线：地面贴图、大尺度污渍层、世界空间装饰散布。
public static class BackgroundSetup
{
    const string Dir = "Assets/Textures/Background/";

    [MenuItem("Tools/SurvivorLikeGame/Apply Background Art")]
    public static void Apply()
    {
        var floor = LoadSprite("bg_stone_floor.png");
        var grunge = LoadSprite("bg_grunge_large.png");
        if (floor == null || grunge == null)
        {
            Debug.LogError("[BackgroundSetup] Missing background textures under " + Dir);
            return;
        }

        var bg = GameObject.Find("Background");
        if (bg == null) { Debug.LogError("[BackgroundSetup] Background object not found"); return; }
        bg.GetComponent<SpriteRenderer>().sprite = floor;

        var grungeGo = GameObject.Find("BackgroundGrunge");
        if (grungeGo == null) { Debug.LogError("[BackgroundSetup] BackgroundGrunge object not found"); return; }
        var grungeSr = grungeGo.GetComponent<SpriteRenderer>();
        grungeSr.sprite = grunge;
        grungeSr.sortingOrder = -9;
        grungeSr.drawMode = SpriteDrawMode.Simple;
        grungeGo.transform.localScale = new Vector3(8f, 8f, 1f);
        var follow = grungeGo.GetComponent<BackgroundFollow>();
        if (follow != null) follow.tileSize = 128f;

        var decalGo = GameObject.Find("BackgroundDecals");
        if (decalGo == null) { Debug.LogError("[BackgroundSetup] BackgroundDecals object not found"); return; }
        var decals = decalGo.GetComponent<BackgroundDecals>();
        decals.decals = new[]
        {
            Entry("decal_rubble_a.png", 26f),
            Entry("decal_rubble_b.png", 22f),
            Entry("decal_crack_a.png", 18f),
            Entry("decal_crack_b.png", 12f),
            Entry("decal_ember_a.png", 8f),
            Entry("decal_ember_b.png", 7f),
            Entry("decal_stain_a.png", 8f),
            Entry("decal_bones_a.png", 3f),
            Entry("decal_dust_a.png", 8f),
            Entry("decal_debris_a.png", 3f),
        };
        decals.cellSize = 4f;
        decals.density = 0.7f;
        decals.seed = 1337;
        decals.z = 4.9f;
        decals.maxDecals = 200;
        decals.paddingCells = 1;
        decals.minAlpha = 0.75f;
        decals.maxAlpha = 1f;

        EditorUtility.SetDirty(bg);
        EditorUtility.SetDirty(grungeGo);
        EditorUtility.SetDirty(decalGo);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[BackgroundSetup] Background art applied.");
    }

    static Sprite LoadSprite(string file)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(Dir + file);
    }

    static BackgroundDecals.DecalEntry Entry(string file, float weight)
    {
        return new BackgroundDecals.DecalEntry
        {
            sprite = LoadSprite(file),
            weight = weight,
        };
    }
}