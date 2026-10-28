using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    private Transform target;

    void Start()
    {
        if (PlayerController.Instance != null)
            target = PlayerController.Instance.transform;
    }

    void LateUpdate()
    {
        if (target == null)
        {
            if (PlayerController.Instance != null)
                target = PlayerController.Instance.transform;
            return;
        }

        // 紧密跟随：不做平滑滞后，避免移动时主角相对地面漂移。
        // 震屏仍叠加在真实目标位置上，下一帧直接覆盖，不会污染基准位置。
        Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);
        transform.position = desired + CameraShake.Offset;
    }
}