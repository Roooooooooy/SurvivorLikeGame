using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public float smoothSpeed = 5f;
    private Transform target;

    void Start()
    {
        if (PlayerController.Instance != null)
            target = PlayerController.Instance.transform;
    }

    void LateUpdate()
    {
        if (target == null)
        {
            if (PlayerController.Instance != null)
                target = PlayerController.Instance.transform;
            return;
        }

        Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);
        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
    }
}