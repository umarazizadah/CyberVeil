using CyberVeil.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CyberVeil.Player
{
    [RequireComponent(typeof(CharacterStateMachine))]
    /// <summary>
    /// Handles sprint input, sprint timing, and switching player states during a sprint
    /// </summary>
    public class PlayerSprint : MonoBehaviour
    {
        [Header("Sprint Settings")]
        [SerializeField] private float sprintDuration = 2f;

        private float sprintTimeRemaining;
        private CharacterStateMachine playerState;

        private void Start()
        {
            playerState = GetComponent<CharacterStateMachine>();
        }

        private void Update()
        {
            UpdateSprint();
        }

        public void HandleSprintInput()
        {
            // Prevents overlapping conflicting scritps
            if (playerState.CurrentState != CharacterState.Attacking
                && playerState.CurrentState != CharacterState.Sprinting
                && Keyboard.current != null
                && Keyboard.current.leftShiftKey.wasPressedThisFrame)
            {
                StartSprint();
            }
        }

        private void StartSprint()
        {
            playerState.ChangeState(CharacterState.Sprinting);
            sprintTimeRemaining = sprintDuration;
        }

        private void UpdateSprint()
        {
            if (playerState.CurrentState == CharacterState.Sprinting)
            {
                sprintTimeRemaining -= Time.deltaTime;
                if (sprintTimeRemaining <= 0)
                {
                    playerState.ChangeState(CharacterState.Idle);
                }
            }
        }
    }
}
