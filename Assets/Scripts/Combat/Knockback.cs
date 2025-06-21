using UnityEngine;
namespace CyberVeil.Combat
{
    // handles knockback logic for any GameObject it's attached to
    public class Knockback : MonoBehaviour, IKnockbackable
    {
        [Header("Knockback Settings")]
        [SerializeField] private float knockbackForce = 1f; // how far the object gets knocked back                                                       
        [SerializeField] private float directionInfluence = 0.7f; // How much the attacker's facing direction influences the knockback (vs. straight-away)

        /// <summary>
        /// Applies knockback to this GameObject based on the attacker's position and facing direction.
        /// </summary>
        /// <param name="attacker">The transform of the attacker (usually the player or enemy)</param>
        public void ApplyKnockback(Transform attacker)
        {
            Vector3 attackForward = attacker.forward; // Gets direction the attacker is facing                                                      
            Vector3 awayFromAttacker = (transform.position - attacker.position).normalized; // Gets the direction away from the attacker (from attacker to this object)

            // Blend the two directions based on directionInfluence (0 = direct away, 1 = attacker forward)
            Vector3 finalDirection = Vector3.Lerp(awayFromAttacker, attackForward, directionInfluence);
            finalDirection.y = 0; // Ensures the knockback is horizontal, no vertical movemenet

            transform.position += finalDirection * knockbackForce; // Moves the object in the final knockback direction by the force ammt
        }
    }
}