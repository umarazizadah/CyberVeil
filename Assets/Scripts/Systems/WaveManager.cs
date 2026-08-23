using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.Serialization;
using CyberVeil.Enemies;
using CyberVeil.Systems;
using CyberVeil.UI;
using CyberVeil.World;

namespace CyberVeil.Systems
{
    /// <summary>
    /// Manages the spawning and progression of enemy waves
    /// Waves auto chain EXCEPT after the final wave, which triggers upgrade phase
    /// Handles wave definitions, spawn timing, and invokes events when waves start or end
    /// Supports randomized enemy types and spawn points for each wave
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        public enum WaveClearMode
        {
            KillAll = 0,
            SurviveTime = 1
        }

        public enum SpawnPattern
        {
            Staggered = 0,
            Burst = 1,
            Ambush = 2,
            Crescendo = 3
        }

        [System.Serializable]
        public class SpawnGroup
        {
            public string name;
            public GameObject[] enemyPrefabs;
            public int enemyCount = 5;
            public float spawnRate = 1.5f;
            public SpawnPattern pattern = SpawnPattern.Staggered;
            public int burstSize = 3;
            public float ambushDelay = 1f;
            public float crescendoEndRate = 0.4f;
            public SpawnLane lane = SpawnLane.Any;
            public float delayBeforeNextGroup = 0f;
        }

        [System.Serializable]
        public class Wave // Can edit waves in the unity editor
        {
            public string name;
            public GameObject[] enemyPrefabs;
            public int enemyCount;
            public float spawnRate = 1.5f;
            public WaveClearMode clearMode = WaveClearMode.KillAll;
            public float surviveSeconds = 30f;
            public TrialCurseModifier.CurseType forcedCurse = TrialCurseModifier.CurseType.None;
            public SpawnGroup[] spawnGroups;
        }

        [System.Serializable]
        public class VeilShardReward
        {
            [Min(0)] public int waveIndex;
            public VeilUpgradePair upgradePair;
        }

        [Header("Waves")]
        [FormerlySerializedAs("trial1")]
        public Wave[] waves = new Wave[3];

        public Transform[] spawnPoints;

        [Header("Modifiers")]
        [SerializeField] private TrialCurseModifier trialCurse;

        [Header("Group Rewards")]
        [SerializeField] private GameObject healingCrystalPrefab;

        [Header("Veil Shard Rewards")]
        [SerializeField] private GameObject veilShardUpgradePrefab;
        [SerializeField] private Transform veilShardSpawnPoint;
        [Tooltip("Each reward assigns a data-authored upgrade pair to a zero-based wave index.")]
        [SerializeField] private VeilShardReward[] veilShardRewards = Array.Empty<VeilShardReward>();
        [SerializeField, Min(0f)] private float shardSettleSeconds = 0.75f;
        [SerializeField, Range(0, 31)] private int interactableLayer = 3;

        [Header("Flow")]
        public bool autoStartOnPlay = false;

        // State
        private int waveIndex = 0;
        private bool waveInProgress = false;
        private bool waitingForUpgrade = false;
        private bool waitingForShardChoice;
        private VeilShardInteractable activeVeilShard;
        private readonly HashSet<int> resolvedShardWaveIndices = new HashSet<int>();

        private int aliveEnemies = 0; // Tracks actual alive enemies for the current user

        private int[] groupAliveCounts;
        private bool[] groupCrystalSpawned;
        private Vector3[] groupLastDeathPositions;

        private readonly List<Transform> spawnPointsAny = new List<Transform>();
        private readonly List<Transform> spawnPointsLeft = new List<Transform>();
        private readonly List<Transform> spawnPointsRight = new List<Transform>();
        private readonly List<Transform> spawnPointsFront = new List<Transform>();
        private readonly List<Transform> spawnPointsBack = new List<Transform>();
        private readonly List<Transform> spawnPointsCenter = new List<Transform>();

