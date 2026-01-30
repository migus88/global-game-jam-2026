using System;
using Cysharp.Threading.Tasks;
using Game.Conversation.Data;
using Game.Conversation.Events;
using Game.Events;
using Game.GameState.Events;
using Game.Sound;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Conversation
{
    public class ConversationManager : MonoBehaviour
    {
        [SerializeField]
        private ConversationUI _conversationUI;

        private EventAggregator _eventAggregator;
        private ConversationConfiguration _configuration;
        private SoundManager _soundManager;

        private Transform _currentEnemy;
        private ConversationQuestion _currentQuestion;
        private bool _isInConversation;

        public bool IsInConversation => _isInConversation;

        [Inject]
        public void Construct(
            EventAggregator eventAggregator,
            ConversationConfiguration configuration,
            SoundManager soundManager)
        {
            _eventAggregator = eventAggregator;
            _configuration = configuration;
            _soundManager = soundManager;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();
            _eventAggregator?.Subscribe<PlayerCaughtEvent>(OnPlayerCaught);

            if (_conversationUI != null)
            {
                _conversationUI.AnswerSelected += OnAnswerSelected;
                _conversationUI.Hide();
            }
        }

        private void ResolveDependenciesIfNeeded()
        {
            if (_eventAggregator != null && _configuration != null && _soundManager != null)
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
        }

        private void OnDestroy()
        {
            _eventAggregator?.Unsubscribe<PlayerCaughtEvent>(OnPlayerCaught);

            if (_conversationUI != null)
            {
                _conversationUI.AnswerSelected -= OnAnswerSelected;
            }
        }

        private void OnPlayerCaught(PlayerCaughtEvent evt)
        {
            if (_isInConversation)
            {
                return;
            }

            StartConversation(evt.Enemy).Forget();
        }

        private async UniTaskVoid StartConversation(Transform enemy)
        {
            _isInConversation = true;
            _currentEnemy = enemy;

            _eventAggregator?.Publish(new GamePausedEvent(PauseReason.Conversation));
            _eventAggregator?.Publish(new ConversationStartedEvent(enemy));

            await UniTask.Delay(TimeSpan.FromSeconds(_configuration.DelayBeforeQuestion));

            _currentQuestion = _configuration.GetRandomQuestion();

            if (_currentQuestion == null)
            {
                EndConversation(true).Forget();
                return;
            }

            if (_currentQuestion.AudioClip != null)
            {
                _soundManager?.PlayClip2D(_currentQuestion.AudioClip);
            }

            _conversationUI?.ShowQuestion(_currentQuestion);
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

            if (wasCorrect)
            {
                _eventAggregator?.Publish(new GameResumedEvent());
            }
            else
            {
                _eventAggregator?.Publish(new GameOverEvent("You were caught!"));
            }

            _isInConversation = false;
            _currentEnemy = null;
            _currentQuestion = null;

            await UniTask.Yield();
        }
    }
}
