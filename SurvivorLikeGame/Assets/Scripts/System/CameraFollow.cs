using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("HD-2D")]
    [Tooltip("开启后相机按 (x, 0, y) 跟随玩家，使用透视俯角，而不是 2D 正交跟随。")]
    public bool hd2d = false;
    public Vector3 hd2dOffset = new Vector3(0f, 9f, -14f);
    public Vector3 hd2dEuler = new Vector3(30f, 0f, 0f);

    private Transform target;

    void Start()
    {
        if (PlayerController.Instance != null)
            target = PlayerController.Instance.transform;
        if (hd2d)
            transform.rotation = Quaternion.Euler(hd2dEuler);
    }

    void LateUpdate()
    {
        if (target == null)
        {
            if (PlayerController.Instance != null)
                target = PlayerController.Instance.transform;
            return;
        }

        if (hd2d)
        {
            // 逻辑在 XY，视觉世界在 XZ；震屏偏移也投到地面上
            Vector3 shake = CameraShake.Offset;
            Vector3 p = target.position;
            transform.position = new Vector3(
                p.x + hd2dOffset.x + shake.x,
                hd2dOffset.y,
                p.y + hd2dOffset.z + shake.y);
            return;
        }

        // 紧密跟随：不做平滑滞后，避免移动时主角相对地面漂移。
        // 震屏仍叠加在真实目标位置上，下一帧直接覆盖，不会污染基准位置。
        Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);
        transform.position = desired + CameraShake.Offset;
    }
}