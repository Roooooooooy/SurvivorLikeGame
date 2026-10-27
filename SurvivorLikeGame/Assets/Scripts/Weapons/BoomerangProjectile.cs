using UnityEngine;

// 回旋镖弹体：前 outTime 秒沿初始方向飞出，之后每帧朝玩家回航；
// DamageDealer 持续判定（isContinuous=true），往返均可命中。寿命到期/回身归还对象池。
public class BoomerangProjectile : MonoBehaviour
{
    private Rigidbody2D rb;
    private Transform player;
    private Vector2 outwardDir;
    private float outSpeed;
    private float outTime;
    private float returnSpeed;
    private float remainLife;
    private float elapsed;

    public void Init(Vector2 direction, float speedOut, float flyOutTime,
        float speedReturn, float lifetime, int damage, PlayerStats stats, float scale)
    {
        // 攻击范围加成：同时放大外观与判定半径
        transform.localScale = new Vector3(scale, scale, 1f);

        outwardDir = direction.normalized;
        outSpeed = speedOut;
        outTime = flyOutTime;
        returnSpeed = speedReturn;
        remainLife = lifetime;
        elapsed = 0f; // 池化复用：重置飞行阶段计时

        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = outwardDir * outSpeed;
            rb.angularVelocity = 720f; // 飞行时自旋
        }

        player = stats != null ? stats.transform
            : (PlayerController.Instance != null ? PlayerController.Instance.transform : null);

        var dealer = GetComponent<DamageDealer>();
        if (dealer == null) dealer = gameObject.AddComponent<DamageDealer>();
        dealer.damage = damage;
        dealer.isContinuous = true;   // 往返持续伤害，同一敌人按全局 tickDamageInterval 限频
        dealer.destroyOnHit = false;
        dealer.SetSourceStats(stats);
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        remainLife -= Time.deltaTime;
        if (remainLife <= 0f)
        {
            SmallObjectPool.Release(gameObject); // 池化复用，替代 Destroy
            return;
        }

        if (elapsed < outTime) return; // 飞出阶段保持初速度

        if (player == null)
        {
            // 玩家已丢失（死亡/切场景）：保留原来 0.5 秒延迟销毁的语义，用剩余寿命倒计时归还
            remainLife = Mathf.Min(remainLife, 0.5f);
            return;
        }

        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
        if (toPlayer.sqrMagnitude < 0.36f) // 回到玩家身边即回收
        {
            SmallObjectPool.Release(gameObject);
            return;
        }

        if (rb != null) rb.linearVelocity = toPlayer.normalized * returnSpeed;
    }
}
