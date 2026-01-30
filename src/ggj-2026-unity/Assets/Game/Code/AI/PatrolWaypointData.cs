using System;
using UnityEngine;

namespace Game.AI
{
    [Serializable]
    public class PatrolWaypointData
    {
        [field: SerializeField]
        public Vector3 Position { get; set; }

        [field: SerializeField, Min(0f)]
        public float WaitDelay { get; set; }

        [field: SerializeField]
        public bool IsObservation { get; set; }

        [field: SerializeField]
        public string AnimatorParameterName { get; set; } = string.Empty;

        [field: SerializeField]
        public bool AnimatorParameterValue { get; set; }

        public PatrolWaypointData()
        {
        }

        public PatrolWaypointData(Vector3 position)
        {
            Position = position;
        }

        public PatrolWaypointData(Vector3 position, float waitDelay)
        {
            Position = position;
            WaitDelay = waitDelay;
        }
    }
}
