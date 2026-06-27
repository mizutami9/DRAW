using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Button))]
    public sealed class LanguageButtonCommand : MonoBehaviour
    {
        [SerializeField] private LocalizationManager.Language language;

        private void Awake()
        {
            Button button = GetComponent<Button>();
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(SelectLanguage);
        }

        private void SelectLanguage()
        {
            LocalizationManager.SetLanguage(language);
            EventSystem.current?.SetSelectedGameObject(null);
        }
    }
}
