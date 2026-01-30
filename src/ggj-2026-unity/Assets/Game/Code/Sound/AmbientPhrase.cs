using System;
using UnityEngine;

namespace Game.Sound
{
    [Serializable]
    public class AmbientPhrase
    {
        [field: SerializeField]
        public AudioClip Clip { get; private set; }

        [field: SerializeField, TextArea(2, 4)]
        public string Text { get; private set; }
    }
}
