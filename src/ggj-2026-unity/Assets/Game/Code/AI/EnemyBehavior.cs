using System;
using Game.AI.BehaviorTree;
using Game.AI.BehaviorTree.Nodes;
using Game.Detection;
using Game.LevelEditor.Runtime;
using UnityEngine;

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
    public class EnemyBehavior : MonoBehaviour
    {
        private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");

        public event Action<EnemyState> StateChanged;
        public event Action PlayerFullyDetected;

        [Header("Detection")]
        [SerializeField] private VisionCone _visionCone;

        [Header("Movement")]
        [SerializeField] private float _followSpeed = 2f;
        [SerializeField] private float _followRotationSpeed = 120f;

        [Header("Search")]
        [SerializeField] private float _searchDuration = 3f;
        [SerializeField, Range(30f, 180f)] private float _searchScanAngle = 120f;
        [SerializeField] private float _searchScanSpeed = 90f;

        private EnemyPatrolController _patrolController;
        private CharacterController _characterController;
        private Animator _animator;
        private IBehaviorNode _behaviorTree;

        private EnemyState _currentState = EnemyState.Patrol;
        private Transform _lastKnownTarget;
        private Vector3 _lastKnownPosition;
        private float _searchTimer;
        private bool _wasDetecting;
        private bool _isSearchScanning;
        private float _searchScanProgress;
        private Quaternion _searchStartRotation;
        private float _gravity = -9.81f;
        private float _verticalVelocity;

        public EnemyState CurrentState => _currentState;
        public bool IsPlayerVisible => _visionCone != null && _visionCone.CurrentTarget != null;
        public float DetectionProgress => _visionCone?.DetectionProgress ?? 0f;

        private void Awake()
        {
            _patrolController = GetComponent<EnemyPatrolController>();
            _characterController = GetComponent<CharacterController>();
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
            // ├── Alert branch (Sequence): IsFullyDetected? -> Alert
            // ├── Detecting branch (Sequence): IsPlayerVisible? -> StopPatrol -> FollowPlayer
            // ├── Searching branch (Sequence): WasDetecting? -> Search -> ResumePatrol
            // └── Patrol branch: Patrol

            _behaviorTree = new SelectorNode(
                // Alert branch - game over
                new SequenceNode(
                    new ConditionNode(IsFullyDetected),
                    new ActionNode(DoAlert)
                ),
                // Detecting branch - follow player
                new SequenceNode(
                    new ConditionNode(() => IsPlayerVisible),
                    new ActionNode(DoDetecting)
                ),
                // Searching branch - look around after losing player
                new SequenceNode(
                    new ConditionNode(() => _currentState == EnemyState.Searching),
                    new ActionNode(DoSearching)
                ),
                // Patrol branch - default behavior
                new ActionNode(DoPatrol)
            );
        }

        private bool IsFullyDetected()
        {
            return _visionCone != null && _visionCone.IsDetected;
        }

        private BehaviorStatus DoAlert()
        {
            if (_currentState != EnemyState.Alert)
            {
                SetState(EnemyState.Alert);
                _patrolController.StopPatrol();
                SetWalking(false);
                Debug.Log($"[EnemyBehavior] Player fully detected! Game Over!");
                PlayerFullyDetected?.Invoke();
            }

            // Stay in alert forever
            return BehaviorStatus.Running;
        }

        private BehaviorStatus DoDetecting()
        {
            if (_currentState != EnemyState.Detecting)
            {
                SetState(EnemyState.Detecting);
                _patrolController.StopPatrol();
                _wasDetecting = true;
            }

            // Track last known position
            if (_visionCone.CurrentTarget != null)
            {
                _lastKnownTarget = _visionCone.CurrentTarget;
                _lastKnownPosition = _lastKnownTarget.position;
            }

            // Slowly walk towards player
            MoveTowardsTarget(_lastKnownPosition);

            return BehaviorStatus.Success;
        }

        private BehaviorStatus DoSearching()
        {
            // Initialize search state
            if (!_isSearchScanning)
            {
                _isSearchScanning = true;
                _searchScanProgress = 0f;
                _searchStartRotation = transform.rotation;
                _searchTimer = 0f;
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
                return BehaviorStatus.Success;
            }

            return BehaviorStatus.Running;
        }

        private BehaviorStatus DoPatrol()
        {
            if (_wasDetecting)
            {
                // Just lost sight of player, enter search mode
                _wasDetecting = false;
                SetState(EnemyState.Searching);
                return BehaviorStatus.Failure; // Let search branch handle it
            }

            if (_currentState != EnemyState.Patrol)
            {
                SetState(EnemyState.Patrol);
                _patrolController.StartPatrol();
            }

            return BehaviorStatus.Success;
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

        private void MoveTowardsTarget(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.25f)
            {
                // Close enough, just face target
                SetWalking(false);
                return;
            }

            SetWalking(true);

            // Rotate towards target
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _followRotationSpeed * Time.deltaTime
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
            Vector3 move = direction.normalized * (_followSpeed * Time.deltaTime);
            move.y = _verticalVelocity * Time.deltaTime;
            _characterController.Move(move);
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
