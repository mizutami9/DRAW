using DrawBody.Prototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.EditorTools
{
    public static partial class Phase0SceneBuilder
    {
        private static Dropdown CreateStageObjectDropdown(string name, Transform parent, Font font, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject dropdownObject = CreatePanel(name, parent, new Color(0.98f, 0.96f, 0.9f, 0.96f));
            RectTransform rect = dropdownObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            AddUiOutline(dropdownObject, new Color(0.12f, 0.11f, 0.1f, 0.65f), new Vector2(1.5f, -1.5f));

            Dropdown dropdown = dropdownObject.AddComponent<Dropdown>();
            Navigation navigation = dropdown.navigation;
            navigation.mode = Navigation.Mode.None;
            dropdown.navigation = navigation;
            dropdown.targetGraphic = dropdownObject.GetComponent<Image>();

            Text caption = CreateText("Label", dropdownObject.transform, font, 18, TextAnchor.MiddleLeft);
            caption.color = Color.black;
            caption.rectTransform.anchorMin = Vector2.zero;
            caption.rectTransform.anchorMax = Vector2.one;
            caption.rectTransform.offsetMin = new Vector2(12f, 0f);
            caption.rectTransform.offsetMax = new Vector2(-42f, 0f);
            dropdown.captionText = caption;

            Text arrow = CreateText("Arrow", dropdownObject.transform, font, 18, TextAnchor.MiddleCenter);
            arrow.text = "\u25be";
            arrow.color = Color.black;
            arrow.rectTransform.anchorMin = new Vector2(1f, 0f);
            arrow.rectTransform.anchorMax = new Vector2(1f, 1f);
            arrow.rectTransform.pivot = new Vector2(1f, 0.5f);
            arrow.rectTransform.anchoredPosition = new Vector2(-16f, 0f);
            arrow.rectTransform.sizeDelta = new Vector2(28f, 0f);

            GameObject template = CreatePanel("Template", dropdownObject.transform, new Color(0.96f, 0.93f, 0.86f, 0.98f));
            RectTransform templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, -2f);
            templateRect.sizeDelta = new Vector2(0f, 190f);
            AddUiOutline(template, new Color(0.12f, 0.11f, 0.1f, 0.65f), new Vector2(1.5f, -1.5f));

            GameObject viewport = CreatePanel("Viewport", template.transform, new Color(1f, 1f, 1f, 0.01f));
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect);
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 190f);

            ScrollRect scrollRect = template.AddComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            GameObject item = CreatePanel("Item", content.transform, new Color(0.98f, 0.96f, 0.9f, 0.98f));
            RectTransform itemRect = item.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 1f);
            itemRect.anchorMax = new Vector2(1f, 1f);
            itemRect.pivot = new Vector2(0.5f, 1f);
            itemRect.anchoredPosition = Vector2.zero;
            itemRect.sizeDelta = new Vector2(0f, 34f);

            Toggle itemToggle = item.AddComponent<Toggle>();
            itemToggle.targetGraphic = item.GetComponent<Image>();
            ColorBlock toggleColors = itemToggle.colors;
            toggleColors.normalColor = new Color(0.98f, 0.96f, 0.9f, 0.98f);
            toggleColors.highlightedColor = new Color(0.88f, 0.95f, 0.88f, 0.98f);
            toggleColors.pressedColor = new Color(0.78f, 0.88f, 0.78f, 0.98f);
            itemToggle.colors = toggleColors;

            Text itemLabel = CreateText("Item Label", item.transform, font, 17, TextAnchor.MiddleLeft);
            itemLabel.color = Color.black;
            itemLabel.rectTransform.anchorMin = Vector2.zero;
            itemLabel.rectTransform.anchorMax = Vector2.one;
            itemLabel.rectTransform.offsetMin = new Vector2(12f, 0f);
            itemLabel.rectTransform.offsetMax = new Vector2(-12f, 0f);
            dropdown.itemText = itemLabel;

            dropdown.options = new System.Collections.Generic.List<Dropdown.OptionData>();
            dropdown.value = 0;
            dropdown.RefreshShownValue();
            dropdown.template = templateRect;
            template.SetActive(false);
            return dropdown;
        }

        private static Dropdown CreateStageCategoryDropdown(string name, Transform parent, Font font, Vector2 anchoredPosition, Vector2 size)
        {
            Dropdown dropdown = CreateStageObjectDropdown(name, parent, font, anchoredPosition, size);
            System.Collections.Generic.List<Dropdown.OptionData> options = new System.Collections.Generic.List<Dropdown.OptionData>();
            StageObjectCategory[] categories = StageObjectCatalog.Categories;
            for (int i = 0; i < categories.Length; i++)
            {
                options.Add(new Dropdown.OptionData(StageObjectCatalog.GetCategoryLabel(categories[i])));
            }

            dropdown.options = options;
            dropdown.value = 0;
            dropdown.RefreshShownValue();
            return dropdown;
        }

        private static InputField CreateStageSearchInput(string name, Transform parent, Font font, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject fieldObject = CreatePanel(name, parent, new Color(0.98f, 0.96f, 0.9f, 0.96f));
            RectTransform rect = fieldObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            AddUiOutline(fieldObject, new Color(0.12f, 0.11f, 0.1f, 0.65f), new Vector2(1.5f, -1.5f));

            InputField input = fieldObject.AddComponent<InputField>();
            input.targetGraphic = fieldObject.GetComponent<Image>();
            input.characterLimit = 32;

            Text text = CreateText("Text", fieldObject.transform, font, 17, TextAnchor.MiddleLeft);
            text.color = Color.black;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(12f, 0f);
            text.rectTransform.offsetMax = new Vector2(-12f, 0f);
            input.textComponent = text;

            Text placeholder = CreateText("Placeholder", fieldObject.transform, font, 16, TextAnchor.MiddleLeft);
            placeholder.text = LocalizationManager.T("stage_editor_search_placeholder");
            AddLocalizedText(placeholder.gameObject, "stage_editor_search_placeholder");
            placeholder.color = new Color(0.25f, 0.22f, 0.16f, 0.45f);
            placeholder.rectTransform.anchorMin = Vector2.zero;
            placeholder.rectTransform.anchorMax = Vector2.one;
            placeholder.rectTransform.offsetMin = new Vector2(12f, 0f);
            placeholder.rectTransform.offsetMax = new Vector2(-12f, 0f);
            input.placeholder = placeholder;
            return input;
        }

        private static Font CreateDefaultFont()
        {
            Font projectFont = AssetDatabase.LoadAssetAtPath<Font>(ProjectFontPath);
            if (projectFont == null && System.IO.File.Exists(ProjectFontPath))
            {
                AssetDatabase.ImportAsset(ProjectFontPath);
                projectFont = AssetDatabase.LoadAssetAtPath<Font>(ProjectFontPath);
            }

            if (projectFont != null)
            {
                return projectFont;
            }

            Font font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Meiryo", "Arial" }, 18);
            if (font != null)
            {
                return font;
            }

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null)
            {
                return font;
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static Sprite CreateSquareSprite()
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(SquareTexturePath);
            if (existing != null)
            {
                return existing;
            }

            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Generated");
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "GeneratedSquareTexture";
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            sprite.name = "GeneratedSquareSprite";
            AssetDatabase.CreateAsset(texture, SquareTexturePath);
            AssetDatabase.AddObjectToAsset(sprite, texture);
            AssetDatabase.SaveAssets();
            return sprite;
        }

        private static Sprite LoadTitleLogoSprite()
        {
            if (!System.IO.File.Exists(TitleLogoPath))
            {
                Debug.LogWarning($"Title logo not found: {TitleLogoPath}");
                return null;
            }

            TextureImporter importer = AssetImporter.GetAtPath(TitleLogoPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            else
            {
                AssetDatabase.ImportAsset(TitleLogoPath);
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(TitleLogoPath);
        }

        private static GameObject CreateMarker(string name, Vector3 localPosition, Transform parent)
        {
            GameObject marker = new GameObject(name);
            marker.transform.SetParent(parent);
            marker.transform.localPosition = localPosition;
            return marker;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void AssignObject(Object target, string propertyName, Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignObjectArray(Object target, string propertyName, Object[] values)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignLayerMask(Object target, string propertyName, int value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignVector2(Object target, string propertyName, Vector2 value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.vector2Value = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignFloat(Object target, string propertyName, float value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"Serialized float property was not found: {target.GetType().Name}.{propertyName}", target);
                return;
            }

            property.floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignBool(Object target, string propertyName, bool value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignColor(Object target, string propertyName, Color value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.colorValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignInt(Object target, string propertyName, int value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignEnum(Object target, string propertyName, int value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.enumValueIndex = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignString(Object target, string propertyName, string value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(scenePath, true)
            };
        }
    }
}
