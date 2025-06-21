using UnityEngine;
using System.Collections;
using CyberVeil.Player;

namespace CyberVeil.VFX
{
    public class DissolveEffectHandler : MonoBehaviour
    {
        [Header("References")]
        public Renderer targetRenderer;
        public Material baseMaterial;
        public Material dissolveDashMaterial;
        public Material dissolveDamageMaterial;
        public toggleAxe toggleAxe1;
        public toggleAxe2 toggleAxe2;

        [Header("Dissolve Settings")]
        public string dissolveProperty = "_Dissolve";


        private Material runtimeMat;

        private void Start()
        {
            targetRenderer.material = baseMaterial;

        }
        public IEnumerator DissolveOut(Material dissolveMaterial, float dissolveDuration)
        {
            targetRenderer.material = dissolveMaterial;
            runtimeMat = targetRenderer.material;

            toggleAxe1?.HideAxe();
            toggleAxe2?.HideAxe2();

            float timer = 0f;
            while (timer < dissolveDuration)
            {
                float val = Mathf.Lerp(0f, 1f, timer / dissolveDuration);
                runtimeMat.SetFloat(dissolveProperty, val);
                timer += Time.deltaTime;
                yield return null;
            }

            runtimeMat.SetFloat(dissolveProperty, 1f);
        }

        public IEnumerator DissolveIn(Material dissolveMaterial, float dissolveDuration)
        {
            float timer = 0f;
            while (timer < dissolveDuration)
            {
                float val = Mathf.Lerp(1f, 0f, timer / dissolveDuration);
                runtimeMat.SetFloat(dissolveProperty, val);
                timer += Time.deltaTime;
                yield return null;
            }

            runtimeMat.SetFloat(dissolveProperty, 0f);

            toggleAxe1?.ShowAxe();
            toggleAxe2?.ShowAxe2();

            targetRenderer.material = baseMaterial;
        }

        public void FlashDamageMaterial(float duration)
        {
            StartCoroutine(FlashMaterial(dissolveDamageMaterial, duration));
        }

        private IEnumerator FlashMaterial(Material tempMat, float duration)
        {
            targetRenderer.material = tempMat;
            targetRenderer.material.SetFloat(dissolveProperty, 1f);
            yield return new WaitForSeconds(duration);
            targetRenderer.material = baseMaterial;
            targetRenderer.material.SetFloat(dissolveProperty, 0f);
        }

    }
}
