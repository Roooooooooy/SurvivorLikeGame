using UnityEngine;
using System.Collections.Generic;

public class OrbWeapon : WeaponBase
{
    public float rotationSpeed = 180f;
    public float orbRadius = 2f;
    public GameObject orbPrefab;
    public int orbCount = 1;
    public Color evolvedColor = new Color(1f, 0.85f, 0.3f);

    private Transform orbsContainer;
    private List<GameObject> activeOrbs = new List<GameObject>();
    private float currentAngle = 0f;

    void Start()
    {
        orbsContainer = new GameObject("Orbs").transform;
        orbsContainer.SetParent(transform);
        orbsContainer.localPosition = Vector3.zero;
        RefreshOrbs();
    }

    protected override void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isGameOver) return;

        currentAngle += rotationSpeed * Time.deltaTime;
        orbsContainer.localRotation = Quaternion.Euler(0, 0, currentAngle);

        // 攻击范围加成实时生效：缩放容器同时放大轨道半径与法球判定
        float area = Stats != null ? Stats.areaMultiplier : 1f;
        orbsContainer.localScale = new Vector3(area, area, 1f);

        // This weapon continuously deals damage, no discrete attack
    }

    protected override void Attack()
    {
        // Not used for orb weapon
    }

    public void AddOrb()
    {
        orbCount++;
        RefreshOrbs();
    }

    public override void Evolve(EvolutionRecipe recipe)
    {
        base.Evolve(recipe);
        rotationSpeed *= recipe.speedMultiplier;
        orbCount += recipe.bonusCount;
        RefreshOrbs();
    }

    void RefreshOrbs()
    {
        foreach (var orb in activeOrbs) Destroy(orb);
        activeOrbs.Clear();

        if (orbPrefab == null) return;

        float angleStep = 360f / orbCount;
        for (int i = 0; i < orbCount; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * orbRadius;

            GameObject orb = Instantiate(orbPrefab, orbsContainer);
            orb.transform.localPosition = pos;

            if (evolved)
            {
                var sr = orb.GetComponent<SpriteRenderer>();
                if (sr) sr.color = evolvedColor;
            }

            var damageDealer = orb.GetComponent<DamageDealer>();
            if (damageDealer == null) damageDealer = orb.AddComponent<DamageDealer>();
            damageDealer.damage = damage;
            damageDealer.damageInterval = 0.5f; // Can hit same enemy every 0.5s
            damageDealer.isContinuous = true;
            damageDealer.SetSourceStats(Stats);

            activeOrbs.Add(orb);
        }
    }
}
