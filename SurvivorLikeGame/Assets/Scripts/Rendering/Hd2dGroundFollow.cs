using UnityEngine;

// 3D 地面跟随玩家。地面 shader 用世界坐标采样，所以移动 Plane 不会让纹理漂移。
public class Hd2dGroundFollow : MonoBehaviour
{
    public Transform target;

    void LateUpdate()
    {
        if (target == null)
        {
            var player = PlayerController.Instance;
            if (player == null) return;
            target = player.transform;
        }

        Vector3 p = target.position;
        transform.position = new Vector3(p.x, transform.position.y, p.y);
    }
}
