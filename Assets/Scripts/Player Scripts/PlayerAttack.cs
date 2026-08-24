using System;
using System.Collections;
using CyberVeil.Combat;
using CyberVeil.Core;
using CyberVeil.Systems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CyberVeil.Player
{
    /// <summary>
    /// Preserves CyberVeil's original permissive attack cadence while the
    /// data-driven combo selects the upgraded character animation and presentation.
    /// </summary>
    public class PlayerAttack : MonoBehaviour
    {
        public event Action OnAttackRejected;
        public event Action<PlayerAttackStep, float> OnAttackStepStarted;

        [Header("Attack Settings")]
        public bool canAttack = true;
        [SerializeField] private int attackComboCount;
        [SerializeField] private float attackVolume = 0.5f;
        [SerializeField] private float slashVolume = 0.3f;
        [SerializeField, Min(0.05f)] private float lightAttackDuration = 0.45f;
        [SerializeField, Min(0.05f)] private float comboAttackCooldown = 0.6f;
        [SerializeField] private float attackMovementBoost = 30f;
        [SerializeField, Min(0.1f)] private float maximumPlaybackSpeed = 3.5f;

        [Header("Combo")]
        [SerializeField] private PlayerComboDefinition comboDefinition;
        [SerializeField] private PlayerSlashEmitter slashEmitter;

        [Header("Damage Settings")]
        public float attackRange = 2f;
        public int attackDamage = 25;

        [Header("Axe References")]
        public toggleAxe toggleAxe;
        public toggleAxe2 toggleAxe2;

        [Header("Attack Gate")]
        [SerializeField] private MonoBehaviour attackGateBehaviour;

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
        private IAttackGate attackGate;
        private PlayerController playerController;
        private CharacterStateMachine stateMachine;
        private PlayerAttackStep activeStep;
        private Coroutine lightAttackWatchdog;
        private Coroutine comboRecoveryRoutine;
        private Coroutine weaponRevealRoutine;
        private Coroutine heavyLungeRoutine;
        private Coroutine heavyEndRoutine;
        private Coroutine heavyCooldownRoutine;
        private bool lightAttackActive;
        private bool heavyAttackActive;
        private bool comboWindowOpen;
        private bool bufferedAttack;
        private bool hitResolved;
        private bool canHeavyAttack = true;
        private bool heavyChargeInProgress;
        private bool queuedLightAfterHeavy;
        private float heavyChargeStartTime;
        private float lightAttackEarliestFinishTime;
        private int attackToken;
        private int heavyAttackToken;

        public int CurrentComboStep => attackComboCount;
        public bool IsLightAttackActive => lightAttackActive;
        public bool IsHeavyAttackActive => heavyAttackActive;
        public bool IsAnyAttackActive => lightAttackActive || heavyAttackActive;
        public bool IsComboWindowOpen => comboWindowOpen;
        public bool HasBufferedAttack => bufferedAttack || queuedLightAfterHeavy;

        /// <summary>
        /// Cancels only an attack input that has not started yet. The attack already
        /// playing remains active, allowing dash to act as an intentional queue cancel.
        /// </summary>
        public bool TryCancelQueuedAttackForDash()
        {
            if (!bufferedAttack && !queuedLightAfterHeavy)
                return false;

            bufferedAttack = false;
            queuedLightAfterHeavy = false;
            return true;
        }

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            stateMachine = GetComponent<CharacterStateMachine>();
            veilSurgeSkill = GetComponent<VeilSurgeSkill>();
            slashEmitter = slashEmitter != null ? slashEmitter : GetComponent<PlayerSlashEmitter>();
            attackGate = attackGateBehaviour as IAttackGate;
            if (attackGate == null)
                attackGate = GetComponent<AttackLimiterMechanic>();
        }

        private void OnEnable()
        {
            if (stateMachine != null)
                stateMachine.OnStateChange += OnCharacterStateChanged;
        }

        private void Start()
        {
            HideAxes();
        }

        private void OnDisable()
        {
            if (stateMachine != null)
                stateMachine.OnStateChange -= OnCharacterStateChanged;

            StopAllCoroutines();
            lightAttackActive = false;
            heavyAttackActive = false;
            heavyChargeInProgress = false;
            comboWindowOpen = false;
            bufferedAttack = false;
            queuedLightAfterHeavy = false;
            canAttack = true;
            canHeavyAttack = true;
            activeStep = null;
            StopHeavyChargeVfx();
            HideAxes();
            ResetLegacyMovementBoost();
            if (stateMachine != null && stateMachine.CurrentState == CharacterState.Attacking)
                stateMachine.ChangeState(CharacterState.Idle);
        }

        public void HandleAttackInput()
        {
            if (Mouse.current == null || stateMachine == null)
                return;

            CharacterState state = stateMachine.CurrentState;
            if (state == CharacterState.Damaged)
                return;

            if (heavyChargeInProgress)
            {
                if (!Mouse.current.rightButton.isPressed)
                    ReleaseHeavyCharge();
                return;
            }

            if (heavyAttackActive)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    queuedLightAfterHeavy = true;
                    if (state != CharacterState.Attacking)
                        StartQueuedLightAfterHeavy();
                }
                return;
            }

            if (lightAttackActive)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame && HasFollowingComboStep())
                    bufferedAttack = true;
                return;
            }

            if (state == CharacterState.Attacking)
                return;

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                BeginHeavyCharge();
                return;
            }

            if (canAttack && Mouse.current.leftButton.wasPressedThisFrame)
                BeginLightAttack(attackComboCount, false);
        }

        private bool BeginLightAttack(int stepIndex, bool chained)
        {
            if ((!chained && !canAttack)
                || comboDefinition == null
                || !comboDefinition.TryGetStep(stepIndex, out PlayerAttackStep step))
            {
                if (comboDefinition == null)
                    Debug.LogError("PlayerAttack requires a PlayerComboDefinition.", this);
                return false;
            }

            if (!CanPassAttackGate())
            {
                RejectAttack();
                return false;
            }

            StopRoutine(ref comboRecoveryRoutine);
            StopRoutine(ref lightAttackWatchdog);
            attackGate?.RecordAttack();

            attackComboCount = stepIndex;
            activeStep = step;
            lightAttackActive = true;
            canAttack = false;
            comboWindowOpen = false;
            bufferedAttack = false;
            hitResolved = false;
            int token = ++attackToken;
            lightAttackEarliestFinishTime = Time.time + Mathf.Max(0.05f, lightAttackDuration);

            stateMachine.ChangeState(CharacterState.Attacking);
            float playbackSpeed = GetEffectivePlaybackSpeed(step);
            OnAttackStepStarted?.Invoke(step, playbackSpeed);
            UpdateAxeVisuals(step, token);
            OnAnimationHit();
            ApplyLegacyAttackMovementBoost(stepIndex);
            lightAttackWatchdog = StartCoroutine(
                LightAttackWatchdog(token, lightAttackDuration));
            return true;
        }

        /// <summary>
        /// Resolves once per step. Legacy combat calls this immediately; the clip's
        /// later Hit event becomes a harmless no-op for the already-resolved attack.
        /// </summary>
        public void OnAnimationHit()
        {
            if (!lightAttackActive || activeStep == null || hitResolved)
                return;

            hitResolved = true;
            Vector3 direction = GetAttackDirection();
            float range = attackRange * activeStep.RangeMultiplier;
            var upgrades = PlayerStatsUpgradeManager.Instance;
            float upgradeMultiplier = upgrades ? upgrades.DamageMultiplier : 1f;
            int finalDamage = Mathf.RoundToInt(
                attackDamage * activeStep.DamageMultiplier * upgradeMultiplier);

            CombatHitResult hitResult = default;
            if (CombatManager.Instance != null)
            {
                hitResult = CombatManager.Instance.DealDamageInRadiusWithResult(
                    transform.position,
                    range,
                    finalDamage,
                    gameObject);
            }

            if (slashEmitter != null)
            {
                slashEmitter.EmitSlash(
                    activeStep,
                    direction,
                    veilSurgeSkill != null && veilSurgeSkill.IsVeilSurge);
                if (hitResult.HitAny)
                    slashEmitter.EmitImpact(hitResult.FirstHitPosition);
            }

            if (hitResult.HitAny
                && activeStep.HitStopDuration > 0f
                && HitstopManager.Instance != null)
            {
                HitstopManager.Instance.DoHitstop(
                    activeStep.HitStopDuration,
                    activeStep.HitStopTimeScale);
            }

            SoundManager.PlaySound(SoundType.ATTACK, attackVolume);
            SoundManager.PlaySound(SoundType.SLASH, slashVolume);
        }

        public void OpenComboWindow()
        {
            if (!lightAttackActive || !HasFollowingComboStep())
                return;

            comboWindowOpen = true;
        }

        public void CloseComboWindow()
        {
            comboWindowOpen = false;
        }

        public void FinishLightAttack()
        {
            if (!lightAttackActive || activeStep == null || comboDefinition == null)
                return;

            // Fast animation playback (for example Veil Surge) may reach the clip's
            // Finish event early. The original combat always held a light attack for
            // its fixed duration, so presentation cannot shorten that gameplay lock.
            if (Time.time + 0.005f < lightAttackEarliestFinishTime)
                return;

            if (!hitResolved)
                OnAnimationHit();

            StopRoutine(ref lightAttackWatchdog);
            comboWindowOpen = false;
            int completedIndex = attackComboCount;
            int nextIndex = (completedIndex + 1) % comboDefinition.StepCount;
            bool completedCombo = completedIndex >= comboDefinition.StepCount - 1;
            bool shouldChain = bufferedAttack
                && !completedCombo;

            lightAttackActive = false;
            bufferedAttack = false;
            activeStep = null;

            if (shouldChain && BeginLightAttack(nextIndex, true))
                return;

            attackComboCount = nextIndex;
            HideAxes();
            if (stateMachine.CurrentState == CharacterState.Attacking)
                stateMachine.ChangeState(CharacterState.Idle);

            if (completedCombo)
            {
                float recovery = GetLegacyComboRecoveryDuration();
                if (recovery > 0f)
                {
                    canAttack = false;
                    comboRecoveryRoutine = StartCoroutine(
                        UnlockLightAttackAfterDelay(recovery, attackToken));
                    return;
                }
            }

            canAttack = true;
        }

        private IEnumerator LightAttackWatchdog(int token, float duration)
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, duration));
            if (lightAttackActive && token == attackToken)
            {
                lightAttackWatchdog = null;
                FinishLightAttack();
            }
        }

        private IEnumerator UnlockLightAttackAfterDelay(float delay, int token)
        {
            yield return new WaitForSeconds(delay);
            if (!lightAttackActive && token == attackToken)
                canAttack = true;
            comboRecoveryRoutine = null;
        }

        private void BeginHeavyCharge()
        {
            if (!canHeavyAttack || lightAttackActive || !CanPassAttackGate())
            {
                if (canHeavyAttack && !lightAttackActive)
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
            if (Time.time - heavyChargeStartTime >= heavyChargeSeconds)
                TryStartHeavyAttack();
            else
                HideAxes();
        }

        private void TryStartHeavyAttack()
        {
            if (!canHeavyAttack || !CanPassAttackGate())
            {
                if (canHeavyAttack)
                    RejectAttack();
                return;
            }
            StartHeavyAttack();
        }

        private void StartHeavyAttack()
        {
            attackGate?.RecordAttack();
            canHeavyAttack = false;
            heavyAttackActive = true;
            queuedLightAfterHeavy = false;
            int token = ++heavyAttackToken;
            stateMachine.ChangeState(CharacterState.Attacking);

            var upgrades = PlayerStatsUpgradeManager.Instance;
            float damageMultiplier = upgrades ? upgrades.DamageMultiplier : 1f;
            float heavyUpgradeMultiplier = upgrades
                ? upgrades.HeavyDamageMultiplier
                : VeilRunManager.CurrentHeavyDamageMultiplier;
            float range = heavyAttackRange > 0f ? heavyAttackRange : attackRange;
            int finalDamage = Mathf.RoundToInt(
                attackDamage * heavyDamageMultiplier * damageMultiplier * heavyUpgradeMultiplier);
            CombatManager.Instance?.DealDamageInRadius(
                transform.position,
                range,
                finalDamage,
                gameObject);

            Vector3 direction = GetAttackDirection();
            heavySlash?.PlaySlash(direction);
            StopHeavyChargeVfx();
            ShowHeavyAxes();
            SoundManager.PlaySound(SoundType.ATTACK, attackVolume);
            SoundManager.PlaySound(SoundType.SLASH, slashVolume);

            StopRoutine(ref heavyLungeRoutine);
            heavyLungeRoutine = StartCoroutine(HeavyLungeRoutine(direction));
            StopRoutine(ref heavyEndRoutine);
            heavyEndRoutine = StartCoroutine(EndHeavyAttackAfterDelay(token));
            StopRoutine(ref heavyCooldownRoutine);
            heavyCooldownRoutine = StartCoroutine(ResetHeavyCooldownAfterDelay());
        }

        private IEnumerator EndHeavyAttackAfterDelay(int token)
        {
            yield return new WaitForSeconds(heavyAttackDuration);
            if (heavyAttackActive && token == heavyAttackToken)
            {
                heavyAttackActive = false;
                HideHeavyAxes();
                if (stateMachine.CurrentState == CharacterState.Attacking)
                    stateMachine.ChangeState(CharacterState.Idle);

                heavyEndRoutine = null;
                if (queuedLightAfterHeavy)
                    StartQueuedLightAfterHeavy();
                yield break;
            }
            heavyEndRoutine = null;
        }

        private void StartQueuedLightAfterHeavy()
        {
            if (!queuedLightAfterHeavy || comboDefinition == null)
                return;

            queuedLightAfterHeavy = false;
            ++heavyAttackToken;
            heavyAttackActive = false;
            StopRoutine(ref heavyEndRoutine);
            HideHeavyAxes();
            canAttack = true;

            if (!BeginLightAttack(attackComboCount, true)
                && stateMachine.CurrentState == CharacterState.Attacking)
            {
                stateMachine.ChangeState(CharacterState.Idle);
            }
        }

        private IEnumerator ResetHeavyCooldownAfterDelay()
        {
            yield return new WaitForSeconds(heavyAttackCooldown);
            canHeavyAttack = true;
            heavyCooldownRoutine = null;
        }

        private IEnumerator HeavyLungeRoutine(Vector3 direction)
        {
            CharacterController controller = playerController?.GetCharacterController();
            if (controller == null)
            {
                heavyLungeRoutine = null;
                yield break;
            }

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
                controller.Move(target - last);
                last = target;
                yield return null;
            }
            heavyLungeRoutine = null;
        }

        private void ApplyLegacyAttackMovementBoost(int stepIndex)
        {
            CharacterController controller = playerController?.GetCharacterController();
            if (playerController == null || controller == null)
                return;

            playerController.speed = 1f;
            attackMovementBoost -= 10f;
            controller.Move(GetAttackDirection() * attackMovementBoost * Time.deltaTime);

            if (comboDefinition != null && stepIndex >= comboDefinition.StepCount - 1)
            {
                attackMovementBoost = 30f;
                playerController.speed = playerController.defaultSpeed;
            }
        }

        private void UpdateAxeVisuals(PlayerAttackStep step, int token)
        {
            StopRoutine(ref weaponRevealRoutine);
            HideAxesImmediately();
            float revealDelay = Mathf.Clamp(step.CrossFadeTime, 0f, 0.06f);
            if (revealDelay > 0f)
            {
                weaponRevealRoutine = StartCoroutine(
                    RevealStepAxeAfterDelay(step, token, revealDelay));
                return;
            }

            ShowStepAxe(step);
        }

        private IEnumerator RevealStepAxeAfterDelay(
            PlayerAttackStep step,
            int token,
            float delay)
        {
            yield return new WaitForSeconds(delay);
            weaponRevealRoutine = null;
            if (lightAttackActive && token == attackToken && activeStep == step)
                ShowStepAxe(step);
        }

        private void ShowStepAxe(PlayerAttackStep step)
        {
            if (step.UseSecondaryAxe)
            {
                toggleAxe2?.ShowAxe2Static();
            }
            else
            {
                toggleAxe?.ShowAxeStatic();
            }
        }

        private void ShowHeavyAxes()
        {
            toggleAxe?.ShowAxe();
            if (toggleAxe2 != null)
            {
                toggleAxe2.HideAxe2Immediate();
                toggleAxe2.ShowAxe2();
            }
        }

        private void HideHeavyAxes()
        {
            StopRoutine(ref weaponRevealRoutine);
            toggleAxe?.HideAxe();
            toggleAxe2?.HideAxe2();
        }

        private void HideAxes()
        {
            StopRoutine(ref weaponRevealRoutine);
            HideAxesImmediately();
        }

        private void HideAxesImmediately()
        {
            toggleAxe?.HideAxe();
            toggleAxe2?.HideAxe2Immediate();
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
            {
                heavyChargeParticles.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            heavyChargeParticles.gameObject.SetActive(false);
        }

        private void OnCharacterStateChanged(CharacterState state)
        {
            if (state == CharacterState.Damaged)
            {
                CancelAttackForInterruption();
                return;
            }

            // The old combat allowed another state (most notably the dash coroutine)
            // to release the Attacking state while the heavy attack was still running.
            // Preserve that as an explicit buffered link instead of competing Invokes.
            if (state == CharacterState.Idle
                && heavyAttackActive
                && queuedLightAfterHeavy)
            {
                StartQueuedLightAfterHeavy();
            }
        }

        private void CancelAttackForInterruption()
        {
            ++attackToken;
            ++heavyAttackToken;
            lightAttackActive = false;
            heavyAttackActive = false;
            heavyChargeInProgress = false;
            comboWindowOpen = false;
            bufferedAttack = false;
            queuedLightAfterHeavy = false;
            hitResolved = false;
            activeStep = null;
            canAttack = true;
            attackComboCount = 0;
            StopRoutine(ref lightAttackWatchdog);
            StopRoutine(ref comboRecoveryRoutine);
            StopRoutine(ref heavyEndRoutine);
            StopRoutine(ref heavyLungeRoutine);
            StopHeavyChargeVfx();
            HideAxes();
            ResetLegacyMovementBoost();
        }

        private bool HasFollowingComboStep()
        {
            return comboDefinition != null
                && attackComboCount >= 0
                && attackComboCount < comboDefinition.StepCount - 1;
        }

        private float GetLegacyComboRecoveryDuration()
        {
            float surgeMultiplier = veilSurgeSkill != null
                ? Mathf.Max(0.05f, veilSurgeSkill.GetAttackSpeedMultiplier())
                : 1f;
            float legacyTotalCooldown = comboAttackCooldown / surgeMultiplier;
            return Mathf.Max(0f, legacyTotalCooldown - lightAttackDuration);
        }

        private void ResetLegacyMovementBoost()
        {
            attackMovementBoost = 30f;
            if (playerController != null)
                playerController.speed = playerController.defaultSpeed;
        }

        private bool CanPassAttackGate()
        {
            if (veilSurgeSkill != null && veilSurgeSkill.ShouldBypassAttackLocking)
                return true;
            return attackGate == null || attackGate.CanStartAttack;
        }

        private float GetEffectivePlaybackSpeed(PlayerAttackStep step)
        {
            float surgeMultiplier = veilSurgeSkill != null
                ? veilSurgeSkill.GetAttackSpeedMultiplier()
                : 1f;
            return Mathf.Clamp(
                step.PlaybackSpeed * surgeMultiplier,
                0.05f,
                Mathf.Max(0.1f, maximumPlaybackSpeed));
        }

        private Vector3 GetAttackDirection()
        {
            Vector3 direction = playerController != null
                ? playerController.GetAttackAimDirection()
                : transform.forward;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
        }

        private void RejectAttack()
        {
            SoundManager.PlaySound(SoundType.ATTACKLOCK, 0.6f);
            OnAttackRejected?.Invoke();
        }

        private void StopRoutine(ref Coroutine routine)
        {
            if (routine == null)
                return;
            StopCoroutine(routine);
            routine = null;
        }
    }
}
