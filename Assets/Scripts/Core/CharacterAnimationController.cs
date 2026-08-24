using UnityEngine;
using CyberVeil.Player;

namespace CyberVeil.Core
{
    /// <summary>
    /// Controls transitions between player animations based on state changes
    /// Listens to the PlayerStateMachine and drives Animator transitions accordingly.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterAnimationController : MonoBehaviour
    {
        [Header("Transition Response")]
        [SerializeField, Min(0f)] private float locomotionCrossFade = 0.045f;
        [SerializeField, Min(0f)] private float combatCrossFade = 0.08f;
        [SerializeField, Min(0.01f)] private float startAnimationHold = 0.14f;
        [SerializeField, Min(0.01f)] private float brakeAnimationHold = 0.16f;
        [SerializeField, Min(0.01f)] private float turnAnimationHold = 0.15f;
        [SerializeField, Min(0.01f)] private float turnRetriggerDelay = 0.18f;
        [SerializeField, Range(1f, 180f)] private float turnAnimationThreshold = 28f;

        [Header("Procedural Locomotion Pose")]
        [SerializeField, Min(0f)] private float startLeanDegrees = 11f;
        [SerializeField, Min(0f)] private float brakeLeanDegrees = 13f;
        [SerializeField, Min(0f)] private float cruiseLeanDegrees = 2.5f;
        [SerializeField, Min(0f)] private float turnBankDegrees = 14f;
        [SerializeField, Min(0f)] private float turnTwistDegrees = 7f;
        [SerializeField, Min(0.01f)] private float poseResponse = 22f;
        [SerializeField, Min(0.01f)] private float poseRecovery = 16f;

        private Animator animator;
        private CharacterStateMachine stateMachine;
        private PlayerController playerController;
        private PlayerAttack playerAttack;
        private Transform spine1;
        private Transform spine2;
        private Transform spine3;

        private int activeStateHash;
        private float transientStateUntil;
        private float nextTurnAllowed;
        private float startPoseStrength;
        private float brakePoseStrength;
        private float smoothedTurn;
        private float smoothedForwardLean;
        private float smoothedBank;
        private float smoothedTwist;
        private bool wasInputMoving;
        private bool hasAttackSpeedParameter;

        // Using Animator.StringToHash to avoid expensive string lookups at runtime
        private static readonly int animIDIdle = Animator.StringToHash("Idle");
        private static readonly int animIDMove = Animator.StringToHash("Move");
        private static readonly int animIDSprint = Animator.StringToHash("Sprint");
        private static readonly int animIDAttack = Animator.StringToHash("Attack");
        private static readonly int animIDDamage = Animator.StringToHash("TakeDamage");
        private static readonly int animIDStrafe = Animator.StringToHash("Strafe");
        private static readonly int animIDStart = Animator.StringToHash("LocomotionStart");
        private static readonly int animIDBrake = Animator.StringToHash("LocomotionBrake");
        private static readonly int animIDTurnLeft = Animator.StringToHash("TurnLeft");
        private static readonly int animIDTurnRight = Animator.StringToHash("TurnRight");
        private static readonly int animIDAttackSpeed = Animator.StringToHash("AttackSpeed");

        private void Awake()
        {
            animator = GetComponent<Animator>();
            stateMachine = GetComponent<CharacterStateMachine>();
            playerController = GetComponent<PlayerController>();
            playerAttack = GetComponent<PlayerAttack>();
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == animIDAttackSpeed)
                {
                    hasAttackSpeedParameter = true;
                    break;
                }
            }

