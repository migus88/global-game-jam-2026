using System;
using UnityEngine;

namespace Game.Sound
{
    [Serializable]
    public class NamedSoundEffectEntry
    {
        [field: SerializeField]
        public string Name { get; private set; }

        [field: SerializeField]
        public AudioClip[] Clips { get; private set; }
    }
}
