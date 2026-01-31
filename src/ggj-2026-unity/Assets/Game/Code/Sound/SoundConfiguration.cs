using System.Collections.Generic;
using UnityEngine;

namespace Game.Sound
{
    [CreateAssetMenu(fileName = "SoundConfiguration", menuName = "Game/Sound Configuration")]
    public class SoundConfiguration : ScriptableObject
    {
        [field: SerializeField, Header("Background Music")]
        public AudioClip[] BackgroundMusic { get; private set; }

        [field: SerializeField, Header("Level Intro")]
        public AmbientPhrase IntroPhrase { get; private set; }

        [field: SerializeField, Header("Ambient Phrases")]
        public AmbientPhrase[] AmbientPhrases { get; private set; }

        [field: SerializeField, Range(0f, 2f)]
        public float AmbientPhraseVolume { get; private set; } = 1f;

        [field: SerializeField, Header("Sound Effects")]
        public SoundEffectEntry[] SoundEffects { get; private set; }

        [field: SerializeField, Header("Named Sound Effects")]
        public NamedSoundEffectEntry[] NamedSoundEffects { get; private set; }

        private Dictionary<SoundEffectType, AudioClip[]> _soundEffectMap;
        private Dictionary<string, AudioClip[]> _namedSoundEffectMap;

        public void Initialize()
        {
            _soundEffectMap = new Dictionary<SoundEffectType, AudioClip[]>();
            _namedSoundEffectMap = new Dictionary<string, AudioClip[]>();

            if (SoundEffects != null)
            {
                foreach (var entry in SoundEffects)
                {
                    if (entry.Type != SoundEffectType.None && entry.Clips != null && entry.Clips.Length > 0)
                    {
                        _soundEffectMap[entry.Type] = entry.Clips;
                    }
                }
            }

            if (NamedSoundEffects != null)
            {
                foreach (var entry in NamedSoundEffects)
                {
                    if (!string.IsNullOrEmpty(entry.Name) && entry.Clips != null && entry.Clips.Length > 0)
                    {
                        _namedSoundEffectMap[entry.Name] = entry.Clips;
                    }
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

        public AudioClip GetNamedSoundEffect(string name)
        {
            if (_namedSoundEffectMap == null)
            {
                Initialize();
            }

            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (_namedSoundEffectMap.TryGetValue(name, out var clips) && clips.Length > 0)
            {
                return clips[Random.Range(0, clips.Length)];
            }

            return null;
        }
    }
}
