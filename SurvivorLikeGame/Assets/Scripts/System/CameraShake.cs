using UnityEngine;

// 相机震动：静态接口，CameraFollow.LateUpdate 读取 Offset 叠加到平滑跟随结果上。
// 使用 unscaledTime，顿帧（timeScale=0.05）和暂停期间仍然正常衰减；
// 多段命中只取最大幅度、不累加，避免持续伤害把画面抖散。
public static class CameraShake
{
    private static float amplitude;   // 当前震动幅度（世界单位）
    private static float duration;    // 本次震动总时长
    private static float endTime;     // unscaledTime 下的结束时刻

    private static int sampledFrame = -1;
    private static Vector3 sampledOffset;

    public static void Add(float strength, float time)
    {
        if (strength <= 0f || time <= 0f) return;

        float now = Time.unscaledTime;
        if (now >= endTime)
        {
            amplitude = strength;
            duration = time;
            endTime = now + time;
        }
        else
        {
            // 正在震动：幅度取大者；时长只延长不缩短
            amplitude = Mathf.Max(amplitude, strength);
            if (time > endTime - now)
            {
                duration = time;
                endTime = now + time;
            }
        }
    }

    // 每帧只随机采样一次（多个调用方读到同一偏移，避免帧内抖动不一致）
    public static Vector3 Offset
    {
        get
        {
            if (Time.frameCount != sampledFrame)
            {
                sampledFrame = Time.frameCount;
                sampledOffset = Sample();
            }
            return sampledOffset;
        }
    }

    private static Vector3 Sample()
    {
        float now = Time.unscaledTime;
        if (duration <= 0f || now >= endTime)
        {
            amplitude = 0f;
            return Vector3.zero;
        }

        // 剩余时间越少幅度越小（二次衰减，收尾更干脆）
        float remain = (endTime - now) / duration;
        float strength = amplitude * remain * remain;
        Vector2 dir = Random.insideUnitCircle;
        return new Vector3(dir.x * strength, dir.y * strength, 0f);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        amplitude = 0f;
        duration = 0f;
        endTime = 0f;
        sampledFrame = -1;
        sampledOffset = Vector3.zero;
    }
}
