using UnityEngine;

// 世界空间障碍物：玩家用物理碰撞挡住，敌人由仿真 Job 做圆形排斥绕行。
// 可被玩家武器破坏；破坏后由 ObstacleField 回收，离开区域再回来会重新生成。
public class Obstacle : MonoBehaviour
{
    public int maxHealth;
    public int health;
    public float radius;
    public Vector2Int cell;

    SpriteRenderer sr;
    CircleCollider2D circle;
    ObstacleField field;
    float flashTimer;
    float lastContinuousHit = -999f;
    bool broken;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        circle = GetComponent<CircleCollider2D>();
    }

    public void Init(ObstacleField owner, Sprite sprite, float r, int hp, Vector2Int cellKey)
    {
        field = owner;
        cell = cellKey;
        radius = r;
        maxHealth = health = Mathf.Max(1, hp);
        broken = false;
        flashTimer = 0f;
        lastContinuousHit = -999f;

        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (circle == null) circle = GetComponent<CircleCollider2D>();
        if (sr != null)
        {
            sr.sprite = sprite;
            sr.color = Color.white;
            sr.enabled = true;
            sr.sortingOrder = 0;
        }
        if (circle != null)
        {
            circle.radius = r;
            circle.enabled = true;
        }
    }

    // 返回 true 表示这次攻击有效（用于表现层的命中反馈）
    public bool TakeDamage(int damage, bool continuous)
    {
        if (broken) return false;
        if (continuous)
        {
            // 法球每帧都在范围内，按固定间隔结算，避免瞬间打碎
            if (Time.time - lastContinuousHit < 0.3f) return false;
            lastContinuousHit = Time.time;
        }

        health -= Mathf.Max(1, damage);
        flashTimer = 0.08f;
        if (health <= 0)
        {
            Break();
            return true;
        }
        return true;
    }

    void Update()
    {
        if (flashTimer <= 0f) return;
        flashTimer -= Time.deltaTime;
        if (sr != null)
            sr.color = flashTimer > 0f ? new Color(1f, 0.84f, 0.7f, 1f) : Color.white;
    }

    void Break()
    {
        broken = true;
        if (circle != null) circle.enabled = false;
        if (sr != null) sr.enabled = false;

        int coins = Mathf.Clamp(maxHealth / 60, 1, 3);
        var sim = EnemySimulation.Instance;
        if (sim != null) sim.SpawnCoins(transform.position, coins, 1);

        if (field != null) field.NotifyBroken(this);
    }
}