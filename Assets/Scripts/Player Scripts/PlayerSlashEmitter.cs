using CyberVeil.VFX;
using UnityEngine;

namespace CyberVeil.Player
{
    /// <summary>
    /// Emits all light-slash and impact VFX from one prefab-owned component.
    /// Scene objects remain particle templates for ParticleManager, not attack logic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSlashEmitter : MonoBehaviour
    {
        [SerializeField] private Transform origin;

        private void Awake()
        {
            if (origin == null)
                origin = transform;
        }

        public void EmitSlash(PlayerAttackStep step, Vector3 forwardDirection, bool veilSurgeActive)
        {
            if (step == null || ParticleManager.Instance == null)
                return;

            Transform source = origin != null ? origin : transform;
            Vector3 forward = forwardDirection.sqrMagnitude > 0.001f
                ? forwardDirection.normalized
                : source.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 offset = step.VfxLocalOffset;
            Vector3 position = source.position
                + forward * (step.VfxForwardDistance + offset.z)
                + right * offset.x
                + Vector3.up * offset.y;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up)
                * Quaternion.Euler(step.VfxEulerOffset);

            bool hasCompleteSurgeSet =
                ParticleManager.Instance.HasEffect(VFXType.SurgeSlash1)
                && ParticleManager.Instance.HasEffect(VFXType.SurgeSlash2)
                && ParticleManager.Instance.HasEffect(VFXType.SurgeSlash3);
            VFXType requested = veilSurgeActive
                && hasCompleteSurgeSet
                && ParticleManager.Instance.HasEffect(step.SurgeSlashVfx)
                    ? step.SurgeSlashVfx
                    : step.SlashVfx;
            ParticleManager.Instance.TryPlayEffect(requested, position, rotation);
        }

        public void EmitImpact(Vector3 position)
        {
            if (ParticleManager.Instance == null)
                return;

            ParticleManager.Instance.TryPlayEffect(
                VFXType.SlashHit,
                position,
                Quaternion.identity);
            ParticleManager.Instance.TryPlayEffect(
                VFXType.SlashImpact,
                position,
                Quaternion.identity);
        }
    }
}
