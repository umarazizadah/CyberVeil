using UnityEngine;
using CyberVeil.Systems;

namespace CyberVeil.Audio
{
    /// <summary>
    /// Detects object movement and plays footstep sounds at intervals when moving.
    /// </summary>
    public class FootStepDetector : MonoBehaviour
    {
        [SerializeField] private float minimumSpeed = 0.1f; // Minimum speed to trigger footsteps
        [SerializeField] private float stepVolume = 0.4f;
        [SerializeField] private float stepInterval = 0.5f; // Time between step sound changes

        // Stores position from previous frame
        private Vector3 lastPosition; // Movement between frames
        private bool wasMoving = false;
        private float stepTimer = 0f; // Controls time between footstep sounds while moving

        void Start()
        {
            // Stores the current position so it can be compared in the next frame
            lastPosition = transform.position;
        }

        void Update()
        {
            // Calculate current speed
            float currentSpeed = Vector3.Distance(transform.position, lastPosition) / Time.deltaTime; // Measures how far object moved this frame
            bool isMoving = currentSpeed > minimumSpeed;

            // Start footsteps if just started moving
            if (isMoving && !wasMoving)
            {
                SoundManager.PlayWalkingSound(stepVolume);
                stepTimer = stepInterval; // Reset step timer
            }

            // Update step timer if the player is moving
            if (isMoving)
            {
                stepTimer -= Time.deltaTime;
                if (stepTimer <= 0f) // If time is up, change step sound
                {
                    SoundManager.StopWalkingSound();
                    SoundManager.PlayWalkingSound(stepVolume);
                    stepTimer = stepInterval; // Reset step timer
                }
            }
            // Stop footsteps if just stopped moving
            else if (!isMoving && wasMoving)
            {
                SoundManager.StopWalkingSound();
            }

            // Updates state variables for next frame
            wasMoving = isMoving;
            lastPosition = transform.position;
        }
    }
}
