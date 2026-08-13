using System.Collections.Generic;
using UnityEngine;

namespace CyberVeil.Systems
{
    /// <summary>
    /// Optional lifecycle hooks for components that keep runtime-only state.
    /// </summary>
    public interface IPooledObject
    {
        void OnTakenFromPool();
        void OnReturnedToPool();
    }

    /// <summary>
    /// Scene-local, prefab-keyed pool for short-lived attack and projectile objects.
    /// Pools are destroyed with their scene so references cannot leak between levels.
    /// </summary>
    public static class RuntimeObjectPool
    {
        private static RuntimeObjectPoolHost host;

        public static int CreatedCount { get; internal set; }
        public static int ReusedCount { get; internal set; }
        public static int ReleasedCount { get; internal set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatistics()
        {
            host = null;
            CreatedCount = 0;
            ReusedCount = 0;
            ReleasedCount = 0;
        }

        public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null)
                return null;

            return GetHost().Get(prefab, position, rotation, parent);
        }

        public static T Get<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null)
            where T : Component
        {
            GameObject instance = Get(prefab != null ? prefab.gameObject : null, position, rotation, parent);
            return instance != null ? instance.GetComponent<T>() : null;
        }

        public static void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0)
                return;

            GetHost().Prewarm(prefab, count);
        }

        public static void Prewarm<T>(T prefab, int count) where T : Component
        {
            Prewarm(prefab != null ? prefab.gameObject : null, count);
        }

        /// <summary>
        /// Returns true for pooled instances, including an already-returned instance.
        /// Returns false when the object was not created by this pool.
        /// </summary>
        public static bool Release(GameObject instance)
        {
            if (instance == null)
                return true;

            PooledObjectInstance marker = instance.GetComponent<PooledObjectInstance>();
            return marker != null && marker.Owner != null && marker.Owner.Release(marker);
        }

        private static RuntimeObjectPoolHost GetHost()
        {
            if (host != null)
                return host;

            GameObject hostObject = new GameObject("Runtime Object Pool");
            host = hostObject.AddComponent<RuntimeObjectPoolHost>();
            return host;
        }
    }

    internal sealed class RuntimeObjectPoolHost : MonoBehaviour
    {
        private readonly Dictionary<GameObject, Queue<PooledObjectInstance>> pools =
            new Dictionary<GameObject, Queue<PooledObjectInstance>>();

        private Transform inactiveRoot;

        private void Awake()
        {
            GameObject root = new GameObject("Inactive Instances");
            inactiveRoot = root.transform;
            inactiveRoot.SetParent(transform, false);
            root.SetActive(false);
        }

        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            Queue<PooledObjectInstance> pool = GetPool(prefab);
            PooledObjectInstance marker;
            if (pool.Count > 0)
            {
                marker = pool.Dequeue();
                RuntimeObjectPool.ReusedCount++;
            }
            else
            {
                marker = Create(prefab);
            }

            marker.InPool = false;
            marker.RestoreInitialState();
            marker.transform.SetParent(parent, false);
            marker.transform.SetPositionAndRotation(position, rotation);
            marker.gameObject.SetActive(true);
            marker.NotifyTakenFromPool();
            return marker.gameObject;
        }

        public void Prewarm(GameObject prefab, int count)
        {
            Queue<PooledObjectInstance> pool = GetPool(prefab);
            while (pool.Count < count)
            {
                PooledObjectInstance marker = Create(prefab);
                marker.InPool = true;
                pool.Enqueue(marker);
            }
        }

        public bool Release(PooledObjectInstance marker)
        {
            if (marker == null || marker.Owner != this)
                return false;

            if (marker.InPool)
                return true;

            marker.InPool = true;
            RuntimeObjectPool.ReleasedCount++;
            marker.NotifyReturnedToPool();
            marker.ResetTransientState();
            marker.gameObject.SetActive(false);
            marker.transform.SetParent(inactiveRoot, false);
            GetPool(marker.Prefab).Enqueue(marker);
            return true;
        }

        private Queue<PooledObjectInstance> GetPool(GameObject prefab)
        {
            Queue<PooledObjectInstance> pool;
            if (!pools.TryGetValue(prefab, out pool))
            {
                pool = new Queue<PooledObjectInstance>();
                pools.Add(prefab, pool);
            }

            return pool;
        }

        private PooledObjectInstance Create(GameObject prefab)
        {
            GameObject instance = Instantiate(prefab, inactiveRoot);
            RuntimeObjectPool.CreatedCount++;
            instance.name = prefab.name;
            PooledObjectInstance marker = instance.GetComponent<PooledObjectInstance>();
            if (marker == null)
                marker = instance.AddComponent<PooledObjectInstance>();

            marker.Initialize(prefab, this);
            instance.SetActive(false);
            return marker;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class PooledObjectInstance : MonoBehaviour
    {
        private Transform[] cachedTransforms;
        private Vector3[] localPositions;
        private Quaternion[] localRotations;
        private Vector3[] localScales;
        private bool[] childActiveStates;
        private Collider[] cachedColliders;
        private bool[] colliderEnabledStates;
        private Renderer[] cachedRenderers;
        private bool[] rendererEnabledStates;
        private MonoBehaviour[] cachedBehaviours;
        private Rigidbody[] cachedRigidbodies;
        private ParticleSystem[] cachedParticleSystems;
        private TrailRenderer[] cachedTrails;
        private AudioSource[] cachedAudioSources;
        private Animator[] cachedAnimators;
        private IPooledObject[] cachedPoolCallbacks;

        public GameObject Prefab { get; private set; }
        public RuntimeObjectPoolHost Owner { get; private set; }
        public bool InPool { get; set; }

        public void Initialize(GameObject prefab, RuntimeObjectPoolHost owner)
        {
            Prefab = prefab;
            Owner = owner;
            CaptureInitialState();
        }

        public void RestoreInitialState()
        {
            if (cachedTransforms == null)
                return;

            for (int i = 0; i < cachedTransforms.Length; i++)
            {
                Transform item = cachedTransforms[i];
                if (item == null)
                    continue;

                if (i > 0)
                {
                    item.localPosition = localPositions[i];
                    item.localRotation = localRotations[i];
                }

                item.localScale = localScales[i];
                if (i > 0)
                    item.gameObject.SetActive(childActiveStates[i]);
            }

            for (int i = 0; i < cachedColliders.Length; i++)
                if (cachedColliders[i] != null)
                    cachedColliders[i].enabled = colliderEnabledStates[i];

            for (int i = 0; i < cachedRenderers.Length; i++)
                if (cachedRenderers[i] != null)
                    cachedRenderers[i].enabled = rendererEnabledStates[i];
        }

        public void ResetTransientState()
        {
            foreach (MonoBehaviour behaviour in cachedBehaviours)
                if (behaviour != null && behaviour != this)
                    behaviour.StopAllCoroutines();

            foreach (Rigidbody body in cachedRigidbodies)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.Sleep();
            }

            foreach (ParticleSystem particles in cachedParticleSystems)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.Clear(true);
            }

            foreach (TrailRenderer trail in cachedTrails)
                trail.Clear();

            foreach (AudioSource source in cachedAudioSources)
            {
                source.Stop();
                source.time = 0f;
            }

            foreach (Animator animator in cachedAnimators)
            {
                animator.Rebind();
                animator.Update(0f);
            }
        }

        public void NotifyTakenFromPool()
        {
            foreach (IPooledObject item in cachedPoolCallbacks)
                item.OnTakenFromPool();
        }

        public void NotifyReturnedToPool()
        {
            foreach (IPooledObject item in cachedPoolCallbacks)
                item.OnReturnedToPool();
        }

        private void CaptureInitialState()
        {
            cachedTransforms = GetComponentsInChildren<Transform>(true);
            localPositions = new Vector3[cachedTransforms.Length];
            localRotations = new Quaternion[cachedTransforms.Length];
            localScales = new Vector3[cachedTransforms.Length];
            childActiveStates = new bool[cachedTransforms.Length];
            for (int i = 0; i < cachedTransforms.Length; i++)
            {
                localPositions[i] = cachedTransforms[i].localPosition;
                localRotations[i] = cachedTransforms[i].localRotation;
                localScales[i] = cachedTransforms[i].localScale;
                childActiveStates[i] = cachedTransforms[i].gameObject.activeSelf;
            }

            cachedColliders = GetComponentsInChildren<Collider>(true);
            colliderEnabledStates = new bool[cachedColliders.Length];
            for (int i = 0; i < cachedColliders.Length; i++)
                colliderEnabledStates[i] = cachedColliders[i].enabled;

            cachedRenderers = GetComponentsInChildren<Renderer>(true);
            rendererEnabledStates = new bool[cachedRenderers.Length];
            for (int i = 0; i < cachedRenderers.Length; i++)
                rendererEnabledStates[i] = cachedRenderers[i].enabled;

            cachedBehaviours = GetComponentsInChildren<MonoBehaviour>(true);
            cachedRigidbodies = GetComponentsInChildren<Rigidbody>(true);
            cachedParticleSystems = GetComponentsInChildren<ParticleSystem>(true);
            cachedTrails = GetComponentsInChildren<TrailRenderer>(true);
            cachedAudioSources = GetComponentsInChildren<AudioSource>(true);
            cachedAnimators = GetComponentsInChildren<Animator>(true);

            MonoBehaviour[] behaviours = cachedBehaviours;
            var callbacks = new List<IPooledObject>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                IPooledObject callback = behaviours[i] as IPooledObject;
                if (callback != null)
                    callbacks.Add(callback);
            }
            cachedPoolCallbacks = callbacks.ToArray();
        }
    }
}
