using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace CyberVeil.Systems
{
    /// <summary>
    /// Manages progression through game levels
    /// Tracks current level and handles scene transitions after portal upgrades
    /// Persists across scenes using DontDestroyOnLoad
    /// </summary>
    public class SceneProgressManager : MonoBehaviour
    {
        public static SceneProgressManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (Instance != null)
                return;

            SceneProgressManager existing = FindFirstObjectByType<SceneProgressManager>(
                FindObjectsInactive.Include);
            if (existing != null)
                return;

            new GameObject(nameof(SceneProgressManager)).AddComponent<SceneProgressManager>();
        }

        [Header("Level Scenes")]
        [Tooltip("Ordered list of level scene names")]
        [SerializeField] private string[] levelSceneNames = new string[]
        {
            "CyberVeil_Level1",
            "CyberVeil_Level2",
            "CyberVeil_Level3"
        };

        [Header("Transition Settings")]
        [SerializeField] private float transitionDelay = 1.5f; // Delay before loading next scene

        private int currentLevelIndex = 0;
        public int CurrentLevelIndex => currentLevelIndex;

        private void Awake()
        {
            // Singleton pattern - persist across scenes
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                SceneManager.sceneLoaded += HandleSceneLoaded;
                SyncCurrentLevelIndex(SceneManager.GetActiveScene());
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            SyncCurrentLevelIndex(SceneManager.GetActiveScene());
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Instance = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SyncCurrentLevelIndex(scene);
        }

        private void SyncCurrentLevelIndex(Scene scene)
        {
            string currentSceneName = scene.name;
            for (int i = 0; i < levelSceneNames.Length; i++)
            {
                if (levelSceneNames[i] == currentSceneName)
                {
                    currentLevelIndex = i;
                    Debug.Log($"SceneProgressManager: Starting at level {i + 1} ({currentSceneName})");
                    break;
                }
            }
        }

        /// <summary>
        /// Loads the next level scene in sequence
        /// Called after the player completes portal upgrades
        /// </summary>
        public void LoadNextLevel()
        {
            StartCoroutine(LoadNextLevelCoroutine());
        }

        private IEnumerator LoadNextLevelCoroutine()
        {
            currentLevelIndex++;

            // Check if there are more levels
            if (currentLevelIndex >= levelSceneNames.Length)
            {
                Debug.Log("All levels completed!");
                // You can add end-game logic here (e.g., credits, final screen)
                yield break;
            }

            string nextSceneName = levelSceneNames[currentLevelIndex];
            Debug.Log($"Loading next level: {nextSceneName}");

            // Fade to black before loading the next scene
            ScreenFadeManager fadeManager = ScreenFadeManager.Instance;
            if (fadeManager != null)
            {
                bool sceneLoaded = false;

                // Start fade to black and load scene when fade completes
                fadeManager.FadeToBlack(() =>
                {
                    SceneManager.LoadScene(nextSceneName);
                    sceneLoaded = true;
                });

                // Wait for the scene to load
                yield return new WaitUntil(() => sceneLoaded);

                // Give the new scene a moment to initialize
                yield return new WaitForSeconds(0.5f);

                // Fade from black in the new scene
                fadeManager.FadeFromBlack();
            }
            else
            {
                // Fallback if fade manager is not available
                yield return new WaitForSeconds(transitionDelay);
                SceneManager.LoadScene(nextSceneName);
            }
        }

        /// <summary>
        /// Resets progress to the first level
        /// </summary>
        public void ResetToFirstLevel()
        {
            currentLevelIndex = 0;
            SceneManager.LoadScene(levelSceneNames[0]);
        }

        /// <summary>
        /// Gets the name of the current level
        /// </summary>
        public string GetCurrentLevelName()
        {
            if (currentLevelIndex >= 0 && currentLevelIndex < levelSceneNames.Length)
                return levelSceneNames[currentLevelIndex];
            return "Unknown";
        }

        /// <summary>
        /// Checks if there's a next level available
        /// </summary>
        public bool HasNextLevel()
        {
            return currentLevelIndex + 1 < levelSceneNames.Length;
        }
    }
}
