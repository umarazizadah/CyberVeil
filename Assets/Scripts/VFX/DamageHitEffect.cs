using UnityEngine;
using System.Collections;

namespace CyberVeil.VFX
{
    public class DamageHitEffect : MonoBehaviour, IDamageVisual
    {
        public DissolveEffectHandler shaderEffectHandler;
        public void PlayDamageEffect()
        {
            //switch to dmg material
            shaderEffectHandler.targetRenderer.material = shaderEffectHandler.dissolveDamageMaterial;

            //instantly
            shaderEffectHandler.targetRenderer.material.SetFloat(shaderEffectHandler.dissolveProperty, 1f);

            //waits, then return to base
            StartCoroutine(ResetToBaseMaterial());
        }

        public IEnumerator ResetToBaseMaterial()
        {
            yield return new WaitForSeconds(0.3f); //brief flash duration

            //fully visible again
            shaderEffectHandler.targetRenderer.material = shaderEffectHandler.baseMaterial;
            shaderEffectHandler.targetRenderer.material.SetFloat(shaderEffectHandler.dissolveProperty, 0f);
        }
    }
}
