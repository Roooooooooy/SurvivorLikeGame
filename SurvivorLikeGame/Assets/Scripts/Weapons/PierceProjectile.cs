using UnityEngine;

// 穿透箭弹体：直线飞行，DamageDealer 持续做圆形范围判定；
// 命中不销毁（由 isContinuous + destroyOnHit=false 保证），寿命到期归还对象池。
public class PierceProjectile : MonoBehaviour
{
    private Rigidbody2D rb;
    private float remainLife;

    public void Init(Vector2 direction, float speed, float lifetime, int damage, PlayerStats stats, float scale)
    {
        // 攻击范围加成：同时放大外观与判定半径
        transform.localScale = new Vector3(scale, scale, 1f);

        rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = direction.normalized * speed;

        var dealer = GetComponent<DamageDealer>();
        if (dealer == null) dealer = gameObject.AddComponent<DamageDealer>();
        dealer.damage = damage;
        dealer.isContinuous = true;   // 同一敌人按全局 tickDamageInterval 限频
        dealer.destroyOnHit = false;  // 穿透：命中不销毁
        dealer.SetSourceStats(stats);

        remainLife = lifetime;
    }

    void Update()
    {
        remainLife -= Time.deltaTime;
        if (remainLife <= 0f) SmallObjectPool.Release(gameObject);
    }
}
