using UnityEngine;
using CyberVeil.Systems;

namespace CyberVeil.Audio
{
    /// <summary>
    /// Plays a damage sound using the specified sound type and volume
    /// </summary>
    public class DamageSoundEffect : MonoBehaviour, IDamageSound 
    {
        [Header("Sound Settings")]
        public SoundType soundType = SoundType.PLAYERDAMAGE;
        [SerializeField] private float volume = 0.5f;

        public void PlayDamageSound() // Triggers the assigned damage sound via SoundManager
        {
            SoundManager.PlaySound(soundType, volume);
        }
    }
}
