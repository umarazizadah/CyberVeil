using UnityEngine;

namespace CyberVeil.Systems
{
    public class WaveTrigger : MonoBehaviour
    {
        public WaveManager waveManager;

        void Update()
        {
            if (!waveManager.IsWaveInProgress() && Input.GetKeyDown(KeyCode.N))
            {
                waveManager.StartNextWave();
            }
        }
    }
}
