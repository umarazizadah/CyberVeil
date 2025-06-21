using UnityEngine;
/// <summary>
/// Interface for components that can receive knockback from an attacker
/// </summary>

namespace CyberVeil.Combat
{
    public interface IKnockbackable
    {
        void ApplyKnockback(Transform attacker);
    }
}
