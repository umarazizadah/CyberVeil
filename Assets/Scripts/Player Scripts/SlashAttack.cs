using System.Collections;
using UnityEngine;
using CyberVeil.Combat;
using CyberVeil.Systems;

namespace CyberVeil.Player
{
    public class SlashAttack : MonoBehaviour
    {
        //reference to the particle system
        public ParticleSystem slashParticleSystem;
        public ParticleSystem slashParticleSystem2;
        public ParticleSystem slashHit;
        public ParticleSystem slashHit2;

        //stores player transform
        public Transform playerTransform;
        public Vector3 offset = new Vector3(0f, 0.0f, 0);

        //reference to the new attack component
        public PlayerAttack playerAttack;


        void Start()
        {
            //auto-find PlayerAttack if not assigned
            if (playerAttack == null)
            {
                playerAttack = GetComponent<PlayerAttack>();
            }
        }

        public void PlaySlash(Vector3 forwardDir)
        {
            Vector3 slashPosition = playerTransform.position + forwardDir * 1.0f + offset;
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

                //play hit particles
                if (slashHit != null && slashHit2 != null)
                {
                    slashHit.transform.position = other.transform.position;
                    slashHit2.transform.position = other.transform.position;
                    slashHit.Play();
                    slashHit2.Play();
                }

                //apply hit stop
                HitstopManager.Instance.DoHitstop(0.005f, 0f); //adjust duration & freeze level

                // Deal damage
                IDamagable damagable = other.GetComponent<IDamagable>();
                damagable?.TakeDamage(25);

                // deal knockback
                IKnockbackable knockback = other.GetComponent<IKnockbackable>();
                knockback?.ApplyKnockback(playerTransform);

            }
        }

        private IEnumerator toggleCollider()
        {
            //collider is enabled, waits for attack to register then is disabled
            Collider collider = GetComponent<Collider>();
            collider.enabled = true;
            yield return new WaitForSeconds(0.3f);
            collider.enabled = false;
        }

    }
}