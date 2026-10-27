using UnityEngine;

public class ExpGem : MonoBehaviour
{
    public int expAmount = 1;
    public float moveSpeed = 8f;
    
    private Transform targetPlayer = null;

    void OnEnable()
    {
        targetPlayer = null; // 池化重生：清掉上一轮的磁吸目标
    }

    void Update()
    {
        if (targetPlayer != null)
        {
            transform.position = Vector2.MoveTowards(transform.position, targetPlayer.position, moveSpeed * Time.deltaTime);
        }
    }

    public void SetTarget(Transform player)
    {
        targetPlayer = player;
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Player"))
        {
            var p = col.GetComponent<PlayerController>();
            if (p != null)
            {
                p.GainExp(expAmount);
                CombatEvents.RaisePickup(transform.position, expAmount); // 拾取事件交给表现层
                SmallObjectPool.Release(gameObject); // 归还对象池，替代 Destroy
            }
        }
    }
}