using Game.Conversation.Events;
using Game.Events;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.UI
{
    public class ConversationUIVisibility : MonoBehaviour
    {
        [SerializeField]
        private bool _hideOnConversation = true;

        [SerializeField]
        private CanvasGroup _canvasGroup;

        [SerializeField]
        private GameObject _targetObject;

        private EventAggregator _eventAggregator;

        [Inject]
        public void Construct(EventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();

            _eventAggregator?.Subscribe<ConversationStartedEvent>(OnConversationStarted);
            _eventAggregator?.Subscribe<ConversationEndedEvent>(OnConversationEnded);
        }

        private void ResolveDependenciesIfNeeded()
        {
            if (_eventAggregator != null)
            {
                return;
            }

            var lifetimeScope = FindAnyObjectByType<LifetimeScope>();

            if (lifetimeScope == null)
            {
                return;
            }

            _eventAggregator = lifetimeScope.Container.Resolve<EventAggregator>();
        }

        private void OnDestroy()
        {
            _eventAggregator?.Unsubscribe<ConversationStartedEvent>(OnConversationStarted);
            _eventAggregator?.Unsubscribe<ConversationEndedEvent>(OnConversationEnded);
        }

        private void OnConversationStarted(ConversationStartedEvent evt)
        {
            SetVisible(!_hideOnConversation);
        }

        private void OnConversationEnded(ConversationEndedEvent evt)
        {
            SetVisible(_hideOnConversation);
        }

        private void SetVisible(bool visible)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.interactable = visible;
                _canvasGroup.blocksRaycasts = visible;
            }

            if (_targetObject != null)
            {
                _targetObject.SetActive(visible);
            }
        }
    }
}
