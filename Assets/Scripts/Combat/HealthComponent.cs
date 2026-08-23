using CyberVeil.Audio;
using CyberVeil.Core;
using CyberVeil.VFX;
using UnityEngine;
using System;
using CyberVeil.Player;

namespace CyberVeil.Combat
{
    /// <summary>
    /// Handles health management for any damageable entity, including damage intake,
    /// faction identity, UI event broadcasting and integration with visual/audio feedback and death logic
    /// </summary>
    public class HealthComponent : MonoBehaviour
    {
         public Faction faction; // To determine which team each entity belongs to
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int currentHealth;

        public event Action<HealthComponent> OnHealthChanged; // Event for UI (or other systems) to suscribe to whenever health changes

        // Public readonly to expose private fields
        public float Normalized => GetEffectiveMax() > 0 ? (float)currentHealth / GetEffectiveMax() : 0f; // Returns health as a percentage for use in UI

        private int lastKnownMax; // Track previous max to detect upgrades

        private void Awake()
        {
           lastKnownMax = GetEffectiveMax();
           currentHealth = lastKnownMax; // Sets health to max at start

           // Subscribe to player stat changes if this is the player
           if (faction == Faction.Player)
           {
               var mods = PlayerStatsUpgradeManager.Instance;
               if (mods != null)
                   mods.OnChanged += HandleStatsUpgraded;
           }
        }

        private void OnEnable()
        {
            OnHealthChanged?.Invoke(this); // Broadcast initial state so UI is correct at game start
        }

        private void OnDestroy()
        {
            // Unsubscribe to prevent memory leaks
            if (faction == Faction.Player)
            {
                var mods = PlayerStatsUpgradeManager.Instance;
                if (mods != null)
                    mods.OnChanged -= HandleStatsUpgraded;
            }
        }

        /// <summary>
        /// When player stats are upgraded, scale current health proportionally
        /// </summary>
        private void HandleStatsUpgraded()
        {
            int newMax = GetEffectiveMax();
            if (newMax != lastKnownMax && lastKnownMax > 0)
            {
                // Scale current health proportionally: if you had 50/100, now you have 60/120
                float healthPercent = (float)currentHealth / lastKnownMax;
                currentHealth = Mathf.RoundToInt(newMax * healthPercent);
                lastKnownMax = newMax;
                OnHealthChanged?.Invoke(this);
            }
        }

        /// <summary>
        /// Applies damage to the entity, triggers optional feedback systems,
        /// and destroys the entity if health reaches zero or below
        /// </summary>
        /// <param name="damage">The amount of damage to apply.</param>
        public void TakeDamage(int damage)
        {
            currentHealth = Mathf.Clamp(currentHealth - damage, 0, GetEffectiveMax());

            OnHealthChanged?.Invoke(this); // Broadcast new health state to UI

            /// <summary>
            /// Local feedback systems for effects (only cares about THIS) using null checks to prevent runtime crashes
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

        public void HealPercent(float percent)
        {
            if (percent <= 0f)
                return;

            int amount = Mathf.CeilToInt(GetEffectiveMax() * percent);
            Heal(amount);
        }

        public void Heal(int amount)
        {
            if (amount <= 0)
                return;

            int maxHealthValue = GetEffectiveMax();
            int newHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealthValue);
            if (newHealth == currentHealth)
                return;

            currentHealth = newHealth;
            OnHealthChanged?.Invoke(this);
        }

        private int GetEffectiveMax()
        {
            // Only the PLAYER gets the bonus; others use base maxHealth unchanged
            if (faction != Faction.Player) return maxHealth;

            var mods = PlayerStatsUpgradeManager.Instance;
            float multiplier = mods ? mods.MaxHealthMultiplier : 1f;
            return Mathf.Max(1, Mathf.RoundToInt(maxHealth * multiplier));
        }


        private void Die()
        {
            var enemyAudio = GetComponent<Enemies.EnemyAudio>();
            enemyAudio?.PlayDeath();

            Destroy(gameObject); //simple death logic
        }
    }
}
