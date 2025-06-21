using System.Collections;
using UnityEngine;
using CyberVeil.Combat;
using CyberVeil.Systems;


namespace CyberVeil.Player
{
    public class SlashAttack3 : MonoBehaviour
    {
        //reference to the particle system
        public ParticleSystem slashParticleSystem;
        public ParticleSystem slashParticleSystem2;
        public ParticleSystem slashHit;
        public ParticleSystem slashHit2;
        //stores player transform
        public Transform playerTransform;
        public Vector3 offset = new Vector3(0, 0.0f, 0);
        //reference player controller
        public PlayerAttack playerAttack;

        void Start()
        {
            //auto-finds PlayerAttack if not assigned
            if (playerAttack == null)
            {
                playerAttack = GetComponent<PlayerAttack>();
            }
        }

        public void PlaySlash(Vector3 forwardDir)
        {
            Vector3 slashPosition = playerTransform.position + offset;
            Quaternion slashRotation = playerTransform.rotation;

            transform.position = slashPosition;
            transform.rotation = slashRotation;

            slashParticleSystem.transform.localPosition = Vector3.zero;
            slashParticleSystem.transform.localRotation = Quaternion.identity;

            slashParticleSystem.Play();
            slashParticleSystem2.Play();
            StartCoroutine(toggleCollider());
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Enemy"))
            {
                Debug.Log("Enemy hit by slash!");

                // Play hit particles
                if (slashHit != null && slashHit2 != null)
                {
                    slashHit.transform.position = other.transform.position;
                    slashHit2.transform.position = other.transform.position;
                    slashHit.Play();
                    slashHit2.Play();
                }

                //apply hit stop
                HitstopManager.Instance.DoHitstop(0.1f, 0f); //adjust duration & freeze level

                IDamagable damagable = other.GetComponent<IDamagable>();
                damagable?.TakeDamage(25);

                IKnockbackable knockback = other.GetComponent<IKnockbackable>();
                knockback?.ApplyKnockback(playerTransform);
            }
        }

        private IEnumerator toggleCollider()
        {
            Collider collider = GetComponent<Collider>();
            collider.enabled = true;
            yield return new WaitForSeconds(0.3f);
            collider.enabled = false;
        }

    }
}
