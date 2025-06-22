using CyberVeil.Combat;
using CyberVeil.Core;
using CyberVeil.Systems;
using UnityEngine;
namespace CyberVeil.Player
{
    /// <summary>
    /// Handles how the player responds to taking damage
    /// Implements IDamageStateResponder so other systems can trigger standardized damage response behavior
    /// </summary>
    public class PlayerDamageResponder : MonoBehaviour, IDamageStateResponder
    {
        private CharacterStateMachine stateMachine;
        void Start()
        {
            stateMachine = GetComponent<CharacterStateMachine>();
        }

        public void OnDamaged()
        {
            stateMachine.ChangeState(CharacterState.Damaged); // Switch the player's state to Damaged
            HitstopManager.Instance.DoHitstop(0.05f, 0f); // Triggers hitstop effect

        }

        public void OnDamageAnimationFinished()
        {
            stateMachine.ChangeState(CharacterState.Idle);
        }
    }
}
