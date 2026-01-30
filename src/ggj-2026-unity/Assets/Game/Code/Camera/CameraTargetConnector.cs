using Game.Player;
using Unity.Cinemachine;
using UnityEngine;
using VContainer;

namespace Game.Camera
{
    public class CameraTargetConnector : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera _cinemachineCamera;

        [Inject]
        public void Construct(PlayerCameraTarget playerCameraTarget)
        {
            if (_cinemachineCamera != null && playerCameraTarget != null)
            {
                _cinemachineCamera.Follow = playerCameraTarget.CameraTarget;
                _cinemachineCamera.LookAt = playerCameraTarget.CameraTarget;
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
