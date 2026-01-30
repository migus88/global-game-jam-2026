using Game.Events;
using Game.Hiding.Events;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

namespace Game.Hiding
{
    public class HidingSpot : MonoBehaviour
    {
        [SerializeField, Header("Detection")]
        private float _detectionRadius = 2f;

        [SerializeField]
        private LayerMask _playerLayer;

        [SerializeField, Header("Visual States")]
        private GameObject _freeObject;

        [SerializeField]
        private GameObject _occupiedObject;

        [SerializeField, Header("Sound")]
        private string _enterSoundName;

        [SerializeField, Header("Hint Display")]
        private SpriteRenderer _hintRenderer;

        [SerializeField, Header("Input")]
        private InputActionReference _hideActionReference;

        private EventAggregator _eventAggregator;
        private HideConfiguration _configuration;
        private UnityEngine.Camera _mainCamera;

        private bool _isPlayerInRange;
        private bool _isOccupied;
        private Transform _playerTransform;
        private readonly Collider[] _detectionResults = new Collider[1];

        [Inject]
        public void Construct(EventAggregator eventAggregator, HideConfiguration configuration)
        {
            _eventAggregator = eventAggregator;
            _configuration = configuration;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();

            _eventAggregator?.Subscribe<PlayerHideStateChangedEvent>(OnPlayerHideStateChanged);

            _mainCamera = UnityEngine.Camera.main;
            InitializeHintDisplay();
            InitializeVisualState();

            if (_hideActionReference != null && _hideActionReference.action != null)
            {
                _hideActionReference.action.Enable();
                _hideActionReference.action.performed += OnHideActionPerformed;
            }
        }

        private void ResolveDependenciesIfNeeded()
        {
            if (_eventAggregator != null && _configuration != null)
            {
                return;
            }

            var lifetimeScope = Object.FindAnyObjectByType<LifetimeScope>();

            if (lifetimeScope == null)
            {
                return;
            }

            _eventAggregator ??= lifetimeScope.Container.Resolve<EventAggregator>();
            _configuration ??= lifetimeScope.Container.Resolve<HideConfiguration>();
        }

        private void InitializeVisualState()
        {
            if (_freeObject != null)
            {
                _freeObject.SetActive(true);
            }

            if (_occupiedObject != null)
            {
                _occupiedObject.SetActive(false);
            }

            _isOccupied = false;
        }

        private void OnPlayerHideStateChanged(PlayerHideStateChangedEvent evt)
        {
            if (evt.HidingSpot != transform)
            {
                return;
            }

            _isOccupied = evt.IsHidden;

            if (_freeObject != null)
            {
                _freeObject.SetActive(!_isOccupied);
            }

            if (_occupiedObject != null)
            {
                _occupiedObject.SetActive(_isOccupied);
            }
        }

        private void InitializeHintDisplay()
        {
            if (_hintRenderer != null)
            {
                _hintRenderer.gameObject.SetActive(false);
                return;
            }

            Debug.LogWarning($"[HidingSpot] No hint renderer assigned on {gameObject.name}");
        }

        private void Update()
        {
            CheckPlayerProximity();
            UpdateBillboard();
        }

        private void CheckPlayerProximity()
        {
            var count = Physics.OverlapSphereNonAlloc(
                transform.position,
                _detectionRadius,
                _detectionResults,
                _playerLayer);

            var wasInRange = _isPlayerInRange;
            _isPlayerInRange = count > 0;

            if (_isPlayerInRange && !wasInRange)
            {
                _playerTransform = _detectionResults[0].transform;
                OnPlayerEntered();
            }
            else if (!_isPlayerInRange && wasInRange)
            {
                OnPlayerExited();
                _playerTransform = null;
            }
        }

        private void OnPlayerEntered()
        {
            ShowHint();
            _eventAggregator?.Publish(new PlayerEnteredHidingZoneEvent(transform, transform.position));
        }

        private void OnPlayerExited()
        {
            HideHint();
            _eventAggregator?.Publish(new PlayerExitedHidingZoneEvent(transform));
        }

        private void OnHideActionPerformed(InputAction.CallbackContext context)
        {
            if (!_isPlayerInRange)
            {
                return;
            }

            _eventAggregator?.Publish(new HideActionRequestedEvent(transform, transform.position, _enterSoundName));
        }

        private void ShowHint()
        {
            if (_hintRenderer != null)
            {
                _hintRenderer.gameObject.SetActive(true);
            }
        }

        private void HideHint()
        {
            if (_hintRenderer != null)
            {
                _hintRenderer.gameObject.SetActive(false);
            }
        }

        private void UpdateBillboard()
        {
            if (_hintRenderer == null || !_hintRenderer.gameObject.activeSelf || _mainCamera == null)
            {
                return;
            }

            _hintRenderer.transform.rotation = _mainCamera.transform.rotation;
        }

        private void OnDestroy()
        {
            if (_hideActionReference != null && _hideActionReference.action != null)
            {
                _hideActionReference.action.performed -= OnHideActionPerformed;
            }

            _eventAggregator?.Unsubscribe<PlayerHideStateChangedEvent>(OnPlayerHideStateChanged);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawSphere(transform.position, _detectionRadius);

            Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
            Gizmos.DrawWireSphere(transform.position, _detectionRadius);
        }
    }
}
