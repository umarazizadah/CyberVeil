using System.Collections;
using UnityEngine;
using CyberVeil.UI;
using CyberVeil.Systems;

namespace CyberVeil.World
{
    /// <summary>
    /// Listens for the end-of-trial upgrade phase and shows a short prompt
    /// using the same InteractPromptUI used by NPCs and the portal.
    /// Shows "Head to the portal" for a short duration.
    /// </summary>
    [DisallowMultipleComponent]
    public class UpgradePromptUI : MonoBehaviour
    {
    [Tooltip("How long (seconds) the \"Head to the portal\" prompt stays visible")]
        [SerializeField] private float visibleSeconds = 8f;

        private Coroutine hideCoroutine;

        private void OnEnable()
        {
            WaveManager.OnUpgradePhaseStarted += HandleUpgradePhaseStarted;
        }

        private void OnDisable()
        {
            WaveManager.OnUpgradePhaseStarted -= HandleUpgradePhaseStarted;
        }

        private void HandleUpgradePhaseStarted(int trialIndex)
        {
            // Show the centralized InteractPromptUI with the message
            var prompt = FindObjectOfType<InteractPromptUI>(true);
            if (prompt == null) return;

            prompt.Show("Enter the portal");

            // Cancel any existing hide coroutine and start a new one
            if (hideCoroutine != null) StopCoroutine(hideCoroutine);
            hideCoroutine = StartCoroutine(HideAfterDelay(prompt, visibleSeconds));
        }

        private IEnumerator HideAfterDelay(InteractPromptUI prompt, float delay)
        {
            yield return new WaitForSeconds(delay);
            prompt.Hide();
            hideCoroutine = null;
        }
    }
}
