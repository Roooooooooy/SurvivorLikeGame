using System.Collections.Generic;
using UnityEngine;

// 世界空间背景装饰散布：按 cellSize 网格做确定性哈希，
// 同一格永远得到同一装饰，相机移动时只增删视野边缘的格子，
// 因此图案不会随相机漂移，也不会出现整屏重复的平铺。
public class BackgroundDecals : MonoBehaviour
{
    [System.Serializable]
    public struct DecalEntry
    {
        public Sprite sprite;
        [Min(0f)] public float weight;
    }

    public DecalEntry[] decals;
    public float cellSize = 4f;
    [Range(0f, 1f)] public float density = 0.55f;
    public int seed = 1337;
    public float z = 4.9f;          // 背景在 z=5，敌人 z<=1
    public int maxDecals = 200;
    public int paddingCells = 1;
    [Range(0f, 1f)] public float minAlpha = 0.75f;
    [Range(0f, 1f)] public float maxAlpha = 1f;

    private Camera cam;
    private SpriteRenderer[] pool;
    private int poolCursor;
    private readonly Dictionary<Vector2Int, SpriteRenderer> active = new();
    private readonly List<Vector2Int> stale = new();
    private float totalWeight;
    private Vector2Int lastMin, lastMax;
    private bool hasRange;

    void Start()
    {
        cam = Camera.main;
        totalWeight = 0f;
        if (decals != null)
        {
            foreach (var d in decals)
                totalWeight += Mathf.Max(0f, d.weight);
        }
        pool = new SpriteRenderer[Mathf.Max(1, maxDecals)];
        for (int i = 0; i < pool.Length; i++)
        {
            var go = new GameObject($"Decal {i}");
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            pool[i] = go.AddComponent<SpriteRenderer>();
        }
        Refresh();
    }

    void LateUpdate()
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }
        if (decals == null || decals.Length == 0 || totalWeight <= 0f) return;

        GetCellRange(out var min, out var max);
        if (!hasRange || min != lastMin || max != lastMax)
            Refresh();
    }

    private void GetCellRange(out Vector2Int min, out Vector2Int max)
    {
        float distance = z - cam.transform.position.z;
        if (distance < 0.01f) distance = 0.01f;
        Vector3 bl = cam.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
        Vector3 tr = cam.ViewportToWorldPoint(new Vector3(1f, 1f, distance));
        min = new Vector2Int(
            Mathf.FloorToInt(bl.x / cellSize) - paddingCells,
            Mathf.FloorToInt(bl.y / cellSize) - paddingCells);
        max = new Vector2Int(
            Mathf.FloorToInt(tr.x / cellSize) + paddingCells,
            Mathf.FloorToInt(tr.y / cellSize) + paddingCells);
    }

    private void Refresh()
    {
        GetCellRange(out lastMin, out lastMax);
        hasRange = true;

        stale.Clear();
        foreach (var kv in active)
        {
            if (kv.Key.x < lastMin.x || kv.Key.x > lastMax.x ||
                kv.Key.y < lastMin.y || kv.Key.y > lastMax.y)
                stale.Add(kv.Key);
        }
        foreach (var key in stale)
        {
            active[key].gameObject.SetActive(false);
            active.Remove(key);
        }

        for (int y = lastMin.y; y <= lastMax.y; y++)
        {
            for (int x = lastMin.x; x <= lastMax.x; x++)
            {
                var key = new Vector2Int(x, y);
                if (active.ContainsKey(key)) continue;

                uint h = Hash(x, y, seed);
                if (h / 4294967295f >= density) continue;

                DecalEntry entry = Pick(h);
                if (entry.sprite == null) continue;

                SpriteRenderer sr = TakeFromPool();
                if (sr == null) return;

                float ox = (Hash(x, y, seed + 101) & 0xFFFF) / 65535f * cellSize;
                float oy = (Hash(x, y, seed + 202) & 0xFFFF) / 65535f * cellSize;
                int rot = (int)(Hash(x, y, seed + 303) % 4) * 90;
                bool flip = (Hash(x, y, seed + 404) & 1) == 1;
                float alpha = Mathf.Lerp(minAlpha, maxAlpha, (Hash(x, y, seed + 505) & 0xFFFF) / 65535f);

                var tr = sr.transform;
                tr.localPosition = new Vector3(x * cellSize + ox, y * cellSize + oy, z);
                tr.localRotation = Quaternion.Euler(0f, 0f, rot);
                tr.localScale = Vector3.one;
                sr.sprite = entry.sprite;
                sr.flipX = flip;
                sr.color = new Color(1f, 1f, 1f, alpha);
                sr.gameObject.SetActive(true);
                active[key] = sr;
            }
        }
    }

    private DecalEntry Pick(uint h)
    {
        float t = (h % 100000) / 100000f * totalWeight;
        foreach (var d in decals)
        {
            float w = Mathf.Max(0f, d.weight);
            if (t < w) return d;
            t -= w;
        }
        return decals[decals.Length - 1];
    }

    private SpriteRenderer TakeFromPool()
    {
        for (int i = 0; i < pool.Length; i++)
        {
            int idx = (poolCursor + i) % pool.Length;
            if (!pool[idx].gameObject.activeSelf)
            {
                poolCursor = (idx + 1) % pool.Length;
                return pool[idx];
            }
        }
        return null;
    }

    private static uint Hash(int x, int y, int s)
    {
        unchecked
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u +
                     (uint)s * 2246822519u + 0x9E3779B9u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return h;
        }
    }
}