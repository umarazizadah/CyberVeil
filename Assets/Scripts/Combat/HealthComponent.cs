using CyberVeil.Audio;
using CyberVeil.Core;
using CyberVeil.VFX;
using UnityEngine;

namespace CyberVeil.Combat
{
    /// <summary>
    /// Handles health management for any damageable entity, including damage intake,
    /// faction identity, and integration with visual/audio feedback and death logic
    /// </summary>
    public class HealthComponent : MonoBehaviour
    {
         public Faction faction; // To determine which team each entity belongs to
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int currentHealth;

        private void Start()
        {
            currentHealth = maxHealth; // Sets health to max at start
        }

        /// <summary>
        /// Applies damage to the entity, triggers optional feedback systems,
        /// and destroys the entity if health reaches zero or below
        /// </summary>
        /// <param name="damage">The amount of damage to apply.</param>
        public void TakeDamage(int damage)
        {
            currentHealth -= damage;

            /// <summary>
            /// Using null checks to prevent runtime crashes
            /// </summary>
            IDamageVisual damageVisual = GetComponent<IDamageVisual>();
            damageVisual?.PlayDamageEffect();

            IDamageSound damageSound = GetComponent<IDamageSound>();
            damageSound?.PlayDamageSound();

            IDamageStateResponder damageStateResponder = GetComponent<IDamageStateResponder>();
            damageStateResponder?.OnDamaged();

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            Destroy(gameObject); //simple death logic
        }
    }
}
