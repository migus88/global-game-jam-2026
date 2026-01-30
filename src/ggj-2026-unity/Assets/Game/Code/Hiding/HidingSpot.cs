using Game.Events;
using Game.Hiding.Events;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Game.Hiding
{
    public class HidingSpot : MonoBehaviour
    {
        [SerializeField, Header("Detection")]
        private float _detectionRadius = 2f;

        [SerializeField]
        private LayerMask _playerLayer;

        [SerializeField, Header("Hint Display")]
        private Vector3 _hintOffset = new(0f, 2f, 0f);

        [SerializeField, Header("Input")]
        private InputActionReference _hideActionReference;

        private EventAggregator _eventAggregator;
        private HideConfiguration _configuration;
        private UnityEngine.Camera _mainCamera;
        private GameObject _hintObject;
        private SpriteRenderer _hintRenderer;

        private bool _isPlayerInRange;
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
            _mainCamera = UnityEngine.Camera.main;
            CreateHintDisplay();

            if (_hideActionReference != null && _hideActionReference.action != null)
            {
                _hideActionReference.action.Enable();
                _hideActionReference.action.performed += OnHideActionPerformed;
            }
        }

        private void CreateHintDisplay()
        {
            _hintObject = new GameObject("HideHint");
            _hintObject.transform.SetParent(transform);
            _hintObject.transform.localPosition = _hintOffset;

            _hintRenderer = _hintObject.AddComponent<SpriteRenderer>();

            if (_configuration != null)
            {
                _hintRenderer.sprite = _configuration.HintButtonSprite;
                _hintRenderer.color = _configuration.HintColor;

                var size = _configuration.HintSize;
                _hintObject.transform.localScale = new Vector3(size, size, size);
            }

            _hintObject.SetActive(false);
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

            _eventAggregator?.Publish(new HideActionRequestedEvent(transform, transform.position));
        }

        private void ShowHint()
        {
            if (_hintObject != null)
            {
                _hintObject.SetActive(true);
            }
        }

        private void HideHint()
        {
            if (_hintObject != null)
            {
                _hintObject.SetActive(false);
            }
        }

        private void UpdateBillboard()
        {
            if (_hintObject == null || !_hintObject.activeSelf || _mainCamera == null)
            {
                return;
            }

            _hintObject.transform.rotation = _mainCamera.transform.rotation;
        }

        private void OnDestroy()
        {
            if (_hideActionReference != null && _hideActionReference.action != null)
            {
                _hideActionReference.action.performed -= OnHideActionPerformed;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawSphere(transform.position, _detectionRadius);

            Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
            Gizmos.DrawWireSphere(transform.position, _detectionRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + _hintOffset);
            Gizmos.DrawWireCube(transform.position + _hintOffset, new Vector3(0.5f, 0.5f, 0.1f));
        }
    }
}
