using UnityEngine;
using System.Collections.Generic;
using CyberVeil.Systems;

namespace CyberVeil.Enemies
{
    /// <summary>
    /// Enables enemies to support multiple attack types by handling selection logic
    /// Selects and manages enemy attacks based on distance and cooldown
    /// Tracks cooldowns externally to ensure attack timing is consistent,
    /// even when attacks are defined as reusable prefabs via ScriptableObjects

    /// </summary>
    public class EnemyAttackSelector : MonoBehaviour
    {
        [Tooltip("List of available attacks this enemy can perform")]
        [SerializeField] private List<EnemyAttackData> attacks;
        private float[] lastUsedTimestamps; // Stores the last-used timestamp per attack
        private Transform player;

        private EnemyAttackData selectedAttack; // Caches the most recently selected attack for execution and cooldown tracking

        /// <summary>
        /// Initializes the cooldown tracking array and caches the player reference
        /// This ensures that cooldown state persists across multiple attack prefab executions
        /// </summary>
        private void Awake()
        {
            player = PlayerReference.PlayerTransform;
            if (attacks == null)
                attacks = new List<EnemyAttackData>();
            lastUsedTimestamps = new float[attacks.Count];
            for (int i = 0; i < attacks.Count; i++)
            {
                lastUsedTimestamps[i] = -Mathf.Infinity; // Ensures all attacks are initially ready

                EnemyAttackData attack = attacks[i];
                if (attack == null || attack.attackPrefab == null)
                    continue;

                EnemyProjectileAttack projectileAttack = attack.attackPrefab.GetComponent<EnemyProjectileAttack>();
                if (projectileAttack != null)
                    projectileAttack.PrewarmProjectiles();
            }
        }

        /// <summary>
        /// Determines whether any attack is off cooldown and within usable range
        /// Also caches the selected attack for immediate use if one is ready
        /// </summary>
        public bool HasAttackReady()
        {
            if (player == null || attacks == null)
                return false;

            float distSqr = (transform.position - player.position).sqrMagnitude;
            for (int i = 0; i < attacks.Count; i++)
            {
                if (attacks[i] == null)
                    continue;

                float rangeSqr = attacks[i].attackRange * attacks[i].attackRange;
                float effectiveCooldown = attacks[i].cooldown * VeilRunManager.CurrentEnemyAttackRecoveryMultiplier;
                if (Time.time >= lastUsedTimestamps[i] + effectiveCooldown &&
                    distSqr <= rangeSqr)
                {
                    selectedAttack = attacks[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the previously selected attack (from HasAttackReady) and logs its use
        /// This allows cooldowns to be enforced without modifying the attack prefab itself
        /// </summary>
        public EnemyAttackData GetSelectedAttack()
        {
            if (selectedAttack != null)
            {
                int i = attacks.IndexOf(selectedAttack);
                if (i >= 0) lastUsedTimestamps[i] = Time.time;
            }
            return selectedAttack;
        }
    }
}
