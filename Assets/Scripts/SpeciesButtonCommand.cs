using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Button))]
    public sealed class SpeciesButtonCommand : MonoBehaviour
    {
        [SerializeField] private DrawManager drawManager;
        [SerializeField] private DrawManager.Species species;

        private void Awake()
        {
            if (drawManager == null)
            {
                drawManager = FindObjectOfType<DrawManager>();
            }

            Button button = GetComponent<Button>();
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(SelectSpecies);
        }

        private void SelectSpecies()
        {
            drawManager?.SetSpecies(species);
            EventSystem.current?.SetSelectedGameObject(null);
        }
    }
}
