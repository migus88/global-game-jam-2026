using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configuration;
using Game.LevelEditor.Data;
using UnityEngine;
using UnityEngine.AI;
using VContainer;

namespace Game.LevelEditor.Runtime
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyPatrolController : MonoBehaviour
    {
        private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");

        [Header("Movement")]
        [SerializeField] private float _patrolSpeed = 3f;
        [SerializeField] private float _rotationSpeed = 180f;

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
        private NavMeshAgent _navAgent;
        private CancellationTokenSource _patrolCts;
        private int _currentWaypointIndex;
        private bool _isPatrolling;
        private bool _movingForward = true;
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
            _animator = GetComponentInChildren<Animator>();
            _navAgent = GetComponent<NavMeshAgent>();
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

            // Configure NavMeshAgent
            _navAgent.speed = _patrolSpeed;
            _navAgent.angularSpeed = _rotationSpeed;
            _navAgent.updateRotation = true;
        }

        public void StartPatrol()
        {
            StopPatrol();
            _navAgent.isStopped = false;
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

            if (_navAgent != null && _navAgent.isOnNavMesh)
            {
                _navAgent.isStopped = true;
            }
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

                // Move to waypoint using NavMesh
                await MoveToWaypointAsync(targetPosition, ct);

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
                    SetWalking(false);
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
            _navAgent.isStopped = true;
            _navAgent.updateRotation = false;

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

            _navAgent.updateRotation = true;
            _navAgent.isStopped = false;
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

        private async UniTask MoveToWaypointAsync(Vector3 targetPosition, CancellationToken ct)
        {
            _navAgent.isStopped = false;
            _navAgent.SetDestination(targetPosition);
            SetWalking(true);

            // Wait until we reach the destination
            while (!ct.IsCancellationRequested)
            {
                // Check if path is still being calculated
                if (_navAgent.pathPending)
                {
                    await UniTask.Yield(ct);
                    continue;
                }

                // Check if we've reached the destination
                if (!_navAgent.hasPath || _navAgent.remainingDistance <= _navAgent.stoppingDistance)
                {
                    break;
                }

                // Update walking animation based on velocity
                SetWalking(_navAgent.velocity.sqrMagnitude > 0.01f);

                await UniTask.Yield(ct);
            }

            SetWalking(false);
        }
    }
}
