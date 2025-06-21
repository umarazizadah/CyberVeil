using UnityEngine;
using System.Collections;
using CyberVeil.Combat;
using CyberVeil.Systems;


namespace CyberVeil.Player
{
    public class SlashAttack2 : MonoBehaviour
    {
        //reference to the particle system
        public ParticleSystem slashParticleSystem;
        public ParticleSystem slashParticleSystem2;
        public ParticleSystem slashHit;
        public ParticleSystem slashHit2;
        //stores player transform
        public Transform playerTransform;
        public Vector3 offset = new Vector3(0.5f, 0.5f, 10f);

        public float xRotate = 90f;
        public float yRotate = 45f;
        public float zRotate = 45f;
        //reference to the new attack component
        public PlayerAttack playerAttack;


        void Start()
        {
            // Auto-find PlayerAttack if not assigned
            if (playerAttack == null)
            {
                playerAttack = GetComponent<PlayerAttack>();
            }
        }
        public void PlaySlash(Vector3 forwardDir)
        {
            Vector3 slashPosition = playerTransform.position + forwardDir * 1.2f + offset;
            Quaternion slashRotation = playerTransform.rotation;

            transform.position = slashPosition;
            transform.rotation = slashRotation;
            transform.Rotate(xRotate, yRotate, zRotate); // apply rotation offsets

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

                // Apply hit pause
                HitstopManager.Instance.DoHitstop(0.05f, 0f); //adjust duration & freeze level

                // Deal damage
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
