using System;
using Game.Configuration;
using UnityEngine;
using VContainer;

namespace Game.Detection
{
    public class VisionCone : MonoBehaviour
    {
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int FillColorProperty = Shader.PropertyToID("_FillColor");
        private static readonly int FillProperty = Shader.PropertyToID("_Fill");
        private static readonly int MaxDistanceProperty = Shader.PropertyToID("_MaxDistance");

        public event Action<Transform> Detected;
        public event Action LostTarget;

        [SerializeField] private bool _overrideSettings;
        [SerializeField] private float _viewDistance = 10f;
        [SerializeField, Range(10f, 180f)] private float _viewAngle = 60f;
        [SerializeField] private LayerMask _obstacleLayerOverride;
        [SerializeField] private LayerMask _targetLayerOverride;

        private GameConfiguration _config;
        private VisionConeSettings _settings;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Material _material;
        private Mesh _coneMesh;

        private Vector3[] _vertices;
        private Vector2[] _uvs;
        private int[] _triangles;
        private float[] _rayDistances;
        private int _segments;
        private float _heightOffset;

        private Transform _currentTarget;
        private float _detectionProgress;
        private bool _isDetected;

        public float DetectionProgress => _detectionProgress;
        public bool IsDetected => _isDetected;
        public Transform CurrentTarget => _currentTarget;

        private float ViewDistance => _overrideSettings ? _viewDistance : _settings.ViewDistance;
        private float ViewAngle => _overrideSettings ? _viewAngle : _settings.ViewAngle;
        private LayerMask ObstacleLayer => _overrideSettings ? _obstacleLayerOverride : _settings.ObstacleLayer;
        private LayerMask TargetLayer => _overrideSettings ? _targetLayerOverride : _settings.TargetLayer;

        [Inject]
        public void Construct(GameConfiguration config)
        {
            _config = config;
            _settings = config.VisionCone;
        }

        private void Awake()
        {
            SetupMeshComponents();
        }

        private void Start()
        {
            if (_settings == null)
            {
                Debug.LogWarning($"VisionCone on {gameObject.name}: No GameConfiguration injected. Using default values.");
                _overrideSettings = true;
            }

            InitializeMesh();
            CreateMaterial();
            UpdateVisual(0f);
        }

        private void Update()
        {
            UpdateConeMesh();
            UpdateDetection();
            UpdateVisual(_detectionProgress);
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }

            if (_coneMesh != null)
            {
                Destroy(_coneMesh);
            }
        }

        private void SetupMeshComponents()
        {
            _meshFilter = gameObject.AddComponent<MeshFilter>();
            _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
        }

        private void InitializeMesh()
        {
            _segments = _settings?.ConeSegments ?? 32;
            _heightOffset = _settings?.ConeHeightOffset ?? 0.05f;

            _coneMesh = new Mesh { name = "VisionCone" };
            _coneMesh.MarkDynamic();

            _vertices = new Vector3[_segments + 2];
            _uvs = new Vector2[_segments + 2];
            _triangles = new int[_segments * 3];
            _rayDistances = new float[_segments + 1];

            // Center vertex
            _vertices[0] = new Vector3(0f, _heightOffset, 0f);
            _uvs[0] = new Vector2(0f, 0f);

            // Setup triangles (these don't change)
            for (int i = 0; i < _segments; i++)
            {
                _triangles[i * 3] = 0;
                _triangles[i * 3 + 1] = i + 1;
                _triangles[i * 3 + 2] = i + 2;
            }

            _coneMesh.vertices = _vertices;
            _coneMesh.triangles = _triangles;
            _coneMesh.uv = _uvs;

            _meshFilter.mesh = _coneMesh;
        }

        private void UpdateConeMesh()
        {
            float halfAngle = ViewAngle * 0.5f;
            float angleStep = ViewAngle / _segments;
            float maxDist = ViewDistance;
            Vector3 origin = transform.position;

            for (int i = 0; i <= _segments; i++)
            {
                float currentAngle = -halfAngle + angleStep * i;
                Vector3 direction = Quaternion.Euler(0f, currentAngle, 0f) * transform.forward;

                float hitDistance = maxDist;

                if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDist, ObstacleLayer))
                {
                    hitDistance = hit.distance;
                }

                _rayDistances[i] = hitDistance;

                // Convert to local space
                Vector3 localDir = Quaternion.Euler(0f, currentAngle, 0f) * Vector3.forward;
                _vertices[i + 1] = new Vector3(
                    localDir.x * hitDistance,
                    _heightOffset,
                    localDir.z * hitDistance
                );

