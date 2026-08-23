using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using CyberVeil.VFX;
using CyberVeil.Core;
using CyberVeil.Systems;

namespace CyberVeil.Player
{
    /// <summary>
    /// Controls player movement and integrates sprinting, dashing, attacking, and state management.
    /// Movement feel improvements:
    /// - Velocity-based acceleration/deceleration (instead of direction-only smoothing)
    /// - Sprint ramp (visual/feel smoothing; does NOT change your sprint script)
    /// - Turn-boost on sharp direction changes (snappier melee movement)
    /// - Ground "stick" to reduce CharacterController floaty bumps
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float defaultSpeed = 2.8f;        // Normal speed
        public float speed;                      // Current speed (for UI/debug/other scripts)
        public bool canMove = true;              // Disable/enable movement
        private Vector2 move;                    // Raw input
        public Vector3 moveDirection;            // Smoothed planar movement velocity (not normalized)
        public Vector3 lastDirection = Vector3.forward; // Last non-zero direction

        [Header("Acceleration Settings")]
        public float acceleration = 18f;
        public float deceleration = 24f;

        [Header("Rotation Feel")]
        [SerializeField] private float baseTurnSpeed = 16f;      // Normal turn speed
        [SerializeField] private float boostedTurnSpeed = 28f;   // Turn speed on sharp direction changes
        [SerializeField] private float turnBoostDotThreshold = 0.2f; // Lower = boosts more often

        [Header("Sprint Feel (does NOT change PlayerSprint)")]
        [SerializeField] private float sprintSpeed = 3.5f;       // Your sprint speed (same as before)
        [SerializeField] private float sprintRampUp = 10f;       // How fast we blend to sprint
        [SerializeField] private float sprintRampDown = 14f;     // How fast we blend back to walk

        [Header("Gravity")]
        public float gravity = -20f;
        [SerializeField] private float groundedStick = -2f;      // Keeps controller grounded
        private Vector3 verticalVelocity;

        [Header("Components")]
        public PlayerParticles dustParticle;
        private Camera mainCamera;
        private CharacterController characterController;
        private PlayerDash playerDash;
        private PlayerSprint playerSprint;
        private PlayerAttack playerAttack;
        private CharacterStateMachine stateMachine;

        // Internal movement state
        private Vector3 planarVelocity = Vector3.zero; // smoothed planar velocity
        private float sprintBlend = 0f;                // 0..1 (for feel only)

        // Uses Unity's input system to store move input for later processing
        public void onMove(InputAction.CallbackContext context)
        {
            move = context.ReadValue<Vector2>();
        }

        private void Start()
        {
            mainCamera = Camera.main;
            characterController = GetComponent<CharacterController>();
            playerDash = GetComponent<PlayerDash>();
            playerSprint = GetComponent<PlayerSprint>();
            playerAttack = GetComponent<PlayerAttack>();
            stateMachine = GetComponent<CharacterStateMachine>();
            speed = defaultSpeed;

            // Initialize lastDirection to match the player's current facing direction
            lastDirection = transform.forward;
        }

        private void Update()
        {
            // If a cinematic camera is active, prevent movement and movement-like inputs
            bool cinematicActive = false;
            if (CinematicCamera.Instance != null)
                cinematicActive = CinematicCamera.Instance.IsActive;

            if (!cinematicActive)
            {
                if (playerDash != null) playerDash.HandleDashInput();
                if (playerSprint != null) playerSprint.HandleSprintInput();
                if (playerAttack != null) playerAttack.HandleAttackInput();

                if (canMove)
                {
                    MovePlayer();
                }

                UpdateMovementState();
            }
            else
            {
                // While cinematic is active force player to idle and hide movement VFX
                if (stateMachine != null)
                    stateMachine.ChangeState(CharacterState.Idle);
            }

            // Dust VFX toggling based on movement
            if (dustParticle != null)
            {
                if (cinematicActive)
                    dustParticle.HideParticle();
                else if (moveDirection.sqrMagnitude > 0.01f)
                    dustParticle.ShowParticle();
                else
                    dustParticle.HideParticle();
            }
        }

