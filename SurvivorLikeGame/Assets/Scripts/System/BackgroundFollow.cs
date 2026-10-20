using UnityEngine;

// 无限平铺背景：跟随相机，位置按平铺周期吸附，
// 使纹理图案与世界坐标保持锁定（移动时图案不"漂移"）。
public class BackgroundFollow : MonoBehaviour
{
    public float tileSize = 16f; // 一个平铺周期的世界尺寸（512px / PPU32）

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 cp = cam.transform.position;
        float x = Mathf.Round(cp.x / tileSize) * tileSize;
        float y = Mathf.Round(cp.y / tileSize) * tileSize;
        transform.position = new Vector3(x, y, 5f); // z=5：在敌人(z=1)与玩家(z=0)之后
    }
}
