using Game.Configuration;
using Game.LevelEditor.Data;
using Game.Sound;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    [SerializeField] private GameConfiguration _gameConfiguration;
    [SerializeField] private LevelConfiguration _levelConfiguration;
    [SerializeField] private SoundConfiguration _soundConfiguration;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(_gameConfiguration);

        if (_levelConfiguration != null)
        {
            builder.RegisterInstance(_levelConfiguration);
        }

        if (_soundConfiguration != null)
        {
            builder.RegisterInstance(_soundConfiguration);
            builder.Register<SoundManager>(Lifetime.Singleton)
                .WithParameter(_soundConfiguration);
        }
    }
}
