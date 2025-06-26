using UnityEngine;
using System.Collections;
using CyberVeil.VFX;
namespace CyberVeil.Enemies
{
    /// <summary>
    /// Implements a shield lunge attack effect for the Mushroom enemy
    /// Moves the shield forward and back to simulate an impact animation
    /// </summary>
    public class MushroomShieldAttack : MonoBehaviour, IAttackEffect
    {
        [SerializeField] private Transform shieldTransform;
        [SerializeField] private float lungeDuration = 0.15f;
        [SerializeField] private float returnDelay = 0.05f;

        public IEnumerator PerformEffect(float delay)
        {
            Vector3 original = shieldTransform.localPosition;
            Vector3 lunge = original + shieldTransform.forward * -0.5f;

            // Smooth lunge forward
            yield return MoveShield(shieldTransform, original, lunge, lungeDuration);

            // Short pause to emphasize impact
            yield return new WaitForSeconds(returnDelay);

            // Smooth return
            yield return MoveShield(shieldTransform, lunge, original, lungeDuration);
        }

        /// <summary>
        /// Smoothly moves the shield transform from one position to another over time
        /// </summary>
        private IEnumerator MoveShield(Transform target, Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                target.localPosition = Vector3.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            target.localPosition = to; // Snap at end
        }
    }
}
