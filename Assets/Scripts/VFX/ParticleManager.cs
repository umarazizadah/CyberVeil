using CyberVeil.Player;
using UnityEngine;

namespace CyberVeil.VFX
{

    /// <summary>
    /// Manages slash particle effects for each stage of the player's attack combo
    /// Uses a Singleton pattern for global access from other scripts like PlayerAttack
    /// </summary>
    public class ParticleManager : MonoBehaviour
    {
        // Singleton pattern where any other script can call
        public static ParticleManager Instance { get; private set; }

        [Header("Slash VFX References")]
        public SlashAttack slash1;
        public SlashAttack2 slash2;
        public SlashAttack3 slash3;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
            }
            else
            {
                Instance = this;
            }
        }

        /// <summary>
        /// Plays the slash effect based on current combo count
        /// Called by PlayerAttack.cs when a slash lands
        /// </summary>
        public void PlaySlash(int comboCount, Vector3 playerPos, Vector3 forwardDirection)
        {
            switch (comboCount)
            {
                case 0:
                    if (slash1 != null)
                        slash1.PlaySlash(forwardDirection);
                    break;

                case 1:
                    if (slash2 != null)
                        slash2.PlaySlash(forwardDirection);
                    break;

                case 2:
                    if (slash3 != null)
                        slash3.PlaySlash(forwardDirection);
                    break;
            }
        }
    }
}
