using System;
using UnityEngine;

namespace Game.Conversation.Data
{
    [Serializable]
    public class ConversationAnswer
    {
        [field: SerializeField]
        public string Text { get; private set; }

        [field: SerializeField]
        public bool IsCorrect { get; private set; }
    }
}
