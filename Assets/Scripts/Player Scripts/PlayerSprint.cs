using UnityEngine;
using UnityEngine.InputSystem;

namespace CyberVeil.Player
{
    [RequireComponent(typeof(PlayerStateMachine))]
    /// <summary>
    /// Handles sprint input, sprint timing, and switching player states during a sprint
    /// </summary>
    public class PlayerSprint : MonoBehaviour
    {
        [Header("Sprint Settings")]
        [SerializeField] private float sprintDuration = 2f;

        private float sprintTimeRemaining;
        private PlayerStateMachine playerState;

        private void Start()
        {
            playerState = GetComponent<PlayerStateMachine>();
        }

        private void Update()
        {
            UpdateSprint();
        }

        public void HandleSprintInput()
        {
            // Prevents overlapping conflicting scritps
            if (playerState.CurrentState != PlayerState.Attacking
                && playerState.CurrentState != PlayerState.Sprinting
                && Keyboard.current != null
                && Keyboard.current.leftShiftKey.wasPressedThisFrame)
            {
                StartSprint();
            }
        }

        private void StartSprint()
        {
            playerState.ChangeState(PlayerState.Sprinting);
            sprintTimeRemaining = sprintDuration;
        }

        private void UpdateSprint()
        {
            if (playerState.CurrentState == PlayerState.Sprinting)
            {
                sprintTimeRemaining -= Time.deltaTime;
                if (sprintTimeRemaining <= 0)
                {
                    playerState.ChangeState(PlayerState.Idle);
                }
            }
        }
    }
}
