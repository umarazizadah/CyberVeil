using UnityEngine;

namespace CyberVeil.VFX
{
    public class dustParticle : MonoBehaviour
    {

        public void ShowDustParticle()
        {
            gameObject.SetActive(true); //enable the GameObject
        }

        public void HideDustParticle()
        {
            gameObject.SetActive(false); //disable the GameObject
        }

    }
}
