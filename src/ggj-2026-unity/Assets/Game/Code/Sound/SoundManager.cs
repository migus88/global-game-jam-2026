using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Sound
{
    public class SoundManager : IDisposable
    {
        private readonly SoundConfiguration _configuration;
        private readonly GameObject _audioSourceContainer;
        private readonly AudioSource _backgroundMusicSource;
        private readonly List<AudioSource> _audioSourcePool;
        private readonly int _poolSize;

        private bool _isDisposed;

        public SoundManager(SoundConfiguration configuration, int poolSize)
        {
            _configuration = configuration;
            _poolSize = poolSize;
            _audioSourcePool = new List<AudioSource>(poolSize);

            _configuration.Initialize();

            _audioSourceContainer = new GameObject("[SoundManager]");
            Object.DontDestroyOnLoad(_audioSourceContainer);

            _backgroundMusicSource = CreateAudioSource("BackgroundMusic", true, false);
            _backgroundMusicSource.loop = true;
            _backgroundMusicSource.volume = 0.5f;

            InitializePool();
        }

        private void InitializePool()
        {
            for (int i = 0; i < _poolSize; i++)
            {
                var source = CreateAudioSource($"PooledSource_{i}", false, true);
                source.gameObject.SetActive(false);
                _audioSourcePool.Add(source);
            }
        }

        private AudioSource CreateAudioSource(string name, bool is2D, bool pooled)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_audioSourceContainer.transform);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = is2D ? 0f : 1f;

            if (!is2D)
            {
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 2f;
                source.maxDistance = 50f;
            }

            return source;
        }

        public void PlayBackgroundMusic(AudioClip clip = null)
        {
            if (_isDisposed)
            {
                return;
            }

            var musicClip = clip ?? _configuration.GetRandomBackgroundMusic();

            if (musicClip == null)
            {
                return;
            }

            _backgroundMusicSource.clip = musicClip;
            _backgroundMusicSource.Play();
        }

        public void StopBackgroundMusic()
        {
            if (_isDisposed)
            {
                return;
            }

            _backgroundMusicSource.Stop();
        }

        public void StopAllSounds()
        {
            if (_isDisposed)
            {
                return;
            }

            foreach (var source in _audioSourcePool)
            {
                if (source != null && source.isPlaying)
                {
                    source.Stop();
                    source.clip = null;
                    source.gameObject.SetActive(false);
                }
            }
        }

        public void SetBackgroundMusicVolume(float volume)
        {
            if (_isDisposed)
            {
                return;
            }

            _backgroundMusicSource.volume = Mathf.Clamp01(volume);
        }

        public (AmbientPhrase phrase, AudioSource source) PlayRandomAmbientPhrase(Vector3 position)
        {
            if (_isDisposed)
            {
                return (null, null);
            }

            var phrase = _configuration.GetRandomAmbientPhrase();

            if (phrase?.Clip == null)
            {
                return (null, null);
            }

            var volume = _configuration.AmbientPhraseVolume;
            var source = PlayClipAtPositionWithSource(phrase.Clip, position, volume);
            return (phrase, source);
        }

        public AudioSource PlayClipAtPositionWithSource(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (_isDisposed || clip == null)
            {
                return null;
            }

            var source = GetPooledSource();

            if (source == null)
            {
                AudioSource.PlayClipAtPoint(clip, position, volume);
                return null;
            }

            source.transform.position = position;
            source.spatialBlend = 1f;
            source.volume = volume;
            source.clip = clip;
            source.gameObject.SetActive(true);
            source.Play();

            ReturnToPoolAfterPlaying(source, clip.length).Forget();
            return source;
        }

        public void PlaySoundEffect(SoundEffectType type, Vector3? position = null)
        {
            if (_isDisposed)
            {
                return;
            }

            var clip = _configuration.GetSoundEffect(type);

            if (clip == null)
            {
                return;
            }

            if (position.HasValue)
            {
                PlayClipAtPosition(clip, position.Value);
            }
            else
            {
                PlayClip2D(clip);
            }
        }

        public void PlayNamedSoundEffect(string name, Vector3? position = null, float volume = 1f)
        {
            if (_isDisposed)
            {
                return;
            }

            var clip = _configuration.GetNamedSoundEffect(name);

            if (clip == null)
            {
                return;
            }

            if (position.HasValue)
            {
                PlayClipAtPosition(clip, position.Value, volume);
            }
            else
            {
                PlayClip2D(clip, volume);
            }
        }

        public void PlayClipAtPosition(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (_isDisposed || clip == null)
            {
                return;
            }

            var source = GetPooledSource();

            if (source == null)
            {
                AudioSource.PlayClipAtPoint(clip, position, volume);
                return;
            }

            source.transform.position = position;
            source.spatialBlend = 1f;
            source.volume = volume;
            source.clip = clip;
            source.gameObject.SetActive(true);
            source.Play();

            ReturnToPoolAfterPlaying(source, clip.length).Forget();
        }

        public void PlayClip2D(AudioClip clip, float volume = 1f)
        {
            if (_isDisposed || clip == null)
            {
                return;
            }

            var source = GetPooledSource();

            if (source == null)
            {
                return;
            }

            source.spatialBlend = 0f;
            source.volume = volume;
            source.clip = clip;
            source.gameObject.SetActive(true);
            source.Play();

            ReturnToPoolAfterPlaying(source, clip.length).Forget();
        }

        private AudioSource GetPooledSource()
        {
            foreach (var source in _audioSourcePool)
            {
                if (!source.gameObject.activeSelf)
                {
                    return source;
                }
            }

            var newSource = CreateAudioSource($"PooledSource_{_audioSourcePool.Count}", false, true);
            newSource.gameObject.SetActive(false);
            _audioSourcePool.Add(newSource);

            return newSource;
        }

        private async UniTaskVoid ReturnToPoolAfterPlaying(AudioSource source, float duration)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(duration + 0.1f));

            if (source != null && !_isDisposed)
            {
                source.Stop();
                source.clip = null;
                source.gameObject.SetActive(false);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            if (_audioSourceContainer != null)
            {
                Object.Destroy(_audioSourceContainer);
            }

            _audioSourcePool.Clear();
        }
    }
}
