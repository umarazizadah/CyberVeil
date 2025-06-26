using System.Collections;
namespace CyberVeil.Enemies
{
    /// <summary>
    /// Interface standardizes how different enemies execute attacks in the game
    /// Polymorphic behavior across diverse enemy types for example, mushroom might use a basic melee swing, while a slime might leap
    /// Coroutine allows for precise control over attack timing, such as syncing damage with animations or VFX
    /// </summary>
    public interface IEnemyAttack
    {
        IEnumerator ExecuteAttack();
    }
}
