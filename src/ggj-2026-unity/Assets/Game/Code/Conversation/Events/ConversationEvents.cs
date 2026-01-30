using Game.Events;
using UnityEngine;

namespace Game.Conversation.Events
{
    public readonly struct PlayerCaughtEvent : IEvent
    {
        public Transform Enemy { get; }
        public Transform Player { get; }

        public PlayerCaughtEvent(Transform enemy, Transform player)
        {
            Enemy = enemy;
            Player = player;
        }
    }

    public readonly struct ConversationStartedEvent : IEvent
    {
        public Transform Enemy { get; }

        public ConversationStartedEvent(Transform enemy)
        {
            Enemy = enemy;
        }
    }

    public readonly struct ConversationEndedEvent : IEvent
    {
        public bool WasCorrect { get; }
        public Transform Enemy { get; }

        public ConversationEndedEvent(bool wasCorrect, Transform enemy)
        {
            WasCorrect = wasCorrect;
            Enemy = enemy;
        }
    }
}