        // Events for external systems (UI, music)
        public static event Action<int, int> OnWaveStarted;
        public static event Action<int, int> OnWaveCleared;
        public static event Action<int> OnUpgradePhaseStarted; // Fired after wave 3 cleared

        private void Awake()
        {
            CacheSpawnPoints();
            if (trialCurse == null)
                trialCurse = FindObjectOfType<TrialCurseModifier>();

            // Subscribe to tutorial completion to start waves
            TutorialUI.OnTutorialComplete += StartRun;
        }

        private void Start()
        {
            if (autoStartOnPlay) StartRun();
        }

        private void OnDestroy()
        {
            TutorialUI.OnTutorialComplete -= StartRun;
        }

        public void StartRun()
        {
            if (waveInProgress || waitingForShardChoice || waitingForUpgrade)
                return;

            waveIndex = 0;
            waitingForUpgrade = false;
            waitingForShardChoice = false;
            resolvedShardWaveIndices.Clear();

            if (!waveInProgress) StartCoroutine(RunCurrentWave());
        }

        /// <summary>
        /// Called after the player uses portal upgrade UI
        /// Loads the next level scene via SceneProgressManager
        /// </summary>
        public void ContinueAfterUpgrade()
        {
            if (!AreAllWavesComplete()) return;

            waitingForUpgrade = false;

            // Check if SceneProgressManager exists and load next level
            if (SceneProgressManager.Instance != null)
            {
                if (SceneProgressManager.Instance.HasNextLevel())
                {
                    Debug.Log("Loading next level after upgrade...");
                    SceneProgressManager.Instance.LoadNextLevel();
                }
                else
                {
                    Debug.Log("All levels complete!");
                    // You can add end-game logic here
                }
            }
            else
            {
                Debug.LogWarning("SceneProgressManager not found! Cannot transition to next level.");
            }
        }

        private IEnumerator RunCurrentWave()
        {
            waveInProgress = true;
            aliveEnemies = 0;

            Wave wave = GetWave(waveIndex);
            if (wave == null)
            {
                Debug.Log($"Missing wave data for wave {waveIndex + 1}");
                waveInProgress = false;
                yield break;
            }

            SetupGroupTracking(wave);
            ApplyWaveModifier(wave);
            OnWaveStarted?.Invoke(0, waveIndex);

            yield return StartCoroutine(SpawnWave(wave));

            if (wave.clearMode == WaveClearMode.SurviveTime)
            {
                yield return new WaitForSeconds(wave.surviveSeconds);
            }
            else
            {
                // Wait until all enemies are dead
                yield return new WaitUntil(() => aliveEnemies <= 0);
            }

            // Wave cleared
            OnWaveCleared?.Invoke(0, waveIndex);

            if (ShouldSpawnShardReward(waveIndex))
                yield return StartCoroutine(SpawnAndWaitForShardChoice(waveIndex));

            // Decide what happens next
            bool isFinalWave = (waveIndex >= waves.Length - 1);

            if (isFinalWave)
            {
                // Pause for upgrade teleporter
                waitingForUpgrade = true;
                SoundManager.PlaySound(SoundType.TRIALEND,0.7f);
                waveInProgress = false;

                OnUpgradePhaseStarted?.Invoke(0);
                yield break;
            }
            else
            {
                // Auto start next wave in same trial
                waveIndex++;
                waveInProgress = false;

                // Small delay between waves
                yield return new WaitForSeconds(1.0f);

                StartCoroutine(RunCurrentWave());
            }
        }

        private bool ShouldSpawnShardReward(int clearedWaveIndex)
        {
            if (resolvedShardWaveIndices.Contains(clearedWaveIndex))
                return false;

            VeilShardReward reward = GetShardReward(clearedWaveIndex);
            if (reward == null || reward.upgradePair == null)
                return false;

            return VeilRunManager.Instance == null || VeilRunManager.Instance.CanSelect(reward.upgradePair);
        }

