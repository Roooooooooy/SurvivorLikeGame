using UnityEngine;

public class ExpPickupRadius : MonoBehaviour
{
    public float radius = 3f;
    private CircleCollider2D pickupCol;

    void Start()
    {
        pickupCol = gameObject.AddComponent<CircleCollider2D>();
        pickupCol.isTrigger = true;
        pickupCol.radius = radius;
    }

    void Update()
    {
        if (pickupCol) pickupCol.radius = radius;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("ExpGem"))
        {
            var gem = other.GetComponent<ExpGem>();
            if (gem != null)
            {
                gem.SetTarget(transform);
            }
            return;
        }

        // 金币复用同一拾取半径：进入范围后开始磁吸
        var coin = other.GetComponent<CoinPickup>();
        if (coin != null)
        {
            coin.SetTarget(transform);
        }
    }
}