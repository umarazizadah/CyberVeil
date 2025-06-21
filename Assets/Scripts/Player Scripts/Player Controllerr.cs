using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using CyberVeil.VFX;


namespace CyberVeil.Player
{
    /// <summary>
    /// Controls player movement and integrates sprinting, dashing, attacking, and state management
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float defaultSpeed = 2.8f; // Normal speed
        public float speed; // Current speed 
        private float rotationSpeed = 5f; // Speed for rotating towards movement direction
        public bool canMove = true; // Flag to disable/ enable movement (used when in attack state)
        private Vector2 move; // Raw input value 
        public Vector3 moveDirection; // Current smoothed movement direction
        public Vector3 lastDirection = Vector3.forward; // Stores last direction to rotate even if no input

        [Header("Acceleration Settings")]
        // Makes character feel heavier, more physical (accelerates and decelerates smoothly)
        public float acceleration = 12f;
        public float deceleration = 14f;
        private Vector3 currentVelocity = Vector3.zero;

        public float gravity = -20f; // Downward gravity force
        private Vector3 velocity; // Gravity velocity

        [Header("Components")]
        public dustParticle dustParticle;
        private Camera mainCamera; // Cache main camera for movement direction calculations
        private CharacterController characterController; // Built in physics movement
        private PlayerDash playerDash;
        private PlayerSprint playerSprint;
        private PlayerAttack playerAttack;
        private PlayerStateMachine stateMachine;

        // Uses unitys input handling system to handle movement input, stores input vector for processing later
        public void onMove(InputAction.CallbackContext context)
        {
            move = context.ReadValue<Vector2>();
        }

        private void Start()
        {
            // Grabs all requires components at start
            mainCamera = Camera.main;
            characterController = GetComponent<CharacterController>();
            playerDash = GetComponent<PlayerDash>();
            playerSprint = GetComponent<PlayerSprint>();
            playerAttack = GetComponent<PlayerAttack>();
            stateMachine = GetComponent<PlayerStateMachine>();
            speed = defaultSpeed;
        }

        void Update()
        {
            // First processes dash/sprint/attack inputs
            if (playerDash != null) playerDash.HandleDashInput();
            if (playerSprint != null) playerSprint.HandleSprintInput();
            if (playerAttack != null) playerAttack.HandleAttackInput();

            // Then handles movement if allowed
            if (canMove)
            {
                MovePlayer();
            }

            UpdateMovementState(); // Updates state machine

            if (dustParticle != null) // Handles dust particle visibility based on movement
            {
                if (moveDirection.sqrMagnitude > 0.01f)
                    dustParticle.ShowDustParticle();
                else
                    dustParticle.HideDustParticle();
            }
        }

        /// <summary>
        /// Translates 2D input into camera-relative 3D movement and rotates the player.
        /// </summary>
        private void MovePlayer()
        {
            if (mainCamera == null) return;

            // Calculates forward/right vectors based on camera
            Vector3 cameraForward = mainCamera.transform.forward;
            Vector3 cameraRight = mainCamera.transform.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();


            // Convert 2D input into 3D direction relative to camera
            Vector3 targetInputDirection = cameraForward * move.y + cameraRight * move.x;
            if (targetInputDirection.magnitude > 1f)
                targetInputDirection.Normalize();

            // Applies acceleration + deceleration for smoother start/stop
            if (targetInputDirection.magnitude > 0.1f)
            {
                currentVelocity = Vector3.MoveTowards(currentVelocity, targetInputDirection, acceleration * Time.deltaTime);
                lastDirection = currentVelocity.normalized;
            }
            else
            {
                currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, deceleration * Time.deltaTime);
            }

            moveDirection = currentVelocity;

            // Rotates player smoothly toward last movement direction and keeps rotation locked when dashing
            if (moveDirection.sqrMagnitude > 0.01f && (playerDash == null || !playerDash.IsDashing))
            {
                Quaternion targetRotation = Quaternion.LookRotation(lastDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            if (stateMachine.CurrentState == PlayerState.Attacking) // Fully locks movement during attacks
                return;


            // Handles movement vectors depending on state:
            Vector3 movement = Vector3.zero;

            if (playerDash != null && playerDash.IsDashing) // Dash - move in last direction
            {
                movement = lastDirection;
            }
            else if (playerSprint != null && stateMachine.CurrentState == PlayerState.Sprinting) // Sprint - faster speed
            {
                speed = 5.5f;
                movement = moveDirection * speed;
            }
            else // Walk - normal speed.
            {
                speed = defaultSpeed;
                movement = moveDirection * speed;
            }


            // Applies manual gravity, keeps player grounded using CharacterController
            if (!characterController.isGrounded)
            {
                velocity.y += gravity * Time.deltaTime;
            }
            else
            {
                velocity.y = -1f;
            }
            characterController.Move((movement + velocity) * Time.deltaTime);
        }

        private void UpdateMovementState() //keeps annimations fully synced with players movement
        {
            if (stateMachine.CurrentState == PlayerState.Attacking || stateMachine.CurrentState == PlayerState.Damaged)
                return;

            if (move.magnitude > 0.1f)
            {
                if (playerSprint != null && stateMachine.CurrentState == PlayerState.Sprinting)
                    stateMachine.ChangeState(PlayerState.Sprinting);
                else
                    stateMachine.ChangeState(PlayerState.Moving);
            }
            else
            {
                stateMachine.ChangeState(PlayerState.Idle);
            }
        }

        // Locks player movement for when in attack state or damaged state
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

        // Clean getter functions to access private movement values from other scripts
        public Vector2 GetMoveInput() { return move; }
        public Vector3 GetLastDirection() { return lastDirection; }
        public CharacterController GetCharacterController() { return characterController; }
    }
}
