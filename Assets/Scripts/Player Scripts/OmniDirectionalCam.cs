using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

        [SerializeField] private float rotationSpeed = 5.0f; // Mouse sensitivity
        [SerializeField] private float minYAngle = -20f; 
        [SerializeField] private float maxYAngle = 60f;

        private Vector3 velocity = Vector3.zero; // Used internally for smoothing
        private float yaw = 0f; // How far around the Y-axis the camera rotates
        private float pitch = 0f; // How far up/down the camera looks 

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
            if (target == null)
                return;

            // Get mouse input
            float mouseX = Input.GetAxis("Mouse X") * rotationSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * rotationSpeed;

            // Update rotation values
            yaw += mouseX;
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, minYAngle, maxYAngle); // Prevent upside-down view

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0); // Convert angles into rotation
           
            Vector3 desiredPosition = target.position + rotation * offset; // Calculate target camera position based on offset

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime); //Smooth follow to avoid jitter

            Vector3 lookTarget = target.position + Vector3.up * 1.5f; // Look at players head (slightly above)
            transform.rotation = Quaternion.LookRotation(lookTarget - transform.position);
        }
    }
}
