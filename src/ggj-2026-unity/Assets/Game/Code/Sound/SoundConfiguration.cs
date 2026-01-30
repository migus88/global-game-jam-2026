using System.Collections.Generic;
using UnityEngine;

namespace Game.Sound
{
    [CreateAssetMenu(fileName = "SoundConfiguration", menuName = "Game/Sound Configuration")]
    public class SoundConfiguration : ScriptableObject
    {
        [field: SerializeField, Header("Background Music")]
        public AudioClip[] BackgroundMusic { get; private set; }

        [field: SerializeField, Header("Ambient Phrases")]
        public AmbientPhrase[] AmbientPhrases { get; private set; }

        [field: SerializeField, Header("Sound Effects")]
        public SoundEffectEntry[] SoundEffects { get; private set; }

        private Dictionary<SoundEffectType, AudioClip[]> _soundEffectMap;

        public void Initialize()
        {
            _soundEffectMap = new Dictionary<SoundEffectType, AudioClip[]>();

            if (SoundEffects == null)
            {
                return;
            }

            foreach (var entry in SoundEffects)
            {
                if (entry.Type != SoundEffectType.None && entry.Clips != null && entry.Clips.Length > 0)
                {
                    _soundEffectMap[entry.Type] = entry.Clips;
                }
            }
        }

        public AudioClip GetRandomBackgroundMusic()
        {
            if (BackgroundMusic == null || BackgroundMusic.Length == 0)
            {
                return null;
            }

            return BackgroundMusic[Random.Range(0, BackgroundMusic.Length)];
        }

        public AmbientPhrase GetRandomAmbientPhrase()
        {
            if (AmbientPhrases == null || AmbientPhrases.Length == 0)
            {
                return null;
            }

            return AmbientPhrases[Random.Range(0, AmbientPhrases.Length)];
        }

        public AudioClip GetSoundEffect(SoundEffectType type)
        {
            if (_soundEffectMap == null)
            {
                Initialize();
            }

            if (_soundEffectMap.TryGetValue(type, out var clips) && clips.Length > 0)
            {
                return clips[Random.Range(0, clips.Length)];
            }

            return null;
        }
    }
}
