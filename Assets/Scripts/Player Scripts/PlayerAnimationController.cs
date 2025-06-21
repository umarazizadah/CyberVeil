using UnityEngine;

namespace CyberVeil.Player
{
    /// <summary>
    /// Controls transitions between player animations based on state changes
    /// Listens to the PlayerStateMachine and drives Animator transitions accordingly.
    /// </summary>
    public class PlayerAnimationController : MonoBehaviour
    {
        private Animator animator;
        private PlayerStateMachine stateMachine;

        // Using Animator.StringToHash to avoid expensive string lookups at runtime
        private static readonly int animIDIdle = Animator.StringToHash("Idle");
        private static readonly int animIDMove = Animator.StringToHash("Move");
        private static readonly int animIDSprint = Animator.StringToHash("Sprint");
        private static readonly int animIDAttack = Animator.StringToHash("Attack");
        private static readonly int animIDDamage = Animator.StringToHash("TakeDamage");

        private void Start()
        {
            animator = GetComponent<Animator>();
            stateMachine = GetComponent<PlayerStateMachine>();

            // Subscribe to state change events from the player
            stateMachine.OnStateChange += OnPlayerStateChanged; // WHenever the players state changes, run the method
        }

        /// <summary>
        /// Triggered whenever the player state changes
        /// Crossfades into the appropriate animation
        /// </summary>
        private void OnPlayerStateChanged(PlayerState newState)
        {
            switch (newState)
            {
                case PlayerState.Idle:
                    animator.CrossFade(animIDIdle, 0.2f, 0);
                    break;
                case PlayerState.Moving:
                    animator.CrossFade(animIDMove, 0.2f, 0);
                    break;
                case PlayerState.Dashing:
                    animator.CrossFade(animIDSprint, 0.2f, 0);
                    break;
                case PlayerState.Sprinting:
                    animator.CrossFade(animIDSprint, 0.2f, 0);
                    break;
                case PlayerState.Attacking:
                    animator.CrossFade(animIDAttack, 0.1f, 0);
                    break;
                case PlayerState.Damaged:
                    animator.CrossFade(animIDDamage, 0.1f, 0);
                    break;
            }
        }
    }
}
