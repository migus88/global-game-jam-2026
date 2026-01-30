using Migs.MLock.Interfaces;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.GameState
{
    public class LockablePlayerMovement : MonoBehaviour, ILockable<GameLockTags>
    {
        [SerializeField]
        private MonoBehaviour _movementController;

        private GameLockService _lockService;

        public GameLockTags LockTags => GameLockTags.PlayerMovement;

        [Inject]
        public void Construct(GameLockService lockService)
        {
            _lockService = lockService;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();

            if (_movementController == null)
            {
                _movementController = GetComponent<MonoBehaviour>();
            }

            _lockService?.Subscribe(this);
        }

        private void ResolveDependenciesIfNeeded()
        {
            if (_lockService != null)
            {
                return;
            }

            var lifetimeScope = FindAnyObjectByType<LifetimeScope>();

            if (lifetimeScope == null)
            {
                return;
            }

            _lockService ??= lifetimeScope.Container.Resolve<GameLockService>();
        }

        private void OnDestroy()
        {
            _lockService?.Unsubscribe(this);
        }

        public void HandleLocking()
        {
            if (_movementController != null)
            {
                _movementController.enabled = false;
            }
        }

        public void HandleUnlocking()
        {
            if (_movementController != null)
            {
                _movementController.enabled = true;
            }
        }
    }
}
