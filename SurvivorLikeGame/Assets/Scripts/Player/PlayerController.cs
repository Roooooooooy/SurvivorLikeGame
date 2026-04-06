using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance;

    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Stats")]
    public int maxHealth = 100;
    public int currentHealth;
    public float invincibleDuration = 0.5f;

    [Header("Experience")]
    public int currentExp = 0;
    public int expToNextLevel = 10;
    public int level = 1;

    private float invincibleTimer = 0f;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private WeaponManager weaponManager;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        weaponManager = GetComponent<WeaponManager>();
        currentHealth = maxHealth;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isGameOver) return;

        HandleMovement();

        if (invincibleTimer > 0f)
            invincibleTimer -= Time.deltaTime;
    }

    void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector2 dir = new Vector2(h, v).normalized;
        rb.velocity = dir * moveSpeed;

        // Flip sprite based on movement direction
        if (h != 0)
            spriteRenderer.flipX = h < 0;
    }

    public void TakeDamage(int damage)
    {
        if (invincibleTimer > 0f) return;

        currentHealth -= damage;
        invincibleTimer = invincibleDuration;

        // Flash effect
        StartCoroutine(FlashRed());

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    System.Collections.IEnumerator FlashRed()
    {
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = Color.white;
    }

    void Die()
    {
        if (GameManager.Instance) GameManager.Instance.GameOver();
    }

    public void GainExp(int amount)
    {
        currentExp += amount;
        while (currentExp >= expToNextLevel)
        {
            currentExp -= expToNextLevel;
            LevelUp();
        }
    }

    void LevelUp()
    {
        level++;
        expToNextLevel = Mathf.RoundToInt(expToNextLevel * 1.2f);
        // Restore some health on level up
        currentHealth = Mathf.Min(currentHealth + 20, maxHealth);

        if (weaponManager) weaponManager.ShowUpgradeOptions();
    }

    public float GetHealthPercent() => (float)currentHealth / maxHealth;
    public float GetExpPercent() => (float)currentExp / expToNextLevel;
}
