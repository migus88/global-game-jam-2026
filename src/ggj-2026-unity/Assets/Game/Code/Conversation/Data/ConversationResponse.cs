using System;
using UnityEngine;

namespace Game.Conversation.Data
{
    [Serializable]
    public class ConversationResponse
    {
        [field: SerializeField]
        public AudioClip AudioClip { get; private set; }

        [field: SerializeField, TextArea(2, 4)]
        public string Text { get; private set; }
    }
}
