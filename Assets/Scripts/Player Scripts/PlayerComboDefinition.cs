using System;
using CyberVeil.VFX;
using UnityEngine;

namespace CyberVeil.Player
{
    /// <summary>
    /// Reusable data for the player's light-attack sequence. PlayerAttack owns combo
    /// decisions; this asset only describes how each step should look and feel.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PlayerComboDefinition",
        menuName = "CyberVeil/Player/Combo Definition")]
    public sealed class PlayerComboDefinition : ScriptableObject
    {
        [SerializeField, Min(0.05f)] private float defaultResetDelay = 0.65f;
        [SerializeField] private PlayerAttackStep[] steps = Array.Empty<PlayerAttackStep>();

        public int StepCount => steps?.Length ?? 0;
        public float DefaultResetDelay => Mathf.Max(0.05f, defaultResetDelay);

        public bool TryGetStep(int index, out PlayerAttackStep step)
        {
            if (steps == null || steps.Length == 0)
            {
                step = null;
                return false;
            }

            int wrappedIndex = ((index % steps.Length) + steps.Length) % steps.Length;
            step = steps[wrappedIndex];
            return step != null;
        }

        public float GetResetDelay(PlayerAttackStep step)
        {
            if (step != null && step.ComboResetDelayOverride >= 0f)
                return Mathf.Max(0.05f, step.ComboResetDelayOverride);

            return DefaultResetDelay;
        }

#if UNITY_EDITOR
        public void EditorConfigure(float resetDelay, PlayerAttackStep[] configuredSteps)
        {
            defaultResetDelay = Mathf.Max(0.05f, resetDelay);
            steps = configuredSteps ?? Array.Empty<PlayerAttackStep>();
        }
#endif
    }

    /// <summary>
    /// Inspector-authored settings for one light-attack step.
    /// </summary>
    [Serializable]
    public sealed class PlayerAttackStep
    {
        [Header("Animation")]
        [SerializeField] private string displayName = "Slash";
        [SerializeField] private string animatorState = "SlashAttack1";
        [SerializeField, Min(0.05f)] private float playbackSpeed = 1f;
        [SerializeField, Min(0f)] private float crossFadeTime = 0.05f;
        [SerializeField, Min(0.05f)] private float fallbackDuration = 1f;

        [Header("Combat")]
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;
        [SerializeField, Min(0f)] private float rangeMultiplier = 1f;
        [SerializeField, Min(0f)] private float forwardImpulse = 0.2f;
        [SerializeField, Min(-1f)] private float comboResetDelayOverride = -1f;

        [Header("Slash Presentation")]
        [SerializeField] private VFXType slashVfx = VFXType.Slash1;
        [SerializeField] private VFXType surgeSlashVfx = VFXType.SurgeSlash1;
        [SerializeField, Min(0f)] private float vfxForwardDistance = 1f;
        [SerializeField] private Vector3 vfxLocalOffset = new Vector3(0f, 0.8f, 0f);
        [SerializeField] private Vector3 vfxEulerOffset = new Vector3(0f, -60f, 0f);
        [SerializeField, Min(0f)] private float hitStopDuration = 0.01f;
        [SerializeField, Range(0f, 1f)] private float hitStopTimeScale;
        [SerializeField] private bool useSecondaryAxe;

        public string DisplayName => displayName;
        public string AnimatorState => animatorState;
        public int AnimatorStateHash => Animator.StringToHash(animatorState);
        public float PlaybackSpeed => Mathf.Max(0.05f, playbackSpeed);
        public float CrossFadeTime => Mathf.Max(0f, crossFadeTime);
        public float FallbackDuration => Mathf.Max(0.05f, fallbackDuration);
        public float DamageMultiplier => Mathf.Max(0f, damageMultiplier);
        public float RangeMultiplier => Mathf.Max(0f, rangeMultiplier);
        public float ForwardImpulse => Mathf.Max(0f, forwardImpulse);
        public float ComboResetDelayOverride => comboResetDelayOverride;
        public VFXType SlashVfx => slashVfx;
        public VFXType SurgeSlashVfx => surgeSlashVfx;
        public float VfxForwardDistance => Mathf.Max(0f, vfxForwardDistance);
        public Vector3 VfxLocalOffset => vfxLocalOffset;
        public Vector3 VfxEulerOffset => vfxEulerOffset;
        public float HitStopDuration => Mathf.Max(0f, hitStopDuration);
        public float HitStopTimeScale => Mathf.Clamp01(hitStopTimeScale);
        public bool UseSecondaryAxe => useSecondaryAxe;

#if UNITY_EDITOR
        public PlayerAttackStep(
            string name,
            string state,
            float speed,
            float crossFade,
            float safetyDuration,
            float damageScale,
            float rangeScale,
            float impulse,
            VFXType normalVfx,
            VFXType surgeVfx,
            float forwardDistance,
            Vector3 localOffset,
            Vector3 eulerOffset,
            float hitStop,
            float hitStopScale,
            bool secondaryAxe,
            float resetDelayOverride = -1f)
        {
            displayName = name;
            animatorState = state;
            playbackSpeed = speed;
            crossFadeTime = crossFade;
            fallbackDuration = safetyDuration;
            damageMultiplier = damageScale;
            rangeMultiplier = rangeScale;
            forwardImpulse = impulse;
            slashVfx = normalVfx;
            surgeSlashVfx = surgeVfx;
            vfxForwardDistance = forwardDistance;
            vfxLocalOffset = localOffset;
            vfxEulerOffset = eulerOffset;
            hitStopDuration = hitStop;
            hitStopTimeScale = hitStopScale;
            useSecondaryAxe = secondaryAxe;
            comboResetDelayOverride = resetDelayOverride;
        }
#endif
    }
}
