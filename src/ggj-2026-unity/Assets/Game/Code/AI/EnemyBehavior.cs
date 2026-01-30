using System;
using CleverCrow.Fluid.BTs.Tasks;
using CleverCrow.Fluid.BTs.Trees;
using Game.Detection;
using Game.LevelEditor.Runtime;
using UnityEngine;
using UnityEngine.AI;

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
    public class EnemyBehavior : MonoBehaviour
    {
        private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");

        public event Action<EnemyState> StateChanged;
        public event Action PlayerFullyDetected;

        [Header("Detection")]
        [SerializeField] private VisionCone _visionCone;

        [Header("Movement")]
        [SerializeField] private float _followSpeed = 2f;

        [Header("Search")]
        [SerializeField] private float _searchDuration = 3f;
        [SerializeField, Range(30f, 180f)] private float _searchScanAngle = 120f;
        [SerializeField] private float _searchScanSpeed = 90f;

        private EnemyPatrolController _patrolController;
        private NavMeshAgent _navAgent;
        private Animator _animator;
        private BehaviorTree _behaviorTree;

        private EnemyState _currentState = EnemyState.Patrol;
        private Vector3 _lastKnownPosition;
        private float _searchTimer;
        private bool _wasDetecting;
        private bool _isSearchScanning;
        private Quaternion _searchStartRotation;

        public EnemyState CurrentState => _currentState;
        public bool IsPlayerVisible => _visionCone != null && _visionCone.CurrentTarget != null;
        public float DetectionProgress => _visionCone?.DetectionProgress ?? 0f;

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
            if (_visionCone != null)
            {
                _visionCone.Detected += OnPlayerDetected;
                _visionCone.LostTarget += OnPlayerLost;
            }

            BuildBehaviorTree();

            // Start in patrol state
            SetState(EnemyState.Patrol);
            _patrolController.StartPatrol();
        }

        private void OnDestroy()
        {
            if (_visionCone != null)
            {
                _visionCone.Detected -= OnPlayerDetected;
                _visionCone.LostTarget -= OnPlayerLost;
            }
        }

        private void Update()
        {
            _behaviorTree?.Tick();
        }

        private void BuildBehaviorTree()
        {
            // Behavior tree structure:
            // Root (Selector)
            // ├── Alert branch: IsFullyDetected? -> Alert
            // ├── Detecting branch: IsPlayerVisible? -> FollowPlayer
            // ├── Searching branch: IsSearching? -> Search
            // └── Patrol branch: Patrol

            _behaviorTree = new BehaviorTreeBuilder(gameObject)
                .Selector()
                    // Alert branch - game over
                    .Sequence()
                        .Condition("IsFullyDetected", () => _visionCone != null && _visionCone.IsDetected)
                        .Do("Alert", DoAlert)
                    .End()
                    // Detecting branch - follow player
                    .Sequence()
                        .Condition("IsPlayerVisible", () => IsPlayerVisible)
                        .Do("FollowPlayer", DoDetecting)
                    .End()
                    // Searching branch - look around after losing player
                    .Sequence()
                        .Condition("IsSearching", () => _currentState == EnemyState.Searching)
                        .Do("Search", DoSearching)
                    .End()
                    // Patrol branch - default behavior
                    .Do("Patrol", DoPatrol)
                .End()
                .Build();
        }

        private TaskStatus DoAlert()
        {
            if (_currentState != EnemyState.Alert)
            {
                SetState(EnemyState.Alert);
                _patrolController.StopPatrol();
                _navAgent.isStopped = true;
                SetWalking(false);
                Debug.Log($"[EnemyBehavior] Player fully detected! Game Over!");
                PlayerFullyDetected?.Invoke();
            }

            // Stay in alert forever
            return TaskStatus.Continue;
        }

        private TaskStatus DoDetecting()
        {
            if (_currentState != EnemyState.Detecting)
            {
                SetState(EnemyState.Detecting);
                _patrolController.StopPatrol();
                _wasDetecting = true;
                _navAgent.speed = _followSpeed;
            }

            // Track last known position
            if (_visionCone.CurrentTarget != null)
            {
                _lastKnownPosition = _visionCone.CurrentTarget.position;
            }

            // Move towards player using NavMesh
            _navAgent.isStopped = false;
            _navAgent.SetDestination(_lastKnownPosition);
            SetWalking(_navAgent.velocity.sqrMagnitude > 0.01f);

            return TaskStatus.Success;
        }

        private TaskStatus DoSearching()
        {
            // Initialize search state
            if (!_isSearchScanning)
            {
                _isSearchScanning = true;
                _searchStartRotation = transform.rotation;
                _searchTimer = 0f;
                _navAgent.isStopped = true;
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
                SetState(EnemyState.Patrol);
                return TaskStatus.Success;
            }

            return TaskStatus.Continue;
        }

        private TaskStatus DoPatrol()
        {
            if (_wasDetecting)
            {
                // Just lost sight of player, enter search mode
                _wasDetecting = false;
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
