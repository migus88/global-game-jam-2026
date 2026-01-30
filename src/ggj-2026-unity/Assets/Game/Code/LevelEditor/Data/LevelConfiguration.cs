using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.LevelEditor.Data
{
    [CreateAssetMenu(fileName = "LevelConfiguration", menuName = "Game/Level Configuration")]
    public class LevelConfiguration : ScriptableObject
    {
        [field: SerializeField, Header("Player")]
        public AssetReferenceGameObject PlayerPrefab { get; private set; }

        [field: SerializeField, Header("Walls")]
        public AssetReferenceGameObject WallPrefab { get; private set; }

        [field: SerializeField]
        public Vector3 WallSize { get; private set; } = new(1f, 2f, 1f);
    }
}
