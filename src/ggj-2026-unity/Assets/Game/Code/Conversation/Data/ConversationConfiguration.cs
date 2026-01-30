using UnityEngine;

namespace Game.Conversation.Data
{
    [CreateAssetMenu(fileName = "ConversationConfiguration", menuName = "Game/Conversation Configuration")]
    public class ConversationConfiguration : ScriptableObject
    {
        [field: SerializeField, Header("Questions")]
        public ConversationQuestion[] Questions { get; private set; }

        [field: SerializeField, Header("Correct Responses")]
        public ConversationResponse[] CorrectResponses { get; private set; }

        [field: SerializeField, Header("Incorrect Responses")]
        public ConversationResponse[] IncorrectResponses { get; private set; }

        [field: SerializeField, Header("Timing")]
        public float DelayBeforeQuestion { get; private set; } = 0.5f;

        [field: SerializeField]
        public float DelayAfterResponse { get; private set; } = 1f;

        public ConversationQuestion GetRandomQuestion()
        {
            if (Questions == null || Questions.Length == 0)
            {
                return null;
            }

            return Questions[Random.Range(0, Questions.Length)];
        }

        public ConversationResponse GetRandomCorrectResponse()
        {
            if (CorrectResponses == null || CorrectResponses.Length == 0)
            {
                return null;
            }

            return CorrectResponses[Random.Range(0, CorrectResponses.Length)];
        }

        public ConversationResponse GetRandomIncorrectResponse()
        {
            if (IncorrectResponses == null || IncorrectResponses.Length == 0)
            {
                return null;
            }

            return IncorrectResponses[Random.Range(0, IncorrectResponses.Length)];
        }
    }
}
