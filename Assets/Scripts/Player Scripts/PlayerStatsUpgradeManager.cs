using System;
using CyberVeil.Systems;
using UnityEngine;

namespace CyberVeil.Player
{
    /// <summary>
    /// Central manager for stat upgrades for the player
    /// The base stats (like default health, base damage, etc.) live in other scripts,
    /// at runtime, reads from this class to get upgraded values (damage, movement, dash distance, HP)
    /// </summary>
    public class PlayerStatsUpgradeManager : MonoBehaviour
    {
        public static PlayerStatsUpgradeManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (Instance != null)
                return;

            PlayerStatsUpgradeManager existing = FindFirstObjectByType<PlayerStatsUpgradeManager>(
                FindObjectsInactive.Include);
            if (existing != null)
                return;

            new GameObject(nameof(PlayerStatsUpgradeManager)).AddComponent<PlayerStatsUpgradeManager>();
        }

        [Header("Multipliers")]
        [Tooltip("1.0 = no change, 1.1 = +10% damage")]
        [SerializeField] private float damageMultiplier = 1f;

        [Header("Additive / Percent Bonuses")]
        [Tooltip("Extra fraction of base max health. 0.2 = +20% of base")]
        [SerializeField] private float maxHealthPct = 0f;
        [SerializeField] private float moveSpeedAdd = 0f;
        [SerializeField] private float dashDistanceAdd = 0f;
        
        /// <summary>
        /// Event triggered whenever any stat upgrade changes, other scrips subscribe to this
        /// to refresh derived values whenever an upgrade is applied.
        /// <summary>
        public event Action OnChanged;

        private void Awake()
        {
            if (Instance && Instance != this) 
            { 
                Destroy(gameObject); 
                return; 
            }
            Instance = this;
            
            // Persist this across scene loads to keep upgrades
            DontDestroyOnLoad(gameObject);
        }

        // ---------------- READ API -----------------
        // Properties are "read-only", clamped or returned safely to prevent invalid values
        public float DamageMultiplier => Mathf.Max(0f,
            VeilRunManager.ModifyCurrent(VeilEffectType.GlobalDamage, damageMultiplier));
        public float HeavyDamageMultiplier => Mathf.Max(0f,
            VeilRunManager.ModifyCurrent(VeilEffectType.HeavyDamage, 1f));
        public float MaxHealthMultiplier => Mathf.Max(0.01f,
            VeilRunManager.ModifyCurrent(VeilEffectType.MaximumHealth, 1f + maxHealthPct));
        public float MaxHealthPct => MaxHealthMultiplier - 1f;
        public float MoveSpeedAdd => moveSpeedAdd;
        public float DashDistanceAdd => dashDistanceAdd;

        public float GetMoveSpeed(float baseSpeed)
        {
            return Mathf.Max(0f,
                VeilRunManager.ModifyCurrent(VeilEffectType.MovementSpeed, baseSpeed + moveSpeedAdd));
        }

        public float GetDashDistance(float baseDistance)
        {
            return Mathf.Max(0f,
                VeilRunManager.ModifyCurrent(VeilEffectType.DashDistance, baseDistance + dashDistanceAdd));
        }

        // ---------------- WRITE API -----------------
        /// <summary>
        /// Increases multipliers, percenteges or flat bonuses by the specified amount and raises OnChanged
        /// </summary>
        public void AddDamageMultiplier(float add) { damageMultiplier += add; OnChanged?.Invoke(); }
        public void AddMaxHealthPercent(float addPct) { maxHealthPct += addPct; OnChanged?.Invoke(); }
        public void AddMoveSpeed(float add) { moveSpeedAdd += add; OnChanged?.Invoke(); }
        public void AddDashDistance(float add) { dashDistanceAdd += add; OnChanged?.Invoke(); }

        public void RefreshFromRunModifiers()
        {
            OnChanged?.Invoke();
        }

        /// <summary>Clears run-scoped upgrades when the player starts a genuine new run.</summary>
        public void ResetAllUpgrades()
        {
            damageMultiplier = 1f;
            maxHealthPct = 0f;
            moveSpeedAdd = 0f;
            dashDistanceAdd = 0f;
            OnChanged?.Invoke();
        }
    }
}
