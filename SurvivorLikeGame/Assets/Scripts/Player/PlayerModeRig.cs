using UnityEngine;

namespace RpgGame
{
    // 按区域模式启用/禁用玩家能力。幸存者区目前继续使用旧的 PlayerController。
    [DisallowMultipleComponent]
    public class PlayerModeRig : MonoBehaviour
    {
        public PlayerMotor motor;
        public PlayerHealth health;
        public ActionCombatController actionCombat;
        public WorldInteractor interactor;
        public RegionMode mode = RegionMode.ActionRPG;

        void Awake()
        {
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (health == null) health = GetComponent<PlayerHealth>();
            if (actionCombat == null) actionCombat = GetComponent<ActionCombatController>();
            if (interactor == null) interactor = GetComponent<WorldInteractor>();
        }

        public void ApplyMode(RegionMode newMode)
        {
            mode = newMode;
            bool action = newMode == RegionMode.ActionRPG;

            if (actionCombat != null) actionCombat.enabled = action;
            if (interactor != null) interactor.enabled = action;
            if (motor != null) motor.SetControlEnabled(health == null || !health.IsDead);
        }

        public void TeleportTo(Vector3 position)
        {
            if (motor != null) motor.Teleport(position);
            else transform.position = new Vector3(position.x, position.y, transform.position.z);
        }
    }
}
