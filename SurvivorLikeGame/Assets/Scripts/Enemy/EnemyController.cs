using UnityEngine;

public enum EnemyBehavior
{
    Chase = 0,     // 追踪 + 接触伤害（默认）
    Exploder = 1,  // 贴近玩家后引燃，延时范围爆炸
}

public class EnemyController : MonoBehaviour
{
    public float moveSpeed = 3f;
    public int maxHealth = 30;
    public int damage = 10;
    public int expValue = 1;
    public GameObject expGemPrefab;

    [Header("行为")]
    public EnemyBehavior behavior = EnemyBehavior.Chase;
    public float explodeTriggerRange = 1.5f; // 进入该距离开始引燃
    public float explodeRadius = 2.5f;       // 爆炸伤害半径
    public float fuseTime = 0.6f;            // 引线时长

    private int currentHealth;
    private Transform player;
    private SpriteRenderer spriteRenderer;
    private bool isDead = false;
    private bool fusing = false;
    private Color baseColor = Color.white;

    [SerializeField] private Sprite sprite1;
    [SerializeField] private Sprite sprite2;
    [SerializeField] private SpriteRenderer sr;

    void Start()
    {
        currentHealth = maxHealth;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (sr == null) sr = spriteRenderer;
        baseColor = sr.color;

        var p = PlayerController.Instance;
        if (p != null) player = p.transform;

        // 只有两张皮肤都配置了才随机换皮，否则保留 SpriteRenderer 上设置的贴图
        if (sprite1 != null && sprite2 != null)
            sr.sprite = Random.Range(0, 2) >= 1 ? sprite1 : sprite2;
    }

    void Update()
    {
        if (isDead || player == null || (GameManager.Instance && GameManager.Instance.isGameOver)) return;

        Vector2 dir = (player.position - transform.position).normalized;
        transform.position = Vector2.MoveTowards(transform.position, player.position, moveSpeed * Time.deltaTime);

        if (dir.x != 0) spriteRenderer.flipX = dir.x < 0;

        if (behavior == EnemyBehavior.Exploder && !fusing &&
            Vector2.Distance(transform.position, player.position) <= explodeTriggerRange)
        {
            StartCoroutine(FuseAndExplode());
        }
    }

    public void TakeDamage(int dmg)
    {
        if (isDead) return;

        currentHealth -= dmg;
        StartCoroutine(FlashWhite());

        if (currentHealth <= 0) Die();
    }

    // 受击闪白后恢复自身染色（而不是写死白色）
    System.Collections.IEnumerator FlashWhite()
    {
        sr.color = Color.white;
        yield return new WaitForSeconds(0.1f);
        sr.color = Color.Lerp(baseColor, Color.white, 0.5f);
        yield return new WaitForSeconds(0.1f);
        sr.color = baseColor;
    }

    // 引燃：红白闪烁预警，时间到即爆炸
    System.Collections.IEnumerator FuseAndExplode()
    {
        fusing = true;
        float t = 0f;
        while (t < fuseTime)
        {
            t += Time.deltaTime;
            float pulse = Mathf.PingPong(t * 8f, 1f);
            sr.color = Color.Lerp(baseColor, Color.red, pulse);
            yield return null;
        }
        Explode();
    }

    void Explode()
    {
        if (isDead) return;
        var p = PlayerController.Instance;
        if (p != null && Vector2.Distance(transform.position, p.transform.position) <= explodeRadius)
            p.TakeDamage(damage);
        Die();
    }

    void Die()
    {
        isDead = true;
        if (GameManager.Instance) GameManager.Instance.AddKill();

        if (expGemPrefab != null)
        {
            var gemObj = Instantiate(expGemPrefab, transform.position, Quaternion.identity);
            var gem = gemObj.GetComponent<ExpGem>();
            if (gem != null) gem.expAmount = expValue;
        }

        Destroy(gameObject);
    }

    void OnCollisionStay2D(Collision2D col)
    {
        if (isDead) return;
        if (behavior == EnemyBehavior.Exploder) return; // 自爆怪的伤害走爆炸，不做接触伤害

        if (col.gameObject.CompareTag("Player"))
        {
            var p = col.gameObject.GetComponent<PlayerController>();
            if (p != null) p.TakeDamage(damage);
        }
    }
}
