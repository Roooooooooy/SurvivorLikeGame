using UnityEngine;

public abstract class WeaponBase : MonoBehaviour
{
    public int damage = 10;
    public float attackInterval = 1f;
    protected float attackTimer = 0f;

    protected virtual void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isGameOver) return;
        attackTimer += Time.deltaTime;
        if (attackTimer >= attackInterval)
        {
            attackTimer = 0f;
            Attack();
        }
    }

    protected abstract void Attack();
}
