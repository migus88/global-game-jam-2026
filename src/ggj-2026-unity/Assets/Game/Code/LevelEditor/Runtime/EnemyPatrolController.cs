using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.AI;
using Game.Configuration;
using UnityEngine;
using UnityEngine.AI;
using VContainer;

namespace Game.LevelEditor.Runtime
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyPatrolController : MonoBehaviour
    {
        private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");

        [Header("Patrol Path")]
        [SerializeField] private List<PatrolWaypointData> _patrolWaypoints = new();

        [Header("Movement")]
        [SerializeField] private float _patrolSpeed = 3f;
        [SerializeField] private float _rotationSpeed = 180f;
        [SerializeField] private float _rotateBeforeMoveThreshold = 30f;

        [Header("Observation Override")]
        [SerializeField] private bool _overrideObservationSettings;
        [SerializeField, Range(30f, 180f)] private float _scanAngle = 120f;
        [SerializeField] private float _scanSpeed = 90f;
        [SerializeField] private float _pauseAtEnds = 0.3f;

        private GameConfiguration _config;
        private Animator _animator;
        private NavMeshAgent _navAgent;
        private CancellationTokenSource _patrolCts;
        private int _currentWaypointIndex;
        private bool _isPatrolling;
        private bool _movingForward = true;
        private bool _isWalking;

        public bool IsPatrolling => _isPatrolling;
        public int CurrentWaypointIndex => _currentWaypointIndex;
        public IReadOnlyList<PatrolWaypointData> PatrolWaypoints => _patrolWaypoints;

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

        private void Start()
        {
            // Configure NavMeshAgent
            if (_navAgent != null)
            {
                _navAgent.speed = _patrolSpeed;
                _navAgent.angularSpeed = _rotationSpeed;
                _navAgent.updateRotation = true;
            }
        }

        private void OnDestroy()
        {
            StopPatrol();
        }

        public void StartPatrol()
        {
            StopPatrol();

            if (_navAgent == null || !_navAgent.isOnNavMesh)
            {
                Debug.LogWarning($"[EnemyPatrolController] {gameObject.name} is not on NavMesh, cannot start patrol");
                return;
            }

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
            // Start from current position
            var fullPath = new List<Vector3> { transform.position };

            foreach (var waypoint in _patrolWaypoints)
            {
                fullPath.Add(waypoint.Position);
            }

            return fullPath;
        }

        private PatrolWaypointData GetWaypointData(int pathIndex)
        {
            // Index 0 is spawn position (no waypoint data)
            if (pathIndex <= 0 || pathIndex > _patrolWaypoints.Count)
            {
                return null;
            }

            return _patrolWaypoints[pathIndex - 1];
        }

        private async UniTaskVoid PatrolLoopAsync(CancellationToken ct)
        {
            _isPatrolling = true;
            _currentWaypointIndex = 0;
            _movingForward = true;

            var fullPath = BuildFullPath();

            // Need at least 2 points to patrol
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
                        _movingForward = false;
                        nextIndex = _currentWaypointIndex - 1;
                    }
                }
                else
                {
                    nextIndex = _currentWaypointIndex - 1;
                    if (nextIndex < 0)
                    {
                        _movingForward = true;
                        nextIndex = _currentWaypointIndex + 1;
                    }
                }

                nextIndex = Mathf.Clamp(nextIndex, 0, fullPath.Count - 1);

                if (nextIndex == _currentWaypointIndex)
                {
                    await UniTask.Yield(ct);
                    continue;
                }

                Vector3 targetPosition = fullPath[nextIndex];
                var waypointData = GetWaypointData(nextIndex);
                bool hasDelay = waypointData?.WaitDelay > 0f;
                bool hasAnimatorAction = !string.IsNullOrEmpty(waypointData?.AnimatorParameterName);
                bool isObservation = waypointData?.IsObservation ?? false;

                await MoveToWaypointAsync(targetPosition, ct);

                if (ct.IsCancellationRequested)
                {
                    break;
                }

                if (_animator != null && hasAnimatorAction)
                {
                    _animator.SetBool(waypointData.AnimatorParameterName, waypointData.AnimatorParameterValue);
                }

                if (isObservation)
                {
                    await PerformObservationAsync(waypointData.WaitDelay, ct);
                }
                else if (hasDelay)
                {
                    SetWalking(false);
                    await UniTask.Delay((int)(waypointData.WaitDelay * 1000), cancellationToken: ct);
                }

                if (ct.IsCancellationRequested)
                {
                    break;
                }

                _currentWaypointIndex = nextIndex;
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
            float minDuration = (ScanAngle / ScanSpeed) * 2f + PauseAtEnds * 2f;
            float actualDuration = Mathf.Max(duration, minDuration);

            while (elapsed < actualDuration && !ct.IsCancellationRequested)
            {
                await RotateToAsync(leftRotation, ct);
                if (ct.IsCancellationRequested) break;

                await UniTask.Delay((int)(PauseAtEnds * 1000), cancellationToken: ct);
                if (ct.IsCancellationRequested) break;

                await RotateToAsync(rightRotation, ct);
                if (ct.IsCancellationRequested) break;

                await UniTask.Delay((int)(PauseAtEnds * 1000), cancellationToken: ct);
                if (ct.IsCancellationRequested) break;

                elapsed += (ScanAngle / ScanSpeed) * 2f + PauseAtEnds * 2f;
            }

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
            // Calculate direction to target
            Vector3 directionToTarget = (targetPosition - transform.position).normalized;
            directionToTarget.y = 0f;

            if (directionToTarget.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                float angleToTarget = Quaternion.Angle(transform.rotation, targetRotation);

                // If angle is large, rotate first before moving
                if (angleToTarget > _rotateBeforeMoveThreshold)
                {
                    _navAgent.isStopped = true;
                    _navAgent.updateRotation = false;
                    SetWalking(false);

                    await RotateTowardsAsync(targetRotation, _rotationSpeed, ct);

                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    _navAgent.updateRotation = true;
                }
            }

            _navAgent.isStopped = false;
            _navAgent.SetDestination(targetPosition);
            SetWalking(true);

            while (!ct.IsCancellationRequested)
            {
                if (_navAgent.pathPending)
                {
                    await UniTask.Yield(ct);
                    continue;
                }

                if (!_navAgent.hasPath || _navAgent.remainingDistance <= _navAgent.stoppingDistance)
                {
                    break;
                }

                SetWalking(_navAgent.velocity.sqrMagnitude > 0.01f);
                await UniTask.Yield(ct);
            }

            SetWalking(false);
        }

        private async UniTask RotateTowardsAsync(Quaternion targetRotation, float speed, CancellationToken ct)
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
                    speed * Time.deltaTime
                );

                await UniTask.Yield(ct);
            }
        }

#if UNITY_EDITOR
        public void AddWaypoint(Vector3 position)
        {
            _patrolWaypoints.Add(new PatrolWaypointData(position));
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void InsertWaypoint(int index, Vector3 position)
        {
            _patrolWaypoints.Insert(index, new PatrolWaypointData(position));
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void RemoveWaypoint(int index)
        {
            if (index >= 0 && index < _patrolWaypoints.Count)
            {
                _patrolWaypoints.RemoveAt(index);
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        public void SetWaypointPosition(int index, Vector3 position)
        {
            if (index >= 0 && index < _patrolWaypoints.Count)
            {
                _patrolWaypoints[index].Position = position;
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        public void ClearWaypoints()
        {
            _patrolWaypoints.Clear();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
