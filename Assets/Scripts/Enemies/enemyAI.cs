using UnityEngine;
using System.Collections;
using CyberVeil.Systems;
using CyberVeil.Combat;  // <-- Added for CombatManager

namespace CyberVeil.Enemies
{
    public class EnemyAI : MonoBehaviour
    {
        [Header("Targeting")]
        public Transform playerTransform;

        [Header("Movement")]
        public float speed = 1f;
        public bool follow = true;

        [Header("Attack")]
        public float attackRange = 2.5f;
        public float attackCooldown = 2f;
        private float attackTimer = 0f;
        public float damageRadius = 1f;
        public int normalAttackDamage = 20;

        [Header("Leap Attack (Optional)")]
        public bool canLeap = false;
        public float leapRange = 7f;
        public float leapForce = 7f;
        public float leapCooldown = 5f;
        private float leapTimer = 0f;
        private bool isLeaping = false;
        public GameObject effect;
        public float leapVol = 0.3f;
        public float splatVol = 0.4f;
        public float leapDamageRadius = 2f;
        public int leapDamage = 30;

        private EnemyAnimationController enemyAnimationController;

        private void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;

            enemyAnimationController = GetComponent<EnemyAnimationController>();
        }

        void Update()
        {
            if (playerTransform == null || isLeaping) return;

            float distance = Vector3.Distance(transform.position, playerTransform.position);
            Vector3 lookDir = playerTransform.position - transform.position;
            lookDir.y = 0f;
            transform.rotation = Quaternion.LookRotation(lookDir);

            attackTimer += Time.deltaTime;
            leapTimer += Time.deltaTime;

            // Leap Logic
            if (canLeap && distance <= leapRange && leapTimer >= leapCooldown)
            {
                StartCoroutine(LeapAttack());
                return;
            }

            if (follow)
            {
                Vector3 direction = (playerTransform.position - transform.position).normalized;

                // Wall sliding
                if (Physics.Raycast(transform.position, direction, out RaycastHit hit, 0.6f))
                {
                    Vector3 slideDir = Vector3.ProjectOnPlane(direction, hit.normal).normalized;
                    transform.position += slideDir * speed * Time.deltaTime;
                }
                else
                {
                    transform.position += direction * speed * Time.deltaTime;
                }

                if (distance <= attackRange && attackTimer >= attackCooldown)
                {
                    attackTimer = 0f;
                    enemyAnimationController?.StartAttack();
                }
                else
                {
                    enemyAnimationController?.EndAttack();
                }
            }
        }

        public void DealNormalAttackDamage()
        {
            //  Use CombatManager to handle AoE damage logic
            CombatManager.Instance.DealDamageInRadius(transform.position, damageRadius, normalAttackDamage, gameObject);

            //  Still handle knockback (which is your custom system)
            Collider[] hits = Physics.OverlapSphere(transform.position, damageRadius);
            foreach (Collider hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    Knockback playerKnockback = hit.GetComponent<Knockback>();
                    playerKnockback?.ApplyKnockback(transform);
                }
            }
        }

        private IEnumerator LeapAttack()
        {
            isLeaping = true;
            leapTimer = 0f;

            enemyAnimationController?.changeAnimation("Attack");
            SoundManager.PlaySound(SoundType.SLIMEJUMP, leapVol);

            yield return new WaitForSeconds(0.3f);

            Vector3 leapDir = (playerTransform.position - transform.position);
            leapDir.y = 0;
            leapDir = leapDir.normalized;

            Vector3 targetPosition = transform.position + leapDir * leapForce;
            targetPosition.y = transform.position.y;

            float jumpTime = 0.4f;
            float elapsed = 0f;
            Vector3 startPosition = transform.position;

            while (elapsed < jumpTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / jumpTime;
                Vector3 pos = Vector3.Lerp(startPosition, targetPosition, t);
                pos.y = startPosition.y + Mathf.Sin(t * Mathf.PI) * 2f;
                transform.position = pos;
                yield return null;
            }

            transform.position = new Vector3(targetPosition.x, startPosition.y, targetPosition.z);
            SoundManager.PlaySound(SoundType.SLIMESPLAT, splatVol);

            Collider[] hits = Physics.OverlapSphere(transform.position, damageRadius);
            foreach (Collider hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    IDamagable damagable = hit.GetComponent<IDamagable>();
                    damagable?.TakeDamage(leapDamage);
                }
            }

            if (effect)
            {
                GameObject slimeEffect = Instantiate(effect, transform.position, Quaternion.identity);
                Destroy(slimeEffect, 0.7f);
            }

            enemyAnimationController?.changeAnimation("Move");
            yield return new WaitForSeconds(0.5f);
            isLeaping = false;
        }
    }
}
