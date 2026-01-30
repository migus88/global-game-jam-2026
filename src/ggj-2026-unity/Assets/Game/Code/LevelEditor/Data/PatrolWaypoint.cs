using System;
using UnityEngine;

namespace Game.LevelEditor.Data
{
    [Serializable]
    public class PatrolWaypoint
    {
        [field: SerializeField]
        public Vector2Int GridPosition { get; set; }

        [field: SerializeField, Min(0f)]
        public float WaitDelay { get; set; }

        [field: SerializeField]
        public bool IsObservation { get; set; }

        [field: SerializeField]
        public string AnimatorParameterName { get; set; } = string.Empty;

        [field: SerializeField]
        public bool AnimatorParameterValue { get; set; }

        public PatrolWaypoint()
        {
        }

        public PatrolWaypoint(Vector2Int gridPosition)
        {
            GridPosition = gridPosition;
        }

        public PatrolWaypoint(Vector2Int gridPosition, float waitDelay)
        {
            GridPosition = gridPosition;
            WaitDelay = waitDelay;
        }
    }
}
