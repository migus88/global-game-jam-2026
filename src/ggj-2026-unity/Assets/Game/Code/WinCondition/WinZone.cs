using Game.Events;
using Game.Scenes.Events;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.WinCondition
{
    [RequireComponent(typeof(Collider))]
    public class WinZone : MonoBehaviour
    {
        [SerializeField]
        private LayerMask _playerLayer;

        private EventAggregator _eventAggregator;
        private bool _triggered;

        [Inject]
        public void Construct(EventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();

            var col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void ResolveDependenciesIfNeeded()
        {
            if (_eventAggregator != null)
            {
                return;
            }

            var lifetimeScope = FindAnyObjectByType<LifetimeScope>();

            if (lifetimeScope == null)
            {
                return;
            }

            _eventAggregator ??= lifetimeScope.Container.Resolve<EventAggregator>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered)
            {
                return;
            }

            if ((_playerLayer.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            _triggered = true;
            _eventAggregator?.Publish(new PlayerWonEvent());
        }
    }
}
