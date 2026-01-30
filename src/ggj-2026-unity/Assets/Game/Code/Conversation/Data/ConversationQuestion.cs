using System;
using UnityEngine;

namespace Game.Conversation.Data
{
    [Serializable]
    public class ConversationQuestion
    {
        [field: SerializeField]
        public AudioClip AudioClip { get; private set; }

        [field: SerializeField, TextArea(2, 4)]
        public string Text { get; private set; }

        [field: SerializeField]
        public ConversationAnswer[] Answers { get; private set; }
    }
}
