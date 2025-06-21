
namespace CyberVeil.Combat
{
    /// <summary>
    /// Represents an entity that can take damage and die
    /// Used to standardize damage behavior across enemies, players, and objects
    /// </summary>
    public interface IDamagable
    {
        void TakeDamage(int amount);
        void Die();
    }
}
