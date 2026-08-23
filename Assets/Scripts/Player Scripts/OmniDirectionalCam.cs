using UnityEngine;
using UnityEngine.InputSystem;
using CyberVeil.Systems;

namespace CyberVeil.Player
{
    /// <summary>
    /// A 360 player follow camera with mouse controlled orbiting, smooths camera motion and clamps vertical angle
    /// </summary>
    public class OmniDirectionalCam : MonoBehaviour
    {
        [SerializeField] private Transform target; // The object (player) the camera follows
        [SerializeField] private float smoothTime = 0.2f; // Delay in camera following
        [SerializeField] private Vector3 offset = new Vector3(0, 3, -5); // Offset from the target (so camera is behind and above player)

        [Header("Mouse Look")]
        [SerializeField, Min(0f)] private float smoothingSharpness = 24f;
        [SerializeField, Min(1f)] private float maximumMouseDelta = 500f;
        [SerializeField] private float minYAngle = -20f; 
        [SerializeField] private float maxYAngle = 60f;

        private Vector3 velocity = Vector3.zero; // Used internally for smoothing
        private float yaw = 0f; // How far around the Y-axis the camera rotates
        private float pitch = 0f; // How far up/down the camera looks 
        private Vector2 smoothedAngularVelocity;
        private float mouseSensitivity;
        private bool invertY;
        private bool smoothingEnabled;

        private void OnEnable()
        {
            CameraSettings.Changed += RefreshCameraSettings;
            RefreshCameraSettings();
        }

        private void OnDisable()
        {
            CameraSettings.Changed -= RefreshCameraSettings;
            smoothedAngularVelocity = Vector2.zero;
        }

        void Start()
        {
            // Set initial pitch/yaw to match camera's current rotation
            if (target != null)
            {
                yaw = transform.eulerAngles.y;
                pitch = transform.eulerAngles.x;
            }

            // Lock cursor for gameplay immersion
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void LateUpdate()
        {
            // If a cinematic is active, do not process mouse input or change the camera transform
            if (CinematicCamera.Instance != null && CinematicCamera.Instance.IsActive)
                return;

            if (target == null)
                return;

            Vector2 angularDelta = ReadMouseAngularDelta();

            // Update rotation values
            yaw += angularDelta.x;
            pitch += angularDelta.y;
            pitch = Mathf.Clamp(pitch, minYAngle, maxYAngle); // Prevent upside-down view

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0); // Convert angles into rotation
           
            Vector3 desiredPosition = target.position + rotation * offset; // Calculate target camera position based on offset

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime); //Smooth follow to avoid jitter

            Vector3 lookTarget = target.position + Vector3.up * 1.5f; // Look at players head (slightly above)
            transform.rotation = Quaternion.LookRotation(lookTarget - transform.position);
        }

        private Vector2 ReadMouseAngularDelta()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return Vector2.zero;

            Vector2 rawDelta = Vector2.ClampMagnitude(
                mouse.delta.ReadValue(),
                maximumMouseDelta);

            float verticalSign = invertY ? 1f : -1f;
            Vector2 angularDelta = new Vector2(
                rawDelta.x * mouseSensitivity,
                rawDelta.y * mouseSensitivity * verticalSign);

            if (!smoothingEnabled || smoothingSharpness <= 0f)
            {
                smoothedAngularVelocity = Vector2.zero;
                return angularDelta;
            }

            float deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= 0f)
                return Vector2.zero;

            Vector2 targetAngularVelocity = angularDelta / deltaTime;
            float blend = 1f - Mathf.Exp(-smoothingSharpness * deltaTime);
            smoothedAngularVelocity = Vector2.Lerp(
                smoothedAngularVelocity,
                targetAngularVelocity,
                blend);
            return smoothedAngularVelocity * deltaTime;
        }

        private void RefreshCameraSettings()
        {
            mouseSensitivity = CameraSettings.Sensitivity;
            invertY = CameraSettings.InvertY;
            smoothingEnabled = CameraSettings.SmoothingEnabled;
            smoothedAngularVelocity = Vector2.zero;
        }
    }
}