        private IEnumerator SpawnAndWaitForShardChoice(int clearedWaveIndex)
        {
            waitingForShardChoice = true;

            VeilShardReward reward = GetShardReward(clearedWaveIndex);

            if (veilShardUpgradePrefab == null || veilShardSpawnPoint == null || reward?.upgradePair == null)
            {
                Debug.LogError("Required Veil shard reward is missing its prefab, spawn point, or upgrade pair.", this);
                resolvedShardWaveIndices.Add(clearedWaveIndex);
                waitingForShardChoice = false;
                yield break;
            }

            GameObject instance = Instantiate(
                veilShardUpgradePrefab,
                veilShardSpawnPoint.position,
                veilShardSpawnPoint.rotation);
            activeVeilShard = instance.GetComponent<VeilShardInteractable>();
            if (activeVeilShard == null)
                activeVeilShard = instance.AddComponent<VeilShardInteractable>();
            activeVeilShard.Initialize(
                this,
                clearedWaveIndex,
                reward.upgradePair,
                shardSettleSeconds,
                interactableLayer);

            yield return new WaitUntil(() => !waitingForShardChoice);
        }

        public void NotifyVeilShardChoiceResolved(int clearedWaveIndex, VeilShardInteractable shard)
        {
            if (!waitingForShardChoice || shard == null || shard != activeVeilShard)
                return;

            resolvedShardWaveIndices.Add(clearedWaveIndex);
            activeVeilShard = null;
            waitingForShardChoice = false;
        }

        private bool HaveAllRequiredShardChoicesResolved()
        {
            if (veilShardRewards == null || veilShardRewards.Length == 0)
                return true;

            for (int i = 0; i < veilShardRewards.Length; i++)
            {
                VeilShardReward reward = veilShardRewards[i];
                if (reward == null || reward.upgradePair == null)
                    continue;
                if (resolvedShardWaveIndices.Contains(reward.waveIndex))
                    continue;
                if (!reward.upgradePair.Repeatable && VeilRunManager.Instance != null &&
                    VeilRunManager.Instance.HasSelected(reward.upgradePair.StableId))
                    continue;

                if (reward.waveIndex >= 0)
                    return false;
            }
            return true;
        }

        private VeilShardReward GetShardReward(int clearedWaveIndex)
        {
            if (veilShardRewards == null)
                return null;

            for (int i = 0; i < veilShardRewards.Length; i++)
            {
                VeilShardReward reward = veilShardRewards[i];
                if (reward != null && reward.waveIndex == clearedWaveIndex)
                    return reward;
            }
            return null;
        }

        private void ApplyWaveModifier(Wave wave)
        {
            if (trialCurse == null)
                return;

            if (wave.forcedCurse != TrialCurseModifier.CurseType.None)
                trialCurse.ApplyForcedCurse(wave.forcedCurse);
        }

        private void SetupGroupTracking(Wave wave)
        {
            if (wave.spawnGroups == null || wave.spawnGroups.Length == 0)
            {
                groupAliveCounts = null;
                groupCrystalSpawned = null;
                groupLastDeathPositions = null;
                return;
            }

            int count = wave.spawnGroups.Length;
            groupAliveCounts = new int[count];
            groupCrystalSpawned = new bool[count];
            groupLastDeathPositions = new Vector3[count];
        }

        private void SpawnHealingCrystal(Vector3 position)
        {
            if (healingCrystalPrefab == null)
                return;

            Instantiate(healingCrystalPrefab, position, Quaternion.identity);
        }

        private IEnumerator SpawnWave(Wave wave)
        {
            if (wave.spawnGroups != null && wave.spawnGroups.Length > 0)
            {
                for (int i = 0; i < wave.spawnGroups.Length; i++)
                {
                    SpawnGroup group = wave.spawnGroups[i];
                    if (group != null)
                        yield return StartCoroutine(SpawnGroupRoutine(group, i));

                    // Delay before spawning the next group
                    if (i < wave.spawnGroups.Length - 1 && group != null && group.delayBeforeNextGroup > 0f)
                        yield return new WaitForSeconds(group.delayBeforeNextGroup);
                }

                yield break;
            }

            if (wave.enemyPrefabs == null || wave.enemyPrefabs.Length == 0)
                yield break;

            for (int i = 0; i < wave.enemyCount; i++)
            {
                SpawnEnemy(wave.enemyPrefabs, SpawnLane.Any, -1);
                if (wave.spawnRate > 0f)
                    yield return new WaitForSeconds(wave.spawnRate);
                else
                    yield return null;
            }
        }

