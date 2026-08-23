using System;
using System.Collections.Generic;
using CyberVeil.Player;
using UnityEngine;

namespace CyberVeil.Systems
{
    [Serializable]
    public sealed class VeilUpgradeSelection
    {
        [SerializeField] private string pairId;
        [SerializeField] private string optionId;
        [SerializeField] private VeilChoiceKind choiceKind;
        [SerializeField] private VeilUpgradePair pair;

        public string PairId => pairId;
        public string OptionId => optionId;
        public VeilChoiceKind ChoiceKind => choiceKind;
        public VeilUpgradePair Pair => pair;
        public VeilUpgradeOption Option => pair != null ? pair.GetOption(choiceKind) : null;

        public VeilUpgradeSelection(VeilUpgradePair selectedPair, VeilChoiceKind selectedKind)
        {
            pair = selectedPair;
            pairId = selectedPair != null ? selectedPair.StableId : string.Empty;
            choiceKind = selectedKind;
            VeilUpgradeOption option = selectedPair != null ? selectedPair.GetOption(selectedKind) : null;
            optionId = option != null ? option.OptionId : string.Empty;
        }
    }

    /// <summary>
    /// Owns persistent run decisions and evaluates their data-authored modifiers.
    /// Gameplay systems query final values without knowing individual upgrade names.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class VeilRunManager : MonoBehaviour
    {
        public enum CorruptionTier
        {
            Controlled,
            Tainted,
            Unstable,
            Consumed,
            VeilTaken
        }

        public static VeilRunManager Instance { get; private set; }

        public event Action<int, int> OnCorruptionChanged;
        public event Action<CorruptionTier, CorruptionTier> OnCorruptionTierChanged;
        public event Action<VeilUpgradeSelection> OnUpgradeSelected;
        public event Action OnRunReset;

        [SerializeField, Range(0, 100)] private int corruption;
        [SerializeField] private List<VeilUpgradeSelection> selections = new List<VeilUpgradeSelection>();

        public int Corruption => corruption;
        public CorruptionTier Tier => GetTier(corruption);
        public IReadOnlyList<VeilUpgradeSelection> Selections => selections;

        public static float CurrentHeavyDamageMultiplier => ModifyCurrent(VeilEffectType.HeavyDamage, 1f);
        public static float CurrentEnemyAttackRecoveryMultiplier =>
            ModifyCurrent(VeilEffectType.EnemyAttackRecovery, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        private static VeilRunManager EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            VeilRunManager existing = FindFirstObjectByType<VeilRunManager>();
            if (existing != null)
                return existing;

            GameObject owner = new GameObject("VeilRunManager");
            return owner.AddComponent<VeilRunManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            corruption = Mathf.Clamp(corruption, 0, 100);
            selections ??= new List<VeilUpgradeSelection>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public int PreviewCorruption(VeilUpgradePair pair, VeilChoiceKind choiceKind)
        {
            if (pair == null)
                return corruption;

            VeilUpgradeOption option = pair.GetOption(choiceKind);
            return option == null ? corruption : Mathf.Clamp(corruption + option.CorruptionChange, 0, 100);
        }

        public bool CanSelect(VeilUpgradePair pair)
        {
            if (pair == null || !pair.HasValidId)
                return false;

            return pair.Repeatable || !HasSelected(pair.StableId);
        }

        public bool HasSelected(string pairId)
        {
            if (string.IsNullOrWhiteSpace(pairId))
                return false;

            for (int i = 0; i < selections.Count; i++)
            {
                if (selections[i] != null && string.Equals(selections[i].PairId, pairId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public bool SelectUpgrade(
            VeilUpgradePair pair,
            VeilChoiceKind choiceKind,
            out VeilUpgradeSelection selection)
        {
            selection = null;
            if (!CanSelect(pair))
                return false;

            VeilUpgradeOption option = pair.GetOption(choiceKind);
            if (option == null || string.IsNullOrWhiteSpace(option.OptionId))
            {
                Debug.LogError($"Veil upgrade pair '{pair.name}' has an invalid {choiceKind} option ID.", pair);
                return false;
            }

            selection = new VeilUpgradeSelection(pair, choiceKind);
            selections.Add(selection);
            SetCorruption(PreviewCorruption(pair, choiceKind));
            OnUpgradeSelected?.Invoke(selection);
            PlayerStatsUpgradeManager.Instance?.RefreshFromRunModifiers();
            return true;
        }

        public float GetModifiedValue(VeilEffectType effectType, float baseValue)
        {
            float additive = 0f;
            float multiplier = 1f;

            for (int i = 0; i < selections.Count; i++)
            {
                VeilUpgradeOption option = selections[i]?.Option;
                if (option == null)
                    continue;

                Accumulate(option.UpgradeEffects, effectType, ref additive, ref multiplier);
                Accumulate(option.BurdenEffects, effectType, ref additive, ref multiplier);
            }

            return (baseValue + additive) * multiplier;
        }

        public static float ModifyCurrent(VeilEffectType effectType, float baseValue)
        {
            return Instance != null ? Instance.GetModifiedValue(effectType, baseValue) : baseValue;
        }

        public static void ResetForNewRun()
        {
            EnsureInstance().ResetState();

            if (PlayerStatsUpgradeManager.Instance != null)
                PlayerStatsUpgradeManager.Instance.ResetAllUpgrades();
        }

        private static void Accumulate(
            IReadOnlyList<VeilUpgradeEffect> effects,
            VeilEffectType requestedType,
            ref float additive,
            ref float multiplier)
        {
            if (effects == null)
                return;

            for (int i = 0; i < effects.Count; i++)
            {
                VeilUpgradeEffect effect = effects[i];
                if (effect.EffectType != requestedType)
                    continue;

                if (effect.Operation == VeilEffectOperation.Multiply)
                    multiplier *= effect.Value;
                else
                    additive += effect.Value;
            }
        }

        private void ResetState()
        {
            int previousCorruption = corruption;
            CorruptionTier previousTier = GetTier(previousCorruption);

            corruption = 0;
            selections.Clear();

            if (previousCorruption != 0)
                OnCorruptionChanged?.Invoke(previousCorruption, corruption);

            CorruptionTier currentTier = GetTier(corruption);
            if (previousTier != currentTier)
                OnCorruptionTierChanged?.Invoke(previousTier, currentTier);

            OnRunReset?.Invoke();
        }

        private void SetCorruption(int value)
        {
            int clamped = Mathf.Clamp(value, 0, 100);
            if (clamped == corruption)
                return;

            int previous = corruption;
            CorruptionTier previousTier = GetTier(previous);
            corruption = clamped;
            CorruptionTier currentTier = GetTier(corruption);

            OnCorruptionChanged?.Invoke(previous, corruption);
            if (previousTier != currentTier)
                OnCorruptionTierChanged?.Invoke(previousTier, currentTier);
        }

        public static CorruptionTier GetTier(int value)
        {
            value = Mathf.Clamp(value, 0, 100);
            if (value >= 100) return CorruptionTier.VeilTaken;
            if (value >= 75) return CorruptionTier.Consumed;
            if (value >= 50) return CorruptionTier.Unstable;
            if (value >= 25) return CorruptionTier.Tainted;
            return CorruptionTier.Controlled;
        }

        public static string GetTierName(CorruptionTier tier)
        {
            return tier == CorruptionTier.VeilTaken ? "VEIL TAKEN" : tier.ToString().ToUpperInvariant();
        }
    }
}
