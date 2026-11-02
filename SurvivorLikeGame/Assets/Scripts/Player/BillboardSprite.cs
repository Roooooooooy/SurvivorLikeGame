using UnityEngine;

// HD-2D：让精灵绕 Y 轴面向相机（八方旅人式的 billboard）。
// 只转 Y 轴，保证脚底始终贴着地面。
public class BillboardSprite : MonoBehaviour
{
    Transform cam;

    void LateUpdate()
    {
        if (cam == null)
        {
            var c = Camera.main;
            if (c == null) return;
            cam = c.transform;
        }

        Vector3 dir = cam.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        // SpriteRenderer 正面朝 +Z，LookRotation(dir) 让正面转向相机
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }
}
