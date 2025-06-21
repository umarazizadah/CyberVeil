using UnityEngine;
using System.Collections;
using System;

namespace CyberVeil.Systems
{
    /// <summary>
    /// Manages the spawning and progression of enemy waves
    /// Handles wave definitions, spawn timing, and invokes events when waves start or end
    /// Supports randomized enemy types and spawn points for each wave
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        [System.Serializable]
        public class Wave // Can edit waves in the unity editor
        {
            public string name;
            public GameObject[] enemyPrefabs;
            public int enemyCount;
            public float spawnRate = 10f;
        }

        public Wave[] waves;
        public Transform[] spawnPoints;

        private int currentWaveIndex = 0;
        private bool waveInProgress = false;

        // Events for external systems (UI, music)
        public static event Action<int> OnWaveStarted;
        public static event Action<int> OnWaveEnded;

        public void StartNextWave()
        {
            if (currentWaveIndex < waves.Length)
            {
                StartCoroutine(SpawnWave(waves[currentWaveIndex]));
                OnWaveStarted?.Invoke(currentWaveIndex);
                currentWaveIndex++;
            }
            else
            {
                Debug.Log("All waves done");
            }
        }

        private IEnumerator SpawnWave(Wave wave)
        {
            waveInProgress = true;

            for (int i = 0; i < wave.enemyCount; i++) // Spawns one enemy per iteration based on the enemycount
            {
                // Picks a random enemy prefab and random spawn location
                GameObject enemyPrefab = wave.enemyPrefabs[UnityEngine.Random.Range(0, wave.enemyPrefabs.Length)];
                Transform spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];

                Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);
                yield return new WaitForSeconds(wave.spawnRate);
            }

            waveInProgress = false;
            OnWaveEnded?.Invoke(currentWaveIndex - 1);
        }

        public bool IsWaveInProgress() // Simple getter method that lets other scripts safely check if a wave is still active
        {
            return waveInProgress;
        }
    }
}