        private IEnumerator SpawnGroupRoutine(SpawnGroup group, int groupIndex)
        {
            if (group.enemyPrefabs == null || group.enemyPrefabs.Length == 0)
                yield break;

            int remaining = Mathf.Max(0, group.enemyCount);
            if (remaining == 0)
                yield break;

            float spawnRate = Mathf.Max(0f, group.spawnRate);

            if (group.pattern == SpawnPattern.Ambush && group.ambushDelay > 0f)
                yield return new WaitForSeconds(group.ambushDelay);

            switch (group.pattern)
            {
                case SpawnPattern.Burst:
                {
                    int burstSize = Mathf.Max(1, group.burstSize);
                    while (remaining > 0)
                    {
                        int count = Mathf.Min(burstSize, remaining);
                        for (int i = 0; i < count; i++)
                        {
                            SpawnEnemy(group.enemyPrefabs, group.lane, groupIndex);
                        }

                        remaining -= count;
                        if (remaining > 0)
                            yield return spawnRate > 0f ? new WaitForSeconds(spawnRate) : null;
                    }
                    break;
                }

                case SpawnPattern.Crescendo:
                {
                    float startRate = Mathf.Max(0.01f, spawnRate);
                    float endRate = Mathf.Max(0.01f, group.crescendoEndRate);
                    for (int i = 0; i < remaining; i++)
                    {
                        SpawnEnemy(group.enemyPrefabs, group.lane, groupIndex);

                        float t = remaining <= 1 ? 1f : (float)i / (remaining - 1);
                        float delay = Mathf.Lerp(startRate, endRate, t);
                        yield return new WaitForSeconds(delay);
                    }
                    break;
                }

                case SpawnPattern.Ambush:
                case SpawnPattern.Staggered:
                default:
                {
                    for (int i = 0; i < remaining; i++)
                    {
                        SpawnEnemy(group.enemyPrefabs, group.lane, groupIndex);
                        if (spawnRate > 0f)
                            yield return new WaitForSeconds(spawnRate);
                        else
                            yield return null;
                    }
                    break;
                }
            }
        }

        private void SpawnEnemy(GameObject[] prefabs, SpawnLane lane, int groupId)
        {
            if (prefabs == null || prefabs.Length == 0)
                return;

            Transform spawnPoint = GetSpawnPoint(lane);
            if (spawnPoint == null)
                return;

            GameObject enemyPrefab = prefabs[UnityEngine.Random.Range(0, prefabs.Length)];
            GameObject enemyInstance = Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);

            aliveEnemies++;
            if (groupId >= 0 && groupAliveCounts != null && groupId < groupAliveCounts.Length)
                groupAliveCounts[groupId]++;

            // Reporter attachment so know when this enemy dies (Destroy)
            var reporter = enemyInstance.GetComponent<WaveEnemyReporter>();
            if (reporter == null) reporter = enemyInstance.AddComponent<WaveEnemyReporter>();
            reporter.Init(this, groupId, waveIndex);

