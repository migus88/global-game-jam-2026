using Cysharp.Threading.Tasks;
using Game.LevelEditor.Data;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.LevelEditor.Runtime
{
    public class LevelLifetimeScope : LifetimeScope
    {
        [SerializeField] private LevelData _levelToLoad;
        [SerializeField] private LevelConfiguration _levelConfiguration;

        protected override void Configure(IContainerBuilder builder)
        {
            if (_levelConfiguration != null)
            {
                builder.RegisterInstance(_levelConfiguration);
            }

            builder.Register<LevelBuilder>(Lifetime.Singleton);
            builder.RegisterEntryPoint<LevelEntryPoint>();
        }

        private class LevelEntryPoint : IStartable
        {
            private readonly LevelBuilder _levelBuilder;
            private readonly LevelLifetimeScope _scope;

            [Inject]
            public LevelEntryPoint(LevelBuilder levelBuilder, LevelLifetimeScope scope)
            {
                _levelBuilder = levelBuilder;
                _scope = scope;
            }

            public void Start()
            {
                if (_scope._levelToLoad != null)
                {
                    BuildLevelAsync().Forget();
                }
                else
                {
                    Debug.LogWarning("LevelLifetimeScope: No LevelData assigned to load");
                }
            }

            private async UniTaskVoid BuildLevelAsync()
            {
                try
                {
                    await _levelBuilder.BuildLevelAsync(_scope._levelToLoad);
                    Debug.Log("Level built successfully");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to build level: {e}");
                }
            }
        }
    }
}
