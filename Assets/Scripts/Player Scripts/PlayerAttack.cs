using UnityEngine;
using UnityEngine.InputSystem;
using CyberVeil.Systems;
using CyberVeil.Combat;
using CyberVeil.VFX;

namespace CyberVeil.Player
{
    /// <summary>
    /// Handles player attack input, combo management, movement boost during attack,
    /// and coordinates attack effects, visuals, and damage
    /// </summary>
    public class PlayerAttack : MonoBehaviour
    {
        [Header("Attack Settings")]
        public bool canAttack = true; // Flag to prevent spam attacking
        [SerializeField] private float attackMovementBoost = 30f; // Boost forward whenever attacking
        [SerializeField] private int attackComboCount = 0;
        [SerializeField] private float attackDuration = 0.45f;
        private float comboAttackCooldown = 0.6f; // Pause after 3 hit combo
        private float attackCooldown = 0.2f; // Quick cooldown between each attack
        [SerializeField] private float attackVolume = 0.5f;
        [SerializeField] private float slashVolume = 0.3f;

        [Header("Damage Settings")]
        public float attackRange = 2f;
        public int attackDamage = 25;

        [Header("Axe References")]
        public toggleAxe toggleAxe;
        public toggleAxe2 toggleAxe2;

        private PlayerController playerController;
        private PlayerStateMachine stateMachine;

        private void Start()
        {
            playerController = GetComponent<PlayerController>();
            stateMachine = GetComponent<PlayerStateMachine>();

            toggleAxe.HideAxe();
            toggleAxe2.HideAxe2();
        }

        public void HandleAttackInput()
        {
            if (stateMachine.CurrentState != PlayerState.Attacking
                && canAttack
                && Mouse.current != null
                && Mouse.current.leftButton.wasPressedThisFrame)
            {
                StartAttack();
                SoundManager.PlaySound(SoundType.ATTACK, attackVolume);
                SoundManager.PlaySound(SoundType.SLASH, slashVolume);

                // Movement boost during attack
                AttackMovementBoost();

                // Axe visuals for combo count
                UpdateAxeVisuals(attackComboCount);

                HandleComboLogic();
            }
        }

        private void StartAttack()
        {
            stateMachine.ChangeState(PlayerState.Attacking);
            Invoke(nameof(EndAttack), attackDuration);

            // Apply combat damage using CombatManager
            CombatManager.Instance.DealDamageInRadius(transform.position, attackRange, attackDamage, gameObject);

            // Trigger slash effect using centralized ParticleManager
            ParticleManager.Instance.PlaySlash(attackComboCount, transform.position, playerController.GetLastDirection());
        }

        private void EndAttack()
        {
            toggleAxe.HideAxe();
            toggleAxe2.HideAxe2();
            stateMachine.ChangeState(PlayerState.Idle);
        }

        private void AttackMovementBoost()
        {
            playerController.speed = 1f;
            attackMovementBoost -= 10;
            Vector3 attackMove = playerController.GetLastDirection() * attackMovementBoost * Time.deltaTime;
            playerController.GetCharacterController().Move(attackMove);
        }

        private void UpdateAxeVisuals(int comboCount)
        {
            if (comboCount == 0 || comboCount == 2)
            {
                toggleAxe.ShowAxe();
            }
            if (comboCount == 1)
            {
                toggleAxe2.HideAxe2();
                toggleAxe2.ShowAxe2();
            }
        }

        private void HandleComboLogic()
        {
            attackComboCount++;
            canAttack = false;

            if (attackComboCount != 3)
            {
                Invoke(nameof(ResetAttackCooldown), attackCooldown);
            }
            else
            {
                Invoke(nameof(ResetAttackCooldown), comboAttackCooldown);
                attackComboCount = 0;
                attackMovementBoost = 30f;
                playerController.speed = playerController.defaultSpeed;
            }
        }

        private void ResetAttackCooldown()
        {
            canAttack = true;
        }
    }
}
