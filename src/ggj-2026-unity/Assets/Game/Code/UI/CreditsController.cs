using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    public class CreditsController : MonoBehaviour
    {
        [SerializeField]
        private GameObject _container;

        [SerializeField]
        private Selectable _closeButton;

        private void Start()
        {
            Hide();
        }

        public void Show()
        {
            _container?.SetActive(true);

            if (_closeButton != null)
            {
                EventSystem.current?.SetSelectedGameObject(_closeButton.gameObject);
            }
        }

        public void Hide()
        {
            _container?.SetActive(false);
        }

        public void OnCloseButtonClicked()
        {
            Hide();
        }
    }
}
