using System.Collections.Generic;
using UnityEngine;

namespace CyberVeil.VFX
{
    /// <summary>
    /// Enumeration of the VFX types in the game
    /// Used to map effect types to particle prefabs for playback
    /// </summary>
    public enum VFXType
    {
        Slash1,
        Slash2,
        Slash3,
        SlashHit,
        SlashImpact,
        PlayerHitSpark,
        MushroomShieldParticle,
        Teleport,
        SlimeSplat,
        GroundSlam,
        AOESlash,
        MushroomHit,
        JabHit,
        VeilSurgeActivation,
        SurgeSlash1,
        SurgeSlash2,
        SurgeSlash3,
        
    }

    /// <summary>
    /// Centralized manager for spawning and reusing particle effects.
    /// Implements object pooling to reduce instantiations and GC pressure
    /// Singleton pattern for global access from other systems 
    /// </summary>
    public class ParticleManager : MonoBehaviour
    {
        // Singleton pattern where any other script can call
        public static ParticleManager Instance { get; private set; }

        /// <summary>
        /// Entry for each effect type in the inspector, with its prefab and desired pool size
        /// </summary>
        [System.Serializable]
        public class VFXEntry
        {
            public VFXType type;
            public ParticleSystem prefab;
            public int poolSize = 5;
        }

        [Header("Particle Effect Library")]
        [SerializeField] private List<VFXEntry> vfxEntries;

        private Dictionary<VFXType, Queue<ParticleSystem>> vfxPools = new();
        private Dictionary<VFXType, ParticleSystem> prefabLookup = new();  

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            Instance = this;

            // Initialize pools and cache prefab references
            foreach (var entry in vfxEntries)
            {
                if (entry.prefab == null) continue;

                prefabLookup[entry.type] = entry.prefab;

                Queue<ParticleSystem> pool = new Queue<ParticleSystem>();
                for (int i = 0; i < entry.poolSize; i++)
                {
                    var obj = Instantiate(entry.prefab, transform);
                    obj.gameObject.SetActive(false);
                    pool.Enqueue(obj);
                }

                vfxPools[entry.type] = pool;
            }
        }

        /// <summary>
        /// Plays a pooled particle effect of the specified type at the given position and rotation
        /// </summary>
        public void PlayEffect(VFXType type, Vector3 position, Quaternion rotation)
        {
            if (!vfxPools.ContainsKey(type))
            {
                Debug.LogWarning($"No pool found for VFX type {type}");
                return;
            }

            ParticleSystem ps = GetFromPool(type);
            ps.transform.SetPositionAndRotation(position, rotation);    
            ps.gameObject.SetActive(true);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            ps.Play();

            // Return the effect to the pool after it finishes playing
            StartCoroutine(ReturnToPoolAfterDelay(type, ps, ps.main.duration + 0.5f));
        }

        /// <summary>
        /// Retrieves a particle system from the pool or instantiates one if empty
        /// </summary>
        private ParticleSystem GetFromPool(VFXType type)
        {
            Queue<ParticleSystem> pool = vfxPools[type];

            if (pool.Count == 0)
            {
                var newEffect = Instantiate(prefabLookup[type], transform);
                newEffect.gameObject.SetActive(false);
                return newEffect;
            }

            return pool.Dequeue();
        }

        /// <summary>
        /// Returns the particle system to the pool after a delay
        /// </summary>
        private System.Collections.IEnumerator ReturnToPoolAfterDelay(VFXType type, ParticleSystem ps, float delay)
        {
            yield return new WaitForSeconds(delay);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            ps.gameObject.SetActive(false);
            vfxPools[type].Enqueue(ps);
        }
    }
}
