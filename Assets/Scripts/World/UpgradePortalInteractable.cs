using CyberVeil.Core;
using System.Collections;
using UnityEngine;
using CyberVeil.Systems;
using CyberVeil.UI;

namespace CyberVeil.World
{
    [DisallowMultipleComponent]
    /// <summary>
    /// Progression-only portal. Veil upgrades are granted exclusively by spawned shards.
    /// </summary>
    public class UpgradePortalInteractable : MonoBehaviour, IInteractable
    {
        [Header("UI")]
        [SerializeField] private string portalName = "Level Portal";
        [SerializeField] private string prompt = "Enter Portal";
        [SerializeField] private DialogueUI dialogueUI;
        [SerializeField] private string incompleteWavesMessage = "Clear all waves and shape every Veil shard";
        public string Prompt => prompt;

        private NameTag nameTag;

        private Coroutine flow;

        public void Interact(IInteractor interactor) // Called by the PlayerInteractor when the player presses E on this object
        {
            if (flow != null)
                return;

            var promptUI = FindObjectOfType<InteractPromptUI>(true);
            if (promptUI) promptUI.gameObject.SetActive(false);

            flow = StartCoroutine(RunInteraction(interactor));
        }

        private void Awake()
        {
            // Cache optional name tag if present on the prefab and initialize it
            nameTag = GetComponentInChildren<NameTag>(true);
            if (nameTag != null)
            {
                nameTag.Show(false);
            }
        }

        private IEnumerator RunInteraction(IInteractor interactor)
        {
            // Check if all waves are complete
            WaveManager waveManager = FindObjectOfType<WaveManager>();
            if (waveManager != null && !waveManager.AreAllWavesComplete())
            {
                if (dialogueUI != null)
                {
                    dialogueUI.ShowLine(incompleteWavesMessage);
                    yield return new WaitForSeconds(2.5f);
                    dialogueUI.Hide();
                }

                var promptUI = FindObjectOfType<InteractPromptUI>(true);
                if (promptUI) promptUI.gameObject.SetActive(true);

                flow = null;
                yield break;
            }

            bool holdStarted = false;
            if (CinematicCamera.Instance != null)
            {
                CinematicCamera.Instance.StartHoldFocus(transform);
                holdStarted = true;
            }

            try
            {
                yield return new WaitForSecondsRealtime(0.6f);
                if (waveManager != null)
                    waveManager.ContinueAfterUpgrade();
                else if (SceneProgressManager.Instance != null && SceneProgressManager.Instance.HasNextLevel())
                    SceneProgressManager.Instance.LoadNextLevel();
            }
            finally
            {
                if (holdStarted && CinematicCamera.Instance != null)
                {
                    CinematicCamera.Instance.EndHoldFocus();
                }
            }

            var promptUI2 = FindObjectOfType<InteractPromptUI>(true);
            if (promptUI2) promptUI2.gameObject.SetActive(true);

            flow = null;
        }

        public void OnFocus(IInteractor interactor) {
            if (nameTag != null) nameTag.Show(true);
        }
        public void OnDefocus(IInteractor interactor) {
            if (nameTag != null) nameTag.Show(false); 
        }
    }
}
