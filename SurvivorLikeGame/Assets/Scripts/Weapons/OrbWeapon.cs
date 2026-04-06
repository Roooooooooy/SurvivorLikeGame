using UnityEngine;
using System.Collections.Generic;

public class OrbWeapon : WeaponBase
{
    public float rotationSpeed = 180f;
    public float orbRadius = 2f;
    public GameObject orbPrefab;
    public int orbCount = 1;

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
            
            var damageDealer = orb.GetComponent<DamageDealer>();
            if (damageDealer == null) damageDealer = orb.AddComponent<DamageDealer>();
            damageDealer.damage = damage;
            damageDealer.damageInterval = 0.5f; // Can hit same enemy every 0.5s
            damageDealer.isContinuous = true;
            
            activeOrbs.Add(orb);
        }
    }
}
