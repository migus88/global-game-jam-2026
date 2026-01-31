using Game.Events;
using Game.Input.Events;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Game.Input
{
    public class ButtonSpriteDisplay : MonoBehaviour
    {
        [SerializeField]
        private InputActionReference _actionReference;

        [SerializeField]
        private ButtonMappingConfig _mappingConfig;

        [SerializeField, Header("Renderers (assign one)")]
        private SpriteRenderer _spriteRenderer;

        [SerializeField]
        private Image _image;

        private EventAggregator _eventAggregator;
        private bool _isSubscribedToTracker;

        [Inject]
        public void Construct(EventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();
            AutoDetectRenderer();
            SubscribeToDeviceChanges();
            UpdateSprite();
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

        private void AutoDetectRenderer()
        {
            if (_spriteRenderer == null && _image == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();

                if (_spriteRenderer == null)
                {
                    _image = GetComponent<Image>();
                }
            }
        }

        private void SubscribeToDeviceChanges()
        {
            _eventAggregator?.Subscribe<InputDeviceChangedEvent>(OnInputDeviceChanged);

            if (InputDeviceTracker.Instance != null)
            {
                InputDeviceTracker.Instance.DeviceChanged += OnDeviceChanged;
                _isSubscribedToTracker = true;
            }
        }

        private void OnInputDeviceChanged(InputDeviceChangedEvent evt)
        {
            UpdateSprite();
        }

        private void OnDeviceChanged(InputDeviceType deviceType)
        {
            UpdateSprite();
        }

        private void UpdateSprite()
        {
            if (_mappingConfig == null || _actionReference == null)
            {
                return;
            }

            var sprite = _mappingConfig.GetCurrentSprite(_actionReference);

            if (sprite == null)
            {
                return;
            }

            if (_spriteRenderer != null)
            {
                _spriteRenderer.sprite = sprite;
            }
            else if (_image != null)
            {
                _image.sprite = sprite;
            }
        }

        public void SetAction(InputActionReference actionReference)
        {
            _actionReference = actionReference;
            UpdateSprite();
        }

        public void SetMappingConfig(ButtonMappingConfig config)
        {
            _mappingConfig = config;
            UpdateSprite();
        }

        public void ForceUpdate()
        {
            UpdateSprite();
        }

        private void OnDestroy()
        {
            _eventAggregator?.Unsubscribe<InputDeviceChangedEvent>(OnInputDeviceChanged);

            if (_isSubscribedToTracker && InputDeviceTracker.Instance != null)
            {
                InputDeviceTracker.Instance.DeviceChanged -= OnDeviceChanged;
            }
        }
    }
}