        /// <summary>
        /// Camera-relative movement with velocity-based accel/decel, snappy turn boost, sprint ramp feel,
        /// and ground stick. Dash behavior remains the same: when dashing, movement is locked to lastDirection.
        /// </summary>
        private void MovePlayer()
        {
            if (mainCamera == null || characterController == null) return;

            // If attacking, fully lock movement (same as original intent)
            if (stateMachine != null && stateMachine.CurrentState == CharacterState.Attacking)
                return;

            // Camera-relative planar axes
            Vector3 cameraForward = mainCamera.transform.forward;
            Vector3 cameraRight = mainCamera.transform.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();

            // Input dir (planar)
            Vector3 inputDir = cameraForward * move.y + cameraRight * move.x;
            float inputMag = inputDir.magnitude;
            bool hasInput = inputMag > 0.1f;
            if (inputMag > 1f) inputDir.Normalize();

            // Determine target walk speed (includes upgrades, exactly like before)
            var mods = PlayerStatsUpgradeManager.Instance;
            float walkSpeed = mods ? mods.GetMoveSpeed(defaultSpeed) : defaultSpeed;

            // Determine if currently sprinting according to state machine (same logic as before)
            bool isSprintingState = (playerSprint != null && stateMachine != null && stateMachine.CurrentState == CharacterState.Sprinting);

            // Sprint "feel" ramp (does not alter PlayerSprint rules; only smooths our movement speed)
            float rampRate = isSprintingState ? sprintRampUp : sprintRampDown;
            sprintBlend = Mathf.MoveTowards(sprintBlend, isSprintingState ? 1f : 0f, rampRate * Time.deltaTime);

            // Blended speed for feel
            float targetSpeed = Mathf.Lerp(walkSpeed, sprintSpeed, sprintBlend);
            speed = targetSpeed; // keep public speed updated for other scripts/debug

            // Velocity-based accel/decel (makes start/stop feel much better) 
            Vector3 desiredPlanarVel = hasInput ? inputDir.normalized * targetSpeed : Vector3.zero;
            float rate = hasInput ? acceleration : deceleration;
            planarVelocity = Vector3.MoveTowards(planarVelocity, desiredPlanarVel, rate * Time.deltaTime);

            // Expose for VFX/anim logic (now represents planar velocity, not a unit direction)
            moveDirection = planarVelocity;

            // Update lastDirection when have meaningful movement (used for dash + facing)
            if (planarVelocity.sqrMagnitude > 0.001f)
                lastDirection = planarVelocity.normalized;

            // Rotation: snappy turn boost on sharp direction changes 
            // Keep rotation locked when dashing 
            bool isDashing = (playerDash != null && playerDash.IsDashing);

            if (!isDashing && hasInput)
            {
                Vector3 desiredFacing = inputDir.normalized;
                float dot = Vector3.Dot(transform.forward, desiredFacing);

                float turnSpeed = (dot < turnBoostDotThreshold) ? boostedTurnSpeed : baseTurnSpeed;
                Quaternion targetRotation = Quaternion.LookRotation(desiredFacing);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }
            else if (!isDashing && !hasInput && lastDirection.sqrMagnitude > 0.001f)
            {
                // Gently settle facing to lastDirection when stop (good for melee readability)
                Quaternion targetRotation = Quaternion.LookRotation(lastDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, (baseTurnSpeed * 0.6f) * Time.deltaTime);
            }

            // Final movement vector (keep dash behavior the same)
            Vector3 movement = planarVelocity;

            if (isDashing)
            {
                movement = lastDirection * targetSpeed;
            }

            // Gravity / grounding
            if (characterController.isGrounded)
            {
                if (verticalVelocity.y < 0f)
                    verticalVelocity.y = groundedStick;
            }
            else
            {
                verticalVelocity.y += gravity * Time.deltaTime;
            }

            characterController.Move((movement + verticalVelocity) * Time.deltaTime);
        }

        /// <summary>
        /// Keeps animations/state synced with movement
        /// </summary>
        private void UpdateMovementState()
        {
            if (stateMachine == null) return;

            if (stateMachine.CurrentState == CharacterState.Attacking || stateMachine.CurrentState == CharacterState.Damaged)
                return;

            if (move.magnitude > 0.1f)
            {
                if (playerSprint != null && stateMachine.CurrentState == CharacterState.Sprinting)
                    stateMachine.ChangeState(CharacterState.Sprinting);
                else
                    stateMachine.ChangeState(CharacterState.Moving);
            }
            else
            {
                stateMachine.ChangeState(CharacterState.Idle);
            }
        }

        // Locks player movement for attacks/damage
        public void LockMovement(float duration)
        {
            if (canMove)
            {
                canMove = false;
                StartCoroutine(UnlockMovement(duration));
            }
        }

        public IEnumerator UnlockMovement(float duration)
        {
            yield return new WaitForSeconds(duration);
            canMove = true;
        }

        // Getters
        public Vector2 GetMoveInput() { return move; }
        public Vector3 GetLastDirection() { return lastDirection; }
        public CharacterController GetCharacterController() { return characterController; }
    }
}
