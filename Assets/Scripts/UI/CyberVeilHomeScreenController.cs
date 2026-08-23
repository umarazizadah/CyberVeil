using System.Collections;
using System.Collections.Generic;
using TMPro;
using CyberVeil.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CyberVeil.UI
{
    /// <summary>
    /// Presentation-only controller for the CyberVeil archive home screen.
    /// It animates scene instances and owns the menu flow without modifying gameplay prefabs.
    /// </summary>
    public sealed class CyberVeilHomeScreenController : MonoBehaviour
    {
        [Header("Archive Scene")]
        public Transform archiveRoot;
        public CyberVeilArchiveShardSystem shardSystem;
        public Transform portalAnchor;
        [Tooltip("Optional world-space convergence point used by the ENTER transition.")]
        public Transform collapseTarget;
        public Transform portalFragment;
        public Transform redFragment;
        public Transform[] fragments;
        public Transform[] floatingDebris;
        public Camera menuCamera;
        public Light portalLight;
        public Light corruptionLight;

        [Header("Primary Menu")]
        public CanvasGroup menuGroup;
        public Button[] menuButtons;
        public TMP_Text[] menuLabels;
        public RectTransform menuSelector;

        [Header("Settings")]
        public CanvasGroup settingsGroup;
        public Button volumeDownButton;
        public Button volumeUpButton;
        public Button sensitivityDownButton;
        public Button sensitivityUpButton;
        public Button invertYButton;
        public Button mouseSmoothingButton;
        public Button fullscreenButton;
        public Button settingsBackButton;
        public TMP_Text volumeValueLabel;
        public TMP_Text sensitivityValueLabel;
        public TMP_Text invertYValueLabel;
        public TMP_Text mouseSmoothingValueLabel;
        public TMP_Text fullscreenValueLabel;
        public RectTransform settingsSelector;
        public RectTransform[] settingsRows;

        [Header("Intro and Transition")]
        public CanvasGroup introGroup;
        public TMP_Text introPrompt;
        public CanvasGroup fadeGroup;
        public string levelOneScene = "CyberVeil_Level1";
        [Min(0.1f)] public float introDuration = 3.4f;
        [Min(0.1f)] public float collapseDuration = 1.75f;

        [Header("Motion")]
        public float archiveTurnSpeed = 0.8f;
        public float fragmentFloatHeight = 0.13f;
        public float fragmentFloatSpeed = 0.7f;
        public float focusedFragmentScale = 1.12f;
        public float focusedFragmentTravel = 0.75f;
        public float debrisSpinSpeed = 12f;
        public float portalSpinSpeed = 10f;

        private Vector3[] fragmentPositions;
        private Quaternion[] fragmentRotations;
        private Vector3[] fragmentScales;
        private Vector3[] debrisPositions;
        private Quaternion[] debrisRotations;

        private Vector3 archiveRestScale;
        private Quaternion archiveRestRotation;
        private Vector3 cameraRestPosition;
        private Quaternion cameraRestRotation;
        private Vector3 portalRestScale;
        private float portalRestIntensity;
        private float corruptionRestIntensity;
        private float archiveTargetYaw;
        private float masterVolume;
        private int selectedMenuIndex = -1;
        private int selectedSettingsIndex;
        private int focusedFragmentIndex = -1;
        private int previousFragmentIndex = -1;
        private bool introRunning;
        private bool settingsOpen;
        private bool transitioning;
        private bool collapsing;

        private static readonly Color SelectedColor = new Color(0.96f, 0.82f, 1f, 1f);
        private static readonly Color IdleColor = new Color(0.67f, 0.62f, 0.73f, 0.88f);
        private static readonly Color SelectorColor = new Color(0.79f, 0.16f, 1f, 1f);

        private void Awake()
        {
            Application.runInBackground = true;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            EnsureSettingsRows();
            BindPrimaryMenu();
            BindSettingsMenu();

            if (EventSystem.current != null)
            {
                // Keyboard navigation is handled here so a submit cannot fire twice through uGUI.
                EventSystem.current.sendNavigationEvents = false;
            }
        }

        private void Start()
        {
            if (shardSystem != null)
            {
                shardSystem.SuspendMotion();
            }

            CachePresentationState();
            LoadSettings();
            SetCanvasState(settingsGroup, false);
            SetCanvasState(menuGroup, false);

            if (fadeGroup != null)
            {
                fadeGroup.alpha = 1f;
                fadeGroup.blocksRaycasts = true;
                fadeGroup.interactable = false;
            }

            SetMainSelection(0, false);
            StartCoroutine(PlayIntro());
        }

        private void Update()
        {
            AnimateArchive();

            if (transitioning || introRunning)
            {
                return;
            }

            if (settingsOpen)
            {
                HandleSettingsInput();
            }
            else
            {
                HandlePrimaryInput();
            }
        }

        private void CachePresentationState()
        {
            if (archiveRoot != null)
            {
                archiveRestScale = archiveRoot.localScale;
                archiveRestRotation = archiveRoot.localRotation;
                archiveTargetYaw = archiveRestRotation.eulerAngles.y;
            }

            if (menuCamera != null)
            {
                cameraRestPosition = menuCamera.transform.position;
                cameraRestRotation = menuCamera.transform.rotation;
            }

            fragments = fragments ?? new Transform[0];
            fragmentPositions = new Vector3[fragments.Length];
            fragmentRotations = new Quaternion[fragments.Length];
            fragmentScales = new Vector3[fragments.Length];
            for (int i = 0; i < fragments.Length; i++)
            {
                if (fragments[i] == null)
                {
                    continue;
                }

                fragmentPositions[i] = fragments[i].localPosition;
                fragmentRotations[i] = fragments[i].localRotation;
                fragmentScales[i] = fragments[i].localScale;
            }

            floatingDebris = floatingDebris ?? new Transform[0];
            debrisPositions = new Vector3[floatingDebris.Length];
            debrisRotations = new Quaternion[floatingDebris.Length];
            for (int i = 0; i < floatingDebris.Length; i++)
            {
                if (floatingDebris[i] == null)
                {
                    continue;
                }

                debrisPositions[i] = floatingDebris[i].localPosition;
                debrisRotations[i] = floatingDebris[i].localRotation;
            }

            if (portalAnchor != null)
            {
                portalRestScale = portalAnchor.localScale;
            }

            if (portalLight != null)
            {
                portalRestIntensity = portalLight.intensity;
            }

            if (corruptionLight != null)
            {
                corruptionRestIntensity = corruptionLight.intensity;
            }
        }

        private void BindPrimaryMenu()
        {
            if (menuButtons == null)
            {
                return;
            }

            for (int i = 0; i < menuButtons.Length; i++)
            {
                Button button = menuButtons[i];
                if (button == null)
                {
                    continue;
                }

                int capturedIndex = i;
                CyberVeilMenuPointer pointer = button.GetComponent<CyberVeilMenuPointer>();
                if (pointer == null)
                {
                    pointer = button.gameObject.AddComponent<CyberVeilMenuPointer>();
                }
                pointer.Configure(this, capturedIndex, false);

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => ActivatePrimaryItem(capturedIndex));
            }
        }

        private void BindSettingsMenu()
        {
            ConfigureSettingsButton(volumeDownButton, 0, DecreaseVolume);
            ConfigureSettingsButton(volumeUpButton, 0, IncreaseVolume);
            ConfigureSettingsButton(sensitivityDownButton, 1, DecreaseMouseSensitivity);
            ConfigureSettingsButton(sensitivityUpButton, 1, IncreaseMouseSensitivity);
            ConfigureSettingsButton(invertYButton, 2, ToggleInvertY);
            ConfigureSettingsButton(mouseSmoothingButton, 3, ToggleMouseSmoothing);
            ConfigureSettingsButton(fullscreenButton, 4, ToggleFullscreen);
            ConfigureSettingsButton(settingsBackButton, 5, CloseSettings);
        }

        private void EnsureSettingsRows()
        {
            if (settingsRows != null
                && settingsRows.Length == 6
                && System.Array.TrueForAll(settingsRows, row => row != null))
            {
                return;
            }

            settingsRows = new[]
            {
                GetButtonRow(volumeUpButton),
                GetButtonRow(sensitivityUpButton),
                GetButtonRow(invertYButton),
                GetButtonRow(mouseSmoothingButton),
                GetButtonRow(fullscreenButton),
                GetButtonRow(settingsBackButton)
            };
        }

        private static RectTransform GetButtonRow(Button button)
        {
            if (button == null)
                return null;

            RectTransform buttonRect = button.transform as RectTransform;
            RectTransform parentRect = button.transform.parent as RectTransform;
            bool parentIsSettingRow = parentRect != null
                && parentRect.GetComponent<CanvasGroup>() == null;
            return parentIsSettingRow && buttonRect != null && parentRect != buttonRect
                ? parentRect
                : buttonRect;
        }

        private void ConfigureSettingsButton(Button button, int row, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            CyberVeilMenuPointer pointer = button.GetComponent<CyberVeilMenuPointer>();
            if (pointer == null)
            {
                pointer = button.gameObject.AddComponent<CyberVeilMenuPointer>();
            }
            pointer.Configure(this, row, true);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private IEnumerator PlayIntro()
        {
            introRunning = true;
            SetCanvasState(introGroup, true);

            if (archiveRoot != null)
            {
                archiveRoot.localScale = archiveRestScale * 0.16f;
                archiveRoot.localRotation = archiveRestRotation * Quaternion.Euler(0f, -38f, 0f);
            }

            if (menuCamera != null)
            {
                menuCamera.transform.position = cameraRestPosition + new Vector3(-1.25f, 1.0f, -3.5f);
                menuCamera.transform.rotation = cameraRestRotation * Quaternion.Euler(0f, -2.5f, 0f);
            }

            float elapsed = 0f;
            while (elapsed < introDuration)
            {
                if (elapsed > 0.12f && AnySkipInput())
                {
                    break;
                }

                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / introDuration);
                float eased = Smooth(progress);

                if (archiveRoot != null)
                {
                    archiveRoot.localScale = Vector3.Lerp(archiveRestScale * 0.16f, archiveRestScale, eased);
                    archiveRoot.localRotation = Quaternion.Slerp(
                        archiveRestRotation * Quaternion.Euler(0f, -38f, 0f),
                        archiveRestRotation,
                        eased);
                }

                if (menuCamera != null)
                {
                    menuCamera.transform.position = Vector3.Lerp(
                        cameraRestPosition + new Vector3(-1.25f, 1.0f, -3.5f),
                        cameraRestPosition,
                        eased);
                    menuCamera.transform.rotation = Quaternion.Slerp(
                        cameraRestRotation * Quaternion.Euler(0f, -2.5f, 0f),
                        cameraRestRotation,
                        eased);
                }

                if (fadeGroup != null)
                {
                    fadeGroup.alpha = 1f - Mathf.Clamp01(progress / 0.34f);
                }

                if (introGroup != null)
                {
                    float tailFade = 1f - Mathf.Clamp01((progress - 0.72f) / 0.28f);
                    introGroup.alpha = tailFade * (0.72f + Mathf.Sin(Time.unscaledTime * 3.2f) * 0.18f);
                }

                yield return null;
            }

            if (archiveRoot != null)
            {
                archiveRoot.localScale = archiveRestScale;
                archiveRoot.localRotation = archiveRestRotation;
                archiveTargetYaw = archiveRestRotation.eulerAngles.y;
            }

            if (menuCamera != null)
            {
                menuCamera.transform.position = cameraRestPosition;
                menuCamera.transform.rotation = cameraRestRotation;
            }

            if (fadeGroup != null)
            {
                fadeGroup.alpha = 0f;
                fadeGroup.blocksRaycasts = false;
            }

            SetCanvasState(introGroup, false);
            introRunning = false;

            if (menuGroup != null)
            {
                menuGroup.alpha = 0f;
                menuGroup.blocksRaycasts = true;
                menuGroup.interactable = true;
                float menuFade = 0f;
                while (menuFade < 1f)
                {
                    menuFade += Time.unscaledDeltaTime / 0.45f;
                    menuGroup.alpha = Smooth(Mathf.Clamp01(menuFade));
                    yield return null;
                }
                menuGroup.alpha = 1f;
            }

            SelectCurrentButton();
            if (shardSystem != null)
            {
                shardSystem.BeginIdle();
            }
            FocusRandomFragment();
        }

        private void AnimateArchive()
        {
            if (shardSystem != null)
            {
                return;
            }

            if (archiveRoot == null)
            {
                return;
            }

            float time = Time.unscaledTime;
            float delta = Time.unscaledDeltaTime;

            if (!introRunning && !collapsing)
            {
                Quaternion targetRotation = Quaternion.Euler(
                    archiveRestRotation.eulerAngles.x,
                    archiveTargetYaw,
                    archiveRestRotation.eulerAngles.z);
                archiveRoot.localRotation = Quaternion.Slerp(
                    archiveRoot.localRotation,
                    targetRotation,
                    1f - Mathf.Exp(-archiveTurnSpeed * delta));
            }

            if (!collapsing && fragmentPositions != null)
            {
                for (int i = 0; i < fragments.Length; i++)
                {
                    Transform fragment = fragments[i];
                    if (fragment == null)
                    {
                        continue;
                    }

                    float phase = i * 1.31f;
                    float bob = Mathf.Sin(time * fragmentFloatSpeed + phase) * fragmentFloatHeight;
                    Vector3 focusOffset = i == focusedFragmentIndex
                        ? new Vector3(0f, 0f, -focusedFragmentTravel)
                        : Vector3.zero;
                    Vector3 targetPosition = fragmentPositions[i] + focusOffset + Vector3.up * bob;
                    Quaternion targetFragmentRotation = fragmentRotations[i] * Quaternion.Euler(
                        Mathf.Sin(time * 0.31f + phase) * 1.5f,
                        Mathf.Sin(time * 0.22f + phase) * 2.2f,
                        Mathf.Cos(time * 0.27f + phase) * 1.1f);
                    Vector3 targetScale = fragmentScales[i] * (i == focusedFragmentIndex ? focusedFragmentScale : 1f);

                    fragment.localPosition = Vector3.Lerp(fragment.localPosition, targetPosition, 1f - Mathf.Exp(-2.1f * delta));
                    fragment.localRotation = Quaternion.Slerp(fragment.localRotation, targetFragmentRotation, 1f - Mathf.Exp(-1.3f * delta));
                    fragment.localScale = Vector3.Lerp(fragment.localScale, targetScale, 1f - Mathf.Exp(-2.2f * delta));
                }
            }

            if (!collapsing && debrisPositions != null)
            {
                for (int i = 0; i < floatingDebris.Length; i++)
                {
                    Transform debris = floatingDebris[i];
                    if (debris == null)
                    {
                        continue;
                    }

                    float phase = i * 0.83f;
                    debris.localPosition = debrisPositions[i] + Vector3.up * Mathf.Sin(time * 0.8f + phase) * 0.09f;
                    debris.localRotation = debrisRotations[i] * Quaternion.Euler(
                        time * debrisSpinSpeed * (0.55f + i * 0.08f),
                        time * debrisSpinSpeed * (0.35f + i * 0.05f),
                        time * debrisSpinSpeed * 0.22f);
                }
            }

            if (!collapsing && portalAnchor != null)
            {
                float portalPulse = 1f + Mathf.Sin(time * 2.15f) * 0.045f;
                portalAnchor.localScale = portalRestScale * portalPulse;
                portalAnchor.Rotate(Vector3.up, portalSpinSpeed * delta, Space.Self);
            }

            if (!collapsing && portalLight != null)
            {
                portalLight.intensity = portalRestIntensity * (0.9f + Mathf.Sin(time * 2.4f) * 0.1f);
            }

            if (corruptionLight != null)
            {
                corruptionLight.intensity = corruptionRestIntensity * (0.77f + Mathf.Pow((Mathf.Sin(time * 1.65f) + 1f) * 0.5f, 3f) * 0.34f);
            }
        }

        private void HandlePrimaryInput()
        {
            if (PressedUp())
            {
                SetMainSelection(Wrap(selectedMenuIndex - 1, menuButtons.Length), true);
            }
            else if (PressedDown())
            {
                SetMainSelection(Wrap(selectedMenuIndex + 1, menuButtons.Length), true);
            }

            if (PressedSubmit())
            {
                ActivatePrimaryItem(selectedMenuIndex);
            }
        }

        private void HandleSettingsInput()
        {
            if (PressedCancel())
            {
                CloseSettings();
                return;
            }

            if (PressedUp())
            {
                SetSettingsSelection(Wrap(selectedSettingsIndex - 1, settingsRows.Length));
            }
            else if (PressedDown())
            {
                SetSettingsSelection(Wrap(selectedSettingsIndex + 1, settingsRows.Length));
            }

            if (selectedSettingsIndex == 0 || selectedSettingsIndex == 1)
            {
                if (PressedLeft())
                {
                    if (selectedSettingsIndex == 0)
                        DecreaseVolume();
                    else
                        DecreaseMouseSensitivity();
                }
                else if (PressedRight() || PressedSubmit())
                {
                    if (selectedSettingsIndex == 0)
                        IncreaseVolume();
                    else
                        IncreaseMouseSensitivity();
                }
            }
            else if (PressedSubmit())
            {
                switch (selectedSettingsIndex)
                {
                    case 2:
                        ToggleInvertY();
                        break;
                    case 3:
                        ToggleMouseSmoothing();
                        break;
                    case 4:
                        ToggleFullscreen();
                        break;
                    default:
                        CloseSettings();
                        break;
                }
            }
        }

        public void SetMainSelection(int index, bool rotateArchive)
        {
            if (menuButtons == null || menuButtons.Length == 0 || transitioning)
            {
                return;
            }

            int previousIndex = selectedMenuIndex;
            index = Wrap(index, menuButtons.Length);
            bool changed = selectedMenuIndex != index;
            selectedMenuIndex = index;
            UpdatePrimaryVisuals();
            SelectCurrentButton();

            if (changed && previousIndex >= 0)
            {
                SoundManager.PlaySound(SoundType.HOMEUISELECT, 0.18f);
                SoundManager.PlaySound(SoundType.HOMEUISELECT2, 0.8f);
            }

            if (changed && rotateArchive && !introRunning)
            {
                FocusRandomFragment();
            }
        }

        public void SetSettingsSelection(int index)
        {
            if (!settingsOpen || settingsRows == null || settingsRows.Length == 0)
            {
                return;
            }

            int previousIndex = selectedSettingsIndex;
            selectedSettingsIndex = Wrap(index, settingsRows.Length);
            UpdateSettingsVisuals();

            if (previousIndex != selectedSettingsIndex && previousIndex >= 0)
            {
               SoundManager.PlaySound(SoundType.HOMEUISELECT, 0.18f);
               SoundManager.PlaySound(SoundType.HOMEUISELECT2, 0.8f);
            }
        }


        private void UpdatePrimaryVisuals()
        {
            if (menuLabels != null)
            {
                for (int i = 0; i < menuLabels.Length; i++)
                {
                    if (menuLabels[i] == null)
                    {
                        continue;
                    }

                    bool selected = i == selectedMenuIndex;
                    menuLabels[i].color = selected ? SelectedColor : IdleColor;
                    menuLabels[i].fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
                    menuLabels[i].characterSpacing = selected ? 5f : 2f;
                }
            }

            if (menuSelector != null && selectedMenuIndex >= 0 && selectedMenuIndex < menuButtons.Length)
            {
                RectTransform selectedRect = menuButtons[selectedMenuIndex].transform as RectTransform;
                if (selectedRect != null)
                {
                    Vector2 position = menuSelector.anchoredPosition;
                    position.y = selectedRect.anchoredPosition.y;
                    menuSelector.anchoredPosition = position;
                }

                Image image = menuSelector.GetComponent<Image>();
                if (image != null)
                {
                    image.color = SelectorColor;
                }
            }
        }

        private void UpdateSettingsVisuals()
        {
            if (settingsSelector != null && settingsRows != null && selectedSettingsIndex < settingsRows.Length)
            {
                Vector2 position = settingsSelector.anchoredPosition;
                position.y = settingsRows[selectedSettingsIndex].anchoredPosition.y;
                settingsSelector.anchoredPosition = position;
            }
        }

        private void FocusRandomFragment()
        {
            if (shardSystem != null)
            {
                focusedFragmentIndex = shardSystem.FocusRandomShard();
                previousFragmentIndex = focusedFragmentIndex;
                return;
            }

            if (fragments == null || fragments.Length == 0)
            {
                return;
            }

            previousFragmentIndex = focusedFragmentIndex;
            if (fragments.Length == 1)
            {
                focusedFragmentIndex = 0;
            }
            else
            {
                int candidate = previousFragmentIndex;
                int guard = 0;
                while (candidate == previousFragmentIndex && guard++ < 12)
                {
                    candidate = Random.Range(0, fragments.Length);
                }
                focusedFragmentIndex = candidate;
            }

            float direction = Random.value > 0.5f ? 1f : -1f;
            // Keep the archive's response readable from every menu item instead of
            // accumulating turns that can eventually push fragments out of frame.
            archiveTargetYaw = archiveRestRotation.eulerAngles.y + Random.Range(8f, 13f) * direction;
        }

        private void ActivatePrimaryItem(int index)
        {
            if (transitioning || introRunning)
            {
                return;
            }

            SetMainSelection(index, false);
            switch (index)
            {
                case 0:
                    VeilRunManager.ResetForNewRun();
                    SoundManager.PlaySound(SoundType.HOMEUIENTER, 0.3f);
                    SoundManager.PlaySound(SoundType.TRIALEND, 0.4f);
                    StartCoroutine(CollapseArchiveAndLoad());
                    break;
                case 1:
                    OpenSettings();
                    break;
                case 2:
                    LeaveGame();
                    break;
            }
        }

        private void OpenSettings()
        {
            settingsOpen = true;
            SetCanvasState(menuGroup, false);
            SetCanvasState(settingsGroup, true);
            SetSettingsSelection(0);

            if (EventSystem.current != null && volumeUpButton != null)
            {
                EventSystem.current.SetSelectedGameObject(volumeUpButton.gameObject);
            }
        }

        public void CloseSettings()
        {
            if (!settingsOpen)
            {
                return;
            }

            settingsOpen = false;
            SetCanvasState(settingsGroup, false);
            SetCanvasState(menuGroup, true);
            SelectCurrentButton();
        }

        private void LoadSettings()
        {
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("CyberVeil.MasterVolume", 0.75f));
            AudioListener.volume = masterVolume;
            CameraSettings.Reload();
            UpdateSettingsLabels();
        }

        public void DecreaseVolume()
        {
            SetVolume(masterVolume - 0.1f);
        }

        public void IncreaseVolume()
        {
            SetVolume(masterVolume + 0.1f);
        }

        private void SetVolume(float value)
        {
            masterVolume = Mathf.Round(Mathf.Clamp01(value) * 10f) / 10f;
            AudioListener.volume = masterVolume;
            PlayerPrefs.SetFloat("CyberVeil.MasterVolume", masterVolume);
            PlayerPrefs.Save();
            UpdateSettingsLabels();
        }

        public void ToggleFullscreen()
        {
            Screen.fullScreen = !Screen.fullScreen;
            UpdateSettingsLabels();
        }

        public void DecreaseMouseSensitivity()
        {
            CameraSettings.SetSensitivity(
                CameraSettings.Sensitivity - CameraSettings.SensitivityStep);
            UpdateSettingsLabels();
        }

        public void IncreaseMouseSensitivity()
        {
            CameraSettings.SetSensitivity(
                CameraSettings.Sensitivity + CameraSettings.SensitivityStep);
            UpdateSettingsLabels();
        }

        public void ToggleInvertY()
        {
            CameraSettings.SetInvertY(!CameraSettings.InvertY);
            UpdateSettingsLabels();
        }

        public void ToggleMouseSmoothing()
        {
            CameraSettings.SetSmoothingEnabled(!CameraSettings.SmoothingEnabled);
            UpdateSettingsLabels();
        }

        private void UpdateSettingsLabels()
        {
            if (volumeValueLabel != null)
            {
                volumeValueLabel.text = Mathf.RoundToInt(masterVolume * 100f) + "%";
            }

            if (sensitivityValueLabel != null)
            {
                sensitivityValueLabel.text = CameraSettings.SensitivityPercent + "%";
            }

            if (invertYValueLabel != null)
            {
                invertYValueLabel.text = CameraSettings.InvertY ? "ON" : "OFF";
            }

            if (mouseSmoothingValueLabel != null)
            {
                mouseSmoothingValueLabel.text = CameraSettings.SmoothingEnabled ? "ON" : "OFF";
            }

            if (fullscreenValueLabel != null)
            {
                fullscreenValueLabel.text = Screen.fullScreen ? "ON" : "OFF";
            }
        }

        private IEnumerator CollapseArchiveAndLoad()
        {
            transitioning = true;
            collapsing = true;
            SetCanvasInteraction(menuGroup, false);
            SetCanvasInteraction(settingsGroup, false);

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            if (shardSystem != null)
            {
                yield return shardSystem.CollapseIntoPortal();
            }
            else
            {

            List<Transform> collapseItems = new List<Transform>();
            for (int i = 0; i < fragments.Length; i++)
            {
                Transform fragment = fragments[i];
                if (fragment != null && fragment != portalFragment)
                {
                    collapseItems.Add(fragment);
                }
            }
            for (int i = 0; i < floatingDebris.Length; i++)
            {
                if (floatingDebris[i] != null)
                {
                    collapseItems.Add(floatingDebris[i]);
                }
            }

            Vector3[] positions = new Vector3[collapseItems.Count];
            Quaternion[] rotations = new Quaternion[collapseItems.Count];
            Vector3[] scales = new Vector3[collapseItems.Count];
            for (int i = 0; i < collapseItems.Count; i++)
            {
                positions[i] = collapseItems[i].position;
                rotations[i] = collapseItems[i].rotation;
                scales[i] = collapseItems[i].localScale;
            }

            Vector3 target = collapseTarget != null
                ? collapseTarget.position
                : (portalAnchor != null ? portalAnchor.position : archiveRoot.position);
            float elapsed = 0f;
            while (elapsed < collapseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / collapseDuration);
                float eased = Smooth(progress);

                for (int i = 0; i < collapseItems.Count; i++)
                {
                    Transform item = collapseItems[i];
                    float stagger = Mathf.Clamp01((progress - i * 0.025f) / 0.78f);
                    float itemEase = Smooth(stagger);
                    Vector3 arc = Vector3.up * Mathf.Sin(itemEase * Mathf.PI) * (0.35f + i * 0.025f);
                    item.position = Vector3.Lerp(positions[i], target, itemEase) + arc;
                    item.rotation = Quaternion.Slerp(rotations[i], Quaternion.Euler(0f, 720f * itemEase, 180f * itemEase), itemEase);
                    item.localScale = Vector3.Lerp(scales[i], Vector3.zero, itemEase);
                }

                if (menuGroup != null)
                {
                    menuGroup.alpha = 1f - Mathf.Clamp01(progress / 0.48f);
                }

                if (portalAnchor != null)
                {
                    portalAnchor.localScale = portalRestScale * (1f + eased * 0.7f + Mathf.Sin(elapsed * 9f) * 0.05f);
                }

                if (portalLight != null)
                {
                    portalLight.intensity = portalRestIntensity * (1f + eased * 2.5f);
                }

                yield return null;
            }

            }

            float fadeElapsed = 0f;
            if (fadeGroup != null)
            {
                fadeGroup.blocksRaycasts = true;
            }
            while (fadeElapsed < 0.65f)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                if (fadeGroup != null)
                {
                    fadeGroup.alpha = Smooth(Mathf.Clamp01(fadeElapsed / 0.65f));
                }
                yield return null;
            }

            AsyncOperation load = SceneManager.LoadSceneAsync(levelOneScene, LoadSceneMode.Single);
            if (load == null)
            {
                transitioning = false;
                Debug.LogError("[CyberVeil Home] Could not load scene: " + levelOneScene);
                yield break;
            }

            while (!load.isDone)
            {
                yield return null;
            }
        }

        private void LeaveGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SelectCurrentButton()
        {
            if (EventSystem.current != null && menuButtons != null && selectedMenuIndex >= 0 && selectedMenuIndex < menuButtons.Length)
            {
                EventSystem.current.SetSelectedGameObject(menuButtons[selectedMenuIndex].gameObject);
            }
        }

        private static void SetCanvasState(CanvasGroup group, bool visible)
        {
            if (group == null)
            {
                return;
            }

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private static void SetCanvasInteraction(CanvasGroup group, bool enabled)
        {
            if (group == null)
            {
                return;
            }

            group.interactable = enabled;
            group.blocksRaycasts = enabled;
        }

        private static int Wrap(int value, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            return (value % count + count) % count;
        }

        private static float Smooth(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static bool AnySkipInput()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad gamepad = Gamepad.current;
            return (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                || (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
                || (gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame));
        }

        private static bool PressedUp()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            return (keyboard != null && (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame))
                || (gamepad != null && gamepad.dpad.up.wasPressedThisFrame);
        }

        private static bool PressedDown()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            return (keyboard != null && (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame))
                || (gamepad != null && gamepad.dpad.down.wasPressedThisFrame);
        }

        private static bool PressedLeft()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            return (keyboard != null && (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame))
                || (gamepad != null && gamepad.dpad.left.wasPressedThisFrame);
        }

        private static bool PressedRight()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            return (keyboard != null && (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame))
                || (gamepad != null && gamepad.dpad.right.wasPressedThisFrame);
        }

        private static bool PressedSubmit()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            return (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
                || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
        }

        private static bool PressedCancel()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                || (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
        }
    }

    /// <summary>
    /// Small bridge that makes pointer hover use the same selection path as keyboard input.
    /// </summary>
    public sealed class CyberVeilMenuPointer : MonoBehaviour, IPointerEnterHandler
    {
        private CyberVeilHomeScreenController controller;
        private int index;
        private bool settingsItem;

        public void Configure(CyberVeilHomeScreenController owner, int itemIndex, bool isSettingsItem)
        {
            controller = owner;
            index = itemIndex;
            settingsItem = isSettingsItem;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (controller == null)
            {
                return;
            }

            if (settingsItem)
            {
                controller.SetSettingsSelection(index);
            }
            else
            {
                controller.SetMainSelection(index, true);
            }
        }
    }
}
