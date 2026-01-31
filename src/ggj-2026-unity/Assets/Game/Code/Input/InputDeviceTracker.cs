using System;
using Game.Events;
using Game.Input.Events;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

namespace Game.Input
{
    public class InputDeviceTracker : MonoBehaviour
    {
        public static InputDeviceTracker Instance { get; private set; }

        [SerializeField]
        private PlayerInput _playerInput;

        private EventAggregator _eventAggregator;
        private InputDeviceType _currentDeviceType = InputDeviceType.KeyboardMouse;

        public InputDeviceType CurrentDeviceType => _currentDeviceType;

        public event Action<InputDeviceType> DeviceChanged;

        [Inject]
        public void Construct(EventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();

            if (_playerInput == null)
            {
                _playerInput = FindAnyObjectByType<PlayerInput>();
            }

            if (_playerInput != null)
            {
                _currentDeviceType = GetDeviceTypeFromScheme(_playerInput.currentControlScheme);
                _playerInput.onControlsChanged += OnControlsChanged;
            }
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

        private void OnControlsChanged(PlayerInput playerInput)
        {
            var newDeviceType = GetDeviceTypeFromScheme(playerInput.currentControlScheme);

            if (newDeviceType == _currentDeviceType)
            {
                return;
            }

            var previousDeviceType = _currentDeviceType;
            _currentDeviceType = newDeviceType;

            DeviceChanged?.Invoke(_currentDeviceType);
            _eventAggregator?.Publish(new InputDeviceChangedEvent(previousDeviceType, _currentDeviceType));
        }

        private InputDeviceType GetDeviceTypeFromScheme(string controlScheme)
        {
            if (string.IsNullOrEmpty(controlScheme))
            {
                return InputDeviceType.KeyboardMouse;
            }

            var schemeLower = controlScheme.ToLowerInvariant();

            if (schemeLower.Contains("gamepad") || schemeLower.Contains("controller") || schemeLower.Contains("joystick"))
            {
                return InputDeviceType.Gamepad;
            }

            return InputDeviceType.KeyboardMouse;
        }

        private void OnDestroy()
        {
            if (_playerInput != null)
            {
                _playerInput.onControlsChanged -= OnControlsChanged;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
