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

        [Header("Game Scenes")]
        [field: SerializeField, Tooltip("Addressable label for game level scenes")]
        public string GameSceneLabel { get; private set; } = "Level";

        [Header("Game Over Settings")]
        [field: SerializeField, Range(0.5f, 10f), Tooltip("Delay before showing hint text after game over")]
        public float GameOverHintDelay { get; private set; } = 3f;
    }
}