            spine1 = transform.Find("Armature/Spine1");
            spine2 = transform.Find("Armature/Spine1/Spine2");
            spine3 = transform.Find("Armature/Spine1/Spine2/Spine3");
        }

        private void OnEnable()
        {
            if (stateMachine != null)
                stateMachine.OnStateChange += OnPlayerStateChanged;
            if (playerAttack != null)
                playerAttack.OnAttackStepStarted += OnAttackStepStarted;
        }

        private void Start()
        {
            if (stateMachine != null)
                OnPlayerStateChanged(stateMachine.CurrentState);
        }

        private void OnDisable()
        {
            if (stateMachine != null)
                stateMachine.OnStateChange -= OnPlayerStateChanged;
            if (playerAttack != null)
                playerAttack.OnAttackStepStarted -= OnAttackStepStarted;
        }

        private void Update()
        {
            if (animator == null || playerController == null || stateMachine == null)
                return;

            // Combat and dash movement were allowed to overlap in the original
            // controller. Keep the attack clip authoritative even if the dash
            // coroutine temporarily reports Dashing or Idle underneath it.
            if (playerAttack != null && playerAttack.IsAnyAttackActive)
            {
                wasInputMoving = false;
                return;
            }

            CharacterState state = stateMachine.CurrentState;
            if (IsCombatState(state))
            {
                wasInputMoving = false;
                return;
            }

            if (state == CharacterState.Dashing)
            {
                PlayState(animIDSprint, locomotionCrossFade);
                wasInputMoving = playerController.HasMoveInput;
                return;
            }

            bool inputMoving = playerController.HasMoveInput;
            bool justStarted = inputMoving && !wasInputMoving;
            bool justStopped = !inputMoving && wasInputMoving;
            float now = Time.time;

            if (justStarted)
            {
                startPoseStrength = 1f;
                brakePoseStrength = 0f;
                transientStateUntil = now + startAnimationHold;
                PlayState(animIDStart, locomotionCrossFade, animIDMove);
            }
            else if (justStopped)
            {
                brakePoseStrength = Mathf.Clamp01(
                    playerController.CurrentPlanarSpeed / Mathf.Max(playerController.speed, 0.01f));
                startPoseStrength = 0f;
                transientStateUntil = now + brakeAnimationHold;
                PlayState(animIDBrake, locomotionCrossFade, animIDIdle);
            }
            else if (inputMoving
                     && now >= nextTurnAllowed
                     && Mathf.Abs(playerController.SignedTurnAngle) >= turnAnimationThreshold)
            {
                bool turnRight = playerController.SignedTurnAngle > 0f;
                transientStateUntil = now + turnAnimationHold;
                nextTurnAllowed = now + turnRetriggerDelay;
                PlayState(turnRight ? animIDTurnRight : animIDTurnLeft,
                    locomotionCrossFade,
                    animIDMove);
            }
            else if (now >= transientStateUntil)
            {
                if (!inputMoving && playerController.CurrentPlanarSpeed <= 0.08f)
                    PlayState(animIDIdle, locomotionCrossFade);
                else if (state == CharacterState.Sprinting)
                    PlayState(animIDSprint, locomotionCrossFade);
                else
                    PlayState(animIDMove, locomotionCrossFade);
            }

            wasInputMoving = inputMoving;
        }

        /// <summary>
        /// Triggered whenever the player state changes
        /// Crossfades into the appropriate animation
        /// </summary>
        private void OnPlayerStateChanged(CharacterState newState)
        {
            if (newState != CharacterState.Attacking
                && newState != CharacterState.Damaged
                && playerAttack != null
                && playerAttack.IsAnyAttackActive)
            {
                return;
            }

            if (newState != CharacterState.Attacking)
                SetAttackSpeed(1f);

            switch (newState)
            {
                case CharacterState.Dashing:
                    PlayState(animIDSprint, locomotionCrossFade);
                    break;
                case CharacterState.Attacking:
                    ClearLocomotionImpulse();
                    PlayState(animIDAttack, combatCrossFade);
                    break;
                case CharacterState.Damaged:
                    ClearLocomotionImpulse();
                    PlayState(animIDDamage, combatCrossFade);
                    break;
                case CharacterState.Strafing:
                    PlayState(animIDStrafe, locomotionCrossFade, animIDMove);
                    break;
            }
        }

        private void OnAttackStepStarted(PlayerAttackStep step, float playbackSpeed)
        {
            if (step == null)
                return;

            ClearLocomotionImpulse();
            SetAttackSpeed(playbackSpeed);
            PlayState(
                step.AnimatorStateHash,
                step.CrossFadeTime,
                animIDAttack,
                true);
        }

        private void LateUpdate()
        {
            if (playerController == null || stateMachine == null || spine1 == null)
                return;

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            bool allowPose = (playerAttack == null || !playerAttack.IsAnyAttackActive)
                && !IsCombatState(stateMachine.CurrentState)
                && stateMachine.CurrentState != CharacterState.Dashing;

            startPoseStrength = Mathf.MoveTowards(
                startPoseStrength,
                0f,
                deltaTime / Mathf.Max(startAnimationHold, 0.01f));
            brakePoseStrength = Mathf.MoveTowards(
                brakePoseStrength,
                0f,
                deltaTime / Mathf.Max(brakeAnimationHold, 0.01f));

            float turnTarget = allowPose && playerController.HasMoveInput
                ? Mathf.Clamp(playerController.SignedTurnAngle / 90f, -1f, 1f)
                : 0f;
            float turnBlend = ExponentialBlend(allowPose ? poseResponse : poseRecovery, deltaTime);
            smoothedTurn = Mathf.Lerp(smoothedTurn, turnTarget, turnBlend);

            float normalizedSpeed = allowPose ? playerController.NormalizedPlanarSpeed : 0f;
            float forwardTarget = allowPose
                ? startPoseStrength * startLeanDegrees
                    - brakePoseStrength * brakeLeanDegrees
                    + normalizedSpeed * cruiseLeanDegrees
                : 0f;
            float bankTarget = allowPose ? -smoothedTurn * turnBankDegrees : 0f;
            float twistTarget = allowPose ? smoothedTurn * turnTwistDegrees : 0f;
            float poseBlend = ExponentialBlend(allowPose ? poseResponse : poseRecovery, deltaTime);

            smoothedForwardLean = Mathf.Lerp(smoothedForwardLean, forwardTarget, poseBlend);
            smoothedBank = Mathf.Lerp(smoothedBank, bankTarget, poseBlend);
            smoothedTwist = Mathf.Lerp(smoothedTwist, twistTarget, poseBlend);

            ApplyWorldPoseOffset(spine1, 0.52f);
            ApplyWorldPoseOffset(spine2, 0.31f);
            ApplyWorldPoseOffset(spine3, 0.17f);
        }

        private void PlayState(
            int stateHash,
            float transition,
            int fallbackHash = 0,
            bool forceRestart = false)
        {
            if (animator == null || (!forceRestart && activeStateHash == stateHash))
                return;

            int resolvedHash = animator.HasState(0, stateHash)
                ? stateHash
                : fallbackHash;
            if (resolvedHash == 0 || !animator.HasState(0, resolvedHash))
                return;

            animator.CrossFadeInFixedTime(resolvedHash, transition, 0, 0f);
            activeStateHash = resolvedHash;
        }

        private void SetAttackSpeed(float speed)
        {
            if (animator != null && hasAttackSpeedParameter)
                animator.SetFloat(animIDAttackSpeed, Mathf.Max(0.05f, speed));
        }

        private void ApplyWorldPoseOffset(Transform bone, float weight)
        {
            if (bone == null)
                return;

            Quaternion offset = Quaternion.AngleAxis(
                    smoothedForwardLean * weight,
                    transform.right)
                * Quaternion.AngleAxis(
                    smoothedBank * weight,
                    transform.forward)
                * Quaternion.AngleAxis(
                    smoothedTwist * weight,
                    transform.up);
            bone.rotation = offset * bone.rotation;
        }

        private void ClearLocomotionImpulse()
        {
            startPoseStrength = 0f;
            brakePoseStrength = 0f;
            transientStateUntil = 0f;
            smoothedTurn = 0f;
            smoothedForwardLean = 0f;
            smoothedBank = 0f;
            smoothedTwist = 0f;
            wasInputMoving = false;
        }

        private static bool IsCombatState(CharacterState state)
        {
            return state == CharacterState.Attacking || state == CharacterState.Damaged;
        }

        private static float ExponentialBlend(float sharpness, float deltaTime)
        {
            return 1f - Mathf.Exp(-sharpness * deltaTime);
        }
    }
}
