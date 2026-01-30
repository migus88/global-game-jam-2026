using Game.Events;

namespace Game.GameState.Events
{
    public readonly struct GamePausedEvent : IEvent
    {
        public PauseReason Reason { get; }

        public GamePausedEvent(PauseReason reason)
        {
            Reason = reason;
        }
    }

    public readonly struct GameResumedEvent : IEvent
    {
    }

    public readonly struct GameOverEvent : IEvent
    {
        public string Reason { get; }

        public GameOverEvent(string reason)
        {
            Reason = reason;
        }
    }

    public enum PauseReason
    {
        None = 0,
        Conversation = 1,
        Menu = 2,
        GameOver = 3
    }
}
