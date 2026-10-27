using UnityEngine;

public abstract class WeaponBase : MonoBehaviour
{
    public int damage = 10;
    public float attackInterval = 1f;
    public int level = 1;
    public int maxLevel = 5;
    public bool evolved = false;
    // 升级归类：新武器在子类里把 target 设成自己的 WeaponTarget，
    // 升级池/进化配方才能精确匹配（法球、飞弹仍由 TargetOf 里的类型判断兜底）。
    public WeaponTarget target = WeaponTarget.All;
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
            // 发射音效统一在攻击之后播放，OrbWeapon 重写了 Update 不走这里
            AudioManager.Instance.PlayShoot(transform.position);
        }
    }

    protected abstract void Attack();

    public virtual void LevelUp() => level++;

    // 带升级类型：子类可据此区分“伤害/数量/速度/间隔”升级，避免靠字段变化猜测
    public virtual void LevelUp(UpgradeStat stat) => LevelUp();

    public virtual void Evolve(EvolutionRecipe recipe)
    {
        evolved = true;
        damage = Mathf.RoundToInt(damage * recipe.damageMultiplier);
    }
}
