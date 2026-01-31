using Game.Events;

namespace Game.Scenes.Events
{
    public readonly struct StartGameRequestedEvent : IEvent
    {
    }

    public readonly struct LoadingStartedEvent : IEvent
    {
    }

    public readonly struct LoadingCompletedEvent : IEvent
    {
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
}
