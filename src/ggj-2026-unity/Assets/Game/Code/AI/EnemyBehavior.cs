using System;
using CleverCrow.Fluid.BTs.Tasks;
using CleverCrow.Fluid.BTs.Trees;
using Game.Conversation.Events;
using Game.Detection;
using Game.Events;
using Game.GameState;
using Game.LevelEditor.Runtime;
using Migs.MLock.Interfaces;
using UnityEngine;
using UnityEngine.AI;
using VContainer;
using VContainer.Unity;

namespace Game.AI
{
    public enum EnemyState
    {
        Patrol = 0,
        Detecting = 1,
        Searching = 2,
        Alert = 3
    }

    [RequireComponent(typeof(EnemyPatrolController))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyBehavior : MonoBehaviour, ILockable<GameLockTags>
    {
        private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");

        public event Action<EnemyState> StateChanged;
        public event Action PlayerFullyDetected;

        [Header("Detection")]
        [SerializeField] private VisionCone _visionCone;

        [Header("Movement")]
        [SerializeField] private float _followSpeed = 2f;
        [SerializeField] private float _followRotationSpeed = 180f;
        [SerializeField] private float _rotateBeforeMoveThreshold = 30f;

        [Header("Search")]
        [SerializeField] private float _searchDuration = 3f;
        [SerializeField, Range(30f, 180f)] private float _searchScanAngle = 120f;
        [SerializeField] private float _searchScanSpeed = 90f;
        [SerializeField] private float _searchRadius = 5f;
        [SerializeField] private int _searchPointCount = 3;

        [Header("Prediction")]
        [SerializeField] private float _predictionTime = 1f;
        [SerializeField] private float _velocitySampleRate = 0.1f;

        private EnemyPatrolController _patrolController;
        private NavMeshAgent _navAgent;
        private Animator _animator;
        private BehaviorTree _behaviorTree;
        private EventAggregator _eventAggregator;
        private GameLockService _lockService;

        private EnemyState _currentState = EnemyState.Patrol;
        private Vector3 _lastKnownPosition;
        private float _searchTimer;
        private bool _wasDetecting;
        private bool _isSearchScanning;
        private Quaternion _searchStartRotation;
        private bool _isRotatingBeforeFollow;
        private Quaternion _followTargetRotation;
        private bool _isLocked;
        private bool _isIgnoringPlayer;

        // Prediction tracking
        private Vector3 _lastTrackedPosition;
        private Vector3 _playerVelocity;
        private float _velocitySampleTimer;
        private Vector3 _playerMovementDirection;

        // Search points
        private Vector3[] _searchPoints;
        private int _currentSearchPointIndex;
        private bool _isMovingToSearchPoint;
        private bool _isScanningAtSearchPoint;

        public EnemyState CurrentState => _currentState;
        public bool IsPlayerVisible => _visionCone != null && _visionCone.CurrentTarget != null && !_isIgnoringPlayer;
        public float DetectionProgress => _visionCone?.DetectionProgress ?? 0f;
        public bool IsIgnoringPlayer => _isIgnoringPlayer;

        public GameLockTags LockTags => GameLockTags.EnemyAI;

        [Inject]
        public void Construct(EventAggregator eventAggregator, GameLockService lockService)
        {
            _eventAggregator = eventAggregator;
            _lockService = lockService;
        }

        private void Awake()
        {
            _patrolController = GetComponent<EnemyPatrolController>();
            _navAgent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>();

            // Auto-find VisionCone if not assigned
            if (_visionCone == null)
            {
                _visionCone = GetComponentInChildren<VisionCone>();
            }
        }

        private void Start()
        {
            ResolveDependenciesIfNeeded();

            if (_visionCone != null)
            {
                _visionCone.Detected += OnPlayerDetected;
                _visionCone.LostTarget += OnPlayerLost;
            }

            _lockService?.Subscribe(this);
            _eventAggregator?.Subscribe<ConversationEndedEvent>(OnConversationEnded);

            BuildBehaviorTree();

            // Start in patrol state
            SetState(EnemyState.Patrol);
            _patrolController.StartPatrol();
        }

        private void ResolveDependenciesIfNeeded()
        {
            if (_eventAggregator != null && _lockService != null)
            {
                return;
            }

            var lifetimeScope = FindAnyObjectByType<LifetimeScope>();

            if (lifetimeScope == null)
            {
                return;
            }

            _eventAggregator ??= lifetimeScope.Container.Resolve<EventAggregator>();
            _lockService ??= lifetimeScope.Container.Resolve<GameLockService>();
        }

        private void OnDestroy()
        {
            if (_visionCone != null)
            {
                _visionCone.Detected -= OnPlayerDetected;
                _visionCone.LostTarget -= OnPlayerLost;
            }

            _lockService?.Unsubscribe(this);
            _eventAggregator?.Unsubscribe<ConversationEndedEvent>(OnConversationEnded);
        }

        public void HandleLocking()
        {
            _isLocked = true;

            if (_navAgent != null && _navAgent.isOnNavMesh)
            {
                _navAgent.isStopped = true;
            }

            SetWalking(false);
        }

        public void HandleUnlocking()
        {
            _isLocked = false;

            // Only reset state for the robot that had the conversation (is ignoring player)
            // Other robots should resume their current behavior
            if (_isIgnoringPlayer)
            {
                _wasDetecting = false;
                _isSearchScanning = false;
                _isRotatingBeforeFollow = false;
                _isMovingToSearchPoint = false;
                _isScanningAtSearchPoint = false;
                _playerMovementDirection = Vector3.zero;
                SetState(EnemyState.Patrol);
                _patrolController?.StartPatrol();
            }

            if (_navAgent != null && _navAgent.isOnNavMesh)
            {
                _navAgent.isStopped = false;
            }
        }

        private void OnConversationEnded(ConversationEndedEvent evt)
        {
            // Check if this enemy was the one who caught the player
            if (evt.Enemy != transform && evt.Enemy != transform.root)
            {
                return;
            }

            if (evt.WasCorrect)
            {
                StartIgnoringPlayer();
            }
        }

        private void StartIgnoringPlayer()
        {
            _isIgnoringPlayer = true;
            _wasDetecting = false;
            _currentState = EnemyState.Patrol;

            if (_visionCone != null)
            {
                _visionCone.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (_isLocked)
            {
                return;
            }

            _behaviorTree?.Tick();
        }

        private void BuildBehaviorTree()
        {
            // Behavior tree structure:
            // Root (Selector)
            // ├── Alert branch: IsFullyDetected? -> Alert
            // ├── Detecting branch: IsPlayerVisible OR (wasDetecting AND not at destination)? -> GoToLastKnownPosition
            // ├── Searching branch: IsSearching? -> Search (scan in place)
            // └── Patrol branch: Patrol

            _behaviorTree = new BehaviorTreeBuilder(gameObject)
                .Selector()
                    // Alert branch - game over
                    .Sequence()
                        .Condition("IsFullyDetected", () => _visionCone != null && _visionCone.IsDetected && !_isIgnoringPlayer)
                        .Do("Alert", DoAlert)
                    .End()
                    // Detecting branch - go to player's position (continues even if player not visible)
                    .Sequence()
                        .Condition("ShouldPursue", ShouldPursue)
                        .Do("GoToLastKnownPosition", DoDetecting)
                    .End()
                    // Searching branch - look around after reaching last known position
                    .Sequence()
                        .Condition("IsSearching", () => _currentState == EnemyState.Searching)
                        .Do("Search", DoSearching)
                    .End()
                    // Patrol branch - default behavior
                    .Do("Patrol", DoPatrol)
                .End()
                .Build();
        }

        private bool ShouldPursue()
        {
            // Continue pursuing if player is visible
            if (IsPlayerVisible)
            {
                return true;
            }

            // Continue pursuing if we were detecting and haven't reached the last known position
            if (_wasDetecting && _currentState == EnemyState.Detecting)
            {
                // Check if we've reached the destination
                if (!_navAgent.pathPending)
                {
                    // No path or reached destination - stop pursuing
                    if (!_navAgent.hasPath || _navAgent.remainingDistance <= _navAgent.stoppingDistance)
                    {
                        return false;
                    }
                }
                return true;
            }

            return false;
        }

        private TaskStatus DoAlert()
        {
            if (_currentState != EnemyState.Alert)
            {
                SetState(EnemyState.Alert);
                _patrolController.StopPatrol();
                _navAgent.isStopped = true;
                SetWalking(false);
                PlayerFullyDetected?.Invoke();
            }

            // Stay in alert until conversation system handles it
            return TaskStatus.Continue;
        }

        private TaskStatus DoDetecting()
        {
            if (_currentState != EnemyState.Detecting)
            {
                SetState(EnemyState.Detecting);
                _patrolController.StopPatrol();
                _wasDetecting = true;
                _isRotatingBeforeFollow = false;
                _isSearchScanning = false;
                _navAgent.speed = _followSpeed;
                _navAgent.updateRotation = true;
                _velocitySampleTimer = 0f;
                _playerVelocity = Vector3.zero;

                if (_visionCone.CurrentTarget != null)
                {
                    _lastTrackedPosition = _visionCone.CurrentTarget.position;
                }
            }

            // Update last known position and track velocity when player is visible
            if (_visionCone.CurrentTarget != null)
            {
                var currentPlayerPos = _visionCone.CurrentTarget.position;

                // Sample velocity periodically
                _velocitySampleTimer += Time.deltaTime;
                if (_velocitySampleTimer >= _velocitySampleRate)
                {
                    _playerVelocity = (currentPlayerPos - _lastTrackedPosition) / _velocitySampleTimer;
                    _playerVelocity.y = 0f;

                    if (_playerVelocity.sqrMagnitude > 0.1f)
                    {
                        _playerMovementDirection = _playerVelocity.normalized;
                    }

                    _lastTrackedPosition = currentPlayerPos;
                    _velocitySampleTimer = 0f;
                }

                // Calculate predicted position
                var predictedPosition = currentPlayerPos + (_playerVelocity * _predictionTime);

                // Validate predicted position on NavMesh
                if (NavMesh.SamplePosition(predictedPosition, out var hit, 3f, NavMesh.AllAreas))
                {
                    _lastKnownPosition = hit.position;
                }
                else
                {
                    _lastKnownPosition = currentPlayerPos;
                }
            }

            // Check if we need to rotate first before moving
            Vector3 directionToTarget = (_lastKnownPosition - transform.position).normalized;
            directionToTarget.y = 0f;

            if (directionToTarget.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                float angleToTarget = Quaternion.Angle(transform.rotation, targetRotation);

                if (angleToTarget > _rotateBeforeMoveThreshold && !_isRotatingBeforeFollow)
                {
                    _isRotatingBeforeFollow = true;
                    _followTargetRotation = targetRotation;
                    _navAgent.isStopped = true;
                    _navAgent.updateRotation = false;
                    SetWalking(false);
                }

                if (_isRotatingBeforeFollow)
                {
                    float currentAngle = Quaternion.Angle(transform.rotation, _followTargetRotation);
                    if (currentAngle < 1f)
                    {
                        transform.rotation = _followTargetRotation;
                        _isRotatingBeforeFollow = false;
                        _navAgent.updateRotation = true;
                    }
                    else
                    {
                        transform.rotation = Quaternion.RotateTowards(
                            transform.rotation,
                            _followTargetRotation,
                            _followRotationSpeed * Time.deltaTime
                        );
                        return TaskStatus.Continue;
                    }
                }
            }

            // Move towards last known position using NavMesh
            _navAgent.isStopped = false;
            _navAgent.SetDestination(_lastKnownPosition);
            SetWalking(_navAgent.velocity.sqrMagnitude > 0.01f);

            return TaskStatus.Success;
        }

        private TaskStatus DoSearching()
        {
            // If player becomes visible during search, interrupt and pursue
            if (IsPlayerVisible)
            {
                _isSearchScanning = false;
                _isMovingToSearchPoint = false;
                _isScanningAtSearchPoint = false;
                _navAgent.updateRotation = true;
                return TaskStatus.Failure; // Let detecting branch handle it
            }

            // Initialize search (generate search points)
            if (!_isSearchScanning)
            {
                _isSearchScanning = true;
                _searchTimer = 0f;
                GenerateSearchPoints();
                _currentSearchPointIndex = 0;
                _isMovingToSearchPoint = true;
                _isScanningAtSearchPoint = false;
                _navAgent.speed = _followSpeed;
                _navAgent.updateRotation = true;
                _navAgent.isStopped = false;

                if (_searchPoints.Length > 0)
                {
                    _navAgent.SetDestination(_searchPoints[0]);
                    SetWalking(true);
                }
            }

            _searchTimer += Time.deltaTime;

            // Check if search duration elapsed
            if (_searchTimer >= _searchDuration)
            {
                EndSearch();
                return TaskStatus.Success;
            }

            // Moving to a search point
            if (_isMovingToSearchPoint)
            {
                if (!_navAgent.pathPending && (!_navAgent.hasPath || _navAgent.remainingDistance <= _navAgent.stoppingDistance))
                {
                    // Arrived at search point, start scanning
                    _isMovingToSearchPoint = false;
                    _isScanningAtSearchPoint = true;
                    _searchStartRotation = transform.rotation;
                    _navAgent.isStopped = true;
                    _navAgent.updateRotation = false;
                    SetWalking(false);
                }
            }
            // Scanning at search point
            else if (_isScanningAtSearchPoint)
            {
                PerformSearchScan();

                // After one full scan cycle, move to next point
                float scanCycleTime = (_searchScanAngle / _searchScanSpeed) * 2f;
                float scanTime = _searchTimer - GetTimeToReachCurrentPoint();

                if (scanTime >= scanCycleTime)
                {
                    _currentSearchPointIndex++;

                    if (_currentSearchPointIndex < _searchPoints.Length)
                    {
                        // Move to next search point
                        _isScanningAtSearchPoint = false;
                        _isMovingToSearchPoint = true;
                        _navAgent.isStopped = false;
                        _navAgent.updateRotation = true;
                        _navAgent.SetDestination(_searchPoints[_currentSearchPointIndex]);
                        SetWalking(true);
                    }
                    else
                    {
                        // Finished all search points
                        EndSearch();
                        return TaskStatus.Success;
                    }
                }
            }

            return TaskStatus.Continue;
        }

        private void GenerateSearchPoints()
        {
            var points = new System.Collections.Generic.List<Vector3>();

            // First point: in the direction player was moving (if known)
            if (_playerMovementDirection.sqrMagnitude > 0.1f)
            {
                var forwardPoint = _lastKnownPosition + (_playerMovementDirection * _searchRadius);
                if (NavMesh.SamplePosition(forwardPoint, out var hit, 3f, NavMesh.AllAreas))
                {
                    points.Add(hit.position);
                }
            }

            // Generate points around the last known position
            float angleStep = 360f / (_searchPointCount + 1);
            float startAngle = _playerMovementDirection.sqrMagnitude > 0.1f
                ? Mathf.Atan2(_playerMovementDirection.x, _playerMovementDirection.z) * Mathf.Rad2Deg
                : transform.eulerAngles.y;

            for (int i = 0; i < _searchPointCount; i++)
            {
                float angle = startAngle + (angleStep * (i + 1));
                var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var point = _lastKnownPosition + (direction * _searchRadius);

                if (NavMesh.SamplePosition(point, out var hit, 3f, NavMesh.AllAreas))
                {
                    // Check if we can path to this point
                    var path = new NavMeshPath();
                    if (_navAgent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                    {
                        points.Add(hit.position);
                    }
                }
            }

            // Always include the last known position as a fallback
            if (points.Count == 0)
            {
                points.Add(_lastKnownPosition);
            }

            _searchPoints = points.ToArray();
        }

        private float GetTimeToReachCurrentPoint()
        {
            // Rough estimate of time spent moving to current point
            float totalDistance = 0f;
            for (int i = 0; i < _currentSearchPointIndex; i++)
            {
                var from = i == 0 ? _lastKnownPosition : _searchPoints[i - 1];
                var to = _searchPoints[i];
                totalDistance += Vector3.Distance(from, to);
            }
            return totalDistance / _followSpeed;
        }

        private void EndSearch()
        {
            _isSearchScanning = false;
            _isMovingToSearchPoint = false;
            _isScanningAtSearchPoint = false;
            _wasDetecting = false;
            _playerMovementDirection = Vector3.zero;
            _navAgent.updateRotation = true;
            _navAgent.isStopped = false;
            SetState(EnemyState.Patrol);
            _patrolController.StartPatrol();
        }

        private TaskStatus DoPatrol()
        {
            if (_wasDetecting)
            {
                // Reached last known position, enter search mode
                _isSearchScanning = false;
                _isMovingToSearchPoint = false;
                _isScanningAtSearchPoint = false;
                _isRotatingBeforeFollow = false;
                SetState(EnemyState.Searching);
                return TaskStatus.Failure; // Let search branch handle it
            }

            if (_currentState != EnemyState.Patrol)
            {
                SetState(EnemyState.Patrol);
                _patrolController.StartPatrol();
            }

            return TaskStatus.Success;
        }

        private void PerformSearchScan()
        {
            float halfAngle = _searchScanAngle * 0.5f;
            float scanCycleTime = (_searchScanAngle / _searchScanSpeed) * 2f;
            float normalizedTime = (_searchTimer % scanCycleTime) / scanCycleTime;

            // Ping-pong between left and right
            float angle;
            if (normalizedTime < 0.5f)
            {
                // Moving left to right
                angle = Mathf.Lerp(-halfAngle, halfAngle, normalizedTime * 2f);
            }
            else
            {
                // Moving right to left
                angle = Mathf.Lerp(halfAngle, -halfAngle, (normalizedTime - 0.5f) * 2f);
            }

            Quaternion targetRotation = _searchStartRotation * Quaternion.Euler(0f, angle, 0f);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _searchScanSpeed * Time.deltaTime
            );
        }

        private void SetState(EnemyState newState)
        {
            if (_currentState == newState)
            {
                return;
            }

            _currentState = newState;
            StateChanged?.Invoke(_currentState);
        }

        private void SetWalking(bool walking)
        {
            if (_animator != null)
            {
                _animator.SetBool(IsWalkingHash, walking);
            }
        }

        private void OnPlayerDetected(Transform target)
        {
            // This is called when detection progress reaches 1 (full detection)
            // The behavior tree will handle transitioning to Alert state
        }

        private void OnPlayerLost()
        {
            // This is called when detection progress drops back to 0
            // The behavior tree will handle transitioning to Search state
        }
    }
}
