using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public float moveSpeed = 3f;
    public int maxHealth = 30;
    public int damage = 10;
    public GameObject expGemPrefab;
    
    private int currentHealth;
    private Transform player;
    private SpriteRenderer spriteRenderer;
    private bool isDead = false;

    [SerializeField] private Sprite sprite1;
    [SerializeField] private Sprite sprite2;
    [SerializeField] private SpriteRenderer sr;

    void Start()
    {
        currentHealth = maxHealth;
        spriteRenderer = GetComponent<SpriteRenderer>();
        var p = PlayerController.Instance;
        if (p != null) player = p.transform;

        sr.sprite = Random.Range(0, 2) >= 1 ? sprite1 : sprite2;
    }

    void Update()
    {
        if (isDead || player == null || (GameManager.Instance && GameManager.Instance.isGameOver)) return;

        Vector2 dir = (player.position - transform.position).normalized;
        transform.position = Vector2.MoveTowards(transform.position, player.position, moveSpeed * Time.deltaTime);

        if (dir.x != 0) spriteRenderer.flipX = dir.x < 0;
    }

    public void TakeDamage(int dmg)
    {
        if (isDead) return;
        
        currentHealth -= dmg;
        StartCoroutine(FlashWhite());

        if (currentHealth <= 0) Die();
    }

    System.Collections.IEnumerator FlashWhite()
    {
        spriteRenderer.color = Color.white;
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = new Color(1f, 0.5f, 0.5f);
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = Color.white;
    }

    void Die()
    {
        isDead = true;
        if (GameManager.Instance) GameManager.Instance.AddKill();
        
        if (expGemPrefab != null)
        {
            Instantiate(expGemPrefab, transform.position, Quaternion.identity);
        }
        
        Destroy(gameObject);
    }

    void OnCollisionStay2D(Collision2D col)
    {
        if (isDead) return;
        
        if (col.gameObject.CompareTag("Player"))
        {
            var p = col.gameObject.GetComponent<PlayerController>();
            if (p != null) p.TakeDamage(damage);
        }
    }
}