                // UV.x = normalized distance (0 at center, 1 at max range)
                // UV.y = normalized angle position
                _uvs[i + 1] = new Vector2(hitDistance / maxDist, (float)i / _segments);
            }

            _coneMesh.vertices = _vertices;
            _coneMesh.uv = _uvs;
            _coneMesh.RecalculateNormals();
            _coneMesh.RecalculateBounds();
        }

        private void CreateMaterial()
        {
            var shader = Shader.Find("Game/VisionCone");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
                Debug.LogWarning("VisionCone shader not found, using fallback.");
            }

            _material = new Material(shader);
            _material.SetFloat(MaxDistanceProperty, ViewDistance);
            _meshRenderer.material = _material;
        }

        private void UpdateDetection()
        {
            float timeToDetect = _settings?.TimeToDetect ?? 2f;
            float timeToLose = _settings?.TimeToLoseDetection ?? 1.5f;

            Transform visibleTarget = FindVisibleTarget();

            if (visibleTarget != null)
            {
                _currentTarget = visibleTarget;
                _detectionProgress = Mathf.Clamp01(_detectionProgress + Time.deltaTime / timeToDetect);

                if (_detectionProgress >= 1f && !_isDetected)
                {
                    _isDetected = true;
                    Detected?.Invoke(_currentTarget);
                }
            }
            else
            {
                _detectionProgress = Mathf.Clamp01(_detectionProgress - Time.deltaTime / timeToLose);

                if (_detectionProgress <= 0f && _isDetected)
                {
                    _isDetected = false;
                    _currentTarget = null;
                    LostTarget?.Invoke();
                }
            }
        }

        private Transform FindVisibleTarget()
        {
            var colliders = Physics.OverlapSphere(transform.position, ViewDistance, TargetLayer);
            Vector3 rayOrigin = transform.position + Vector3.up * _heightOffset;

            foreach (var col in colliders)
            {
                Vector3 targetPos = col.bounds.center;

                // Calculate horizontal direction and distance (matching visual cone behavior)
                Vector3 toTargetFlat = new Vector3(
                    targetPos.x - rayOrigin.x,
                    0f,
                    targetPos.z - rayOrigin.z
                );
                float horizontalDistance = toTargetFlat.magnitude;
                Vector3 horizontalDir = toTargetFlat.normalized;

                // Check angle
                float angle = Vector3.Angle(transform.forward, horizontalDir);
                if (angle > ViewAngle * 0.5f)
                {
                    continue;
                }

                // Cast HORIZONTAL ray (same as visual cone) to check for obstacles
                // This matches exactly what the visual cone shows
                if (Physics.Raycast(rayOrigin, horizontalDir, out RaycastHit hit, horizontalDistance, ObstacleLayer))
                {
                    // Obstacle blocks the path - skip this target
                    Debug.DrawLine(rayOrigin, hit.point, Color.red);
                    continue;
                }

                // No obstacle in the way - target is visible
                Debug.DrawLine(rayOrigin, rayOrigin + horizontalDir * horizontalDistance, Color.green);
                return col.transform;
            }

            return null;
        }

        private void UpdateVisual(float progress)
        {
            if (_material == null)
            {
                return;
            }

            Color baseColor = _settings?.IdleColor ?? new Color(0f, 1f, 0f, 0.3f);
            Color fillColor = _settings?.AlertColor ?? new Color(1f, 0f, 0f, 0.6f);

            // Interpolate fill color based on progress
            if (progress > 0f && progress < 1f)
            {
                Color detectingColor = _settings?.DetectingColor ?? new Color(1f, 1f, 0f, 0.5f);
                fillColor = Color.Lerp(detectingColor, fillColor, progress);
            }

            _material.SetColor(BaseColorProperty, baseColor);
            _material.SetColor(FillColorProperty, fillColor);
            _material.SetFloat(FillProperty, progress);
            _material.SetFloat(MaxDistanceProperty, ViewDistance);
        }

        private void OnDrawGizmosSelected()
        {
            float distance = _overrideSettings || _settings == null ? _viewDistance : _settings.ViewDistance;
            float angle = _overrideSettings || _settings == null ? _viewAngle : _settings.ViewAngle;

            Gizmos.color = Color.yellow;
            Vector3 leftDir = Quaternion.Euler(0, -angle * 0.5f, 0) * transform.forward;
            Vector3 rightDir = Quaternion.Euler(0, angle * 0.5f, 0) * transform.forward;

            Gizmos.DrawLine(transform.position, transform.position + leftDir * distance);
            Gizmos.DrawLine(transform.position, transform.position + rightDir * distance);

            // Draw arc
            int arcSegments = 20;
            float halfAngle = angle * 0.5f;
            float angleStep = angle / arcSegments;
            Vector3 prevPoint = transform.position + leftDir * distance;

            for (int i = 1; i <= arcSegments; i++)
            {
                float currentAngle = -halfAngle + angleStep * i;
                Vector3 dir = Quaternion.Euler(0, currentAngle, 0) * transform.forward;
                Vector3 point = transform.position + dir * distance;
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }
    }
}
