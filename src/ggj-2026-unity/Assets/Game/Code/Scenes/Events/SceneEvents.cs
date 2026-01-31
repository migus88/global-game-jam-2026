using Game.Events;

namespace Game.Scenes.Events
{
    public readonly struct StartGameRequestedEvent : IEvent
    {
    }

    public readonly struct LoadingStartedEvent : IEvent
    {
        public bool IsTransitioningToGame { get; }

        public LoadingStartedEvent(bool isTransitioningToGame)
        {
            IsTransitioningToGame = isTransitioningToGame;
        }
    }

    public readonly struct LoadingCompletedEvent : IEvent
    {
        public bool IsInGame { get; }

        public LoadingCompletedEvent(bool isInGame)
        {
            IsInGame = isInGame;
        }
    }

    public readonly struct ReturnToMainMenuRequestedEvent : IEvent
    {
    }

    public readonly struct SceneLoadedEvent : IEvent
    {
        public string SceneName { get; }

        public SceneLoadedEvent(string sceneName)
        {
            SceneName = sceneName;
        }
    }

    public readonly struct GameSceneReadyEvent : IEvent
    {
    }

    public readonly struct MainMenuReadyEvent : IEvent
    {
    }
}
