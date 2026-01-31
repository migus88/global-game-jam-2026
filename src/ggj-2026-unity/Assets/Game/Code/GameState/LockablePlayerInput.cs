using Migs.MLock.Interfaces;
using StarterAssets;
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
        private StarterAssetsInputs _starterInputs;
        private bool _isQuitting;

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

            _starterInputs = GetComponent<StarterAssetsInputs>();

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

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        private void OnDestroy()
        {
            _isQuitting = true;
            _lockService?.Unsubscribe(this);
        }

        public void HandleLocking()
        {
            if (_playerInput != null)
            {
                _playerInput.DeactivateInput();
            }

            ResetInputValues();
        }

        public void HandleUnlocking()
        {
            // Skip activation during shutdown to prevent errors
            if (_isQuitting)
            {
                return;
            }

            if (_playerInput != null && _playerInput.enabled)
            {
                _playerInput.ActivateInput();
            }
        }

        private void ResetInputValues()
        {
            if (_starterInputs == null)
            {
                return;
            }

            _starterInputs.move = Vector2.zero;
            _starterInputs.look = Vector2.zero;
            _starterInputs.jump = false;
            _starterInputs.sprint = false;
        }
    }
}
