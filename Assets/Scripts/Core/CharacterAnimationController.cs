
using UnityEngine;

namespace CyberVeil.Core
{
    /// <summary>
    /// Controls transitions between player animations based on state changes
    /// Listens to the PlayerStateMachine and drives Animator transitions accordingly.
    /// </summary>
    public class CharacterAnimationController : MonoBehaviour
    {
        private Animator animator;
        private CharacterStateMachine stateMachine;

        // Using Animator.StringToHash to avoid expensive string lookups at runtime
        private static readonly int animIDIdle = Animator.StringToHash("Idle");
        private static readonly int animIDMove = Animator.StringToHash("Move");
        private static readonly int animIDSprint = Animator.StringToHash("Sprint");
        private static readonly int animIDAttack = Animator.StringToHash("Attack");
        private static readonly int animIDDamage = Animator.StringToHash("TakeDamage");

        private void Start()
        {
            animator = GetComponent<Animator>();
            stateMachine = GetComponent<CharacterStateMachine>();

            // Subscribe to state change events from the player
            stateMachine.OnStateChange += OnPlayerStateChanged; // WHenever the players state changes, run the method
        }

        /// <summary>
        /// Triggered whenever the player state changes
        /// Crossfades into the appropriate animation
        /// </summary>
        private void OnPlayerStateChanged(CharacterState newState)
        {
            switch (newState)
            {
                case CharacterState.Idle:
                    animator.CrossFade(animIDIdle, 0.2f, 0);
                    break;
                case CharacterState.Moving:
                    animator.CrossFade(animIDMove, 0.2f, 0);
                    break;
                case CharacterState.Dashing:
                    animator.CrossFade(animIDSprint, 0.2f, 0);
                    break;
                case CharacterState.Sprinting:
                    animator.CrossFade(animIDSprint, 0.2f, 0);
                    break;
                case CharacterState.Attacking:
                    animator.CrossFade(animIDAttack, 0.1f, 0);
                    break;
                case CharacterState.Damaged:
                    animator.CrossFade(animIDDamage, 0.1f, 0);
                    break;
            }
        }
    }
}
