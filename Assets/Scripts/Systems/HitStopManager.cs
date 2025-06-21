using UnityEngine;
using System.Collections;

namespace CyberVeil.Systems
{
    public class HitstopManager : MonoBehaviour
    {
        public static HitstopManager Instance { get; private set; }

        [SerializeField] private float minTimeScale = 0.01f; //safety

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
            }
            else
            {
                Instance = this;
            }
        }

        public void DoHitstop(float duration, float timeScale = 0f)
        {
            StopAllCoroutines();
            StartCoroutine(HitstopCoroutine(duration, timeScale));
        }

        private IEnumerator HitstopCoroutine(float duration, float timeScale)
        {
            Time.timeScale = Mathf.Max(timeScale, minTimeScale);
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = 1f;
        }
    }
}
