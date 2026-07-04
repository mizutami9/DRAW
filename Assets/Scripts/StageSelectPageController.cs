using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class StageSelectPageController : MonoBehaviour
    {
        [SerializeField] private GameObject[] pages;
        [SerializeField] private Text pageText;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;

        private int currentPage;

        private void Awake()
        {
            if (previousButton != null)
            {
                previousButton.onClick.AddListener(Previous);
            }

            if (nextButton != null)
            {
                nextButton.onClick.AddListener(Next);
            }

            ShowPage(0);
        }

        public void Previous()
        {
            ShowPage(currentPage - 1);
        }

        public void Next()
        {
            ShowPage(currentPage + 1);
        }

        private void ShowPage(int index)
        {
            if (pages == null || pages.Length == 0)
            {
                return;
            }

            currentPage = Mathf.Clamp(index, 0, pages.Length - 1);
            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] != null)
                {
                    pages[i].SetActive(i == currentPage);
                }
            }

            if (pageText != null)
            {
                pageText.text = $"{currentPage + 1} / {pages.Length}";
            }

            if (previousButton != null)
            {
                previousButton.interactable = currentPage > 0;
            }

            if (nextButton != null)
            {
                nextButton.interactable = currentPage < pages.Length - 1;
            }
        }
    }
}
