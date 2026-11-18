using UnityEngine;
using RpgGame;

// 3D 地面跟随玩家。地面 shader 用世界坐标采样，所以移动 Plane 不会让纹理漂移。
public class Hd2dGroundFollow : MonoBehaviour
{
    public Transform target;

    void LateUpdate()
    {
        if (target == null)
        {
            if (RegionRuntime.IsAction)
            {
                if (PlayerHealth.Player != null)
                    target = PlayerHealth.Player.transform;
                else
                {
                    var actionMotor = FindFirstObjectByType<PlayerMotor>();
                    if (actionMotor != null) target = actionMotor.transform;
                }
            }
            else
            {
                var player = PlayerController.Instance;
                if (player != null)
                {
                    target = player.transform;
                }
                else if (PlayerHealth.Player != null)
                {
                    target = PlayerHealth.Player.transform;
                }
                else
                {
                    var motor = FindFirstObjectByType<PlayerMotor>();
                    if (motor != null) target = motor.transform;
                }
            }

            if (target == null) return;
        }

        Vector3 p = target.position;
        transform.position = new Vector3(p.x, transform.position.y, p.y);
    }
}
