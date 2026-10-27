using UnityEngine;

// 敌方子弹：由 EnemySimulation 在 Ranged 敌人开火时生成，直线飞向玩家。
// 只伤害玩家、不进入敌人仿真数组；命中或寿命到期后归还对象池。
public class EnemyProjectile : MonoBehaviour
{
    public int damage = 8;
    public float lifespan = 4f;   // 兜底寿命，防止漏删
    public float hitRadius = 0.45f;

    Vector2 velocity;
    float age;

    // 由 EnemySimulation 主线程调用：方向、速度、伤害都来自敌人类型数据
    public void Launch(Vector2 direction, float moveSpeed, int dmg)
    {
        velocity = direction.normalized * moveSpeed;
        damage = dmg;
        age = 0f;

        // 子弹贴图是菱形宝石，旋转到飞行方向更自然
        float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (age >= lifespan)
        {
            SmallObjectPool.Release(gameObject); // 池化复用，替代 Destroy
            return;
        }

        transform.position += (Vector3)(velocity * dt);

        // 命中判定：与玩家中心的距离，命中后回收（护甲/无敌帧在 TakeDamage 内处理）
        var player = PlayerController.Instance;
        if (player == null) return;
        if (GameManager.Instance != null && GameManager.Instance.isGameOver) return;

        Vector2 delta = (Vector2)player.transform.position - (Vector2)transform.position;
        if (delta.sqrMagnitude <= hitRadius * hitRadius)
        {
            player.TakeDamage(damage);
            SmallObjectPool.Release(gameObject); // 池化复用，替代 Destroy
        }
    }
}
