using System.Collections;
using CyberVeil.Core;
using CyberVeil.Player;
using CyberVeil.Systems;
using CyberVeil.UI;
using CyberVeil.VFX;
using UnityEngine;

namespace CyberVeil.World
{
    /// <summary>
    /// Runtime interaction attached to the project-owned VeilShardUpgrade visual prefab.
    /// The prefab remains presentation-only and keeps its existing GUID and hierarchy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VeilShardInteractable : MonoBehaviour, IInteractable
    {
        public string Prompt => "Shape the Veil";

        private WaveManager waveManager;
        private int rewardWaveIndex;
        private VeilUpgradePair upgradePair;
        private Collider interactionCollider;
        private bool ready;
        private bool interactionInProgress;

        public void Initialize(
            WaveManager owner,
            int waveIndex,
            VeilUpgradePair pair,
            float settleSeconds,
            int interactableLayer)
        {
            waveManager = owner;
            rewardWaveIndex = waveIndex;
            upgradePair = pair;
            SetLayerRecursively(gameObject, interactableLayer);
            PrepareCollider();
            StartCoroutine(SettleRoutine(Mathf.Max(0f, settleSeconds)));
        }

        public void Interact(IInteractor interactor)
        {
            if (!ready || interactionInProgress)
                return;

            interactionInProgress = true;
            StartCoroutine(ChoiceRoutine(interactor));
        }

        public void OnFocus(IInteractor interactor) { }
        public void OnDefocus(IInteractor interactor) { }

        private void PrepareCollider()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            if (colliders.Length > 0)
            {
                interactionCollider = colliders[0];
                for (int i = 0; i < colliders.Length; i++)
                {
                    colliders[i].isTrigger = true;
                    colliders[i].enabled = false;
                }
                return;
            }

            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.15f;
            sphere.isTrigger = true;
            sphere.enabled = false;
            interactionCollider = sphere;
        }

        private IEnumerator SettleRoutine(float seconds)
        {
            Vector3 target = transform.position;
            Vector3 start = target + Vector3.up * 2f;
            transform.position = start;

            if (ParticleManager.Instance != null)
                ParticleManager.Instance.PlayEffect(VFXType.Teleport, target, transform.rotation);

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                float t = seconds <= 0f ? 1f : Mathf.Clamp01(elapsed / seconds);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                transform.position = Vector3.LerpUnclamped(start, target, eased);
                yield return null;
            }

            transform.position = target;
            if (interactionCollider != null)
                interactionCollider.enabled = true;
            ready = true;
        }

        private IEnumerator ChoiceRoutine(IInteractor interactor)
        {
            if (interactor is PlayerInteractor playerInteractor)
                playerInteractor.HidePrompt();

            bool cameraHold = CinematicCamera.Instance != null;
            if (cameraHold)
                CinematicCamera.Instance.StartHoldFocus(transform);

            yield return new WaitForSecondsRealtime(0.45f);

            UpgradeMenu menu = UpgradeMenu.Instance;
            if (menu == null)
            {
                Debug.LogError("Veil shard could not find the Veil choice menu.", this);
                interactionInProgress = false;
                if (cameraHold && CinematicCamera.Instance != null)
                    CinematicCamera.Instance.EndHoldFocus();
                yield break;
            }

            yield return menu.ShowAndWait(upgradePair);

            if (cameraHold && CinematicCamera.Instance != null)
                CinematicCamera.Instance.EndHoldFocus();

            VeilUpgradeSelection selection = menu.LastSelection;
            if (selection == null)
            {
                Debug.LogError("Veil shard choice menu closed without a committed selection.", this);
                interactionInProgress = false;
                yield break;
            }

            if (selection.ChoiceKind == VeilChoiceKind.Absorb)
            {
                ScreenShake.KickSubtle();
                SoundManager.PlaySound(SoundType.TRIALEND, 0.55f);
            }
            else
            {
                SoundManager.PlaySound(SoundType.CARDCLICK, 0.45f);
            }

            waveManager?.NotifyVeilShardChoiceResolved(rewardWaveIndex, this);
            ready = false;

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            yield return CollapseRoutine();
            Destroy(gameObject);
        }

        private IEnumerator CollapseRoutine()
        {
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;
            const float seconds = 0.32f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t * t);
                yield return null;
            }
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            for (int i = 0; i < target.transform.childCount; i++)
                SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
        }
    }
}
