using UnityEngine;

public abstract class WeaponBase : MonoBehaviour
{
    public int damage = 10;
    public float attackInterval = 1f;
    public int level = 1;
    public int maxLevel = 5;
    public bool evolved = false;
    protected float attackTimer = 0f;

    private PlayerStats stats;

    protected PlayerStats Stats
    {
        get
        {
            if (stats == null) stats = GetComponentInParent<PlayerStats>();
            return stats;
        }
    }

    public bool IsMaxLevel => level >= maxLevel;

    // 冷却缩减在攻击节拍上实时生效
    protected virtual float EffectiveInterval =>
        attackInterval * (Stats != null ? 1f - Stats.cooldownReduction : 1f);

    protected virtual void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isGameOver) return;
        attackTimer += Time.deltaTime;
        if (attackTimer >= EffectiveInterval)
        {
            attackTimer = 0f;
            Attack();
        }
    }

    protected abstract void Attack();

    public virtual void LevelUp() => level++;

    public virtual void Evolve(EvolutionRecipe recipe)
    {
        evolved = true;
        damage = Mathf.RoundToInt(damage * recipe.damageMultiplier);
    }
}
