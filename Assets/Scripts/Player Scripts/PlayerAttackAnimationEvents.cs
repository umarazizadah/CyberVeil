using UnityEngine;

namespace CyberVeil.Player
{
    /// <summary>
    /// Small relay used by all light-attack clips. Animation assets communicate
    /// timing only; PlayerAttack remains the authority for combat and combo state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerAttack))]
    public sealed class PlayerAttackAnimationEvents : MonoBehaviour
    {
        private PlayerAttack playerAttack;

        private void Awake()
        {
            playerAttack = GetComponent<PlayerAttack>();
        }

        public void Hit()
        {
            playerAttack?.OnAnimationHit();
        }

        public void OpenComboWindow()
        {
            playerAttack?.OpenComboWindow();
        }

        public void CloseComboWindow()
        {
            playerAttack?.CloseComboWindow();
        }

        public void FinishAttack()
        {
            playerAttack?.FinishLightAttack();
        }
    }
}
