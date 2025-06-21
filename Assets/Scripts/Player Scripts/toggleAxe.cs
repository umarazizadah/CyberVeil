using UnityEngine;

namespace CyberVeil.Player
{
    public class toggleAxe : MonoBehaviour
    {
        public void ShowAxe()
        {
            gameObject.SetActive(true); //enable the GameObject
        }

        public void HideAxe()
        {
            gameObject.SetActive(false); //disable the GameObject

        }

    }
}