using Game.LevelEditor.Data;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.LevelEditor.Runtime
{
    /// <summary>
    /// LifetimeScope for level scenes. Registers LevelConfiguration for any components that need it.
    /// Level content is spawned in edit mode via LevelSpawner, not at runtime.
    /// </summary>
    public class LevelLifetimeScope : LifetimeScope
    {
        [SerializeField] private LevelConfiguration _levelConfiguration;

        protected override void Configure(IContainerBuilder builder)
        {
            if (_levelConfiguration != null)
            {
                builder.RegisterInstance(_levelConfiguration);
            }
        }
    }
}
