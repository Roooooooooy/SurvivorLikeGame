using UnityEngine;

namespace RpgGame
{
    // 自由探索交互基类：宝箱、门、NPC、传送门、存档点都继承它。
    [RequireComponent(typeof(Collider2D))]
    public abstract class Interactable : MonoBehaviour
    {
        public string displayName = "Interact";

        public virtual bool CanInteract => true;
        public virtual string Prompt => displayName;

        public abstract void Interact();
    }
}
