
using System.Collections;

namespace CyberVeil.VFX
{
    /// <summary>
    /// Interface for playing visual effects during an enemy attack
    /// Allows different enemies to trigger unique effects
    /// </summary>
    public interface IAttackEffect
    {
        IEnumerator PerformEffect(float delay);
    }
}