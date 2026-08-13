using System;
using CyberVeil.Core;
using UnityEngine;
namespace CyberVeil.Player
{
    public enum AttackChargeChangeReason
    {
        Spent,
        Reset,
        LimitChanged
    }

    /// <summary>
    /// Enforces a cap on consecutive player attacks until a dash occurs (kind of like a stamina mechanic but with a twist)
    /// Implements <see cref="IAttackGate"/> so the combat system can query whether
    /// a new attack may start and notify the gate whenever an attack is performed
    /// So everytime the player does 4 swings they have to dash to unlock attacking, this adds a unique twist and helps balancing
    /// </summary>
    public class AttackLimiterMechanic : MonoBehaviour, IAttackGate
    {
        [SerializeField] private int limit = 4; // Max number of consecutive attacks before lock

        private int defaultLimit;

        private int count = 0;
        private CharacterStateMachine fsm; // Reference to finate state machine used to listen for dash transitions

        // Public read onlys
        public bool CanStartAttack => count < limit;
        public int CurrentCount => count;
        public int Limit => limit;
        public int RemainingCharges => Mathf.Max(0, limit - count);
        public bool IsLocked => count >= limit;

        /// <summary>
        /// Raised whenever the authoritative slash-charge state changes. The arguments are
        /// remaining charges, maximum charges, and the reason for the change.
        /// </summary>
        public event Action<int, int, AttackChargeChangeReason> OnChargesChanged;

        private void Awake()
        {
            defaultLimit = limit;
            fsm = GetComponent<CharacterStateMachine>();
            if (fsm != null)
                fsm.OnStateChange += OnStateChange; // Subscribe OnStateChange method to fsm state change event 
        }
        
        private void OnDestroy()
        {    
            if (fsm != null)
            {
                fsm.OnStateChange -= OnStateChange; // Unsubscribe to avoid memory leaks
            }
        }

        /// <summary>
        /// Handles state transitions and resets the attack counter according to the configured dash reset policy
        /// </summary>
        private void OnStateChange(CharacterState state)
        {
            if (state == CharacterState.Dashing)
            {
                ResetGate();
            }
        }

        // Called by attack system each time an attack actually begins
        public void RecordAttack()
        {
            if (count < limit)
            {
                count++;
                NotifyChargesChanged(AttackChargeChangeReason.Spent);
            }
        }
        
        //Public reset so other systems can clear the counter
        //(in this case the dash logic)
        public void ResetGate()
        {
            count = 0; // Unlocks attacks again
            NotifyChargesChanged(AttackChargeChangeReason.Reset);
        }

        /// <summary>
        /// Allows runtime modification of the attack limit (e.g., curse modifiers)
        /// </summary>
        public void SetLimit(int newLimit)
        {
            limit = Mathf.Max(1, newLimit);
            count = 0;
            NotifyChargesChanged(AttackChargeChangeReason.LimitChanged);
        }

        /// <summary>
        /// Restores the original limit from when this component initialized
        /// </summary>
        public void ResetLimit()
        {
            limit = Mathf.Max(1, defaultLimit);
            count = 0;
            NotifyChargesChanged(AttackChargeChangeReason.LimitChanged);
        }

        private void NotifyChargesChanged(AttackChargeChangeReason reason)
        {
            OnChargesChanged?.Invoke(RemainingCharges, limit, reason);
        }
    }

}
