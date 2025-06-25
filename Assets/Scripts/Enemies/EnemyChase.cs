using UnityEngine;
namespace CyberVeil.Enemies
{
    /// <summary>
    /// Controls enemy chasing behavior when in the Chase AI state
    /// Moves the enemy toward the player and smoothly rotates to face them
    /// </summary>
    [RequireComponent(typeof(EnemyAIController))]
    public class EnemyChase : MonoBehaviour
    {
        [Header("Chase Settings")]
        public float chaseSpeed = 1.8f;
        public float rotationSpeed = 8f;

        private EnemyAIController enemyAI;
        private Transform player;

        private void Start()
        {
            enemyAI = GetComponent<EnemyAIController>();
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        private void Update()
        {
            if (enemyAI.currentAIState != EnemyAIState.Chase || player == null) return;

            // Move forward toward the player
            Vector3 direction = (player.position - transform.position).normalized;
            Vector3 move = new Vector3(direction.x, 0f, direction.z);
            transform.position += move * chaseSpeed * Time.deltaTime;

            // Smooth rotation toward the player
            Quaternion targetRotation = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        }
    }
}
