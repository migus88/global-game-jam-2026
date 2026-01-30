using System;
using UnityEngine;

namespace Game.Configuration
{
    [CreateAssetMenu(fileName = "GameConfiguration", menuName = "Game/Game Configuration")]
    public class GameConfiguration : ScriptableObject
    {
        [field: SerializeField]
        public VisionConeSettings VisionCone { get; private set; } = new();

        [field: SerializeField]
        public ObservationSettings Observation { get; private set; } = new();
    }

    [Serializable]
    public class ObservationSettings
    {
        [field: SerializeField, Range(30f, 180f), Tooltip("Total angle to scan left and right")]
        public float ScanAngle { get; private set; } = 120f;

        [field: SerializeField, Tooltip("Speed of the scanning rotation in degrees per second")]
        public float ScanSpeed { get; private set; } = 90f;

        [field: SerializeField, Tooltip("Pause duration at each end of the scan")]
        public float PauseAtEnds { get; private set; } = 0.3f;
    }

    [Serializable]
    public class VisionConeSettings
    {
        [Header("Detection")]
        [field: SerializeField, Tooltip("How far the enemy can see")]
        public float ViewDistance { get; private set; } = 10f;

        [field: SerializeField, Range(10f, 180f), Tooltip("Field of view angle")]
        public float ViewAngle { get; private set; } = 60f;

        [field: SerializeField, Tooltip("Layer mask for obstacles that block vision")]
        public LayerMask ObstacleLayer { get; private set; }

        [field: SerializeField, Tooltip("Layer mask for detection targets")]
        public LayerMask TargetLayer { get; private set; }

        [Header("Detection Timing")]
        [field: SerializeField, Tooltip("Seconds to fill detection meter")]
        public float TimeToDetect { get; private set; } = 2f;

        [field: SerializeField, Tooltip("Seconds to lose detection when out of sight")]
        public float TimeToLoseDetection { get; private set; } = 1.5f;

        [Header("Visual")]
        [field: SerializeField]
        public Color IdleColor { get; private set; } = new Color(0f, 1f, 0f, 0.3f);

        [field: SerializeField]
        public Color DetectingColor { get; private set; } = new Color(1f, 1f, 0f, 0.5f);

        [field: SerializeField]
        public Color AlertColor { get; private set; } = new Color(1f, 0f, 0f, 0.6f);

        [field: SerializeField, Tooltip("Height offset for the cone mesh")]
        public float ConeHeightOffset { get; private set; } = 0.05f;

        [field: SerializeField, Range(8, 64), Tooltip("Number of segments for cone mesh")]
        public int ConeSegments { get; private set; } = 24;
    }
}
