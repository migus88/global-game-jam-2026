using System;
using Game.Events;
using Game.Input.Events;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using VContainer;

namespace Game.Input
{
    public class InputDeviceTracker : MonoBehaviour
    {
        public static InputDeviceTracker Instance { get; private set; }

        private EventAggregator _eventAggregator;
        private InputDeviceType _currentDeviceType = InputDeviceType.KeyboardMouse;

        public InputDeviceType CurrentDeviceType => _currentDeviceType;

        public event Action<InputDeviceType> DeviceChanged;

        [Inject]
        public void Construct(EventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            Instance = this;
        }

        private void OnEnable()
        {
            Instance = this;
            InputUser.onChange += OnInputUserChange;
            DetectInitialDevice();
        }

        private void OnDisable()
        {
            InputUser.onChange -= OnInputUserChange;

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void DetectInitialDevice()
        {
            foreach (var user in InputUser.all)
            {
                if (user.controlScheme.HasValue)
                {
                    var deviceType = GetDeviceTypeFromScheme(user.controlScheme.Value.name);
                    _currentDeviceType = deviceType;
                    return;
                }
            }
        }

        private void OnInputUserChange(InputUser user, InputUserChange change, InputDevice device)
        {
            if (change != InputUserChange.ControlSchemeChanged)
            {
                return;
            }

            if (!user.controlScheme.HasValue)
            {
                return;
            }

            var newDeviceType = GetDeviceTypeFromScheme(user.controlScheme.Value.name);

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
    }
}
