using UnityEngine;

namespace Game.Player
{
    public class PlayerCameraTarget : MonoBehaviour
    {
        [field: SerializeField]
        public Transform CameraTarget { get; private set; }

        private void Reset()
        {
            // Default to this transform if not set
            if (CameraTarget == null)
            {
                CameraTarget = transform;
            }
        }
    }
}
