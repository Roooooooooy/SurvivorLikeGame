using UnityEngine;

namespace RpgGame
{
    // 可复用的道具 Prefab：根节点在玩法 XY 平面，模型位于画面 XZ 平面。
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class KayKitWorldAnchor : MonoBehaviour
    {
        public Transform visual;
        public float groundHeight;

        public void Sync()
        {
            if (visual != null)
                visual.position = new Vector3(transform.position.x, groundHeight, transform.position.y);
        }

        void LateUpdate() => Sync();
    }
}
