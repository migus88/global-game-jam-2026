using System;
using Cysharp.Threading.Tasks;
using Game.Conversation.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

            SelectFirstAnswerAsync().Forget();
        }

        private async UniTaskVoid SelectFirstAnswerAsync()
        {
            // Wait for end of frame for UI layout to complete
            await UniTask.WaitForEndOfFrame();

            if (_answerButtons.Length == 0 || !_answerButtons[0].gameObject.activeSelf)
            {
                Debug.LogWarning("[ConversationUI] No answer buttons available to select");
                return;
            }

            var button = _answerButtons[0];
            var eventSystem = EventSystem.current;

            if (eventSystem == null)
            {
                Debug.LogError("[ConversationUI] No EventSystem found!");
                return;
            }

            // Clear current selection first
            eventSystem.SetSelectedGameObject(null);

            // Wait another frame
            await UniTask.Yield();

            // Now select the button
            eventSystem.SetSelectedGameObject(button.gameObject);
            button.Select();

            Debug.Log($"[ConversationUI] Selected button: {button.name}, Current: {eventSystem.currentSelectedGameObject?.name}");
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
