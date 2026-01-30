using System;
using Game.Configuration;
using UnityEngine;
using VContainer;

namespace Game.Detection
{
    public class VisionCone : MonoBehaviour
    {
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int FillProperty = Shader.PropertyToID("_Fill");

        public event Action<Transform> Detected;
        public event Action LostTarget;

        [SerializeField] private bool _overrideSettings;
        [SerializeField] private float _viewDistance = 10f;
        [SerializeField, Range(10f, 180f)] private float _viewAngle = 60f;

        private GameConfiguration _config;
        private VisionConeSettings _settings;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Material _material;
        private Mesh _coneMesh;

        private Transform _currentTarget;
        private float _detectionProgress;
        private bool _isDetected;

        private float ViewDistance => _overrideSettings ? _viewDistance : _settings.ViewDistance;
        private float ViewAngle => _overrideSettings ? _viewAngle : _settings.ViewAngle;

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

            GenerateConeMesh();
            CreateMaterial();
            UpdateVisual(0f);
        }

        private void Update()
        {
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

        private void GenerateConeMesh()
        {
            int segments = _settings?.ConeSegments ?? 24;
            float heightOffset = _settings?.ConeHeightOffset ?? 0.05f;

            _coneMesh = new Mesh { name = "VisionCone" };

            var vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 3];
            var uvs = new Vector2[segments + 2];

            // Center vertex
            vertices[0] = new Vector3(0f, heightOffset, 0f);
            uvs[0] = new Vector2(0.5f, 0f);

            float halfAngle = ViewAngle * 0.5f * Mathf.Deg2Rad;
            float angleStep = ViewAngle * Mathf.Deg2Rad / segments;

            for (int i = 0; i <= segments; i++)
            {
                float angle = -halfAngle + angleStep * i;
                float x = Mathf.Sin(angle) * ViewDistance;
                float z = Mathf.Cos(angle) * ViewDistance;

                vertices[i + 1] = new Vector3(x, heightOffset, z);
                uvs[i + 1] = new Vector2((float)i / segments, 1f);
            }

            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            _coneMesh.vertices = vertices;
            _coneMesh.triangles = triangles;
            _coneMesh.uv = uvs;
            _coneMesh.RecalculateNormals();
            _coneMesh.RecalculateBounds();

            _meshFilter.mesh = _coneMesh;
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
            _meshRenderer.material = _material;
        }

        private void UpdateDetection()
        {
            float timeToDetect = _settings?.TimeToDetect ?? 2f;
            float timeToLose = _settings?.TimeToLoseDetection ?? 1.5f;
            LayerMask targetLayer = _settings?.TargetLayer ?? ~0;
            LayerMask obstacleLayer = _settings?.ObstacleLayer ?? 0;

            Transform visibleTarget = FindVisibleTarget(targetLayer, obstacleLayer);

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

        private Transform FindVisibleTarget(LayerMask targetLayer, LayerMask obstacleLayer)
        {
            var colliders = Physics.OverlapSphere(transform.position, ViewDistance, targetLayer);

            foreach (var col in colliders)
            {
                Vector3 directionToTarget = (col.transform.position - transform.position).normalized;
                float angle = Vector3.Angle(transform.forward, directionToTarget);

                if (angle > ViewAngle * 0.5f)
                {
                    continue;
                }

                float distanceToTarget = Vector3.Distance(transform.position, col.transform.position);

                if (!Physics.Raycast(transform.position, directionToTarget, distanceToTarget, obstacleLayer))
                {
                    return col.transform;
                }
            }

            return null;
        }

        private void UpdateVisual(float progress)
        {
            if (_material == null)
            {
                return;
            }

            Color idleColor = _settings?.IdleColor ?? new Color(0f, 1f, 0f, 0.3f);
            Color detectingColor = _settings?.DetectingColor ?? new Color(1f, 1f, 0f, 0.5f);
            Color alertColor = _settings?.AlertColor ?? new Color(1f, 0f, 0f, 0.6f);

            Color currentColor;
            if (progress < 0.5f)
            {
                currentColor = Color.Lerp(idleColor, detectingColor, progress * 2f);
            }
            else
            {
                currentColor = Color.Lerp(detectingColor, alertColor, (progress - 0.5f) * 2f);
            }

            _material.SetColor(ColorProperty, currentColor);
            _material.SetFloat(FillProperty, progress);
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
            Gizmos.DrawLine(transform.position + leftDir * distance, transform.position + rightDir * distance);
        }
    }
}
