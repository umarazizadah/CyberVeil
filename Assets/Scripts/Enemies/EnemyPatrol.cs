
using UnityEngine;

namespace CyberVeil.Enemies
{
    [System.Serializable]
    public class PatrolPoint // Custom patrol point, where the enemy will move to and how long it stays at the point
    {
        public Transform point;
        public float waitTime = 1.5f;
    }

    [RequireComponent(typeof(EnemyAIController))]
    /// <summary>
    /// Controls enemy patrol movement between a series of patrol points
    /// Integrates with the enemy AI state controller and handles movement, facing, and wait logic
    /// </summary>
    public class EnemyPatrol : MonoBehaviour
    {
        [Header("Patrol Settings")]
        [SerializeField] private PatrolPoint[] patrolPoints;
        [SerializeField] private float patrolSpeed = 2f;
        [SerializeField] private float reachThreshold = 0.3f; // How close the enemy has to get to the patrol point

        private int currentPointIndex = 0; // Keeps track of which patrol point the enemy is moving to 
        private float waitTimer = 0f;
        public bool waiting = false;

        private EnemyAIController enemyAI;

        private void Start()
        {
            enemyAI = GetComponent<EnemyAIController>();
        }
        private void Update()
        {
            if (patrolPoints == null || patrolPoints.Length == 0 || enemyAI.currentAIState != EnemyAIState.Patrol)
                return;

            if (waiting) // When wait timer reaches 0, enemy goes to next point
            {
                waitTimer -= Time.deltaTime;

                if (waitTimer <= 0f)
                {
                    waiting = false;
                    GoToNextPoint();
                }
                return;
            }
            Transform target = patrolPoints[currentPointIndex].point;
            Vector3 direction = (target.position - transform.position).normalized;

            // Move toward the target
            transform.position += direction * patrolSpeed * Time.deltaTime;

            // Face the target (without tilting up/down)
            Vector3 lookAtTarget = new Vector3(target.position.x, transform.position.y, target.position.z);
            transform.LookAt(lookAtTarget);

            // If close enough, begin waiting (Arrival check)
            if (Vector3.Distance(transform.position, target.position) < reachThreshold)
            {
                waiting = true;
                waitTimer = patrolPoints[currentPointIndex].waitTime;
            }
        }

        private void GoToNextPoint() // Increments poiint index and loops back to 0 after the last point
        {
            currentPointIndex = (currentPointIndex + 1) % patrolPoints.Length;
        }

        /// <summary>
        /// Assigns patrol points dynamically from WaveManager (makes the patrol system plug and play with spawned enemies)
        /// </summary>
        public void AssignPatrolPoints(Transform[] points)
        {
            patrolPoints = new PatrolPoint[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                patrolPoints[i] = new PatrolPoint
                {
                    point = points[i],
                    waitTime = 1.5f // Default wait time
                };
            }
        }

    }
}
