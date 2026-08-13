using System.Collections;
using UnityEngine;
using CyberVeil.Systems;
using CyberVeil.VFX;

namespace CyberVeil.Enemies
{
    /// <summary>
    /// Spawns a projectile prefab that handles its own movement and damage on hit.
    /// Designed to be used as an EnemyAttackData attackPrefab.
    /// </summary>
    public class EnemyProjectileAttack : MonoBehaviour, IEnemyAttack
    {
        [Header("Projectile")]
        [SerializeField] private EnemyProjectile projectilePrefab;
        [SerializeField] private Transform muzzle;
        [SerializeField] private float spawnForwardOffset = 0.6f;
        [SerializeField] private float projectileSpeed = 10f;
        [SerializeField] private float projectileLifetime = 3f;
        [SerializeField] private float hitRadius = 0.6f;
        [SerializeField] private int damage = 12;

        [Header("Optional VFX & Audio")]
        [SerializeField] private VFXType particleType = VFXType.SlashHit;
        [SerializeField] private VFXType particleType2 = VFXType.SlashHit;
        [SerializeField] private Vector3 particleSpawnOffset = Vector3.zero;
        [SerializeField] private bool playAudioOnStartAttack = false;
        [SerializeField] private bool playAudioOnEndAttack = false;
        [SerializeField] private bool playSecondParticle = true;
        [SerializeField] private SoundType audioType = SoundType.ATTACK;
        [SerializeField] private SoundType audioType2 = SoundType.ATTACK;
        [SerializeField] private float audioVolume = 0.5f;
        [SerializeField] private float audioVolume2 = 0.5f;
        [SerializeField] private float particleAndOrAudioPlayDelay = 0.5f;
        [SerializeField] private float particleAndOrAudioPlayDelay2 = 0.5f;

        [Header("Telegraph")]
        [SerializeField] private float telegraphSeconds = 0f;

        private Transform player;

        public void PrewarmProjectiles()
        {
            RuntimeObjectPool.Prewarm(projectilePrefab, 4);
        }

        public IEnumerator ExecuteAttack()
        {
            if (player == null)
                player = PlayerReference.PlayerTransform;

            if (projectilePrefab == null || player == null)
                yield break;

            Transform owner = transform.root != null ? transform.root : transform;
            Transform origin = muzzle != null ? muzzle : owner;

            Vector3 dir = (player.position - origin.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                dir = origin.forward;
            else
                dir.Normalize();

            // Keep the shooter in place (handled by AI state) and face the firing direction.
            owner.rotation = Quaternion.LookRotation(dir, Vector3.up);

            StartCoroutine(PlaySpawnEffectsAfterDelay(origin, owner.rotation));

            if (telegraphSeconds > 0f)
                yield return new WaitForSeconds(telegraphSeconds);

            Vector3 spawnPos = origin.position;
            if (muzzle == null)
                spawnPos += dir * Mathf.Max(0f, spawnForwardOffset);

            EnemyProjectile proj = RuntimeObjectPool.Get(
                projectilePrefab,
                spawnPos,
                Quaternion.LookRotation(dir, Vector3.up));
            proj.Init(dir, damage, hitRadius, projectileSpeed, projectileLifetime, transform.root.gameObject);
        }

        private IEnumerator PlaySpawnEffectsAfterDelay(Transform origin, Quaternion rotation)
        {
            if (particleAndOrAudioPlayDelay > 0f)
                yield return new WaitForSeconds(particleAndOrAudioPlayDelay);

            if (playAudioOnStartAttack && ParticleManager.Instance != null)
            {
                Vector3 spawnPos = origin.position + particleSpawnOffset;
                ParticleManager.Instance.PlayEffect(particleType, spawnPos, rotation);
            }
            if (playAudioOnStartAttack)
            {
                SoundManager.PlaySound(audioType, audioVolume);
            }

            if (playAudioOnEndAttack)
            {
                yield return new WaitForSeconds(particleAndOrAudioPlayDelay2);
                SoundManager.PlaySound(audioType2, audioVolume2);
            }
            if (playSecondParticle && ParticleManager.Instance != null)
            {
                Vector3 spawnPos = origin.position + particleSpawnOffset;
                ParticleManager.Instance.PlayEffect(particleType2, spawnPos, rotation);
            }
        }
    }
}
