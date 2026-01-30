using System;
using UnityEngine;

namespace Game.Sound
{
    [Serializable]
    public class SoundEffectEntry
    {
        [field: SerializeField]
        public SoundEffectType Type { get; private set; }

        [field: SerializeField]
        public AudioClip[] Clips { get; private set; }
    }
}
