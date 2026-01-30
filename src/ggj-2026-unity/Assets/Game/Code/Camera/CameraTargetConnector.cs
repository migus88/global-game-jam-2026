using Game.Player;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Camera
{
    public class CameraTargetConnector : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera _cinemachineCamera;
        [SerializeField] private bool _autoFindPlayer = true;

        private void Start()
        {
            // If camera already has a target, don't override
            if (_cinemachineCamera != null && _cinemachineCamera.Follow != null)
            {
                return;
            }

            if (_autoFindPlayer)
            {
                var playerTarget = FindFirstObjectByType<PlayerCameraTarget>();
                if (playerTarget != null)
                {
                    SetTarget(playerTarget.CameraTarget);
                }
            }
        }

        public void SetTarget(Transform target)
        {
            if (_cinemachineCamera != null)
            {
                _cinemachineCamera.Follow = target;
                _cinemachineCamera.LookAt = target;
            }
        }

        private void Reset()
        {
            _cinemachineCamera = GetComponent<CinemachineCamera>();
        }
    }
}
