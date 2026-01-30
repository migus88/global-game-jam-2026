using Game.Configuration;
using Game.LevelEditor.Data;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    [SerializeField] private GameConfiguration _gameConfiguration;
    [SerializeField] private LevelConfiguration _levelConfiguration;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(_gameConfiguration);

        if (_levelConfiguration != null)
        {
            builder.RegisterInstance(_levelConfiguration);
        }
    }
}
