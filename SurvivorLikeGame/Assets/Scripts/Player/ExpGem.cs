using UnityEngine;

public class ExpGem : MonoBehaviour
{
    public int expAmount = 1;
    public float moveSpeed = 8f;
    
    private Transform targetPlayer = null;

    void Update()
    {
        if (targetPlayer != null)
        {
            transform.position = Vector2.MoveTowards(transform.position, targetPlayer.position, moveSpeed * Time.deltaTime);
        }
    }

    public void SetTarget(Transform player)
    {
        targetPlayer = player;
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Player"))
        {
            var p = col.GetComponent<PlayerController>();
            if (p != null)
            {
                p.GainExp(expAmount);
                Destroy(gameObject);
            }
        }
    }
}