using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using CyberVeil.VFX;
using CyberVeil.Systems;
using CyberVeil.Core;

namespace CyberVeil.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(CharacterStateMachine))]
    /// <summary>
    /// Handles the player's dash ability including movement, cooldown, VFX, FOV adjustment, and dissolve effects
    /// </summary>
    public class PlayerDash : MonoBehaviour
    {
        [Header("Dash Settings")]
        [SerializeField] private float dashSpeed = 5f;
        [SerializeField] private float dashDuration = 0.2f;
        [SerializeField] private float dashCooldown = 0.6f;
        [SerializeField] private float dashVol = 0.5f;

        [Header("Visuals")]
        
        public Camera mainCam;
        [SerializeField] private float dashFOV = 75f; // Temporary FOV boost for dramatic dash fee
        [SerializeField] private float fovLerpSpeed = 5f; // How fast the FOV returns back to normal
        [SerializeField] private float dissolveDuration = 0.3f;

        [Header("References")]
        public DissolveEffectHandler dissolveHandler;
        private CharacterStateMachine playerState; 
        private CharacterController controller;
        private PlayerAttack playerAttack;

        private bool isDashing = false;
        private bool canDash = true;
        private float originalFOV;
        private int lastProcessedDashInputFrame = -1;

        public bool IsDashing => isDashing;
        public bool CanDash => canDash;
        public float CooldownProgress { get; private set; } = 1f;
        public Vector3 LastDashDirection { get; private set; } = Vector3.forward;

        public event Action<Vector3> OnDashStarted;
        public event Action<float> OnCooldownProgressChanged;
        public event Action OnDashReady;
        public event Action OnDashRejected;

        private void Start()
        {
            playerState = GetComponent<CharacterStateMachine>();
            controller = GetComponent<CharacterController>();
            playerAttack = GetComponent<PlayerAttack>();
            if (mainCam == null) mainCam = Camera.main;
            originalFOV = mainCam.fieldOfView;
        }

        private void Update()
        {
            UpdateFOV();
            HandleDashInput();
        }

        public void HandleDashInput()
        {
            // Respect cinematic mode: do not allow dashing while a cinematic is active
            if (CinematicCamera.Instance != null && CinematicCamera.Instance.IsActive)
                return;

            if (Keyboard.current?.spaceKey.wasPressedThisFrame != true)
                return;

            // PlayerController also forwards dash input to this component. Latch the press so
            // one accepted blink cannot be mistaken for a rejected second request in the same frame.
            if (lastProcessedDashInputFrame == Time.frameCount)
                return;
            lastProcessedDashInputFrame = Time.frameCount;

            if (!canDash || isDashing || playerState == null)
            {
                OnDashRejected?.Invoke();
                return;
            }

            // A queued slash has not begun and has produced no damage yet. Let dash
            // cancel that future action while the currently playing slash finishes.
            if (playerState.CurrentState == CharacterState.Attacking
                && (playerAttack == null || !playerAttack.TryCancelQueuedAttackForDash()))
            {
                OnDashRejected?.Invoke();
                return;
            }

            StartCoroutine(PerformDash());
        }

        private IEnumerator PerformDash()
        {
            Vector3 dashDirection = transform.forward;
            if (dashDirection.sqrMagnitude > 0.0001f)
                dashDirection.Normalize();
            else
                dashDirection = Vector3.forward;

            playerState.ChangeState(CharacterState.Dashing);

            // Locks out further dashing
            isDashing = true;
            canDash = false;
            LastDashDirection = dashDirection;
            SetCooldownProgress(0f);
            OnDashStarted?.Invoke(LastDashDirection);
            SoundManager.PlaySound(SoundType.DASH, dashVol);

            // Visuals 
            ParticleManager.Instance.PlayEffect(VFXType.Teleport, transform.position, Quaternion.identity);
            if (mainCam != null) mainCam.fieldOfView = dashFOV;
            StartCoroutine(dissolveHandler.DissolveOut(dissolveHandler.dissolveDashMaterial, dissolveDuration));

            // Movement loop
            float timer = 0f;
            while (timer < dashDuration)
            {
                var mods = CyberVeil.Player.PlayerStatsUpgradeManager.Instance;

                float baseDistance = dashSpeed * dashDuration;
                float effectiveDistance = mods ? mods.GetDashDistance(baseDistance) : baseDistance;
                float effectiveSpeed = dashDuration > 0f ? effectiveDistance / dashDuration : dashSpeed;

                controller.Move(dashDirection * effectiveSpeed * Time.deltaTime);
                timer += Time.deltaTime;
                yield return null;
            }

            // Post dash cleanup
            StartCoroutine(dissolveHandler.DissolveIn(dissolveHandler.dissolveDashMaterial, dissolveDuration));
            isDashing = false;
            playerState.ChangeState(CharacterState.Idle);
            StartCoroutine(DashCooldown());
        }

        private IEnumerator DashCooldown()
        {
            float duration = Mathf.Max(0f, dashCooldown);
            if (duration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    yield return null;
                    elapsed += Time.deltaTime;
                    if (elapsed < duration)
                        SetCooldownProgress(elapsed / duration);
                }
            }

            canDash = true;
            SetCooldownProgress(1f);
            OnDashReady?.Invoke();
        }

        private void SetCooldownProgress(float progress)
        {
            CooldownProgress = Mathf.Clamp01(progress);
            OnCooldownProgressChanged?.Invoke(CooldownProgress);
        }

        private void UpdateFOV()
        {
            if (mainCam != null)
            {
                float targetFOV = isDashing ? dashFOV : originalFOV;
                mainCam.fieldOfView = Mathf.Lerp(mainCam.fieldOfView, targetFOV, Time.deltaTime * fovLerpSpeed);
            }
        }
    }
}


