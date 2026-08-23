using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CyberVeil.Systems;

namespace CyberVeil.UI
{
    /// <summary>
    /// Presents the current Veil corruption value using a prefab-authored UI hierarchy.
    /// Layout and artwork stay editable in the Unity Editor; this component only drives
    /// values, colors, previews, and feedback at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CorruptionMeterUI : MonoBehaviour
    {
        public static CorruptionMeterUI Instance { get; private set; }

        [Header("Editor-Authored UI References")]
        [SerializeField] private RectTransform fillArea;
        [SerializeField] private Image fillImage;
        [SerializeField] private Image previewFillImage;
        [SerializeField] private Image leftSlotGlowImage;
        [SerializeField] private TMP_Text percentageText;
        [SerializeField] private TMP_Text tierText;
        [SerializeField] private TMP_Text previewText;
        [SerializeField] private TMP_Text feedbackText;

        [Header("Corruption Fill Colors")]
        [SerializeField] private Color controlledFillColor = new Color(0.15f, 0.95f, 1f, 1f);
        [SerializeField] private Color taintedFillColor = new Color(0.58f, 0.25f, 1f, 1f);
        [SerializeField] private Color consumedFillColor = new Color(1f, 0.12f, 0.28f, 1f);

        [Header("Left Slot Glow")]
        [SerializeField] private Color baseGlowColor = new Color(0.08f, 0.35f, 1f, 1f);

        private float fillMaxWidth;
        private float fillVisualHeight;
        private float displayedValue;
        private float targetValue;
        private int? previewValue;
        private Coroutine feedbackRoutine;
        private bool committingChoiceFeedback;
        private string pendingTierMessage;
        private VeilRunManager run;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (!ValidateVisualReferences())
            {
                enabled = false;
                return;
            }

            Instance = this;
            CacheEditableLayout();
        }

        private void OnEnable()
        {
            run = VeilRunManager.Instance;
            if (run == null)
                return;

            displayedValue = targetValue = run.Corruption;
            run.OnCorruptionChanged += HandleCorruptionChanged;
            run.OnCorruptionTierChanged += HandleTierChanged;
            RefreshVisuals();
        }

        private void OnDisable()
        {
            if (run != null)
            {
                run.OnCorruptionChanged -= HandleCorruptionChanged;
                run.OnCorruptionTierChanged -= HandleTierChanged;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

        }

        private void Update()
        {
            displayedValue = Mathf.MoveTowards(displayedValue, targetValue, 85f * Time.unscaledDeltaTime);
            RefreshVisuals();
        }

        public void SetPreview(int value, string label)
        {
            previewValue = Mathf.Clamp(value, 0, 100);
            if (previewText != null && run != null)
                previewText.text = $"{run.Corruption}%  ->  {previewValue.Value}%\n{label}";
        }

        public void ClearPreview()
        {
            previewValue = null;
            if (previewText != null)
                previewText.text = string.Empty;
        }

        public void ShowChoiceFeedback(VeilUpgradeSelection selection)
        {
            if (selection?.Option == null)
                return;

            if (feedbackRoutine != null)
                StopCoroutine(feedbackRoutine);

            feedbackRoutine = StartCoroutine(ChoiceFeedbackRoutine(selection));
        }

        private IEnumerator ChoiceFeedbackRoutine(VeilUpgradeSelection selection)
        {
            committingChoiceFeedback = true;
            VeilUpgradeOption option = selection.Option;
            int finalCorruption = run != null ? run.Corruption : Mathf.RoundToInt(targetValue);
            string tierSuffix = string.IsNullOrEmpty(pendingTierMessage) ? string.Empty : $"\n{pendingTierMessage}";
            string burdenLine = option.HasBurden
                ? $"\nBURDEN: {option.BurdenDescription}"
                : string.Empty;
            feedbackText.color = option.CardColor;
            feedbackText.text =
                $"{option.FeedbackHeadline}  -  {finalCorruption}%\n" +
                $"{option.DisplayName.ToUpperInvariant()} ACQUIRED" +
                $"{burdenLine}{tierSuffix}";
            pendingTierMessage = null;

            yield return new WaitForSecondsRealtime(3.2f);
            feedbackText.text = string.Empty;
            committingChoiceFeedback = false;
            feedbackRoutine = null;
        }

        private void HandleCorruptionChanged(int previous, int current)
        {
            targetValue = current;
            ClearPreview();
        }

        private void HandleTierChanged(VeilRunManager.CorruptionTier previous, VeilRunManager.CorruptionTier current)
        {
            pendingTierMessage = $"TIER CROSSED: {VeilRunManager.GetTierName(current)}";
            if (committingChoiceFeedback)
                return;

            if (feedbackRoutine != null)
                StopCoroutine(feedbackRoutine);

            feedbackRoutine = StartCoroutine(TierFeedbackRoutine(current));
        }

        private IEnumerator TierFeedbackRoutine(VeilRunManager.CorruptionTier tier)
        {
            feedbackText.color = GetColorForValue(targetValue);
            feedbackText.text = $"CORRUPTION TIER: {VeilRunManager.GetTierName(tier)}";
            yield return new WaitForSecondsRealtime(2f);
            feedbackText.text = string.Empty;
            feedbackRoutine = null;
        }

        private bool ValidateVisualReferences()
        {
            bool valid = fillArea != null && fillImage != null && previewFillImage != null &&
                leftSlotGlowImage != null &&
                 tierText != null && previewText != null && feedbackText != null;
            if (!valid)
            {
                Debug.LogError(
                    "CorruptionMeterUI is missing editor-authored UI references. " +
                    "Open the VeilCorruptionMeter prefab and reconnect its child fields.", this);
            }

            return valid;
        }

        private void CacheEditableLayout()
        {
            fillMaxWidth = Mathf.Max(1f, fillArea.rect.width);
            fillVisualHeight = Mathf.Max(1f, fillArea.rect.height);
        }

        private void RefreshVisuals()
        {
            if (fillImage == null)
                return;

            float value = Mathf.Clamp(displayedValue, 0f, 100f);
            fillImage.rectTransform.sizeDelta = new Vector2(fillMaxWidth * value / 100f, fillVisualHeight);
            Color color = GetColorForValue(value);
            fillImage.color = color;
            tierText.text = VeilRunManager.GetTierName(VeilRunManager.GetTier(Mathf.RoundToInt(value)));

            float glowValue = previewValue.HasValue ? previewValue.Value : value;
            leftSlotGlowImage.color = GetGlowColor(glowValue);

            if (previewFillImage != null)
            {
                bool showingPreview = previewValue.HasValue;
                previewFillImage.gameObject.SetActive(showingPreview);
                if (showingPreview)
                {
                    float ghostValue = previewValue.Value;
                    previewFillImage.rectTransform.sizeDelta =
                        new Vector2(fillMaxWidth * ghostValue / 100f, fillVisualHeight);
                    Color ghostColor = GetColorForValue(ghostValue);
                    ghostColor.a = 0.48f;
                    previewFillImage.color = ghostColor;
                }
            }
        }

        private Color GetColorForValue(float value)
        {
            if (value <= 50f)
                return Color.Lerp(controlledFillColor, taintedFillColor, value / 50f);
            return Color.Lerp(taintedFillColor, consumedFillColor, (value - 50f) / 50f);
        }

        private Color GetGlowColor(float value)
        {
            float normalized = Mathf.Clamp01(value / 100f);
            return Color.Lerp(baseGlowColor, GetColorForValue(value), normalized);
        }

    }
}
