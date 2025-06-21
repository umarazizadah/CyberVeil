using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using CyberVeil.VFX;
using CyberVeil.Systems;

namespace CyberVeil.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerStateMachine))]
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
        public ParticleSystem dashParticles;
        public Camera mainCam;
        [SerializeField] private float dashFOV = 75f; // Temporary FOV boost for dramatic dash fee
        [SerializeField] private float fovLerpSpeed = 5f; // How fast the FOV returns back to normal
        [SerializeField] private float dissolveDuration = 0.3f;

        [Header("References")]
        public DissolveEffectHandler dissolveHandler;
        private PlayerStateMachine playerState; 
        private CharacterController controller;

        private bool isDashing = false;
        private bool canDash = true;
        private float originalFOV;

        public bool IsDashing => isDashing;

        private void Start()
        {
            playerState = GetComponent<PlayerStateMachine>();
            controller = GetComponent<CharacterController>();
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
            if (Keyboard.current?.spaceKey.wasPressedThisFrame == true && canDash && !isDashing && playerState.CurrentState != PlayerState.Attacking)
            {
                StartCoroutine(PerformDash());
            }
        }

        private IEnumerator PerformDash()
        {
            // Locks out further dashing
            isDashing = true;
            canDash = false;
            SoundManager.PlaySound(SoundType.DASH, dashVol);

            // Visuals
            if (dashParticles != null)
            {
                dashParticles.transform.position = transform.position;
                dashParticles.Play();
            }
            if (mainCam != null) mainCam.fieldOfView = dashFOV;
            StartCoroutine(dissolveHandler.DissolveOut(dissolveHandler.dissolveDashMaterial, dissolveDuration));

            // Movement loop
            float timer = 0f;
            Vector3 dashDirection = transform.forward;
            while (timer < dashDuration)
            {
                controller.Move(dashDirection * dashSpeed * Time.deltaTime); // Pushes player forward during dash
                timer += Time.deltaTime;
                yield return null;
            }

            // Post dash cleanup
            StartCoroutine(dissolveHandler.DissolveIn(dissolveHandler.dissolveDashMaterial, dissolveDuration));
            isDashing = false;
            StartCoroutine(DashCooldown());
        }

        private IEnumerator DashCooldown()
        {
            yield return new WaitForSeconds(dashCooldown);
            canDash = true;
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


