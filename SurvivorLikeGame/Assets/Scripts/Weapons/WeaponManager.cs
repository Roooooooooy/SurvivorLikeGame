using UnityEngine;
using System.Collections.Generic;

public class WeaponManager : MonoBehaviour
{
    [Header("Weapons")]
    public List<WeaponBase> activeWeapons = new List<WeaponBase>();

    // All possible weapon types for upgrade selection
    private string[] allUpgradeOptions = new string[]
    {
        "Orb Damage +20%",
        "Orb Count +1",
        "Orb Speed +20%",
        "Projectile Damage +20%",
        "Projectile Count +1",
        "Projectile Fire Rate +20%",
        "Move Speed +10%",
        "Max Health +20",
        "Pickup Radius +30%"
    };

    private string[] pendingOptions;

    public void ShowUpgradeOptions()
    {
        // Pick 3 random options
        List<string> shuffled = new List<string>(allUpgradeOptions);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = shuffled[i]; shuffled[i] = shuffled[j]; shuffled[j] = tmp;
        }
        pendingOptions = new string[] { shuffled[0], shuffled[1], shuffled[2] };

        if (GameManager.Instance) GameManager.Instance.ShowLevelUp(pendingOptions);
    }

    public void SelectUpgrade(int index)
    {
        if (pendingOptions == null || index >= pendingOptions.Length) return;
        ApplyUpgrade(pendingOptions[index]);
        if (GameManager.Instance) GameManager.Instance.HideLevelUp();
    }

    void ApplyUpgrade(string option)
    {
        var player = GetComponent<PlayerController>();
        var orbWeapon = GetComponentInChildren<OrbWeapon>();
        var projectileWeapon = GetComponentInChildren<ProjectileWeapon>();

        switch (option)
        {
            case "Orb Damage +20%":
                if (orbWeapon) orbWeapon.damage = Mathf.RoundToInt(orbWeapon.damage * 1.2f);
                break;
            case "Orb Count +1":
                if (orbWeapon) orbWeapon.AddOrb();
                break;
            case "Orb Speed +20%":
                if (orbWeapon) orbWeapon.rotationSpeed *= 1.2f;
                break;
            case "Projectile Damage +20%":
                if (projectileWeapon) projectileWeapon.damage = Mathf.RoundToInt(projectileWeapon.damage * 1.2f);
                break;
            case "Projectile Count +1":
                if (projectileWeapon) projectileWeapon.projectileCount++;
                break;
            case "Projectile Fire Rate +20%":
                if (projectileWeapon) projectileWeapon.fireInterval *= 0.8f;
                break;
            case "Move Speed +10%":
                if (player) player.moveSpeed *= 1.1f;
                break;
            case "Max Health +20":
                if (player) { player.maxHealth += 20; player.currentHealth += 20; }
                break;
            case "Pickup Radius +30%":
                var pickup = GetComponent<ExpPickupRadius>();
                if (pickup) pickup.radius *= 1.3f;
                break;
        }
    }
}
