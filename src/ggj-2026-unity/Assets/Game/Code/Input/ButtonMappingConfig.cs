using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Input
{
    [CreateAssetMenu(fileName = "ButtonMappingConfig", menuName = "Game/Input/Button Mapping Config")]
    public class ButtonMappingConfig : ScriptableObject
    {
        [SerializeField]
        private List<ButtonMapping> _buttonMappings = new();

        private Dictionary<string, ButtonMapping> _mappingLookup;

        public Sprite GetSprite(InputActionReference actionReference, InputDeviceType deviceType)
        {
            if (actionReference == null || actionReference.action == null)
            {
                return null;
            }

            return GetSprite(actionReference.action.name, deviceType);
        }

        public Sprite GetSprite(string actionName, InputDeviceType deviceType)
        {
            if (string.IsNullOrEmpty(actionName))
            {
                return null;
            }

            BuildLookupIfNeeded();

            if (!_mappingLookup.TryGetValue(actionName, out var mapping))
            {
                return null;
            }

            return deviceType switch
            {
                InputDeviceType.KeyboardMouse => mapping.KeyboardMouseSprite,
                InputDeviceType.Gamepad => mapping.GamepadSprite,
                _ => mapping.KeyboardMouseSprite
            };
        }

        public Sprite GetCurrentSprite(InputActionReference actionReference)
        {
            var deviceType = InputDeviceTracker.Instance != null
                ? InputDeviceTracker.Instance.CurrentDeviceType
                : InputDeviceType.KeyboardMouse;

            return GetSprite(actionReference, deviceType);
        }

        public Sprite GetCurrentSprite(string actionName)
        {
            var deviceType = InputDeviceTracker.Instance != null
                ? InputDeviceTracker.Instance.CurrentDeviceType
                : InputDeviceType.KeyboardMouse;

            return GetSprite(actionName, deviceType);
        }

        private void BuildLookupIfNeeded()
        {
            if (_mappingLookup != null)
            {
                return;
            }

            _mappingLookup = new Dictionary<string, ButtonMapping>();

            foreach (var mapping in _buttonMappings)
            {
                if (string.IsNullOrEmpty(mapping.ActionName))
                {
                    continue;
                }

                _mappingLookup[mapping.ActionName] = mapping;
            }
        }

        private void OnValidate()
        {
            _mappingLookup = null;
        }
    }

    [Serializable]
    public class ButtonMapping
    {
        [field: SerializeField]
        public string ActionName { get; private set; }

        [field: SerializeField]
        public Sprite KeyboardMouseSprite { get; private set; }

        [field: SerializeField]
        public Sprite GamepadSprite { get; private set; }
    }
}
