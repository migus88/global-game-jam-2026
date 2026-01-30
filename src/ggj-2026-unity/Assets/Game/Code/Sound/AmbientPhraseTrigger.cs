using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.Sound
{
    public class AmbientPhraseTrigger : MonoBehaviour
    {
        [SerializeField, Header("Detection")]
        private float _detectionRadius = 5f;

        [SerializeField]
        private LayerMask _detectionLayer;

        [SerializeField, Header("Cooldown")]
        private float _minCooldown = 5f;

        [SerializeField]
        private float _maxCooldown = 15f;

        [SerializeField, Header("Text Display")]
        private GameObject _textDisplayPrefab;

        [SerializeField]
        private Vector3 _textOffset = new(0f, 2f, 0f);

        [SerializeField]
        private float _textDisplayDuration = 3f;

        private SoundManager _soundManager;
        private UnityEngine.Camera _mainCamera;
        private GameObject _currentTextDisplay;
        private TextMeshPro _currentTextMesh;
        private CancellationTokenSource _cancellationTokenSource;

        private bool _isPlaying;
        private bool _isOnCooldown;
        private readonly Collider[] _detectionResults = new Collider[1];

        [Inject]
        public void Construct(SoundManager soundManager)
        {
            _soundManager = soundManager;
        }

        private void Start()
        {
            _mainCamera = UnityEngine.Camera.main;
            _cancellationTokenSource = new CancellationTokenSource();

            if (_textDisplayPrefab == null)
            {
                CreateDefaultTextDisplay();
            }
        }

        private void CreateDefaultTextDisplay()
        {
            _currentTextDisplay = new GameObject("PhraseText");
            _currentTextDisplay.transform.SetParent(transform);
            _currentTextDisplay.transform.localPosition = _textOffset;

            _currentTextMesh = _currentTextDisplay.AddComponent<TextMeshPro>();
            _currentTextMesh.alignment = TextAlignmentOptions.Center;
            _currentTextMesh.fontSize = 3f;
            _currentTextMesh.color = Color.white;
            _currentTextMesh.enableWordWrapping = true;
            _currentTextMesh.rectTransform.sizeDelta = new Vector2(5f, 2f);

            _currentTextDisplay.SetActive(false);
        }

        private void Update()
        {
            if (_isPlaying || _isOnCooldown || _soundManager == null)
            {
                return;
            }

            CheckForDetection();
            UpdateBillboard();
        }

        private void LateUpdate()
        {
            UpdateBillboard();
        }

        private void UpdateBillboard()
        {
            if (_currentTextDisplay == null || !_currentTextDisplay.activeSelf || _mainCamera == null)
            {
                return;
            }

            var directionToCamera = _mainCamera.transform.position - _currentTextDisplay.transform.position;
            directionToCamera.y = 0f;

            if (directionToCamera.sqrMagnitude > 0.001f)
            {
                _currentTextDisplay.transform.rotation = Quaternion.LookRotation(-directionToCamera);
            }
        }

        private void CheckForDetection()
        {
            var count = Physics.OverlapSphereNonAlloc(
                transform.position,
                _detectionRadius,
                _detectionResults,
                _detectionLayer);

            if (count > 0)
            {
                TriggerPhrase().Forget();
            }
        }

        private async UniTaskVoid TriggerPhrase()
        {
            if (_isPlaying || _isOnCooldown)
            {
                return;
            }

            _isPlaying = true;

            var phrase = _soundManager.PlayRandomAmbientPhrase(transform.position);

            if (phrase == null)
            {
                _isPlaying = false;
                return;
            }

            ShowText(phrase.Text);

            var displayTime = phrase.Clip != null
                ? Mathf.Max(phrase.Clip.length, _textDisplayDuration)
                : _textDisplayDuration;

            try
            {
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(displayTime),
                    cancellationToken: _cancellationTokenSource.Token);
            }
            catch (System.OperationCanceledException)
            {
                return;
            }

            HideText();
            _isPlaying = false;

            StartCooldown().Forget();
        }

        private async UniTaskVoid StartCooldown()
        {
            _isOnCooldown = true;

            var cooldownTime = Random.Range(_minCooldown, _maxCooldown);

            try
            {
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(cooldownTime),
                    cancellationToken: _cancellationTokenSource.Token);
            }
            catch (System.OperationCanceledException)
            {
                return;
            }

            _isOnCooldown = false;
        }

        private void ShowText(string text)
        {
            if (_textDisplayPrefab != null && _currentTextDisplay == null)
            {
                _currentTextDisplay = Instantiate(_textDisplayPrefab, transform);
                _currentTextDisplay.transform.localPosition = _textOffset;
                _currentTextMesh = _currentTextDisplay.GetComponent<TextMeshPro>();
            }

            if (_currentTextDisplay == null)
            {
                return;
            }

            if (_currentTextMesh != null)
            {
                _currentTextMesh.text = text;
            }

            _currentTextDisplay.SetActive(true);
        }

        private void HideText()
        {
            if (_currentTextDisplay != null)
            {
                _currentTextDisplay.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();

            if (_currentTextDisplay != null && _textDisplayPrefab == null)
            {
                Destroy(_currentTextDisplay);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawSphere(transform.position, _detectionRadius);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, _detectionRadius);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + _textOffset);
            Gizmos.DrawWireCube(transform.position + _textOffset, new Vector3(1f, 0.5f, 0.1f));
        }
    }
}
