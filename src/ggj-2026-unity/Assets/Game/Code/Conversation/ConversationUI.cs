using System;
using Game.Conversation.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Conversation
{
    public class ConversationUI : MonoBehaviour
    {
        [SerializeField, Header("Container")]
        private GameObject _container;

        [SerializeField, Header("Text")]
        private TextMeshProUGUI _dialogueText;

        [SerializeField, Header("Answers")]
        private GameObject _answersContainer;

        [SerializeField]
        private Button[] _answerButtons;

        [SerializeField]
        private TextMeshProUGUI[] _answerTexts;

        public event Action<int> AnswerSelected;

        private void Awake()
        {
            SetupButtons();
            Hide();
        }

        private void SetupButtons()
        {
            for (int i = 0; i < _answerButtons.Length; i++)
            {
                int index = i;
                _answerButtons[i].onClick.AddListener(() => OnAnswerClicked(index));
            }
        }

        private void OnDestroy()
        {
            foreach (var button in _answerButtons)
            {
                button.onClick.RemoveAllListeners();
            }
        }

        public void ShowQuestionText(string text)
        {
            _container?.SetActive(true);
            _answersContainer?.SetActive(false);

            if (_dialogueText != null)
            {
                _dialogueText.text = text;
            }
        }

        public void ShowAnswers(ConversationQuestion question)
        {
            _answersContainer?.SetActive(true);

            for (int i = 0; i < _answerButtons.Length; i++)
            {
                if (i < question.Answers.Length)
                {
                    _answerButtons[i].gameObject.SetActive(true);

                    if (_answerTexts[i] != null)
                    {
                        _answerTexts[i].text = question.Answers[i].Text;
                    }
                }
                else
                {
                    _answerButtons[i].gameObject.SetActive(false);
                }
            }
        }

        public void ShowQuestion(ConversationQuestion question)
        {
            ShowQuestionText(question.Text);
            ShowAnswers(question);
        }

        public void ShowResponse(ConversationResponse response)
        {
            _answersContainer?.SetActive(false);

            if (_dialogueText != null)
            {
                _dialogueText.text = response.Text;
            }
        }

        public void HideAnswers()
        {
            _answersContainer?.SetActive(false);
        }

        public void Hide()
        {
            _container?.SetActive(false);
        }

        private void OnAnswerClicked(int index)
        {
            AnswerSelected?.Invoke(index);
        }
    }
}
