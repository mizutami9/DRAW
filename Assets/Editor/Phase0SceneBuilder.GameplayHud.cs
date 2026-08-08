using DrawBody.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.EditorTools
{
    public static partial class Phase0SceneBuilder
    {
        private static GameObject CreateGameplayHud(Transform parent, Font font, DrawManager drawManager, StageManager stageManager)
        {
            GameObject hud = new GameObject("GameplayHud");
            hud.transform.SetParent(parent, false);
            RectTransform hudRect = hud.AddComponent<RectTransform>();
            Stretch(hudRect);

            GameObject hintRoot = new GameObject("GameplayKeyHints");
            hintRoot.transform.SetParent(hud.transform, false);
            RectTransform hintRect = hintRoot.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(0f, 0f);
            hintRect.pivot = new Vector2(0f, 0f);
            hintRect.anchoredPosition = new Vector2(12f, 10f);
            hintRect.sizeDelta = new Vector2(260f, 76f);

            Button tabButton = CreateKeyboardHintButton(
                "GameplayDrawerTabButton",
                hintRoot.transform,
                font,
                "TAB",
                "DROW MENU",
                new Vector2(0f, 40f),
                true);

            Button escButton = CreateKeyboardHintButton(
                "GameplayEscHintButton",
                hintRoot.transform,
                font,
                "ESC",
                "MENU",
                new Vector2(0f, 4f),
                true);

            GameObject drawer = CreatePanel("GameplayActionDrawer", hud.transform, new Color(0.96f, 0.93f, 0.86f, 0.9f));
            AddUiOutline(drawer, new Color(0.12f, 0.11f, 0.1f, 0.75f), new Vector2(2f, -2f));
            RectTransform drawerRect = drawer.GetComponent<RectTransform>();
            drawerRect.anchorMin = new Vector2(0f, 0f);
            drawerRect.anchorMax = new Vector2(0f, 0f);
            drawerRect.pivot = new Vector2(0f, 0f);
            drawerRect.anchoredPosition = new Vector2(-272f, 86f);
            drawerRect.sizeDelta = new Vector2(250f, 440f);

            GameObject content = new GameObject("GameplayActionDrawerContent");
            content.transform.SetParent(drawer.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            Stretch(contentRect);

            Text speciesTitle = CreateText("GameplaySpeciesTitle", content.transform, font, 17, TextAnchor.MiddleLeft);
            speciesTitle.text = string.Empty;
            speciesTitle.color = new Color(0.2f, 0.16f, 0.1f);
            speciesTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            speciesTitle.rectTransform.anchorMax = new Vector2(0f, 1f);
            speciesTitle.rectTransform.pivot = new Vector2(0f, 0.5f);
            speciesTitle.rectTransform.anchoredPosition = new Vector2(118f, -24f);
            speciesTitle.rectTransform.sizeDelta = new Vector2(220f, 24f);
            speciesTitle.gameObject.SetActive(false);

            DrawManager.Species[] species =
            {
                DrawManager.Species.Human,
                DrawManager.Species.Cat,
                DrawManager.Species.Bird,
                DrawManager.Species.Snake,
                DrawManager.Species.Slime
            };

            for (int i = 0; i < species.Length; i++)
            {
                Button button = CreateSpeciesIconButton(
                    $"{species[i]}GameplaySpeciesButton",
                    content.transform,
                    font,
                    species[i],
                    new Vector2(-50f + (i % 2) * 100f, 356f - (i / 2) * 66f),
                    new Vector2(82f, 62f),
                    new Color(0.98f, 0.95f, 0.82f, 0.96f));
                AddUiOutline(button.gameObject, new Color(0.2f, 0.14f, 0.08f, 0.58f), new Vector2(2f, -2f));
                ScaleSpeciesIcon(button.transform, 1.18f);

                Text label = CreateText($"{species[i]}GameplaySpeciesLabel", content.transform, font, 14, TextAnchor.MiddleCenter);
                label.text = string.Empty;
                label.color = Color.black;
                label.rectTransform.anchorMin = new Vector2(0f, 0f);
                label.rectTransform.anchorMax = new Vector2(0f, 0f);
                label.rectTransform.pivot = new Vector2(0.5f, 0f);
                label.rectTransform.anchoredPosition = new Vector2(0f, 86f);
                label.rectTransform.sizeDelta = new Vector2(70f, 22f);
                label.gameObject.SetActive(false);

                SpeciesButtonCommand command = button.gameObject.AddComponent<SpeciesButtonCommand>();
                AssignObject(command, "drawManager", drawManager);
                AssignEnum(command, "species", (int)species[i]);
            }

            Button addCharacter = CreateButton("GameplayAddCharacterButton", content.transform, font, "+ " + LocalizationManager.T("character_add"), new Vector2(0f, 164f), new Vector2(190f, 40f), new Color(0.78f, 0.9f, 1f, 0.92f));
            Button switchCharacter = CreateButton("GameplaySwitchCharacterButton", content.transform, font, "\u21c4 " + LocalizationManager.T("character_control_switch"), new Vector2(0f, 116f), new Vector2(190f, 40f), new Color(0.75f, 0.95f, 0.75f, 0.92f));
            Button deleteCharacter = CreateButton("GameplayDeleteCharacterButton", content.transform, font, "- " + LocalizationManager.T("character_delete"), new Vector2(0f, 68f), new Vector2(190f, 40f), new Color(0.98f, 0.78f, 0.72f, 0.92f));
            Button redraw = CreateButton("GameplayRedrawButton", content.transform, font, "\u270e " + LocalizationManager.T("redraw"), new Vector2(0f, 14f), new Vector2(190f, 46f), new Color(0.98f, 0.91f, 0.66f, 0.96f));

            Button[] actionButtons = { addCharacter, switchCharacter, deleteCharacter, redraw };
            for (int i = 0; i < actionButtons.Length; i++)
            {
                SetButtonLabelColor(actionButtons[i], Color.black);
                SetButtonLabelFontSize(actionButtons[i], 18);
                Text text = actionButtons[i].GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.fontStyle = FontStyle.Bold;
                }

                AddUiOutline(actionButtons[i].gameObject, new Color(0.22f, 0.15f, 0.08f, 0.62f), new Vector2(2f, -2f));
            }
            SetButtonLabelFontSize(redraw, 20);
            AddSketchFrame(redraw.transform, new Vector2(190f, 46f), new Color(0.42f, 0.28f, 0.12f, 0.65f), 2f);

            AddGameplayCommand(addCharacter.gameObject, stageManager, GameplayButtonCommand.Command.AddCharacter);
            AddGameplayCommand(switchCharacter.gameObject, stageManager, GameplayButtonCommand.Command.SwitchCharacter);
            AddGameplayCommand(deleteCharacter.gameObject, stageManager, GameplayButtonCommand.Command.DeleteCharacter);
            AddGameplayCommand(redraw.gameObject, stageManager, GameplayButtonCommand.Command.Redraw);

            GameplayHudDrawer drawerController = drawer.AddComponent<GameplayHudDrawer>();
            AssignObject(drawerController, "drawer", drawerRect);
            AssignObject(drawerController, "contentRoot", content);
            AssignObject(drawerController, "tabButton", tabButton);
            AssignObject(drawerController, "escButton", escButton);
            AssignVector2(drawerController, "openPosition", new Vector2(12f, 86f));
            AssignVector2(drawerController, "closedPosition", new Vector2(-272f, 86f));

            return hud;
        }

        private static void ScaleSpeciesIcon(Transform button, float scale)
        {
            for (int i = 0; i < button.childCount; i++)
            {
                Transform child = button.GetChild(i);
                if (child.name == "IconLine" || child.name == "IconDot")
                {
                    child.localScale = Vector3.one * scale;
                }
            }
        }

        private static Button CreateKeyboardHintButton(string name, Transform parent, Font font, string keyText, string labelText, Vector2 anchoredPosition, bool clickable)
        {
            GameObject root = new GameObject(name + "Hint");
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(0f, 0f);
            rootRect.pivot = new Vector2(0f, 0f);
            rootRect.anchoredPosition = anchoredPosition;
            rootRect.sizeDelta = new Vector2(250f, 32f);

            GameObject keyObject = CreatePanel(name, root.transform, new Color(0.18f, 0.17f, 0.15f, 0.96f));
            RectTransform keyRect = keyObject.GetComponent<RectTransform>();
            keyRect.anchorMin = new Vector2(0f, 0f);
            keyRect.anchorMax = new Vector2(0f, 0f);
            keyRect.pivot = new Vector2(0f, 0f);
            keyRect.anchoredPosition = Vector2.zero;
            keyRect.sizeDelta = new Vector2(68f, 30f);
            AddSketchFrame(keyObject.transform, new Vector2(68f, 30f), Color.white, 1.4f);

            Button keyButton = null;
            if (clickable)
            {
                keyButton = keyObject.AddComponent<Button>();
                Navigation navigation = keyButton.navigation;
                navigation.mode = Navigation.Mode.None;
                keyButton.navigation = navigation;
                ColorBlock colors = keyButton.colors;
                colors.normalColor = new Color(0.18f, 0.17f, 0.15f, 0.96f);
                colors.highlightedColor = new Color(0.28f, 0.26f, 0.22f, 0.98f);
                colors.pressedColor = new Color(0.08f, 0.075f, 0.065f, 0.98f);
                colors.selectedColor = colors.highlightedColor;
                keyButton.colors = colors;
            }

            Text keyLabel = CreateText("Label", keyObject.transform, font, 19, TextAnchor.MiddleCenter);
            keyLabel.text = keyText;
            keyLabel.color = Color.white;
            keyLabel.fontStyle = FontStyle.Bold;
            Stretch(keyLabel.rectTransform);

            Text label = CreateText(name + "Text", root.transform, font, 19, TextAnchor.MiddleLeft);
            label.text = labelText;
            label.color = new Color(0.12f, 0.1f, 0.08f, 0.88f);
            label.fontStyle = FontStyle.Bold;
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(0f, 0f);
            label.rectTransform.pivot = new Vector2(0f, 0f);
            label.rectTransform.anchoredPosition = new Vector2(80f, 2f);
            label.rectTransform.sizeDelta = new Vector2(164f, 28f);
            return keyButton;
        }
    }
}