            // Patrol assignment logic
            EnemyPatrol patrol = enemyInstance.GetComponent<EnemyPatrol>();
            if (patrol != null && spawnPoint.childCount > 0)
            {
                Transform[] patrolPoints = new Transform[spawnPoint.childCount];
                for (int j = 0; j < spawnPoint.childCount; j++)
                {
                    patrolPoints[j] = spawnPoint.GetChild(j);
                }
                patrol.AssignPatrolPoints(patrolPoints);
            }
        }

        private Transform GetSpawnPoint(SpawnLane lane)
        {
            List<Transform> list = GetSpawnPointList(lane);
            if (list == null || list.Count == 0)
                list = spawnPointsAny;

            if (list == null || list.Count == 0)
                return null;

            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        private List<Transform> GetSpawnPointList(SpawnLane lane)
        {
            switch (lane)
            {
                case SpawnLane.Left:
                    return spawnPointsLeft;
                case SpawnLane.Right:
                    return spawnPointsRight;
                case SpawnLane.Front:
                    return spawnPointsFront;
                case SpawnLane.Back:
                    return spawnPointsBack;
                case SpawnLane.Center:
                    return spawnPointsCenter;
                case SpawnLane.Any:
                default:
                    return spawnPointsAny;
            }
        }

        private void CacheSpawnPoints()
        {
            spawnPointsAny.Clear();
            spawnPointsLeft.Clear();
            spawnPointsRight.Clear();
            spawnPointsFront.Clear();
            spawnPointsBack.Clear();
            spawnPointsCenter.Clear();

            if (spawnPoints == null)
                return;

            for (int i = 0; i < spawnPoints.Length; i++)
            {
                Transform point = spawnPoints[i];
                if (point == null)
                    continue;

                spawnPointsAny.Add(point);

                SpawnPoint marker = point.GetComponent<SpawnPoint>();
                SpawnLane lane = marker != null ? marker.Lane : SpawnLane.Any;

                switch (lane)
                {
                    case SpawnLane.Left:
                        spawnPointsLeft.Add(point);
                        break;
                    case SpawnLane.Right:
                        spawnPointsRight.Add(point);
                        break;
                    case SpawnLane.Front:
                        spawnPointsFront.Add(point);
                        break;
                    case SpawnLane.Back:
                        spawnPointsBack.Add(point);
                        break;
                    case SpawnLane.Center:
                        spawnPointsCenter.Add(point);
                        break;
                    case SpawnLane.Any:
                    default:
                        break;
                }
            }
        }

        private Wave GetWave(int wIndex)
        {
            if (waves == null || wIndex < 0 || wIndex >= waves.Length) return null;
            return waves[wIndex];
        }

        public bool IsWaveInProgress() => waveInProgress;
        public bool IsWaitingForUpgrade() => waitingForUpgrade;
        public bool IsWaitingForShardChoice() => waitingForShardChoice;
        public bool AreAllWavesComplete() => waitingForUpgrade && !waitingForShardChoice &&
                                             waveIndex >= waves.Length - 1 && HaveAllRequiredShardChoicesResolved();

        // Called by reporter when an enemy is destroyed/dies
        public void NotifyEnemyDestroyed(Vector3 position, int groupId, int enemyWaveIndex)
        {
            if (enemyWaveIndex == waveIndex)
                aliveEnemies = Mathf.Max(0, aliveEnemies - 1);
            else
                return;

            if (groupId < 0 || groupAliveCounts == null || groupId >= groupAliveCounts.Length)
                return;

            groupAliveCounts[groupId] = Mathf.Max(0, groupAliveCounts[groupId] - 1);
            groupLastDeathPositions[groupId] = position;

            if (groupAliveCounts[groupId] == 0 && !groupCrystalSpawned[groupId])
            {
                groupCrystalSpawned[groupId] = true;
                SpawnHealingCrystal(groupLastDeathPositions[groupId]);
            }
        }

        ///<summary>
        /// Notifies WaveManager when a spawned enemy GameObject is destroyed
        /// </summary>
        public class WaveEnemyReporter : MonoBehaviour
        {
            private WaveManager manager;
            private bool initialized = false;
            private int groupId = -1;
            private int waveId = -1;

            public void Init(WaveManager wm, int reporterGroupId, int reporterWaveId)
            {
                manager = wm;
                initialized = true;
                groupId = reporterGroupId;
                waveId = reporterWaveId;
            }

            private void OnDestroy()
            {
                if (!initialized) return;
                if (manager == null) return;

                manager.NotifyEnemyDestroyed(transform.position, groupId, waveId);
            }
        }
    }
}
