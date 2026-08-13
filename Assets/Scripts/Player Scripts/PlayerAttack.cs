using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using CyberVeil.Systems;
using CyberVeil.Combat;
using CyberVeil.VFX;
using CyberVeil.Core;

namespace CyberVeil.Player
{
    /// <summary>
    /// Handles player attack input, combo management, movement boost during attack,
    /// and coordinates attack effects, visuals, and damage
    /// </summary>
    public class PlayerAttack : MonoBehaviour
    {
        /// <summary>
        /// Raised only when the attack gate rejects an otherwise requested slash.
        /// UI feedback can subscribe without polling combat input.
        /// </summary>
        public event Action OnAttackRejected;

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

        [Header("Slash References")]
        public SlashAttack slash1;
        public SlashAttack2 slash2;
        public SlashAttack3 slash3;

        [Header("Heavy Slash")]
        public SlashAttackCross heavySlash;

        [Header("Heavy Attack Settings")]
        [SerializeField] private float heavyChargeSeconds = 0.45f;
        [SerializeField] private float heavyAttackDuration = 0.55f;
        [SerializeField] private float heavyAttackCooldown = 0.8f;
        [SerializeField] private float heavyAttackRange = 2.5f;
        [SerializeField] private float heavyDamageMultiplier = 1.2f;
        [SerializeField] private float heavyLungeDistance = 2.5f;
        [SerializeField] private float heavyLungeHeight = 0.6f;
        [SerializeField] private float heavyLungeDuration = 0.25f;

        [Header("Heavy Charge VFX")]
        [SerializeField] private ParticleSystem heavyChargeParticles;

        private VeilSurgeSkill veilSurgeSkill;
        [SerializeField] private MonoBehaviour attackGateBehaviour;
        private IAttackGate attackGate;
        private PlayerController playerController;
        private CharacterStateMachine stateMachine;
        private bool canHeavyAttack = true;
        private Coroutine heavyLungeRoutine;
        private bool heavyChargeInProgress;
        private float heavyChargeStartTime;

        private void Start()
        {
            playerController = GetComponent<PlayerController>();
            stateMachine = GetComponent<CharacterStateMachine>();
            attackGate = GetComponent<AttackLimiterMechanic>();
            veilSurgeSkill = GetComponent<VeilSurgeSkill>();

            toggleAxe.HideAxe();
            toggleAxe2.HideAxe2();
        }

        public void HandleAttackInput()
        {
            if (stateMachine.CurrentState == CharacterState.Attacking
                || Mouse.current == null)
                return;

            if (heavyChargeInProgress)
            {
                if (Mouse.current.rightButton.wasReleasedThisFrame)
                    ReleaseHeavyCharge();
                return;
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                BeginHeavyCharge();
                return;
            }

            if (canAttack == false || !Mouse.current.leftButton.wasPressedThisFrame)
                return;

            // Limiter check (bypass if offensive skill active)
            if (veilSurgeSkill != null && veilSurgeSkill.ShouldBypassAttackLocking)
            {
                // During offensive boost, ignore attack locking
            }
            else if (attackGate != null && !attackGate.CanStartAttack)
            {
                RejectAttack();
                return; 
            }

            // Normal attack sequence
            StartAttack();
            SoundManager.PlaySound(SoundType.ATTACK, attackVolume);
            SoundManager.PlaySound(SoundType.SLASH, slashVolume);

            AttackMovementBoost();
            UpdateAxeVisuals(attackComboCount);
            HandleComboLogic();
        }

        private void TryStartHeavyAttack()
        {
            if (!canHeavyAttack)
                return;

            // Limiter check (bypass if offensive skill active)
            if (veilSurgeSkill != null && veilSurgeSkill.ShouldBypassAttackLocking)
            {
                // During offensive boost, ignore attack locking
            }
            else if (attackGate != null && !attackGate.CanStartAttack)
            {
                RejectAttack();
                return;
            }

            StartHeavyAttack();
        }

        private void BeginHeavyCharge()
        {
            if (!canHeavyAttack)
                return;

            // Limiter check (bypass if offensive skill active)
            if (veilSurgeSkill != null && veilSurgeSkill.ShouldBypassAttackLocking)
            {
                // During offensive boost, ignore attack locking
            }
            else if (attackGate != null && !attackGate.CanStartAttack)
            {
                RejectAttack();
                return;
            }

            heavyChargeInProgress = true;
            heavyChargeStartTime = Time.time;
            HideAxes();
            StartHeavyChargeVfx();
        }

        private void ReleaseHeavyCharge()
        {
            if (!heavyChargeInProgress)
                return;

            heavyChargeInProgress = false;
            StopHeavyChargeVfx();

            float heldTime = Time.time - heavyChargeStartTime;
            if (heldTime >= heavyChargeSeconds)
            {
                TryStartHeavyAttack();
                return;
            }

            HideAxes();
        }

