using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configuration;
using Game.LevelEditor.Data;
using UnityEngine;
using VContainer;

namespace Game.LevelEditor.Runtime
{
    [RequireComponent(typeof(CharacterController))]
    public class EnemyPatrolController : MonoBehaviour
    {
        private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private float _rotationSpeed = 180f;
        [SerializeField] private float _smoothTurnRadius = 0.5f;
        [SerializeField] private float _gravity = -9.81f;

        [Header("Observation Override")]
        [SerializeField] private bool _overrideObservationSettings;
        [SerializeField, Range(30f, 180f)] private float _scanAngle = 120f;
        [SerializeField] private float _scanSpeed = 90f;
        [SerializeField] private float _pauseAtEnds = 0.3f;

        private GameConfiguration _config;
        private List<PatrolWaypoint> _patrolPath;
        private LevelData _levelData;
        private Vector3 _spawnPosition;
        private Animator _animator;
        private CharacterController _characterController;
        private CancellationTokenSource _patrolCts;
        private int _currentWaypointIndex;
        private bool _isPatrolling;
        private bool _movingForward = true;
        private float _verticalVelocity;
        private bool _isWalking;

        public bool IsPatrolling => _isPatrolling;
        public int CurrentWaypointIndex => _currentWaypointIndex;

        private float ScanAngle => _overrideObservationSettings ? _scanAngle : (_config?.Observation?.ScanAngle ?? 120f);
        private float ScanSpeed => _overrideObservationSettings ? _scanSpeed : (_config?.Observation?.ScanSpeed ?? 90f);
        private float PauseAtEnds => _overrideObservationSettings ? _pauseAtEnds : (_config?.Observation?.PauseAtEnds ?? 0.3f);

        [Inject]
        public void Construct(GameConfiguration config)
        {
            _config = config;
        }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _characterController = GetComponent<CharacterController>();
        }

        private void OnDestroy()
        {
            StopPatrol();
        }

        public void Initialize(List<PatrolWaypoint> patrolPath, LevelData levelData, Vector3 spawnPosition)
        {
            _patrolPath = patrolPath;
            _levelData = levelData;
            _spawnPosition = spawnPosition;
        }

        public void StartPatrol()
        {
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
            SetWalking(false);
        }

        private void SetWalking(bool walking)
        {
            if (_isWalking == walking)
            {
                return;
            }

            _isWalking = walking;
            if (_animator != null)
            {
                _animator.SetBool(IsWalkingHash, walking);
            }
        }

        private List<Vector3> BuildFullPath()
        {
            var fullPath = new List<Vector3> { _spawnPosition };

            if (_patrolPath != null)
            {
                foreach (var waypoint in _patrolPath)
                {
                    fullPath.Add(_levelData.GridToWorld(waypoint.GridPosition));
                }
            }

            return fullPath;
        }

        private PatrolWaypoint GetWaypointData(int pathIndex)
        {
            // Index 0 is spawn position (no waypoint data)
            if (pathIndex <= 0 || _patrolPath == null || pathIndex > _patrolPath.Count)
            {
                return null;
            }

            return _patrolPath[pathIndex - 1];
        }

