using UnityEngine;

namespace Game.Hiding
{
    [CreateAssetMenu(fileName = "HideConfiguration", menuName = "Game/Hide Configuration")]
    public class HideConfiguration : ScriptableObject
    {
        [field: SerializeField, Header("Effects")]
        public GameObject HideEffectPrefab { get; private set; }

        [field: SerializeField]
        public float EffectDuration { get; private set; } = 1f;

        [field: SerializeField]
        public int EffectPoolSize { get; private set; } = 5;

        [field: SerializeField, Header("Cooldown")]
        public float HideCooldown { get; private set; } = 0.5f;

        [field: SerializeField, Header("Hidden Duration Sound")]
        public float HiddenDurationThreshold { get; private set; } = 5f;

        [field: SerializeField]
        public string HiddenDurationSoundName { get; private set; } = "HiddenLong";
    }
}
