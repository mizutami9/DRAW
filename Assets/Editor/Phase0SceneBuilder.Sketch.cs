using DrawBody.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.EditorTools
{
    public static partial class Phase0SceneBuilder
    {
        private static void AddSketchbookPaper(Transform parent)
        {
            Color blueLine = new Color(0.45f, 0.72f, 0.9f, 0.18f);
            Color redLine = new Color(0.95f, 0.35f, 0.3f, 0.22f);
            for (int i = 0; i < 13; i++)
            {
                float y = 240f - i * 54f;
                CreateIconLine(parent, new Vector2(-640f, y), new Vector2(640f, y), 1.2f, blueLine);
            }

            CreateIconLine(parent, new Vector2(-510f, 300f), new Vector2(-510f, -350f), 1.4f, redLine);
        }

        private static void AddNotebookGrid(Transform parent, Vector2 size)
        {
            Color lineColor = new Color(0.44f, 0.72f, 0.9f, 0.12f);
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            for (float y = -halfHeight + 34f; y < halfHeight; y += 34f)
            {
                CreateIconLine(parent, new Vector2(-halfWidth + 14f, y), new Vector2(halfWidth - 14f, y), 0.8f, lineColor);
            }

            for (float x = -halfWidth + 42f; x < halfWidth; x += 42f)
            {
                CreateIconLine(parent, new Vector2(x, -halfHeight + 12f), new Vector2(x, halfHeight - 12f), 0.6f, new Color(0.75f, 0.78f, 0.72f, 0.07f));
            }
        }

        private static void AddSketchFrame(Transform parent, Vector2 size, Color color, float width)
        {
            Image image = parent.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            Outline outline = parent.GetComponent<Outline>();
            if (outline == null)
            {
                outline = parent.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = color;
            outline.effectDistance = new Vector2(width, -width);
            outline.useGraphicAlpha = true;
        }

        private static void AddMaskingTape(Transform parent, Vector2 position, float rotation)
        {
            // Masking tape was part of the old hand-drawn framing style.
        }

        private static void AddCorkPins(Transform parent)
        {
            CreateIconDot(parent, new Vector2(-92f, 144f), 9f, new Color(0.82f, 0.18f, 0.12f, 0.9f));
            CreateIconDot(parent, new Vector2(92f, 144f), 9f, new Color(0.25f, 0.52f, 0.95f, 0.9f));
            CreateIconLine(parent, new Vector2(-92f, 139f), new Vector2(-92f, 130f), 1.5f, new Color(0.12f, 0.1f, 0.08f, 0.35f));
            CreateIconLine(parent, new Vector2(92f, 139f), new Vector2(92f, 130f), 1.5f, new Color(0.12f, 0.1f, 0.08f, 0.35f));
        }

        private static void AddInkBottleIcon(Transform parent, Vector2 center)
        {
            Color outline = new Color(0.18f, 0.13f, 0.1f, 0.82f);
            Color ink = new Color(0.24f, 0.5f, 0.95f, 0.72f);
            CreateIconLine(parent, center + new Vector2(-10f, 12f), center + new Vector2(10f, 12f), 2f, outline);
            CreateIconLine(parent, center + new Vector2(-7f, 12f), center + new Vector2(-12f, -14f), 2f, outline);
            CreateIconLine(parent, center + new Vector2(7f, 12f), center + new Vector2(12f, -14f), 2f, outline);
            CreateIconLine(parent, center + new Vector2(-12f, -14f), center + new Vector2(12f, -14f), 2f, outline);
            CreateIconLine(parent, center + new Vector2(-9f, -2f), center + new Vector2(9f, -2f), 7f, ink);
            CreateIconLine(parent, center + new Vector2(-5f, 18f), center + new Vector2(5f, 18f), 5f, outline);
            CreateIconDot(parent, center + new Vector2(0f, 2f), 3f, new Color(1f, 1f, 1f, 0.65f));
        }

        private static void AddPencilSliderTrack(Transform parent, float width)
        {
            float half = width * 0.5f - 6f;
            Color paperLine = new Color(0.9f, 0.88f, 0.82f, 0.75f);
            Color graphite = new Color(0.08f, 0.08f, 0.09f, 0.62f);

            CreateIconLine(parent, new Vector2(-half, 0f), new Vector2(half, 1.4f), 5f, paperLine);
            CreateIconLine(parent, new Vector2(-half + 1f, 0.4f), new Vector2(half - 1f, -0.6f), 1.8f, graphite);
            CreateIconLine(parent, new Vector2(-half + 2f, -1.2f), new Vector2(half - 2f, 0.8f), 1.1f, new Color(0.08f, 0.08f, 0.09f, 0.28f));
        }

        private static void AddTinyDoodle(Transform parent, Vector2 position, float scale)
        {
            Color color = new Color(0.28f, 0.42f, 0.9f, 0.13f);
            Vector2 p = position;
            CreateIconLine(parent, p + new Vector2(-34f, -2f) * scale, p + new Vector2(-18f, 12f) * scale, 2f * scale, color);
            CreateIconLine(parent, p + new Vector2(-18f, 12f) * scale, p + new Vector2(2f, 16f) * scale, 2f * scale, color);
            CreateIconLine(parent, p + new Vector2(2f, 16f) * scale, p + new Vector2(22f, 6f) * scale, 2f * scale, color);
            CreateIconLine(parent, p + new Vector2(22f, 6f) * scale, p + new Vector2(36f, -2f) * scale, 2f * scale, color);
        }

        private static float RandomOffset(float seed)
        {
            return Mathf.Sin(seed * 0.37f) * 1.8f;
        }

        private static void AddPaperTexture(GameObject target, Color baseColor, Color fiberColor, float fiberStrength, int seed)
        {
            SketchPaperTexture texture = target.AddComponent<SketchPaperTexture>();
            AssignColor(texture, "baseColor", baseColor);
            AssignColor(texture, "fiberColor", fiberColor);
            AssignFloat(texture, "fiberStrength", fiberStrength);
            AssignInt(texture, "seed", seed);
        }

        private static void CreateSpeciesSketchIcon(Transform parent, DrawManager.Species species)
        {
            Color ink = new Color(0.08f, 0.08f, 0.08f, 1f);
            Color accent = new Color(0.18f, 0.42f, 0.95f, 1f);

            switch (species)
            {
                case DrawManager.Species.Cat:
                    CreateIconLine(parent, new Vector2(-10f, 7f), new Vector2(-5f, 15f), 2f, ink);
                    CreateIconLine(parent, new Vector2(-5f, 15f), new Vector2(-1f, 7f), 2f, ink);
                    CreateIconLine(parent, new Vector2(3f, 7f), new Vector2(8f, 15f), 2f, ink);
                    CreateIconLine(parent, new Vector2(8f, 15f), new Vector2(12f, 7f), 2f, ink);
                    CreateIconLine(parent, new Vector2(-12f, 7f), new Vector2(12f, 7f), 2f, ink);
                    CreateIconLine(parent, new Vector2(-12f, 7f), new Vector2(-14f, -6f), 2f, ink);
                    CreateIconLine(parent, new Vector2(12f, 7f), new Vector2(14f, -6f), 2f, ink);
                    CreateIconLine(parent, new Vector2(-14f, -6f), new Vector2(14f, -6f), 2f, ink);
                    CreateIconDot(parent, new Vector2(-5f, 0f), 2.2f, ink);
                    CreateIconDot(parent, new Vector2(5f, 0f), 2.2f, ink);
                    CreateIconLine(parent, new Vector2(-3f, -4f), new Vector2(0f, -6f), 1.5f, ink);
                    CreateIconLine(parent, new Vector2(3f, -4f), new Vector2(0f, -6f), 1.5f, ink);
                    break;
                case DrawManager.Species.Bird:
                    CreateIconLine(parent, new Vector2(-14f, 3f), new Vector2(-2f, -5f), 2f, ink);
                    CreateIconLine(parent, new Vector2(-2f, -5f), new Vector2(12f, 5f), 2f, ink);
                    CreateIconLine(parent, new Vector2(-4f, 1f), new Vector2(4f, 10f), 2f, accent);
                    CreateIconLine(parent, new Vector2(4f, 10f), new Vector2(12f, 2f), 2f, accent);
                    CreateIconLine(parent, new Vector2(12f, 2f), new Vector2(17f, 5f), 1.6f, ink);
                    CreateIconDot(parent, new Vector2(8f, 3f), 2f, ink);
                    break;
                case DrawManager.Species.Snake:
                    CreateIconLine(parent, new Vector2(-15f, -7f), new Vector2(-7f, 3f), 2.5f, ink);
                    CreateIconLine(parent, new Vector2(-7f, 3f), new Vector2(2f, -2f), 2.5f, ink);
                    CreateIconLine(parent, new Vector2(2f, -2f), new Vector2(10f, 8f), 2.5f, ink);
                    CreateIconLine(parent, new Vector2(10f, 8f), new Vector2(15f, 4f), 2.5f, ink);
                    CreateIconDot(parent, new Vector2(13f, 5f), 2f, ink);
                    break;
                case DrawManager.Species.Slime:
                    CreateIconLine(parent, new Vector2(-14f, -6f), new Vector2(-8f, 7f), 2f, ink);
                    CreateIconLine(parent, new Vector2(-8f, 7f), new Vector2(5f, 12f), 2f, ink);
                    CreateIconLine(parent, new Vector2(5f, 12f), new Vector2(15f, 1f), 2f, ink);
                    CreateIconLine(parent, new Vector2(15f, 1f), new Vector2(10f, -8f), 2f, ink);
                    CreateIconLine(parent, new Vector2(10f, -8f), new Vector2(-14f, -6f), 2f, ink);
                    CreateIconDot(parent, new Vector2(-2f, 0f), 2f, ink);
                    CreateIconDot(parent, new Vector2(6f, 1f), 2f, ink);
                    break;
                default:
                    CreateIconLine(parent, new Vector2(-6f, 12f), new Vector2(6f, 12f), 2f, ink);
                    CreateIconLine(parent, new Vector2(6f, 12f), new Vector2(6f, 0f), 2f, ink);
                    CreateIconLine(parent, new Vector2(6f, 0f), new Vector2(-6f, 0f), 2f, ink);
                    CreateIconLine(parent, new Vector2(-6f, 0f), new Vector2(-6f, 12f), 2f, ink);
                    CreateIconDot(parent, new Vector2(-2.5f, 6f), 2f, ink);
                    CreateIconDot(parent, new Vector2(2.5f, 6f), 2f, ink);
                    CreateIconLine(parent, new Vector2(0f, 0f), new Vector2(0f, -10f), 2.2f, accent);
                    CreateIconLine(parent, new Vector2(0f, -3f), new Vector2(-10f, -8f), 2f, ink);
                    CreateIconLine(parent, new Vector2(0f, -3f), new Vector2(10f, -8f), 2f, ink);
                    CreateIconLine(parent, new Vector2(0f, -10f), new Vector2(-7f, -16f), 2f, ink);
                    CreateIconLine(parent, new Vector2(0f, -10f), new Vector2(7f, -16f), 2f, ink);
                    break;
            }
        }

        private static void CreateIconLine(Transform parent, Vector2 from, Vector2 to, float width, Color color)
        {
            GameObject line = CreatePanel("IconLine", parent, color);
            Image image = line.GetComponent<Image>();
            image.raycastTarget = false;

            RectTransform rect = line.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = (from + to) * 0.5f;
            rect.sizeDelta = new Vector2(Vector2.Distance(from, to), width);
            float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private static void CreateIconDot(Transform parent, Vector2 position, float size, Color color)
        {
            GameObject dot = CreatePanel("IconDot", parent, color);
            Image image = dot.GetComponent<Image>();
            image.raycastTarget = false;

            RectTransform rect = dot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);
        }

        private static GameObject CreateSpriteBox(string name, Vector3 position, Vector2 size, Color color, Transform parent, Sprite squareSprite)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent);
            obj.transform.position = position;
            obj.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = squareSprite;
            renderer.color = color;

            return obj;
        }

        private static void AddSketchBoxOutline(Transform parent, Vector2 size, Color color, float width, int sortingOrder, Vector3 offset = default)
        {
            Vector3[] points =
            {
                offset + new Vector3(-0.52f, -0.48f, 0f),
                offset + new Vector3(-0.49f, 0.53f, 0f),
                offset + new Vector3(0.51f, 0.50f, 0f),
                offset + new Vector3(0.53f, -0.51f, 0f),
                offset + new Vector3(-0.52f, -0.48f, 0f)
            };
            AddDoodleLine("Sketch Outline A", parent, points, color, width / Mathf.Max(Mathf.Max(size.x, size.y), 0.1f), sortingOrder);

            Vector3[] loosePoints =
            {
                offset + new Vector3(-0.50f, -0.52f, 0f),
                offset + new Vector3(-0.54f, 0.48f, 0f),
                offset + new Vector3(0.49f, 0.54f, 0f),
                offset + new Vector3(0.50f, -0.47f, 0f),
                offset + new Vector3(-0.50f, -0.52f, 0f)
            };
            AddDoodleLine("Sketch Outline B", parent, loosePoints, color * 0.9f, width / Mathf.Max(Mathf.Max(size.x, size.y), 0.1f), sortingOrder + 1);
        }

        private static void AddDoorDoodle(Transform parent)
        {
            Vector3[] door =
            {
                new Vector3(-0.28f, -0.48f, 0f),
                new Vector3(-0.28f, 0.44f, 0f),
                new Vector3(0.28f, 0.44f, 0f),
                new Vector3(0.28f, -0.48f, 0f)
            };
            AddDoodleLine("Goal Door Frame", parent, door, Color.black, 0.055f, 25);
            AddDoodleLine("Goal Door Top Scribble", parent, new[] { new Vector3(-0.34f, 0.48f, 0f), new Vector3(0.34f, 0.48f, 0f) }, Color.black, 0.055f, 25);
            AddDoodleLine("Goal Shine A", parent, new[] { new Vector3(-0.6f, 0.35f, 0f), new Vector3(-0.78f, 0.52f, 0f) }, new Color(1f, 0.75f, 0f), 0.035f, 26);
            AddDoodleLine("Goal Shine B", parent, new[] { new Vector3(0.58f, 0.35f, 0f), new Vector3(0.82f, 0.48f, 0f) }, new Color(1f, 0.75f, 0f), 0.035f, 26);
        }

        private static TextMesh CreateDoodleText(string name, string text, Vector3 position, Transform parent, Font font, int fontSize, float characterSize, Color color, TextAnchor anchor)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent);
            textObject.transform.position = position;

            TextMesh mesh = textObject.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.text = text;
            mesh.fontSize = fontSize;
            mesh.characterSize = characterSize;
            mesh.anchor = anchor;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;

            MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = 35;
            }

            return mesh;
        }

        private static LineRenderer AddDoodleLine(string name, Transform parent, Vector3[] points, Color color, float width, int sortingOrder)
        {
            GameObject lineObject = new GameObject(name);
            lineObject.transform.SetParent(parent, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.material = GetDoodleLineMaterial();
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = sortingOrder;
            return line;
        }

        private static void AddDoodleCircle(string name, Transform parent, float radius, Color color, float width, int sortingOrder)
        {
            AddDoodleCircle(name, parent, radius, color, width, sortingOrder, Vector3.zero);
        }

        private static void AddDoodleCircle(string name, Transform parent, float radius, Color color, float width, int sortingOrder, Vector3 center)
        {
            const int segments = 32;
            Vector3[] points = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float wobble = 1f + Mathf.Sin(i * 2.17f) * 0.05f;
                points[i] = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius * wobble;
            }

            AddDoodleLine(name, parent, points, color, width, sortingOrder);
        }

        private static Material GetDoodleLineMaterial()
        {
            if (doodleLineMaterial != null)
            {
                return doodleLineMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            doodleLineMaterial = new Material(shader);
            doodleLineMaterial.name = "Doodle Line Material";
            return doodleLineMaterial;
        }

        private static void SetSortingOrder(GameObject gameObject, int sortingOrder)
        {
            SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = sortingOrder;
            }
        }
    }
}
