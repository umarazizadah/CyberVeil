using UnityEngine;
using System.Collections;

namespace CyberVeil.Player
{
    public class toggleAxe2 : MonoBehaviour
    {
        public void ShowAxe2()
        {
            gameObject.SetActive(true); //enable the GameObject
        }

        public void HideAxe2()
        {
            if (gameObject.activeInHierarchy)
                StartCoroutine(HideAxeWithDelay(0.35f));
        }

        private IEnumerator HideAxeWithDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            gameObject.SetActive(false);

        }
    }
}

