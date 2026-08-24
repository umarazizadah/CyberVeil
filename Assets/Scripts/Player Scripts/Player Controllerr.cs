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
    /// Precision-first, camera-relative locomotion for responsive action gameplay.
    /// Movement direction follows input immediately while speed uses short,
    /// time-based acceleration and braking for polish without sluggish steering.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float defaultSpeed = 3.6f;        // Normal speed
        public float speed;                      // Current speed (for UI/debug/other scripts)
        public bool canMove = true;              // Disable/enable movement
        private Vector2 move;                    // Raw input
        public Vector3 moveDirection;            // Current planar movement velocity
        public Vector3 lastDirection = Vector3.forward; // Last non-zero direction

        [Header("Precision Movement Feel")]
        [SerializeField, Range(0f, 0.4f)] private float inputDeadZone = 0.12f;
        [SerializeField, Min(0.01f)] private float accelerationTime = 0.07f;
        [SerializeField, Min(0.01f)] private float decelerationTime = 0.05f;
        [SerializeField, Range(0f, 1f)] private float startSpeedFraction = 0.32f;
        [SerializeField, Min(0.01f)] private float movingStateSpeedThreshold = 0.08f;

        [Header("Rotation Feel")]
        [SerializeField, Min(0f)] private float turnSpeed = 1080f;
        [SerializeField, Min(0f)] private float sharpTurnSpeed = 1800f;
        [SerializeField, Range(-1f, 1f)] private float sharpTurnDotThreshold = 0.25f;

        [Header("Sprint Feel (does NOT change PlayerSprint)")]
        [SerializeField] private float sprintSpeed = 4.5f;
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

        // Internal movement state. Direction and speed are intentionally tracked
        // separately so a direction change never creates an unwanted wide turn.
        private Vector3 planarVelocity = Vector3.zero;
        private Vector3 movementHeading = Vector3.forward;
        private float currentPlanarSpeed;
        private float sprintBlend;
        private bool hadMoveInput;

        /// <summary>Current code-driven planar speed used by animation and feedback systems.</summary>
        public float CurrentPlanarSpeed => currentPlanarSpeed;

        /// <summary>Planar speed normalized against the currently selected movement speed.</summary>
        public float NormalizedPlanarSpeed => speed > 0.001f
            ? Mathf.Clamp01(currentPlanarSpeed / speed)
            : 0f;

        /// <summary>True while movement input is outside the configured dead zone.</summary>
        public bool HasMoveInput => hadMoveInput;

        /// <summary>
        /// Signed angle remaining between facing and desired movement heading.
        /// Positive values turn right; negative values turn left.
        /// </summary>
        public float SignedTurnAngle { get; private set; }

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
            movementHeading = lastDirection;
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
                ResetPlanarMotion();
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
        /// Camera-relative movement with immediate steering authority, short
        /// time-based speed response, analog input support, and deterministic turning.
        /// </summary>
        private void MovePlayer()
        {
            if (mainCamera == null || characterController == null) return;

            // Camera-relative planar axes
            Vector3 cameraForward = mainCamera.transform.forward;
            Vector3 cameraRight = mainCamera.transform.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();

            float rawInputMagnitude = Mathf.Clamp01(move.magnitude);
            float inputMagnitude = RemapInputMagnitude(rawInputMagnitude);
            bool hasInput = inputMagnitude > 0f;

            Vector3 inputDirection = cameraForward * move.y + cameraRight * move.x;
            if (hasInput && inputDirection.sqrMagnitude > 0.0001f)
                inputDirection.Normalize();

            // Authored attacks still lock planar movement, but aiming must remain
            // responsive between combo steps. The old input-driven sequence briefly
            // returned to locomotion between slashes, which refreshed this direction.
            // Buffered clips no longer have that gap, so update facing explicitly.
            if (stateMachine != null && stateMachine.CurrentState == CharacterState.Attacking)
            {
                UpdateFacing(inputDirection, hasInput, false);
                if (hasInput)
                {
                    movementHeading = inputDirection;
                    lastDirection = inputDirection;
                }
                return;
            }

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

            float desiredSpeed = hasInput ? targetSpeed * inputMagnitude : 0f;

            // Give a new input a modest initial response, then finish accelerating
            // smoothly. This removes the mushy first frames without teleporting.
            if (hasInput && !hadMoveInput)
                currentPlanarSpeed = Mathf.Max(currentPlanarSpeed, desiredSpeed * startSpeedFraction);

            float responseTime = hasInput ? accelerationTime : decelerationTime;
            float speedChangeRate = Mathf.Max(targetSpeed, 0.01f) / responseTime;
            currentPlanarSpeed = Mathf.MoveTowards(
                currentPlanarSpeed,
                desiredSpeed,
                speedChangeRate * Time.deltaTime);

            // Steering is intentionally immediate. Momentum changes magnitude, not
            // heading, so 90/180-degree corrections stay precise.
            if (hasInput)
            {
                movementHeading = inputDirection;
                lastDirection = inputDirection;
            }

            planarVelocity = movementHeading * currentPlanarSpeed;
            if (!hasInput && currentPlanarSpeed <= 0.001f)
                planarVelocity = Vector3.zero;

            moveDirection = planarVelocity;
            hadMoveInput = hasInput;

            // Rotation: snappy turn boost on sharp direction changes 
            // Keep rotation locked when dashing 
            bool isDashing = (playerDash != null && playerDash.IsDashing);

            UpdateFacing(inputDirection, hasInput, isDashing);

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

        private void UpdateFacing(Vector3 inputDirection, bool hasInput, bool rotationLocked)
        {
            if (!rotationLocked && hasInput)
            {
                SignedTurnAngle = Vector3.SignedAngle(
                    transform.forward,
                    inputDirection,
                    Vector3.up);
                float facingDot = Vector3.Dot(transform.forward, inputDirection);
                float degreesPerSecond = facingDot < sharpTurnDotThreshold
                    ? sharpTurnSpeed
                    : turnSpeed;
                Quaternion targetRotation = Quaternion.LookRotation(inputDirection);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    degreesPerSecond * Time.deltaTime);
                return;
            }

            SignedTurnAngle = Mathf.MoveTowards(
                SignedTurnAngle,
                0f,
                sharpTurnSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Keeps animations/state synced with movement
        /// </summary>
        private void UpdateMovementState()
        {
            if (stateMachine == null) return;

            if (stateMachine.CurrentState == CharacterState.Attacking || stateMachine.CurrentState == CharacterState.Damaged)
                return;

            bool isMoving = moveDirection.sqrMagnitude
                > movingStateSpeedThreshold * movingStateSpeedThreshold;

            if (isMoving)
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

        private float RemapInputMagnitude(float rawMagnitude)
        {
            if (rawMagnitude <= inputDeadZone)
                return 0f;

            return Mathf.InverseLerp(inputDeadZone, 1f, rawMagnitude);
        }

        private void ResetPlanarMotion()
        {
            currentPlanarSpeed = 0f;
            planarVelocity = Vector3.zero;
            moveDirection = Vector3.zero;
            hadMoveInput = false;
            SignedTurnAngle = 0f;
        }

        // Getters
        public Vector2 GetMoveInput() { return move; }
        public Vector3 GetLastDirection() { return lastDirection; }
        public Vector3 GetAttackAimDirection()
        {
            if (mainCamera == null || RemapInputMagnitude(Mathf.Clamp01(move.magnitude)) <= 0f)
                return lastDirection;

            Vector3 cameraForward = mainCamera.transform.forward;
            Vector3 cameraRight = mainCamera.transform.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();

            Vector3 inputDirection = cameraForward * move.y + cameraRight * move.x;
            return inputDirection.sqrMagnitude > 0.0001f
                ? inputDirection.normalized
                : lastDirection;
        }
        public CharacterController GetCharacterController() { return characterController; }
    }
}
