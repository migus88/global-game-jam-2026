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

            // Reset from alert state after conversation
            if (_currentState == EnemyState.Alert && !_isIgnoringPlayer)
            {
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
            }

            // Update last known position only when player is visible
            if (_visionCone.CurrentTarget != null)
            {
                _lastKnownPosition = _visionCone.CurrentTarget.position;
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
                _navAgent.updateRotation = true;
                return TaskStatus.Failure; // Let detecting branch handle it
            }

            // Initialize search scan (we've already arrived at last known position)
            if (!_isSearchScanning)
            {
                _isSearchScanning = true;
                _searchStartRotation = transform.rotation;
                _searchTimer = 0f;
                _navAgent.isStopped = true;
                _navAgent.updateRotation = false;
                SetWalking(false);
            }

            // Perform scanning
            _searchTimer += Time.deltaTime;
            PerformSearchScan();

            // Check if search duration elapsed
            if (_searchTimer >= _searchDuration)
            {
                // Search complete, return to patrol
                _isSearchScanning = false;
                _wasDetecting = false;
                _navAgent.updateRotation = true;
                SetState(EnemyState.Patrol);
                _patrolController.StartPatrol();
                return TaskStatus.Success;
            }

            return TaskStatus.Continue;
        }

        private TaskStatus DoPatrol()
        {
            if (_wasDetecting)
            {
                // Reached last known position, enter search mode
                _isSearchScanning = false;
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
