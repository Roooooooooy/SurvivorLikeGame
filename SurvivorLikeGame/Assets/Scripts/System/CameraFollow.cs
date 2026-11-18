using UnityEngine;
using RpgGame;

public class CameraFollow : MonoBehaviour
{
    [Header("HD-2D")]
    [Tooltip("开启后相机按 (x, 0, y) 跟随玩家，使用透视俯角，而不是 2D 正交跟随。")]
    public bool hd2d = false;
    public Vector3 hd2dOffset = new Vector3(0f, 9f, -14f);
    public Vector3 hd2dEuler = new Vector3(30f, 0f, 0f);

    [Tooltip("跟随平滑速度：越大越跟手，越小滞后越明显。")]
    public float followSmooth = 8f;

    private Transform target;
    private Vector3 smoothedPosition;
    private bool hasSmoothedPosition;

    void Start()
    {
        target = ResolveTarget();
        if (hd2d)
            transform.rotation = Quaternion.Euler(hd2dEuler);
    }

    void LateUpdate()
    {
        if (target == null)
        {
            target = ResolveTarget();
            if (target == null) return;
            hasSmoothedPosition = false;
        }

        Vector3 desired;
        if (hd2d)
        {
            // 逻辑在 XY，视觉世界在 XZ
            Vector3 p = target.position;
            desired = new Vector3(p.x + hd2dOffset.x, hd2dOffset.y, p.y + hd2dOffset.z);
        }
        else
        {
            desired = new Vector3(target.position.x, target.position.y, transform.position.z);
        }

        if (!hasSmoothedPosition)
        {
            smoothedPosition = desired;
            hasSmoothedPosition = true;
        }
        else
        {
            // 帧率无关的指数平滑：保留一点滞后和惯性，速度越快偏移越明显
            float t = 1f - Mathf.Exp(-followSmooth * Time.deltaTime);
            smoothedPosition = Vector3.Lerp(smoothedPosition, desired, t);
        }

        // 震屏只叠加在输出上，不写回平滑基准，避免污染跟随位置
        Vector3 shake = CameraShake.Offset;
        transform.position = hd2d
            ? new Vector3(smoothedPosition.x + shake.x, smoothedPosition.y, smoothedPosition.z + shake.y)
            : smoothedPosition + shake;
    }

    // 幸存者区用 PlayerController，动作区用 PlayerHealth/PlayerMotor。
    // 同一套 HD-2D 相机参数可以同时服务两种模式。
    static Transform ResolveTarget()
    {
        // 动作区优先跟随动作玩家，避免误加载的幸存者玩家把镜头拉走。
        if (RegionRuntime.IsAction)
        {
            if (PlayerHealth.Player != null)
                return PlayerHealth.Player.transform;

            var actionMotor = Object.FindFirstObjectByType<PlayerMotor>();
            return actionMotor != null ? actionMotor.transform : null;
        }

        if (PlayerController.Instance != null)
            return PlayerController.Instance.transform;

        if (PlayerHealth.Player != null)
            return PlayerHealth.Player.transform;

        var motor = Object.FindFirstObjectByType<PlayerMotor>();
        return motor != null ? motor.transform : null;
    }
}