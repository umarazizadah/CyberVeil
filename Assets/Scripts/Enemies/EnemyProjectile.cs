using UnityEngine;
using CyberVeil.Combat;
using CyberVeil.Systems;

namespace CyberVeil.Enemies
{
    /// <summary>
    /// Projectile prefab that handles its own movement, lifetime, and damage on collision.
    /// Spawned by EnemyProjectileAttack and moves in a straight line from spawn point.
    /// Applies damage in a radius on impact or after lifetime expires.
    /// </summary>
    public class EnemyProjectile : MonoBehaviour, IPooledObject
    {
        private Vector3 moveDirection;
        private float speed;
        private float lifetime;
        private float elapsedTime;
        private float hitRadius;
        private int damage;
        private GameObject owner; // Enemy that spawned this; used to avoid friendly fire
        private bool hasDetonated;

        private SphereCollider sphereCollider;
        private Rigidbody rb;

        public void Init(Vector3 direction, int damageAmount, float radius, float moveSpeed, float projectileLifetime, GameObject projectileOwner)
        {
            moveDirection = direction.normalized;
            damage = damageAmount;
            hitRadius = radius;
            speed = moveSpeed;
            lifetime = projectileLifetime;
            owner = projectileOwner != null ? projectileOwner : (transform.root != null ? transform.root.gameObject : gameObject);
            elapsedTime = 0f;
            hasDetonated = false;

            // Setup collider for hit detection if not already present
            sphereCollider = GetComponent<SphereCollider>();
            if (sphereCollider == null)
            {
                sphereCollider = gameObject.AddComponent<SphereCollider>();
            }
            sphereCollider.radius = hitRadius;
            sphereCollider.isTrigger = true;
            sphereCollider.enabled = true;

            // Setup rigidbody for physics (kinematic, we control movement)
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        private void Update()
        {
            if (hasDetonated)
                return;

            elapsedTime += Time.deltaTime;

            // Move forward
            transform.position += moveDirection * speed * Time.deltaTime;

            // Check lifetime
            if (elapsedTime >= lifetime)
            {
                Detonate();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Avoid hitting the owner
            if (other.gameObject == owner || other.transform.IsChildOf(owner.transform))
                return;

            // Hit something; detonate
            if (!hasDetonated && other.CompareTag("Enemy") == false) // Don't hit other enemies mid-air
            {
                Detonate();
            }
        }

        private void Detonate()
        {
            if (hasDetonated)
                return;

            hasDetonated = true;

            // Apply damage in radius
            if (CombatManager.Instance != null)
                CombatManager.Instance.DealDamageInRadius(transform.position, hitRadius, damage, owner);

            // Optional: Spawn impact VFX here if needed
            // ParticleManager.Instance.PlayEffect("ProjectileImpact", transform.position, Quaternion.identity);

            if (!RuntimeObjectPool.Release(gameObject))
                Destroy(gameObject);
        }

        public void OnTakenFromPool()
        {
        }

        public void OnReturnedToPool()
        {
            moveDirection = Vector3.zero;
            speed = 0f;
            lifetime = 0f;
            elapsedTime = 0f;
            hitRadius = 0f;
            damage = 0;
            owner = null;
            hasDetonated = true;

            if (sphereCollider != null)
                sphereCollider.enabled = false;

            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}
