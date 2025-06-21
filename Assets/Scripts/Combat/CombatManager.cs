using UnityEngine;
using CyberVeil.Core;

namespace CyberVeil.Combat
{
    /// <summary>
    /// Applies damage to all valid targets in a given radius, ignoring the attacker and filtering by faction
    /// </summary>
    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance { get; private set; }

        private void Awake() // Singleton pattern so exactly one CombatManager is accessible gloably
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject); // Prevents duplication if theres already a copy
            }
            else
            {
                Instance = this;
            }
        }

        // Applies damage in a circular area
        public void DealDamageInRadius(Vector3 position, float radius, int damage, GameObject attacker = null)
        {
            Collider[] hits = Physics.OverlapSphere(position, radius); // Gets all colliders within the given radius

            Faction attackerFaction = Faction.Neutral; // Default

            if (attacker != null) // If attacker is specified, tries to get their Faction, prevents friendly fire by comparing factions later
            {
                HealthComponent attackerHealth = attacker.GetComponent<HealthComponent>();
                if (attackerHealth != null)
                    attackerFaction = attackerHealth.faction;
            }

            foreach (var hit in hits)
            {
                // Skips damaging the attacker (self-damage protection)
                if (attacker != null && hit.gameObject == attacker)
                    continue;

                // Checks if the hit object has a health component
                HealthComponent targetHealth = hit.GetComponent<HealthComponent>();
                if (targetHealth != null)
                {
                    // Applies damage only if the target is from a different faction 
                    if (targetHealth.faction != attackerFaction)
                    {
                        targetHealth.TakeDamage(damage);
                    }
                }
            }
        }
    }
}
