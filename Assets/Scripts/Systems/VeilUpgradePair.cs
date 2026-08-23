using System;
using System.Collections.Generic;
using UnityEngine;

namespace CyberVeil.Systems
{
    public enum VeilChoiceKind
    {
        Purify = 0,
        Absorb = 1
    }

    public enum VeilEffectType
    {
        HeavyDamage = 0,
        GlobalDamage = 1,
        MaximumHealth = 2,
        MovementSpeed = 3,
        DashDistance = 4,
        EnemyAttackRecovery = 5
    }

    public enum VeilEffectOperation
    {
        Add = 0,
        Multiply = 1
    }

    [Serializable]
    public struct VeilUpgradeEffect
    {
        [SerializeField] private VeilEffectType effectType;
        [SerializeField] private VeilEffectOperation operation;
        [SerializeField] private float value;

        public VeilEffectType EffectType => effectType;
        public VeilEffectOperation Operation => operation;
        public float Value => value;
    }

    [Serializable]
    public sealed class VeilUpgradeOption
    {
        [SerializeField] private string optionId;
        [SerializeField] private string choiceLabel;
        [SerializeField] private string feedbackHeadline;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private int corruptionChange;
        [SerializeField] private Color cardColor = Color.white;
        [SerializeField] private Sprite icon;
        [SerializeField, TextArea(2, 5)] private string burdenDescription;
        [SerializeField] private List<VeilUpgradeEffect> upgradeEffects = new List<VeilUpgradeEffect>();
        [SerializeField] private List<VeilUpgradeEffect> burdenEffects = new List<VeilUpgradeEffect>();

        public string OptionId => optionId;
        public string ChoiceLabel => string.IsNullOrWhiteSpace(choiceLabel) ? "CHOICE" : choiceLabel;
        public string FeedbackHeadline => string.IsNullOrWhiteSpace(feedbackHeadline) ? ChoiceLabel : feedbackHeadline;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Unnamed Upgrade" : displayName;
        public string Description => description ?? string.Empty;
        public int CorruptionChange => corruptionChange;
        public Color CardColor => cardColor;
        public Sprite Icon => icon;
        public string BurdenDescription => burdenDescription ?? string.Empty;
        public IReadOnlyList<VeilUpgradeEffect> UpgradeEffects => upgradeEffects;
        public IReadOnlyList<VeilUpgradeEffect> BurdenEffects => burdenEffects;
        public bool HasBurden => !string.IsNullOrWhiteSpace(burdenDescription) || burdenEffects.Count > 0;
    }

    /// <summary>
    /// Designer-authored pair presented by a Veil shard. All player-facing content and
    /// supported stat modifiers live on this asset rather than in the menu or run manager.
    /// </summary>
    [CreateAssetMenu(fileName = "VeilUpgradePair", menuName = "CyberVeil/Veil Upgrade Pair")]
    public sealed class VeilUpgradePair : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private string familyName;
        [SerializeField] private bool repeatable;
        [SerializeField] private VeilUpgradeOption purify = new VeilUpgradeOption();
        [SerializeField] private VeilUpgradeOption absorb = new VeilUpgradeOption();

        public string StableId => stableId;
        public string FamilyName => string.IsNullOrWhiteSpace(familyName) ? name : familyName;
        public bool Repeatable => repeatable;
        public VeilUpgradeOption Purify => purify;
        public VeilUpgradeOption Absorb => absorb;
        public bool HasValidId => !string.IsNullOrWhiteSpace(stableId);

        public VeilUpgradeOption GetOption(VeilChoiceKind kind)
        {
            return kind == VeilChoiceKind.Absorb ? absorb : purify;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(stableId))
                Debug.LogWarning($"Veil upgrade pair '{name}' needs a stable ID before it can be used.", this);
        }
    }
}
