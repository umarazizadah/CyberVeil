using UnityEngine;
using UnityEngine.SceneManagement;
using CyberVeil.Systems;

namespace CyberVeil.UI
{
    /// <summary>
    /// Handles main menu buttons
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        /// <summary>
        /// Called when Start button is clicked - loads Level 1
        /// </summary>
        public void StartGame()
        {
            VeilRunManager.ResetForNewRun();
            SceneManager.LoadScene("CyberVeil_Level1");
        }

        /// <summary>
        /// Called when Quit button is clicked - exits the game
        /// </summary>
        public void QuitGame()
        {
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
    }
}
