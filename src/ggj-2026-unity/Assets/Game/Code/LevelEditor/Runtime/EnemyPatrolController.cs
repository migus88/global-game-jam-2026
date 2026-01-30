using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.LevelEditor.Data;
using UnityEngine;

namespace Game.LevelEditor.Runtime
{
    public class EnemyPatrolController : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private float _rotationSpeed = 180f;
        [SerializeField] private float _arrivalThreshold = 0.1f;

        private List<PatrolWaypoint> _patrolPath;
        private LevelData _levelData;
        private Animator _animator;
        private CancellationTokenSource _patrolCts;
        private int _currentWaypointIndex;
        private bool _isPatrolling;

        public bool IsPatrolling => _isPatrolling;
        public int CurrentWaypointIndex => _currentWaypointIndex;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void OnDestroy()
        {
            StopPatrol();
        }

        public void Initialize(List<PatrolWaypoint> patrolPath, LevelData levelData)
        {
            _patrolPath = patrolPath;
            _levelData = levelData;
        }

        public void StartPatrol()
        {
            if (_patrolPath == null || _patrolPath.Count == 0)
            {
                return;
            }

            StopPatrol();
            _patrolCts = new CancellationTokenSource();
            PatrolLoopAsync(_patrolCts.Token).Forget();
        }

        public void StopPatrol()
        {
            if (_patrolCts != null)
            {
                _patrolCts.Cancel();
                _patrolCts.Dispose();
                _patrolCts = null;
            }

            _isPatrolling = false;
        }

        private async UniTaskVoid PatrolLoopAsync(CancellationToken ct)
        {
            _isPatrolling = true;
            _currentWaypointIndex = 0;

            while (!ct.IsCancellationRequested)
            {
                var waypoint = _patrolPath[_currentWaypointIndex];
                Vector3 targetPosition = _levelData.GridToWorld(waypoint.GridPosition);

                await MoveToPositionAsync(targetPosition, ct);

                if (ct.IsCancellationRequested)
                {
                    break;
                }

                // Apply animator parameter if specified
                if (_animator != null && !string.IsNullOrEmpty(waypoint.AnimatorParameterName))
                {
                    _animator.SetBool(waypoint.AnimatorParameterName, waypoint.AnimatorParameterValue);
                }

                // Wait at waypoint
                if (waypoint.WaitDelay > 0f)
                {
                    await UniTask.Delay(
                        (int)(waypoint.WaitDelay * 1000),
                        cancellationToken: ct
                    );
                }

                if (ct.IsCancellationRequested)
                {
                    break;
                }

                // Move to next waypoint
                _currentWaypointIndex = (_currentWaypointIndex + 1) % _patrolPath.Count;
            }

            _isPatrolling = false;
        }

        private async UniTask MoveToPositionAsync(Vector3 targetPosition, CancellationToken ct)
        {
            // First rotate towards target
            await RotateTowardsAsync(targetPosition, ct);

            if (ct.IsCancellationRequested)
            {
                return;
            }

            // Then move to target
            while (!ct.IsCancellationRequested)
            {
                Vector3 currentPos = transform.position;
                Vector3 direction = targetPosition - currentPos;
                direction.y = 0f;

                float distance = direction.magnitude;
                if (distance <= _arrivalThreshold)
                {
                    transform.position = new Vector3(targetPosition.x, currentPos.y, targetPosition.z);
                    break;
                }

                Vector3 moveDirection = direction.normalized;
                Vector3 newPosition = currentPos + moveDirection * (_moveSpeed * Time.deltaTime);
                transform.position = newPosition;

                await UniTask.Yield(ct);
            }
        }

        private async UniTask RotateTowardsAsync(Vector3 targetPosition, CancellationToken ct)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction);

            while (!ct.IsCancellationRequested)
            {
                float angle = Quaternion.Angle(transform.rotation, targetRotation);
                if (angle < 1f)
                {
                    transform.rotation = targetRotation;
                    break;
                }

                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    _rotationSpeed * Time.deltaTime
                );

                await UniTask.Yield(ct);
            }
        }
    }
}
