using UnityEngine;

// 金币掉落物：节奏与 ExpGem 一致——进入拾取半径后被磁吸，碰到玩家时结算。
// 池化复用，OnEnable 清空上一轮的磁吸目标。
public class CoinPickup : MonoBehaviour
{
    public int value = 1;
    public float moveSpeed = 8f;

    private Transform targetPlayer = null;
    private bool collected = false;

    void OnEnable()
    {
        targetPlayer = null; // 池化重生：清掉上一轮的磁吸目标
        collected = false;
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
        if (collected || !col.CompareTag("Player")) return;
        // 拾取半径是触发器，只负责磁吸；真正拾取以玩家本体碰撞体为准，
        // 避免金币进入半径时立刻被收走，也避免同帧命中两个碰撞体导致重复结算。
        if (col.isTrigger) return;

        collected = true;
        RunStats.AddCoins(value);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayPickup(transform.position);
        SmallObjectPool.Release(gameObject); // 归还对象池，替代 Destroy
    }
}
