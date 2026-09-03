using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Button))]
    public sealed class LanguageButtonCommand : MonoBehaviour
    {
        [SerializeField] private string languageCode = "ja";
        [SerializeField, HideInInspector] private LocalizationManager.Language legacyLanguage;

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
            if (!LocalizationManager.SetLanguage(languageCode))
            {
                LocalizationManager.SetLanguage(legacyLanguage);
            }
            EventSystem.current?.SetSelectedGameObject(null);
        }
    }
}