        private void StartHeavyAttack()
        {
            attackGate?.RecordAttack();
            canHeavyAttack = false;
            stateMachine.ChangeState(CharacterState.Attacking);
            Invoke(nameof(EndAttack), heavyAttackDuration);

            // Damage
            var mods = PlayerStatsUpgradeManager.Instance;
            float dmgMul = mods ? mods.DamageMultiplier : 1f;
            float range = heavyAttackRange > 0f ? heavyAttackRange : attackRange;
            int finalDamage = Mathf.RoundToInt(attackDamage * heavyDamageMultiplier * dmgMul);
            CombatManager.Instance.DealDamageInRadius(transform.position, range, finalDamage, gameObject);

            Vector3 attackDirection = playerController.GetLastDirection();
            if (heavySlash != null)
                heavySlash.PlaySlash(attackDirection);

            StopHeavyChargeVfx();
            ShowHeavyAxes();

            SoundManager.PlaySound(SoundType.ATTACK, attackVolume);
            SoundManager.PlaySound(SoundType.SLASH, slashVolume);

            if (heavyLungeRoutine != null)
                StopCoroutine(heavyLungeRoutine);
            heavyLungeRoutine = StartCoroutine(HeavyLungeRoutine(attackDirection));

            Invoke(nameof(ResetHeavyAttackCooldown), heavyAttackCooldown);
        }

        private void StartAttack()
        {
            attackGate?.RecordAttack();
            stateMachine.ChangeState(CharacterState.Attacking);
            Invoke(nameof(EndAttack), attackDuration);

            // Reads DamageMultiplier from the manager, if mods is null it safely falls back to 1f (no bonus)
            var mods = PlayerStatsUpgradeManager.Instance;
            float dmgMul = mods ? mods.DamageMultiplier : 1f;
            int finalDamage = Mathf.RoundToInt(attackDamage * dmgMul);
            CombatManager.Instance.DealDamageInRadius(transform.position, attackRange, finalDamage, gameObject);

            // Trigger slash effect using centralized ParticleManager
            Vector3 attackDirection = playerController.GetLastDirection();

            if (attackComboCount == 0)
                slash1.PlaySlash(attackDirection);
            else if (attackComboCount == 1)
                slash2.PlaySlash(attackDirection);
            else if (attackComboCount == 2)
                slash3.PlaySlash(attackDirection);
        }

        private void EndAttack()
        {
            StopHeavyChargeVfx();
            HideAxes();
            stateMachine.ChangeState(CharacterState.Idle);
        }

        private IEnumerator HeavyLungeRoutine(Vector3 direction)
        {
            if (playerController == null)
                yield break;

            CharacterController controller = playerController.GetCharacterController();
            if (controller == null)
                yield break;

            float duration = Mathf.Max(0.01f, heavyLungeDuration);
            Vector3 start = transform.position;
            Vector3 forward = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
            Vector3 last = start;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float height = Mathf.Sin(t * Mathf.PI) * heavyLungeHeight;
                Vector3 target = start + forward * (heavyLungeDistance * t) + Vector3.up * height;
                Vector3 delta = target - last;
                controller.Move(delta);
                last = target;
                yield return null;
            }
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

        private void ShowHeavyAxes()
        {
            if (toggleAxe != null)
                toggleAxe.ShowAxe();
            if (toggleAxe2 != null)
            {
                toggleAxe2.HideAxe2();
                toggleAxe2.ShowAxe2();
            }
        }

        private void HideAxes()
        {
            if (toggleAxe != null)
                toggleAxe.HideAxe();
            if (toggleAxe2 != null)
                toggleAxe2.HideAxe2();
        }

        private void StartHeavyChargeVfx()
        {
            if (heavyChargeParticles == null)
                return;

            heavyChargeParticles.gameObject.SetActive(true);
            if (!heavyChargeParticles.isPlaying)
                heavyChargeParticles.Play();
        }

        private void StopHeavyChargeVfx()
        {
            if (heavyChargeParticles == null)
                return;

            if (heavyChargeParticles.isPlaying)
                heavyChargeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            heavyChargeParticles.gameObject.SetActive(false);
        }

        private void HandleComboLogic()
        {
            attackComboCount++;
            canAttack = false;

            // Get attack speed multiplier from skill
            float attackCooldownMultiplier = 1f;
            if (veilSurgeSkill != null && veilSurgeSkill);
            {
                attackCooldownMultiplier = 1f / veilSurgeSkill.GetAttackSpeedMultiplier();
            }

            if (attackComboCount != 3)
            {
                Invoke(nameof(ResetAttackCooldown), attackCooldown * attackCooldownMultiplier);
            }
            else
            {
                Invoke(nameof(ResetAttackCooldown), comboAttackCooldown * attackCooldownMultiplier);
                attackComboCount = 0;
                attackMovementBoost = 30f;
                playerController.speed = playerController.defaultSpeed;
            }
        }

        private void ResetAttackCooldown()
        {
            canAttack = true;
        }

        private void ResetHeavyAttackCooldown()
        {
            canHeavyAttack = true;
        }

        private void RejectAttack()
        {
            SoundManager.PlaySound(SoundType.ATTACKLOCK, 0.6f);
            OnAttackRejected?.Invoke();
        }
    }
}
