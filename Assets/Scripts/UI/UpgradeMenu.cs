using System.Collections;
using CyberVeil.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CyberVeil.UI
{
    /// <summary>Renders any assigned Veil upgrade pair into the existing two-card menu.</summary>
    public sealed class UpgradeMenu : MonoBehaviour
    {
        private static readonly int EnergyColorId = Shader.PropertyToID("_EnergyColor");
        private static readonly int SecondaryColorId = Shader.PropertyToID("_SecondaryColor");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int FocusAmountId = Shader.PropertyToID("_FocusAmount");
        private static readonly int ConfirmationAmountId = Shader.PropertyToID("_ConfirmationAmount");
        private static readonly int EffectIntensityId = Shader.PropertyToID("_EffectIntensity");
        private static readonly int EdgeIntensityId = Shader.PropertyToID("_EdgeIntensity");
        private static readonly int SweepIntensityId = Shader.PropertyToID("_SweepIntensity");
        private static readonly int ParticleIntensityId = Shader.PropertyToID("_ParticleIntensity");
        private static readonly int PurifyBoostId = Shader.PropertyToID("_PurifyBoost");
        private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
        private static readonly int DistortionId = Shader.PropertyToID("_Distortion");
        private static readonly int UnscaledTimeId = Shader.PropertyToID("_UnscaledTime");

        public static UpgradeMenu Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button[] cardButtons;

        [Header("Card Animation")]
        [SerializeField] private bool animateOnShow = true;
        [SerializeField] private float cardStaggerSeconds = 0.08f;
        [SerializeField] private float cardStartScale = 0.85f;
        [SerializeField] private float cardOvershootScale = 1.08f;
        [SerializeField] private float cardPopSeconds = 0.18f;
        [SerializeField] private float cardSettleSeconds = 0.12f;

        [Header("Focused Card Presentation")]
        [SerializeField] private TMP_FontAsset medievalFont;
        [SerializeField, Range(0f, 1f)] private float focusBackdropAlpha = 0.78f;
        [SerializeField] private float focusFadeSeconds = 0.2f;
        [SerializeField] private float focusSlideDistance = 22f;
        [SerializeField, Range(0.85f, 1.15f)] private float focusScale = 1.05f;
        [SerializeField] private float focusRise = 14f;
        [SerializeField] private float focusMoveSeconds = 0.18f;
        [SerializeField] private float textRevealSeconds = 0.24f;

        [Header("Veil Card Effects")]
        [SerializeField] private Shader veilCardEffectShader;
        [SerializeField] private Color purifyEnergyColor = new Color(0.145098f, 0.02352941f, 0.5607843f, 1f);
        [SerializeField] private Color absorbEnergyColor = new Color(0.9f, 0.025f, 0.08f, 1f);
        [SerializeField] private Color absorbSecondaryColor = new Color(0.42f, 0.035f, 0.72f, 1f);
        [SerializeField, Range(0f, 2f)] private float effectIntensity = 1f;
        [SerializeField, Range(0f, 3f)] private float edgeIntensity = 1.15f;
        [SerializeField, Range(0f, 2f)] private float lightSweepIntensity = 0.65f;
        [SerializeField, Range(0f, 2f)] private float particleIntensity = 0.45f;
        [SerializeField, Range(1f, 6f)] private float purifyEmissionBoost = 5f;
        [SerializeField, Range(0f, 8f)] private float effectPulseSpeed = 2.4f;
        [SerializeField, Range(0f, 1f)] private float distortionIntensity = 0.35f;
        [SerializeField, Range(0f, 2f)] private float confirmationSurge = 1f;

        private readonly TMP_Text[] cardLabels = new TMP_Text[2];
        private readonly Image[] cardIcons = new Image[2];
        private readonly CanvasGroup[] cardInfoGroups = new CanvasGroup[2];
        private readonly RectTransform[] cardInfoRects = new RectTransform[2];
        private readonly Image[] cardImages = new Image[2];
        private readonly Material[] cardEffectMaterials = new Material[2];
        private readonly float[] cardEffectFocus = new float[2];
        private readonly float[] cardEffectConfirmation = new float[2];
        private readonly Vector2[] cardBasePositions = new Vector2[2];
        private readonly Coroutine[] cardFocusRoutines = new Coroutine[2];
        private readonly Coroutine[] cardTransformRoutines = new Coroutine[2];
        private TMP_Text heading;
        private Coroutine cardsRoutine;
        private VeilUpgradePair activePair;
        private float previousTimeScale = 1f;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLockMode;
        private bool awaitingAbsorbConfirmation;
        private bool choiceCommitted;
        private int focusedCardIndex = -1;

        public bool IsOpen { get; private set; }
        public int? PickedIndex { get; private set; }
        public VeilUpgradeSelection LastSelection { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            BuildTwoCardPresentation();
            WireButtons();
            HideImmediate();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            for (int i = 0; i < cardEffectMaterials.Length; i++)
            {
                if (cardEffectMaterials[i] != null)
                    Destroy(cardEffectMaterials[i]);
            }
        }

        private void Update()
        {
            if (IsOpen)
                UpdateCardEffects();

            if (!IsOpen || !awaitingAbsorbConfirmation)
                return;

            bool cancelPressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            cancelPressed |= Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (cancelPressed)
            {
                awaitingAbsorbConfirmation = false;
                RefreshCardPresentation();
            }
        }

        private void BuildTwoCardPresentation()
        {
            if (cardButtons == null || cardButtons.Length < 2)
            {
                Debug.LogError("UpgradeMenu requires at least two configured card buttons.", this);
                return;
            }

            TMP_FontAsset displayFont = ResolveMedievalFont();

            for (int i = 0; i < cardButtons.Length; i++)
            {
                Button button = cardButtons[i];
                if (button == null)
                    continue;

                bool used = i < 2;
                button.gameObject.SetActive(used);
                if (!used)
                    continue;

                RectTransform cardRect = button.transform as RectTransform;
                if (cardRect != null)
                {
                    cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                    cardRect.anchoredPosition = new Vector2(i == 0 ? -260f : 260f, 0f);
                    cardBasePositions[i] = cardRect.anchoredPosition;
                }

                cardImages[i] = button.GetComponent<Image>();
                CreateCardEffectMaterial(i);
                CreateCardInfoRoot(button.transform, i, out cardInfoGroups[i], out cardInfoRects[i]);
                cardLabels[i] = CreateCardLabel(cardInfoRects[i], displayFont);
                cardIcons[i] = CreateCardIcon(cardInfoRects[i]);
                SetCardInfoImmediate(i, false);

                VeilChoiceCardFocus focus = button.GetComponent<VeilChoiceCardFocus>();
                if (focus == null)
                    focus = button.gameObject.AddComponent<VeilChoiceCardFocus>();
                focus.Initialize(this, i);
            }

            Navigation leftNavigation = cardButtons[0].navigation;
            leftNavigation.mode = Navigation.Mode.Explicit;
            leftNavigation.selectOnRight = cardButtons[1];
            cardButtons[0].navigation = leftNavigation;

            Navigation rightNavigation = cardButtons[1].navigation;
            rightNavigation.mode = Navigation.Mode.Explicit;
            rightNavigation.selectOnLeft = cardButtons[0];
            cardButtons[1].navigation = rightNavigation;

            RectTransform menuRoot = transform as RectTransform;
            heading = CreateText("VeilChoiceHeading", menuRoot, new Vector2(0f, 400f), new Vector2(1000f, 60f), 28f, displayFont);
            heading.fontStyle = FontStyles.Bold;
            heading.alignment = TextAlignmentOptions.Center;
        }

        private void WireButtons()
        {
            if (cardButtons == null)
                return;

            for (int i = 0; i < cardButtons.Length; i++)
            {
                int index = i;
                Button button = cardButtons[i];
                if (button == null)
                    continue;

                if (i < 2)
                {
                    if (button.GetComponent<PlaySoundOnHover>() == null)
                        button.gameObject.AddComponent<PlaySoundOnHover>();
                    if (button.GetComponent<CursorChangeOnHover>() == null)
                        button.gameObject.AddComponent<CursorChangeOnHover>();
                }

                button.onClick.RemoveAllListeners();
                if (i < 2)
                    button.onClick.AddListener(() => OnPick(index));
            }
        }

        internal void FocusCard(int index)
        {
            if (!IsOpen || activePair == null || index < 0 || index > 1)
                return;

            SetFocusedCard(index);

            if (index != 1 && awaitingAbsorbConfirmation)
            {
                awaitingAbsorbConfirmation = false;
                RefreshCardPresentation();
            }

            VeilRunManager run = VeilRunManager.Instance;
            if (run == null)
                return;

            VeilChoiceKind kind = GetChoiceKind(index);
            VeilUpgradeOption option = activePair.GetOption(kind);
            CorruptionMeterUI.Instance?.SetPreview(
                run.PreviewCorruption(activePair, kind),
                option != null ? option.ChoiceLabel : kind.ToString().ToUpperInvariant());
        }

        internal void ReleaseCardFocus(int index)
        {
            if (!IsOpen || focusedCardIndex != index)
                return;

            focusedCardIndex = -1;
            AnimateCardInfo(index, false);
            AnimateCardTransform(index, false);
        }

        private void OnPick(int index)
        {
            if (!IsOpen || choiceCommitted || activePair == null || index < 0 || index > 1)
                return;

            FocusCard(index);
            VeilChoiceKind kind = GetChoiceKind(index);
            VeilUpgradeOption option = activePair.GetOption(kind);
            if (option == null)
                return;

            if (kind == VeilChoiceKind.Absorb && !awaitingAbsorbConfirmation)
            {
                awaitingAbsorbConfirmation = true;
                RefreshCardPresentation();
                return;
            }

            Commit(kind);
        }

        private void Commit(VeilChoiceKind choiceKind)
        {
            VeilRunManager run = VeilRunManager.Instance;
            if (run == null || activePair == null ||
                !run.SelectUpgrade(activePair, choiceKind, out VeilUpgradeSelection selection))
                return;

            LastSelection = selection;
            PickedIndex = choiceKind == VeilChoiceKind.Purify ? 0 : 1;
            choiceCommitted = true;
            awaitingAbsorbConfirmation = false;
            CorruptionMeterUI.Instance?.ClearPreview();
            CorruptionMeterUI.Instance?.ShowChoiceFeedback(selection);
        }

        private bool Show(VeilUpgradePair pair)
        {
            VeilRunManager run = VeilRunManager.Instance;
            if (run == null || pair == null || !run.CanSelect(pair))
            {
                Debug.LogError("UpgradeMenu received an invalid or unavailable Veil upgrade pair.", this);
                return false;
            }

            activePair = pair;
            gameObject.SetActive(true);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            IsOpen = true;
            PickedIndex = null;
            LastSelection = null;
            choiceCommitted = false;
            awaitingAbsorbConfirmation = false;
            ResetCardFocusVisuals();

            previousTimeScale = Time.timeScale;
            previousCursorVisible = Cursor.visible;
            previousCursorLockMode = Cursor.lockState;
            Time.timeScale = 0f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            heading.text = $"SHAPE THE VEIL  -  {pair.FamilyName.ToUpperInvariant()}";
            RefreshCardPresentation();
            EventSystem.current?.SetSelectedGameObject(cardButtons[0].gameObject);
            FocusCard(0);

            if (animateOnShow)
            {
                if (cardsRoutine != null)
                    StopCoroutine(cardsRoutine);
                for (int i = 0; i < cardTransformRoutines.Length; i++)
                {
                    if (cardTransformRoutines[i] == null)
                        continue;

                    StopCoroutine(cardTransformRoutines[i]);
                    cardTransformRoutines[i] = null;
                }
                cardsRoutine = StartCoroutine(AnimateCardsIn());
            }
            return true;
        }

        private void HideImmediate()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            IsOpen = false;
            if (cardsRoutine != null)
            {
                StopCoroutine(cardsRoutine);
                cardsRoutine = null;
            }

            focusedCardIndex = -1;
            for (int i = 0; i < cardFocusRoutines.Length; i++)
            {
                if (cardFocusRoutines[i] != null)
                {
                    StopCoroutine(cardFocusRoutines[i]);
                    cardFocusRoutines[i] = null;
                }
                SetCardInfoImmediate(i, false);

                if (cardTransformRoutines[i] != null)
                {
                    StopCoroutine(cardTransformRoutines[i]);
                    cardTransformRoutines[i] = null;
                }
                SetCardTransformImmediate(i, false);
                cardEffectFocus[i] = 0f;
                cardEffectConfirmation[i] = 0f;
            }
            gameObject.SetActive(false);
        }

        private void Hide()
        {
            CorruptionMeterUI.Instance?.ClearPreview();
            HideImmediate();
            Time.timeScale = previousTimeScale;
            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousCursorLockMode;
            activePair = null;
        }

        public IEnumerator ShowAndWait(VeilUpgradePair pair)
        {
            if (!Show(pair))
                yield break;

            while (!choiceCommitted)
                yield return null;

            yield return new WaitForSecondsRealtime(0.8f);
            Hide();
        }

        private void RefreshCardPresentation()
        {
            if (activePair == null)
                return;

            VeilRunManager run = VeilRunManager.Instance;
            int current = run != null ? run.Corruption : 0;
            for (int i = 0; i < 2; i++)
            {
                VeilChoiceKind kind = GetChoiceKind(i);
                VeilUpgradeOption option = activePair.GetOption(kind);
                if (option == null || cardLabels[i] == null)
                    continue;

                int result = run != null ? run.PreviewCorruption(activePair, kind) : current;
                string color = ColorUtility.ToHtmlStringRGB(option.CardColor);
                string burden = option.HasBurden
                    ? $"<color=#{color}>BURDEN: {option.BurdenDescription}</color>"
                    : $"<color=#{color}>NO BURDEN</color>";
                string confirmation = kind == VeilChoiceKind.Absorb && awaitingAbsorbConfirmation
                    ? $"\n\n<color=#{color}><b>SELECT AGAIN TO CONFIRM</b></color>"
                    : string.Empty;

                cardLabels[i].text =
                    $"<color=#{color}><b>{option.ChoiceLabel}</b></color>\n\n" +
                    $"<b>{option.DisplayName.ToUpperInvariant()}</b>\n\n" +
                    $"{option.Description}\n\n" +
                    $"CORRUPTION\n{current}%  ->  {result}%\n\n" +
                    $"{burden}{confirmation}";

                Image icon = cardIcons[i];
                if (icon != null)
                {
                    icon.sprite = option.Icon;
                    icon.color = option.CardColor;
                    icon.gameObject.SetActive(option.Icon != null);
                }

                RectTransform labelRect = cardLabels[i].rectTransform;
                labelRect.offsetMax = new Vector2(-48f, option.Icon != null ? -142f : -58f);
            }
        }

        private static VeilChoiceKind GetChoiceKind(int index)
        {
            return index == 1 ? VeilChoiceKind.Absorb : VeilChoiceKind.Purify;
        }

        private TMP_FontAsset ResolveMedievalFont()
        {
            if (medievalFont != null)
                return medievalFont;

            TMP_Text[] sceneTexts = UnityEngine.Object.FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < sceneTexts.Length; i++)
            {
                TMP_FontAsset font = sceneTexts[i] != null ? sceneTexts[i].font : null;
                if (font != null && font.name.Contains("MedievalTimes"))
                {
                    medievalFont = font;
                    break;
                }
            }

            return medievalFont;
        }

        private void SetFocusedCard(int index)
        {
            if (focusedCardIndex == index)
                return;

            focusedCardIndex = index;
            for (int i = 0; i < cardInfoGroups.Length; i++)
            {
                AnimateCardInfo(i, i == index);
                AnimateCardTransform(i, i == index);
            }
        }

        private void AnimateCardInfo(int index, bool visible)
        {
            if (index < 0 || index >= cardInfoGroups.Length || cardInfoGroups[index] == null)
                return;

            if (cardFocusRoutines[index] != null)
                StopCoroutine(cardFocusRoutines[index]);
            cardFocusRoutines[index] = StartCoroutine(AnimateCardInfoRoutine(index, visible));
        }

        private IEnumerator AnimateCardInfoRoutine(int index, bool visible)
        {
            CanvasGroup group = cardInfoGroups[index];
            RectTransform rect = cardInfoRects[index];
            float startAlpha = group.alpha;
            float targetAlpha = visible ? 1f : 0f;
            Vector2 startPosition = rect.anchoredPosition;
            Vector2 targetPosition = visible ? Vector2.zero : new Vector2(0f, -focusSlideDistance);
            Vector3 startScale = rect.localScale;
            Vector3 targetScale = visible ? Vector3.one : Vector3.one * 0.965f;
            TMP_Text label = cardLabels[index];
            int characterCount = 0;
            if (visible && label != null)
            {
                label.ForceMeshUpdate();
                characterCount = label.textInfo.characterCount;
                label.maxVisibleCharacters = 0;
            }
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, focusFadeSeconds);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float progress = 1f - Mathf.Pow(1f - normalized, 3f);
                group.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
                rect.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, progress);
                rect.localScale = Vector3.LerpUnclamped(startScale, targetScale, progress);
                if (visible && label != null)
                {
                    float revealProgress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, textRevealSeconds));
                    label.maxVisibleCharacters = Mathf.CeilToInt(characterCount * revealProgress);
                }
                yield return null;
            }

            group.alpha = targetAlpha;
            rect.anchoredPosition = targetPosition;
            rect.localScale = targetScale;
            if (label != null)
                label.maxVisibleCharacters = visible ? int.MaxValue : 0;
            cardFocusRoutines[index] = null;
        }

        private void SetCardInfoImmediate(int index, bool visible)
        {
            if (index < 0 || index >= cardInfoGroups.Length || cardInfoGroups[index] == null)
                return;

            cardInfoGroups[index].alpha = visible ? 1f : 0f;
            cardInfoGroups[index].interactable = false;
            cardInfoGroups[index].blocksRaycasts = false;
            if (cardInfoRects[index] != null)
            {
                cardInfoRects[index].anchoredPosition = visible
                    ? Vector2.zero
                    : new Vector2(0f, -focusSlideDistance);
                cardInfoRects[index].localScale = visible ? Vector3.one : Vector3.one * 0.965f;
            }
            if (cardLabels[index] != null)
                cardLabels[index].maxVisibleCharacters = visible ? int.MaxValue : 0;
        }

        private void ResetCardFocusVisuals()
        {
            focusedCardIndex = -1;
            for (int i = 0; i < cardInfoGroups.Length; i++)
            {
                if (cardFocusRoutines[i] != null)
                {
                    StopCoroutine(cardFocusRoutines[i]);
                    cardFocusRoutines[i] = null;
                }
                SetCardInfoImmediate(i, false);
                if (cardTransformRoutines[i] != null)
                {
                    StopCoroutine(cardTransformRoutines[i]);
                    cardTransformRoutines[i] = null;
                }
                SetCardTransformImmediate(i, false);
            }
        }

        private void UpdateCardEffects()
        {
            float transitionSpeed = 1f / Mathf.Max(0.01f, focusMoveSeconds);
            for (int i = 0; i < cardEffectMaterials.Length; i++)
            {
                Material material = cardEffectMaterials[i];
                if (material == null)
                    continue;

                float focusTarget = focusedCardIndex == i ? 1f : 0f;
                float confirmationTarget = i == 1 && awaitingAbsorbConfirmation ? confirmationSurge : 0f;
                cardEffectFocus[i] = Mathf.MoveTowards(
                    cardEffectFocus[i], focusTarget, Time.unscaledDeltaTime * transitionSpeed);
                cardEffectConfirmation[i] = Mathf.MoveTowards(
                    cardEffectConfirmation[i], confirmationTarget, Time.unscaledDeltaTime * transitionSpeed * 1.25f);

                bool absorb = i == 1;
                material.SetColor(EnergyColorId, absorb ? absorbEnergyColor : purifyEnergyColor);
                material.SetColor(SecondaryColorId, absorb ? absorbSecondaryColor : purifyEnergyColor);
                material.SetFloat(FocusAmountId, cardEffectFocus[i]);
                material.SetFloat(ConfirmationAmountId, cardEffectConfirmation[i]);
                material.SetFloat(EffectIntensityId, effectIntensity);
                material.SetFloat(EdgeIntensityId, edgeIntensity);
                material.SetFloat(SweepIntensityId, lightSweepIntensity);
                material.SetFloat(ParticleIntensityId, particleIntensity);
                material.SetFloat(PurifyBoostId, purifyEmissionBoost);
                material.SetFloat(PulseSpeedId, effectPulseSpeed);
                material.SetFloat(DistortionId, distortionIntensity);
                material.SetFloat(UnscaledTimeId, Time.unscaledTime);
            }
        }

        private void AnimateCardTransform(int index, bool focused)
        {
            if (cardsRoutine != null || index < 0 || index >= cardTransformRoutines.Length || cardButtons[index] == null)
                return;

            if (cardTransformRoutines[index] != null)
                StopCoroutine(cardTransformRoutines[index]);
            cardTransformRoutines[index] = StartCoroutine(AnimateCardTransformRoutine(index, focused));
        }

        private IEnumerator AnimateCardTransformRoutine(int index, bool focused)
        {
            RectTransform rect = cardButtons[index].transform as RectTransform;
            if (rect == null)
                yield break;

            Vector2 startPosition = rect.anchoredPosition;
            Vector2 targetPosition = cardBasePositions[index] + (focused ? Vector2.up * focusRise : Vector2.zero);
            Vector3 startScale = rect.localScale;
            Vector3 targetScale = Vector3.one * (focused ? focusScale : 1f);
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, focusMoveSeconds);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - normalized, 3f);
                rect.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, eased);
                rect.localScale = Vector3.LerpUnclamped(startScale, targetScale, eased);
                yield return null;
            }

            rect.anchoredPosition = targetPosition;
            rect.localScale = targetScale;
            cardTransformRoutines[index] = null;
        }

        private void SetCardTransformImmediate(int index, bool focused)
        {
            if (index < 0 || index >= cardButtons.Length || cardButtons[index] == null)
                return;

            RectTransform rect = cardButtons[index].transform as RectTransform;
            if (rect == null)
                return;

            rect.anchoredPosition = cardBasePositions[index] + (focused ? Vector2.up * focusRise : Vector2.zero);
            rect.localScale = Vector3.one * (focused ? focusScale : 1f);
        }

        private IEnumerator AnimateCardsIn()
        {
            for (int i = 0; i < 2; i++)
            {
                if (cardButtons[i] != null)
                {
                    RectTransform rect = cardButtons[i].transform as RectTransform;
                    if (rect != null)
                        rect.anchoredPosition = cardBasePositions[i];
                    cardButtons[i].transform.localScale = Vector3.one * cardStartScale;
                }
            }

            for (int i = 0; i < 2; i++)
            {
                Button button = cardButtons[i];
                if (button == null)
                    continue;

                yield return ScaleOverUnscaled(button.transform, cardStartScale, cardOvershootScale, cardPopSeconds);
                yield return ScaleOverUnscaled(button.transform, cardOvershootScale, 1f, cardSettleSeconds);
                if (cardStaggerSeconds > 0f)
                    yield return new WaitForSecondsRealtime(cardStaggerSeconds);
            }
            cardsRoutine = null;

            for (int i = 0; i < 2; i++)
                AnimateCardTransform(i, focusedCardIndex == i);
        }

        private static IEnumerator ScaleOverUnscaled(Transform target, float from, float to, float seconds)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.0001f, seconds);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                target.localScale = Vector3.one * Mathf.LerpUnclamped(from, to, progress);
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        private void CreateCardEffectMaterial(int index)
        {
            Image image = cardImages[index];
            if (image == null)
                return;

            Shader shader = veilCardEffectShader != null
                ? veilCardEffectShader
                : Shader.Find("CyberVeil/UI/Veil Card Energy");
            if (shader == null)
            {
                Debug.LogWarning("Veil card effect shader could not be found. Cards will use their normal artwork.", this);
                return;
            }

            Material material = new Material(shader)
            {
                name = index == 0 ? "Purify Card Energy (Runtime)" : "Absorb Card Energy (Runtime)",
                hideFlags = HideFlags.DontSave
            };
            material.SetFloat(ModeId, index);
            material.SetColor(EnergyColorId, index == 0 ? purifyEnergyColor : absorbEnergyColor);
            material.SetColor(SecondaryColorId, index == 0 ? purifyEnergyColor : absorbSecondaryColor);
            image.material = material;
            cardEffectMaterials[index] = material;
        }

        private void CreateCardInfoRoot(
            Transform parent, int cardIndex, out CanvasGroup group, out RectTransform rootRect)
        {
            GameObject owner = new GameObject(
                "VeilChoiceFocusPresentation", typeof(RectTransform), typeof(CanvasGroup));
            rootRect = owner.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            group = owner.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            GameObject backdropOwner = new GameObject(
                "VeilChoiceDarkBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform backdropRect = backdropOwner.GetComponent<RectTransform>();
            backdropRect.SetParent(rootRect, false);
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = new Vector2(34f, 42f);
            backdropRect.offsetMax = new Vector2(-34f, -44f);
            Image backdrop = backdropOwner.GetComponent<Image>();
            backdrop.color = cardIndex == 1
                ? new Color(0.2470588f, 0.03921569f, 0.05490196f, focusBackdropAlpha)
                : new Color(0.015f, 0.01f, 0.025f, focusBackdropAlpha);
            backdrop.raycastTarget = false;
        }

        private static TMP_Text CreateCardLabel(Transform parent, TMP_FontAsset font)
        {
            GameObject owner = new GameObject("VeilChoiceLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = owner.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(48f, 58f);
            rect.offsetMax = new Vector2(-48f, -58f);
            TextMeshProUGUI text = owner.GetComponent<TextMeshProUGUI>();
            text.fontSize = 25f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 17f;
            text.fontSizeMax = 25f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            if (font != null)
                text.font = font;
            return text;
        }

        private static Image CreateCardIcon(Transform parent)
        {
            GameObject owner = new GameObject("VeilChoiceIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = owner.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -50f);
            rect.sizeDelta = new Vector2(82f, 82f);
            Image icon = owner.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            owner.SetActive(false);
            return icon;
        }

        private static TMP_Text CreateText(
            string name, Transform parent, Vector2 position, Vector2 size, float fontSize, TMP_FontAsset font)
        {
            GameObject owner = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = owner.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TextMeshProUGUI text = owner.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = Color.white;
            text.raycastTarget = false;
            if (font != null)
                text.font = font;
            return text;
        }
    }

    public sealed class VeilChoiceCardFocus : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private UpgradeMenu menu;
        private int cardIndex;
        private bool pointerInside;
        private bool selected;

        public void Initialize(UpgradeMenu owner, int index)
        {
            menu = owner;
            cardIndex = index;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            EventSystem.current?.SetSelectedGameObject(gameObject);
            menu?.FocusCard(cardIndex);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            if (!selected)
                menu?.ReleaseCardFocus(cardIndex);
        }

        public void OnSelect(BaseEventData eventData)
        {
            selected = true;
            menu?.FocusCard(cardIndex);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
            if (!pointerInside)
                menu?.ReleaseCardFocus(cardIndex);
        }

        private void OnDisable()
        {
            pointerInside = false;
            selected = false;
        }
    }
}
