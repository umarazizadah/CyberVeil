using UnityEngine;

namespace CyberVeil.Player
{
    // Using enum to force strict state logic and prevent logic bugs (compared to using strings or ints)
    public enum PlayerState { Idle, Moving, Attacking, Dashing, Damaged, Sprinting }

    /// <summary>
    /// Controls and tracks the player's current state using a finite state machine 
    /// Provides an event system for notifying subscribers of state changes
    /// </summary>

    public class PlayerStateMachine : MonoBehaviour
    {
        // Prevents random scripts from changing the state directly, encapsulation
        [SerializeField] public PlayerState CurrentState { get; private set; } = PlayerState.Idle;

        /// <summary>
        /// Delegate definition for functions that respond to player state changes.
        /// </summary>
        
        public delegate void OnStateChangeDelegate(PlayerState newState);
        /// <summary>
        /// Event triggered whenever the player's state changes
        /// Other systems (animation, audio, VFX) can subscribe to this
        /// </summary>
        public event OnStateChangeDelegate OnStateChange;

        public void ChangeState(PlayerState newState)
        {
            if (CurrentState == newState) return; // Avoids unnecessary logic if the state hasn’t actually changed

            CurrentState = newState; // Updates the current state
            OnStateChange?.Invoke(CurrentState); // Calls all the subscribers callbacks and passes the new state
        }
    }
}