        private async UniTaskVoid PatrolLoopAsync(CancellationToken ct)
        {
            _isPatrolling = true;
            _currentWaypointIndex = 0;
            _movingForward = true;

            var fullPath = BuildFullPath();

            // Need at least 2 points to patrol (spawn + 1 waypoint, or just spawn means no movement)
            if (fullPath.Count < 2)
            {
                _isPatrolling = false;
                return;
            }

            while (!ct.IsCancellationRequested)
            {
                // Determine next index based on direction
                int nextIndex;
                if (_movingForward)
                {
                    nextIndex = _currentWaypointIndex + 1;
                    if (nextIndex >= fullPath.Count)
                    {
                        // Reached end, reverse direction
                        _movingForward = false;
                        nextIndex = _currentWaypointIndex - 1;
                    }
                }
                else
                {
                    nextIndex = _currentWaypointIndex - 1;
                    if (nextIndex < 0)
                    {
                        // Reached start, reverse direction
                        _movingForward = true;
                        nextIndex = _currentWaypointIndex + 1;
                    }
                }

                // Clamp to valid range
                nextIndex = Mathf.Clamp(nextIndex, 0, fullPath.Count - 1);

                if (nextIndex == _currentWaypointIndex)
                {
                    // Nowhere to go
                    await UniTask.Yield(ct);
                    continue;
                }

                Vector3 targetPosition = fullPath[nextIndex];

                // Get waypoint data for delay/animator/observation (only waypoints have this, not spawn)
                var waypointData = GetWaypointData(nextIndex);
                bool hasDelay = waypointData?.WaitDelay > 0f;
                bool hasAnimatorAction = !string.IsNullOrEmpty(waypointData?.AnimatorParameterName);
                bool isObservation = waypointData?.IsObservation ?? false;
                bool shouldStop = hasDelay || hasAnimatorAction || isObservation;

                // Determine next-next position for smooth blending
                int nextNextIndex = _movingForward ? nextIndex + 1 : nextIndex - 1;
                nextNextIndex = Mathf.Clamp(nextNextIndex, 0, fullPath.Count - 1);
                Vector3 nextNextPosition = fullPath[nextNextIndex];

                await MoveToWaypointAsync(targetPosition, nextNextPosition, shouldStop, ct);

                if (ct.IsCancellationRequested)
                {
                    break;
                }

                // Apply animator parameter if specified
                if (_animator != null && hasAnimatorAction)
                {
                    _animator.SetBool(waypointData.AnimatorParameterName, waypointData.AnimatorParameterValue);
                }

                // Perform observation if this is an observation waypoint
                if (isObservation)
                {
                    await PerformObservationAsync(waypointData.WaitDelay, ct);
                }
                // Regular wait at waypoint
                else if (hasDelay)
                {
                    await UniTask.Delay(
                        (int)(waypointData.WaitDelay * 1000),
                        cancellationToken: ct
                    );
                }

                if (ct.IsCancellationRequested)
                {
                    break;
                }

                _currentWaypointIndex = nextIndex;

                // Always yield at least once per loop iteration to prevent freezing
                await UniTask.Yield(ct);
            }

            _isPatrolling = false;
        }

        private async UniTask PerformObservationAsync(float duration, CancellationToken ct)
        {
            SetWalking(false);

            float halfAngle = ScanAngle * 0.5f;
            Quaternion startRotation = transform.rotation;
            Quaternion leftRotation = startRotation * Quaternion.Euler(0f, -halfAngle, 0f);
            Quaternion rightRotation = startRotation * Quaternion.Euler(0f, halfAngle, 0f);

            float elapsed = 0f;

            // If duration is 0, do one full scan cycle
            float minDuration = (ScanAngle / ScanSpeed) * 2f + PauseAtEnds * 2f;
            float actualDuration = Mathf.Max(duration, minDuration);

            while (elapsed < actualDuration && !ct.IsCancellationRequested)
            {
                // Look left
                await RotateToAsync(leftRotation, ct);
                if (ct.IsCancellationRequested) break;

                // Pause at left
                await UniTask.Delay((int)(PauseAtEnds * 1000), cancellationToken: ct);
                if (ct.IsCancellationRequested) break;

                // Look right
                await RotateToAsync(rightRotation, ct);
                if (ct.IsCancellationRequested) break;

                // Pause at right
                await UniTask.Delay((int)(PauseAtEnds * 1000), cancellationToken: ct);
                if (ct.IsCancellationRequested) break;

                elapsed += (ScanAngle / ScanSpeed) * 2f + PauseAtEnds * 2f;
            }

            // Return to forward direction
            await RotateToAsync(startRotation, ct);
        }

        private async UniTask RotateToAsync(Quaternion targetRotation, CancellationToken ct)
        {
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
                    ScanSpeed * Time.deltaTime
                );

                await UniTask.Yield(ct);
            }
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

            SetWalking(true);

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
                        SetWalking(false);
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

                    // Apply gravity
                    if (_characterController.isGrounded)
                    {
                        _verticalVelocity = -0.5f;
                    }
                    else
                    {
                        _verticalVelocity += _gravity * Time.deltaTime;
                    }

                    // Move using CharacterController
                    Vector3 move = moveDirection * (_moveSpeed * Time.deltaTime);
                    move.y = _verticalVelocity * Time.deltaTime;
                    _characterController.Move(move);
                }
                else
                {
                    // Direction is zero, we're at the target
                    SetWalking(false);
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
