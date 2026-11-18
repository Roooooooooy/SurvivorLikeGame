using UnityEngine;
using UnityEngine.InputSystem;

namespace RpgGame
{
    // 搜索附近的可交互对象，按 E / Enter 交互。
    public class WorldInteractor : MonoBehaviour
    {
        public float radius = 1.6f;
        public LayerMask interactableLayers = ~0;

        public Interactable Current { get; private set; }

        void Update()
        {
            if (EquipmentScreen.IsOpen)
            {
                Current = null;
                return;
            }

            Current = FindNearest();

            var keyboard = Keyboard.current;
            if (Current != null && keyboard != null &&
                (keyboard.eKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
            {
                Current.Interact();
            }
        }

        Interactable FindNearest()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, interactableLayers);
            Interactable best = null;
            float bestDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                if (hit == null) continue;

                var interactable = hit.GetComponentInParent<Interactable>();
                if (interactable == null || !interactable.CanInteract) continue;

                float distance = ((Vector2)hit.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = interactable;
                }
            }

            return best;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
