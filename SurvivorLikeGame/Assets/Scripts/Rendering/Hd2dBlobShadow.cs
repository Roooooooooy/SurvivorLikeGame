using UnityEngine;

// 主角接地阴影：XZ 平面上的柔和椭圆，跟随 HD-2D 视图根节点的世界 (x, z)。
// 与敌人贴地阴影一致，给 Spine 主角一个明确的落地点。
public class Hd2dBlobShadow : MonoBehaviour
{
    [Tooltip("HD-2D 视图根节点（位置 x/z 即逻辑 x/y）")]
    public Transform follow;
    public float diameter = 1.45f;
    public float depth = 0.85f;
    [Range(0f, 1f)] public float opacity = 0.5f;
    public float groundY = 0.02f;

    MeshRenderer mr;
    Material mat;
    Texture2D tex;

    void Awake()
    {
        var shader = Shader.Find("HD2D/BlobShadow");
        if (shader == null)
        {
            Debug.LogWarning("[HD2D] 找不到 HD2D/BlobShadow，接地阴影已跳过");
            enabled = false;
            return;
        }

        tex = CreateSoftDot();
        mat = new Material(shader)
        {
            name = "PlayerBlobShadow (Runtime)",
            hideFlags = HideFlags.HideAndDontSave,
        };
        mat.mainTexture = tex;
        mat.SetColor("_Color", new Color(0.02f, 0.015f, 0.025f, opacity));
        mat.renderQueue = 2901; // 与敌人贴地阴影同层，画在角色之前

        var go = new GameObject("ShadowMesh");
        go.transform.SetParent(transform, false);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = CreateQuad();
        mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    void LateUpdate()
    {
        if (follow == null) return;
        Vector3 p = follow.position;
        transform.position = new Vector3(p.x, groundY, p.z);
        transform.rotation = Quaternion.identity;
        transform.localScale = new Vector3(diameter, 1f, depth);
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
        if (tex != null) Destroy(tex);
    }

    static Mesh CreateQuad()
    {
        var m = new Mesh { name = "BlobShadowQuad" };
        m.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
            new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f),
        };
        m.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        m.bounds = new Bounds(Vector3.zero, Vector3.one);
        return m;
    }

    static Texture2D CreateSoftDot()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "BlobShadowDot",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };

        var pixels = new Color32[size * size];
        const float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }
}
