using Game.Configuration;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    [SerializeField] private GameConfiguration _gameConfiguration;
    
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(_gameConfiguration);
    }
}
