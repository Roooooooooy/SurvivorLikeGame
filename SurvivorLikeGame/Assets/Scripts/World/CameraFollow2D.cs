using UnityEngine;

namespace RpgGame
{
    // 动作区相机：跟随玩家并限制在区域边界内。
    [RequireComponent(typeof(Camera))]
    public class CameraFollow2D : MonoBehaviour
    {
        public Transform target;
        public float smooth = 8f;
        public Bounds bounds;
        public bool useBounds = true;

        Camera cam;

        void Awake()
        {
            cam = GetComponent<Camera>();
        }

        void Start()
        {
            if (target == null && PlayerHealth.Player != null)
                target = PlayerHealth.Player.transform;
        }

        void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);

            if (useBounds && bounds.size.x > 0f && bounds.size.y > 0f && cam != null)
            {
                float halfHeight = cam.orthographicSize;
                float halfWidth = halfHeight * cam.aspect;

                float minX = bounds.min.x + halfWidth;
                float maxX = bounds.max.x - halfWidth;
                float minY = bounds.min.y + halfHeight;
                float maxY = bounds.max.y - halfHeight;

                desired.x = minX <= maxX ? Mathf.Clamp(desired.x, minX, maxX) : bounds.center.x;
                desired.y = minY <= maxY ? Mathf.Clamp(desired.y, minY, maxY) : bounds.center.y;
            }

            float t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, t);
        }
    }
}
