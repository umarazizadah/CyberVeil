/// <summary>
/// Interface for components that respond to damage events (like a hurt animation)
/// </summary>
namespace CyberVeil.Combat
{
    public interface IDamageStateResponder
    {
        void OnDamaged();
    }
}
