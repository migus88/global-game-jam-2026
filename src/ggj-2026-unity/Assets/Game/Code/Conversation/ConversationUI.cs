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

        [SerializeField, Header("Question")]
        private TextMeshProUGUI _questionText;

        [SerializeField, Header("Answers")]
        private GameObject _answersContainer;

        [SerializeField]
        private Button[] _answerButtons;

        [SerializeField]
        private TextMeshProUGUI[] _answerTexts;

        [SerializeField, Header("Response")]
        private GameObject _responseContainer;

        [SerializeField]
        private TextMeshProUGUI _responseText;

        public event Action<int> AnswerSelected;

        private void Awake()
        {
            SetupButtons();
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

        public void ShowQuestion(ConversationQuestion question)
        {
            _container?.SetActive(true);
            _responseContainer?.SetActive(false);
            _answersContainer?.SetActive(true);

            if (_questionText != null)
            {
                _questionText.text = question.Text;
            }

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

        public void ShowResponse(ConversationResponse response)
        {
            _answersContainer?.SetActive(false);
            _responseContainer?.SetActive(true);

            if (_responseText != null)
            {
                _responseText.text = response.Text;
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
