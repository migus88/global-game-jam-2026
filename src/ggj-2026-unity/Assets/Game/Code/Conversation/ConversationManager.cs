using System;
using Cysharp.Threading.Tasks;
using Game.Conversation.Data;
using Game.Conversation.Events;
using Game.Events;
using Game.GameState;
using Game.GameState.Events;
using Game.Scenes.Events;
using Game.Sound;
using Migs.MLock.Interfaces;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Conversation
{
    public class ConversationManager : MonoBehaviour
    {
        private static readonly int SpeedAnimatorHash = Animator.StringToHash("Speed");
        private static readonly int MotionSpeedAnimatorHash = Animator.StringToHash("MotionSpeed");

        [SerializeField]
        private ConversationUI _conversationUI;

        private EventAggregator _eventAggregator;
        private ConversationConfiguration _configuration;
        private SoundManager _soundManager;
        private GameLockService _lockService;

        private Transform _currentEnemy;
        private Transform _currentPlayer;
        private Animator _playerAnimator;
        private ConversationQuestion _currentQuestion;
        private ILock<GameLockTags> _currentLock;
        private bool _isInConversation;
        private bool _hasPlayerWon;

        public bool IsInConversation => _isInConversation;

        [Inject]
        public void Construct(
            EventAggregator eventAggregator,
            ConversationConfiguration configuration,
            SoundManager soundManager,
            GameLockService lockService)
        {
            _eventAggregator = eventAggregator;
            _configuration = configuration;
            _soundManager = soundManager;
            _lockService = lockService;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();
            _eventAggregator?.Subscribe<PlayerCaughtEvent>(OnPlayerCaught);
            _eventAggregator?.Subscribe<PlayerWonEvent>(OnPlayerWon);

            if (_conversationUI != null)
            {
                _conversationUI.AnswerSelected += OnAnswerSelected;
                _conversationUI.Hide();
            }
        }

        private void ResolveDependenciesIfNeeded()
        {
            if (_eventAggregator != null && _configuration != null && _soundManager != null && _lockService != null)
            {
                return;
            }

            var lifetimeScope = FindAnyObjectByType<LifetimeScope>();

            if (lifetimeScope == null)
            {
                return;
            }

            _eventAggregator ??= lifetimeScope.Container.Resolve<EventAggregator>();
            _configuration ??= lifetimeScope.Container.Resolve<ConversationConfiguration>();
            _soundManager ??= lifetimeScope.Container.Resolve<SoundManager>();
            _lockService ??= lifetimeScope.Container.Resolve<GameLockService>();
        }

        private void OnDestroy()
        {
            _eventAggregator?.Unsubscribe<PlayerCaughtEvent>(OnPlayerCaught);
            _eventAggregator?.Unsubscribe<PlayerWonEvent>(OnPlayerWon);
            _currentLock?.Dispose();

            if (_conversationUI != null)
            {
                _conversationUI.AnswerSelected -= OnAnswerSelected;
            }
        }

        private void OnPlayerWon(PlayerWonEvent evt)
        {
            _hasPlayerWon = true;
        }

        private void OnPlayerCaught(PlayerCaughtEvent evt)
        {
            if (_isInConversation || _hasPlayerWon)
            {
                return;
            }

            StartConversation(evt.Enemy, evt.Player).Forget();
        }

        private async UniTaskVoid StartConversation(Transform enemy, Transform player)
        {
            _isInConversation = true;
            _currentEnemy = enemy;
            _currentPlayer = player;

            Debug.Log("[ConversationManager] Starting conversation");

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            _currentLock = _lockService?.Lock(GameLockTags.All);

            // Hide the player and stop animations
            if (_currentPlayer != null)
            {
                _playerAnimator = _currentPlayer.GetComponentInChildren<Animator>();

                if (_playerAnimator != null)
                {
                    _playerAnimator.SetFloat(SpeedAnimatorHash, 0f);
                    _playerAnimator.SetFloat(MotionSpeedAnimatorHash, 0f);
                }

                _currentPlayer.gameObject.SetActive(false);
            }

            _eventAggregator?.Publish(new ConversationStartedEvent(enemy));

            await UniTask.Delay(TimeSpan.FromSeconds(_configuration.DelayBeforeQuestion));

            _currentQuestion = _configuration.GetRandomQuestion();

            if (_currentQuestion == null)
            {
                Debug.LogWarning("[ConversationManager] No question found in configuration!");
                EndConversation(true).Forget();
                return;
            }

            Debug.Log($"[ConversationManager] Showing question: {_currentQuestion.Text}");

            if (_conversationUI == null)
            {
                Debug.LogError("[ConversationManager] ConversationUI is not assigned!");
            }

            // Show question with disabled answers
            _conversationUI?.ShowQuestion(_currentQuestion);

            // Play audio and wait for it to finish
            if (_currentQuestion.AudioClip != null)
            {
                _soundManager?.PlayClip2D(_currentQuestion.AudioClip);
                await UniTask.Delay(TimeSpan.FromSeconds(_currentQuestion.AudioClip.length));
            }

            // Enable answer buttons after audio finishes
            _conversationUI?.EnableAnswers();
        }

        private void OnAnswerSelected(int answerIndex)
        {
            if (_currentQuestion == null || _currentQuestion.Answers == null)
            {
                return;
            }

            if (answerIndex < 0 || answerIndex >= _currentQuestion.Answers.Length)
            {
                return;
            }

            // Show only the selected answer (disabled)
            _conversationUI?.ShowSelectedAnswerOnly(answerIndex);

            var answer = _currentQuestion.Answers[answerIndex];
            HandleAnswer(answer.IsCorrect).Forget();
        }

        private async UniTaskVoid HandleAnswer(bool isCorrect)
        {
            _conversationUI?.HideAnswers();

            var response = isCorrect
                ? _configuration.GetRandomCorrectResponse()
                : _configuration.GetRandomIncorrectResponse();

            if (response != null)
            {
                if (response.AudioClip != null)
                {
                    _soundManager?.PlayClip2D(response.AudioClip);
                }

                _conversationUI?.ShowResponse(response);

                var waitTime = response.AudioClip != null
                    ? response.AudioClip.length + _configuration.DelayAfterResponse
                    : _configuration.DelayAfterResponse;

                await UniTask.Delay(TimeSpan.FromSeconds(waitTime));
            }

            await EndConversation(isCorrect);
        }

        private async UniTask EndConversation(bool wasCorrect)
        {
            _conversationUI?.Hide();

            _eventAggregator?.Publish(new ConversationEndedEvent(wasCorrect, _currentEnemy));

            // Show the player again
            if (_currentPlayer != null)
            {
                _currentPlayer.gameObject.SetActive(true);
            }

            if (wasCorrect)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;

                _currentLock?.Dispose();
                _currentLock = null;
            }
            else
            {
                // Keep cursor visible for game over UI
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;

                _eventAggregator?.Publish(new GameOverEvent("You were caught!"));
                // Keep lock active on game over
            }

            _isInConversation = false;
            _currentEnemy = null;
            _currentPlayer = null;
            _playerAnimator = null;
            _currentQuestion = null;

            await UniTask.Yield();
        }
    }
}
