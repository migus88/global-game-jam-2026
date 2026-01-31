using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.Scenes
{
    [CreateAssetMenu(fileName = "SceneConfiguration", menuName = "Game/Scene Configuration")]
    public class SceneConfiguration : ScriptableObject
    {
        [Header("Scene References")]
        [field: SerializeField, Tooltip("Reference to the main menu scene")]
        public AssetReference MainMenuScene { get; private set; }

        [field: SerializeField, Tooltip("Reference to the loading scene")]
        public AssetReference LoadingScene { get; private set; }

        [field: SerializeField, Tooltip("Reference to the game over scene")]
        public AssetReference GameOverScene { get; private set; }

        [field: SerializeField, Tooltip("Reference to the win scene")]
        public AssetReference WinScene { get; private set; }

        [Header("Game Scenes")]
        [field: SerializeField, Tooltip("Addressable label for game level scenes")]
        public string GameSceneLabel { get; private set; } = "Level";

        [Header("Game Over Settings")]
        [field: SerializeField, Range(0.5f, 10f), Tooltip("Delay before showing hint text after game over")]
        public float GameOverHintDelay { get; private set; } = 3f;

        [Header("Music Settings")]
        [field: SerializeField, Range(0f, 1f), Tooltip("Music volume during main menu")]
        public float MainMenuMusicVolume { get; private set; } = 0.3f;

        [field: SerializeField, Range(0f, 1f), Tooltip("Music volume during gameplay")]
        public float GameplayMusicVolume { get; private set; } = 0.6f;

        [field: SerializeField, Range(0.1f, 3f), Tooltip("Duration of volume transition")]
        public float MusicTransitionDuration { get; private set; } = 1f;
    }
}
