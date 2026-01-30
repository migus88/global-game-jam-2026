using Unity.Cinemachine;
using Game.Conversation.Events;
using Game.Detection;
using Game.Events;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Conversation
{
    public class EnemyConversationHandler : MonoBehaviour
    {
        [SerializeField, Header("Camera")]
        private CinemachineCamera _conversationCamera;

        [SerializeField]
        private int _conversationCameraPriority = 20;

        [SerializeField, Header("Components")]
        private VisionCone _visionCone;

        private EventAggregator _eventAggregator;
        private int _originalCameraPriority;
        private bool _isIgnoringPlayer;

        public bool IsIgnoringPlayer => _isIgnoringPlayer;

        [Inject]
        public void Construct(EventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();

            if (_conversationCamera != null)
            {
                _originalCameraPriority = _conversationCamera.Priority;
            }

            if (_visionCone == null)
            {
                _visionCone = GetComponentInChildren<VisionCone>();
            }

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

            _eventAggregator ??= lifetimeScope.Container.Resolve<EventAggregator>();
        }

        private void OnDestroy()
        {
            _eventAggregator?.Unsubscribe<ConversationStartedEvent>(OnConversationStarted);
            _eventAggregator?.Unsubscribe<ConversationEndedEvent>(OnConversationEnded);
        }

        private void OnConversationStarted(ConversationStartedEvent evt)
        {
            if (!IsThisEnemy(evt.Enemy))
            {
                return;
            }

            Debug.Log($"[EnemyConversationHandler] Conversation started, setting camera priority to {_conversationCameraPriority}");

            if (_conversationCamera != null)
            {
                _conversationCamera.Priority = _conversationCameraPriority;
            }
            else
            {
                Debug.LogWarning("[EnemyConversationHandler] Conversation camera is not assigned!");
            }
        }

        private void OnConversationEnded(ConversationEndedEvent evt)
        {
            if (!IsThisEnemy(evt.Enemy))
            {
                return;
            }

            Debug.Log($"[EnemyConversationHandler] Conversation ended, restoring camera priority to {_originalCameraPriority}");

            if (_conversationCamera != null)
            {
                _conversationCamera.Priority = _originalCameraPriority;
            }

            if (evt.WasCorrect)
            {
                StartIgnoringPlayer();
            }
        }

        private bool IsThisEnemy(Transform enemy)
        {
            return enemy == transform || enemy == transform.root;
        }

        private void StartIgnoringPlayer()
        {
            _isIgnoringPlayer = true;

            if (_visionCone != null)
            {
                _visionCone.gameObject.SetActive(false);
            }
        }

        public void StopIgnoringPlayer()
        {
            _isIgnoringPlayer = false;

            if (_visionCone != null)
            {
                _visionCone.gameObject.SetActive(true);
            }
        }
    }
}
