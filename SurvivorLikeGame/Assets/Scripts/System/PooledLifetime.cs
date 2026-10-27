using UnityEngine;

// 池化对象的兜底寿命：到时自动归还对象池，替代原来 Destroy(go, seconds) 的延迟销毁。
// 每次激活（含池化复用）都会重置计时，业务侧只需在生成后设置 lifetime。
public class PooledLifetime : MonoBehaviour
{
    public float lifetime = 3f;
    private float elapsed;

    void OnEnable()
    {
        elapsed = 0f; // 池化复用：从上一轮寿命中恢复
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= lifetime) SmallObjectPool.Release(gameObject);
    }
}
