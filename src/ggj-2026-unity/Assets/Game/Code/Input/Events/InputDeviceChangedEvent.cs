using Game.Events;

namespace Game.Input.Events
{
    public readonly struct InputDeviceChangedEvent : IEvent
    {
        public InputDeviceType PreviousDeviceType { get; }
        public InputDeviceType CurrentDeviceType { get; }

        public InputDeviceChangedEvent(InputDeviceType previousDeviceType, InputDeviceType currentDeviceType)
        {
            PreviousDeviceType = previousDeviceType;
            CurrentDeviceType = currentDeviceType;
        }
    }
}
