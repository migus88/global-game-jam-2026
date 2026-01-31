using System;
using Cysharp.Threading.Tasks;
using Game.Conversation.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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

        [SerializeField, Header("Input")]
        private InputActionReference _navigateAction;

        private int _activeAnswerCount;
        private bool _answersEnabled;

        public event Action<int> AnswerSelected;

        private void Awake()
        {
            SetupButtons();
            Hide();
        }

        private void OnEnable()
        {
            if (_navigateAction != null && _navigateAction.action != null)
            {
                _navigateAction.action.performed += OnNavigatePerformed;
                _navigateAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (_navigateAction != null && _navigateAction.action != null)
            {
                _navigateAction.action.performed -= OnNavigatePerformed;
            }
        }

        private void OnNavigatePerformed(InputAction.CallbackContext context)
        {
            if (!_answersEnabled || _activeAnswerCount == 0)
            {
                return;
            }

            var eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.currentSelectedGameObject != null)
            {
                return;
            }

            // Re-select first interactable button when controller navigates with nothing selected
            for (int i = 0; i < _activeAnswerCount; i++)
            {
                if (_answerButtons[i].interactable)
                {
                    eventSystem.SetSelectedGameObject(_answerButtons[i].gameObject);
                    break;
                }
            }
        }

        private void SetupButtons()
        {
            for (int i = 0; i < _answerButtons.Length; i++)
            {
                int index = i;
                _answerButtons[i].onClick.AddListener(() => OnAnswerClicked(index));

                var trigger = _answerButtons[i].gameObject.AddComponent<EventTrigger>();
                var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener(_ => OnPointerEnterButton());
                trigger.triggers.Add(entry);
            }
        }

        private void OnPointerEnterButton()
        {
            EventSystem.current?.SetSelectedGameObject(null);
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
            _answersContainer?.SetActive(true);

            if (_dialogueText != null)
            {
                _dialogueText.text = question.Text;
            }

            _activeAnswerCount = Mathf.Min(question.Answers.Length, _answerButtons.Length);

            for (int i = 0; i < _answerButtons.Length; i++)
            {
                if (i < question.Answers.Length)
                {
                    _answerButtons[i].gameObject.SetActive(true);
                    _answerButtons[i].interactable = false;

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

        public void EnableAnswers()
        {
            _answersEnabled = true;

            for (int i = 0; i < _activeAnswerCount; i++)
            {
                _answerButtons[i].interactable = true;
            }

            SelectFirstAnswerAsync().Forget();
        }

        private async UniTaskVoid SelectFirstAnswerAsync()
        {
            await UniTask.WaitForEndOfFrame();

            if (_answerButtons.Length == 0 || !_answerButtons[0].gameObject.activeSelf || !_answerButtons[0].interactable)
            {
                return;
            }

            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            eventSystem.SetSelectedGameObject(_answerButtons[0].gameObject);
        }

        public void ShowSelectedAnswerOnly(int selectedIndex)
        {
            _answersEnabled = false;

            for (int i = 0; i < _answerButtons.Length; i++)
            {
                if (i == selectedIndex)
                {
                    _answerButtons[i].interactable = false;
                }
                else
                {
                    _answerButtons[i].gameObject.SetActive(false);
                }
            }

            EventSystem.current?.SetSelectedGameObject(null);
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
            _answersEnabled = false;
            _container?.SetActive(false);
        }

        private void OnAnswerClicked(int index)
        {
            AnswerSelected?.Invoke(index);
        }
    }
}
