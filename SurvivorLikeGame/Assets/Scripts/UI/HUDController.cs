using UnityEngine;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    [Header("Health Bar")]
    public Image healthBarFill;
    public Text healthText;

    [Header("Exp Bar")]
    public Image expBarFill;
    public Text levelText;

    void Update()
    {
        var player = PlayerController.Instance;
        if (player == null) return;

        if (healthBarFill) healthBarFill.fillAmount = player.GetHealthPercent();
        if (healthText) healthText.text = player.currentHealth + "/" + player.maxHealth;
        if (expBarFill) expBarFill.fillAmount = player.GetExpPercent();
        if (levelText) levelText.text = "Lv." + player.level;
    }
}