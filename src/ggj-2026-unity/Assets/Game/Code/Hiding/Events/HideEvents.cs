using Game.Events;
using UnityEngine;

namespace Game.Hiding.Events
{
    public readonly struct PlayerEnteredHidingZoneEvent : IEvent
    {
        public Transform HidingSpot { get; }
        public Vector3 HidePosition { get; }

        public PlayerEnteredHidingZoneEvent(Transform hidingSpot, Vector3 hidePosition)
        {
            HidingSpot = hidingSpot;
            HidePosition = hidePosition;
        }
    }

    public readonly struct PlayerExitedHidingZoneEvent : IEvent
    {
        public Transform HidingSpot { get; }

        public PlayerExitedHidingZoneEvent(Transform hidingSpot)
        {
            HidingSpot = hidingSpot;
        }
    }

    public readonly struct HideActionRequestedEvent : IEvent
    {
        public Transform HidingSpot { get; }
        public Vector3 HidePosition { get; }
        public string EnterSoundName { get; }

        public HideActionRequestedEvent(Transform hidingSpot, Vector3 hidePosition, string enterSoundName)
        {
            HidingSpot = hidingSpot;
            HidePosition = hidePosition;
            EnterSoundName = enterSoundName;
        }
    }

    public readonly struct PlayerHideStateChangedEvent : IEvent
    {
        public bool IsHidden { get; }
        public Transform HidingSpot { get; }

        public PlayerHideStateChangedEvent(bool isHidden, Transform hidingSpot)
        {
            IsHidden = isHidden;
            HidingSpot = hidingSpot;
        }
    }
}
