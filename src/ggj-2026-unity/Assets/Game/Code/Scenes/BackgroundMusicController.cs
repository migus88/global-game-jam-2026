using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.Scenes.Events;
using Game.Sound;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Scenes
{
    public class BackgroundMusicController : MonoBehaviour
    {
        private EventAggregator _eventAggregator;
        private SceneConfiguration _sceneConfiguration;
        private SoundConfiguration _soundConfiguration;
        private SoundManager _soundManager;

        private AudioSource _musicSource;
        private List<int> _shuffledIndices;
        private int _currentShuffleIndex;
        private bool _isInGame;
        private bool _isTransitioning;

        [Inject]
        public void Construct(
            EventAggregator eventAggregator,
            SceneConfiguration sceneConfiguration,
            SoundConfiguration soundConfiguration,
            SoundManager soundManager)
        {
            _eventAggregator = eventAggregator;
            _sceneConfiguration = sceneConfiguration;
            _soundConfiguration = soundConfiguration;
            _soundManager = soundManager;
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = false;
            _musicSource.spatialBlend = 0f;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();
            InitializeShuffleList();

            _eventAggregator?.Subscribe<LoadingCompletedEvent>(OnLoadingCompleted);
            _eventAggregator?.Subscribe<MainMenuReadyEvent>(OnMainMenuReady);
            _eventAggregator?.Subscribe<PlayerWonEvent>(OnPlayerWon);

            SetVolume(_sceneConfiguration?.MainMenuMusicVolume ?? 0.3f);
            PlayNextTrack();
        }

        private void ResolveDependenciesIfNeeded()
        {
            if (_eventAggregator != null && _sceneConfiguration != null && _soundConfiguration != null && _soundManager != null)
            {
                return;
            }

            var lifetimeScope = FindAnyObjectByType<LifetimeScope>();

            if (lifetimeScope == null)
            {
                return;
            }

            _eventAggregator ??= lifetimeScope.Container.Resolve<EventAggregator>();
            _sceneConfiguration ??= lifetimeScope.Container.Resolve<SceneConfiguration>();
            _soundConfiguration ??= lifetimeScope.Container.Resolve<SoundConfiguration>();
            _soundManager ??= lifetimeScope.Container.Resolve<SoundManager>();
        }

        private void Update()
        {
            if (_musicSource != null && !_musicSource.isPlaying && !_isTransitioning)
            {
                PlayNextTrack();
            }
        }

        private void OnDestroy()
        {
            _eventAggregator?.Unsubscribe<LoadingCompletedEvent>(OnLoadingCompleted);
            _eventAggregator?.Unsubscribe<MainMenuReadyEvent>(OnMainMenuReady);
            _eventAggregator?.Unsubscribe<PlayerWonEvent>(OnPlayerWon);
        }

        private void OnLoadingCompleted(LoadingCompletedEvent evt)
        {
            _isInGame = evt.IsInGame;

            var targetVolume = _isInGame
                ? (_sceneConfiguration?.GameplayMusicVolume ?? 0.6f)
                : (_sceneConfiguration?.MainMenuMusicVolume ?? 0.3f);

            var duration = _sceneConfiguration?.MusicTransitionDuration ?? 1f;

            TransitionVolumeAsync(targetVolume, duration).Forget();

            if (_isInGame)
            {
                PlayIntroSound();
            }
        }

        private void PlayIntroSound()
        {
            var introClip = _soundConfiguration?.IntroSound;

            if (introClip == null)
            {
                return;
            }

            _soundManager?.PlayClip2D(introClip);
        }

        private void OnMainMenuReady(MainMenuReadyEvent evt)
        {
            _isInGame = false;

            var targetVolume = _sceneConfiguration?.MainMenuMusicVolume ?? 0.3f;
            var duration = _sceneConfiguration?.MusicTransitionDuration ?? 1f;

            TransitionVolumeAsync(targetVolume, duration).Forget();
        }

        private void OnPlayerWon(PlayerWonEvent evt)
        {
            _musicSource?.Stop();
            _soundManager?.StopAllSounds();
        }

        private void InitializeShuffleList()
        {
            var musicCount = _soundConfiguration?.BackgroundMusic?.Length ?? 0;

            if (musicCount == 0)
            {
                _shuffledIndices = new List<int>();
                return;
            }

            _shuffledIndices = new List<int>(musicCount);

            for (int i = 0; i < musicCount; i++)
            {
                _shuffledIndices.Add(i);
            }

            ShuffleList();
            _currentShuffleIndex = 0;
        }

        private void ShuffleList()
        {
            for (int i = _shuffledIndices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_shuffledIndices[i], _shuffledIndices[j]) = (_shuffledIndices[j], _shuffledIndices[i]);
            }
        }

        private void PlayNextTrack()
        {
            if (_soundConfiguration?.BackgroundMusic == null || _soundConfiguration.BackgroundMusic.Length == 0)
            {
                return;
            }

            if (_shuffledIndices.Count == 0)
            {
                return;
            }

            if (_currentShuffleIndex >= _shuffledIndices.Count)
            {
                ShuffleList();
                _currentShuffleIndex = 0;
            }

            var trackIndex = _shuffledIndices[_currentShuffleIndex];
            var clip = _soundConfiguration.BackgroundMusic[trackIndex];

            if (clip != null)
            {
                _musicSource.clip = clip;
                _musicSource.Play();
                Debug.Log($"BackgroundMusicController: Playing track {trackIndex}: {clip.name}");
            }

            _currentShuffleIndex++;
        }

        private async UniTaskVoid TransitionVolumeAsync(float targetVolume, float duration)
        {
            _isTransitioning = true;

            var startVolume = _musicSource.volume;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                _musicSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
                await UniTask.Yield();
            }

            _musicSource.volume = targetVolume;
            _isTransitioning = false;
        }

        private void SetVolume(float volume)
        {
            if (_musicSource != null)
            {
                _musicSource.volume = Mathf.Clamp01(volume);
            }
        }
    }
}
