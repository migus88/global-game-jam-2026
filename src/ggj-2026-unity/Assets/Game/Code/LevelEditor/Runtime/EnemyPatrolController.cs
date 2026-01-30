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
        [SerializeField] private float _smoothTurnRadius = 0.5f;

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
                int nextIndex = (_currentWaypointIndex + 1) % _patrolPath.Count;
                var nextWaypoint = _patrolPath[nextIndex];

                Vector3 targetPosition = _levelData.GridToWorld(waypoint.GridPosition);
                Vector3 nextPosition = _levelData.GridToWorld(nextWaypoint.GridPosition);

                bool hasDelay = waypoint.WaitDelay > 0f;
                bool hasAnimatorAction = !string.IsNullOrEmpty(waypoint.AnimatorParameterName);
                bool shouldStop = hasDelay || hasAnimatorAction;

                await MoveToWaypointAsync(targetPosition, nextPosition, shouldStop, ct);

                if (ct.IsCancellationRequested)
                {
                    break;
                }

                // Apply animator parameter if specified
                if (_animator != null && hasAnimatorAction)
                {
                    _animator.SetBool(waypoint.AnimatorParameterName, waypoint.AnimatorParameterValue);
                }

                // Wait at waypoint
                if (hasDelay)
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
                _currentWaypointIndex = nextIndex;

                // Always yield at least once per loop iteration to prevent freezing
                await UniTask.Yield(ct);
            }

            _isPatrolling = false;
        }

        private async UniTask MoveToWaypointAsync(Vector3 targetPosition, Vector3 nextPosition, bool shouldStop, CancellationToken ct)
        {
            Vector3 currentPos = transform.position;
            Vector3 toTarget = targetPosition - currentPos;
            toTarget.y = 0f;

            // Rotate towards target first if we're far or stopped
            if (toTarget.magnitude > _smoothTurnRadius * 2f)
            {
                await RotateTowardsAsync(targetPosition, ct);
            }

            if (ct.IsCancellationRequested)
            {
                return;
            }

            // Move towards waypoint
            while (!ct.IsCancellationRequested)
            {
                currentPos = transform.position;
                Vector3 direction = targetPosition - currentPos;
                direction.y = 0f;

                float distance = direction.magnitude;

                // If stopping at waypoint, come to a complete stop
                if (shouldStop)
                {
                    if (distance <= 0.05f)
                    {
                        transform.position = new Vector3(targetPosition.x, currentPos.y, targetPosition.z);
                        break;
                    }
                }
                else
                {
                    // Close enough to pass through
                    if (distance <= 0.1f)
                    {
                        break;
                    }

                    // Smooth pass-through: start blending towards next waypoint when close
                    if (distance <= _smoothTurnRadius)
                    {
                        // Blend direction towards next waypoint
                        Vector3 toNext = nextPosition - targetPosition;
                        toNext.y = 0f;

                        if (toNext.sqrMagnitude > 0.01f)
                        {
                            float blendFactor = 1f - (distance / _smoothTurnRadius);
                            Vector3 blendedTarget = Vector3.Lerp(targetPosition, nextPosition, blendFactor * 0.5f);
                            direction = blendedTarget - currentPos;
                            direction.y = 0f;
                        }
                    }
                }

                // Move and rotate smoothly
                if (direction.sqrMagnitude > 0.001f)
                {
                    Vector3 moveDirection = direction.normalized;

                    // Smooth rotation while moving
                    Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        targetRotation,
                        _rotationSpeed * Time.deltaTime
                    );

                    // Move towards target direction (not transform.forward)
                    float step = _moveSpeed * Time.deltaTime;
                    Vector3 newPosition = currentPos + moveDirection * step;
                    transform.position = newPosition;
                }
                else
                {
                    // Direction is zero, we're at the target
                    break;
                }

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
