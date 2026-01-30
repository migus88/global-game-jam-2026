using Migs.MLock.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

namespace Game.GameState
{
    public class LockablePlayerInput : MonoBehaviour, ILockable<GameLockTags>
    {
        [SerializeField]
        private PlayerInput _playerInput;

        private GameLockService _lockService;

        public GameLockTags LockTags => GameLockTags.PlayerInput;

        [Inject]
        public void Construct(GameLockService lockService)
        {
            _lockService = lockService;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();

            if (_playerInput == null)
            {
                _playerInput = GetComponent<PlayerInput>();
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
            if (_playerInput != null)
            {
                _playerInput.DeactivateInput();
            }
        }

        public void HandleUnlocking()
        {
            if (_playerInput != null)
            {
                _playerInput.ActivateInput();
            }
        }
    }
}
