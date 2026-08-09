using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageBombBreakableWall : MonoBehaviour
    {
        private int requiredHits = 1;
        private int currentHits;
        private Vector2 wallSize = new Vector2(0.5f, 2f);
        private bool broken;
        private TextMesh remainingBombText;
        private static Material crackMaterial;
        private static Sprite squareSprite;
        private static Sprite circleSprite;

        public string ObjectId
        {
            get
            {
                StageEditorObject marker = GetComponent<StageEditorObject>();
                return marker != null ? marker.objectId : gameObject.name;
            }
        }

        public bool IsBroken => broken;
        public int CurrentHits => currentHits;
        public int RequiredHits => requiredHits;

        public void Configure(int explosionsRequired, Vector2 size)
        {
            requiredHits = Mathf.Clamp(explosionsRequired, 1, 5);
            wallSize = new Vector2(Mathf.Max(0.2f, size.x), Mathf.Max(0.2f, size.y));
            CreateBombRequirementBadge();
        }

        public bool HitByBomb(Vector2 blastCenter)
        {
            if (broken)
            {
                return true;
            }

            ApplyDamageStage(currentHits + 1);
            if (currentHits >= requiredHits)
            {
                Break(blastCenter);
            }
            else
            {
                StartCoroutine(PlayDamageFlash());
            }
            return broken;
        }

        public void ApplyNetworkDamage(int hits, Vector2 blastCenter)
        {
            if (broken || hits <= currentHits)
            {
                return;
            }

            ApplyDamageStage(Mathf.Clamp(hits, 0, requiredHits));
            if (currentHits >= requiredHits)
            {
                Break(blastCenter);
            }
        }

        public void Break(Vector2 blastCenter)
        {
            if (broken)
            {
                return;
            }

            currentHits = requiredHits;
            RefreshBombRequirementBadge();
            broken = true;
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
            StartCoroutine(PlayBreakAnimation(blastCenter));
        }

        private void ApplyDamageStage(int targetHits)
        {
            int clampedTarget = Mathf.Clamp(targetHits, 0, requiredHits);
            while (currentHits < clampedTarget)
            {
                CreateCrackStage(currentHits);
                currentHits++;
            }
            RefreshBombRequirementBadge();
        }

        private void CreateBombRequirementBadge()
        {
            if (remainingBombText != null)
            {
                return;
            }

            float unit = Mathf.Clamp(Mathf.Min(wallSize.x, wallSize.y) * 0.34f, 0.15f, 0.34f);
            GameObject badge = new GameObject("Bomb Wall Requirement");
            badge.transform.SetParent(transform, false);
            badge.transform.localPosition = new Vector3(0f, 0f, -0.13f);
            badge.transform.rotation = Quaternion.identity;

            GameObject backObject = new GameObject("Requirement Back");
            backObject.transform.SetParent(badge.transform, false);
            backObject.transform.localScale = new Vector3(unit * 3.25f, unit * 1.42f, 1f);
            SpriteRenderer back = backObject.AddComponent<SpriteRenderer>();
            back.sprite = GetSquareSprite();
            back.color = new Color(1f, 0.93f, 0.62f, 0.9f);
            back.sortingOrder = 29;

            GameObject bombObject = new GameObject("Bomb Mark");
            bombObject.transform.SetParent(badge.transform, false);
            bombObject.transform.localPosition = new Vector3(-unit * 0.84f, -unit * 0.04f, -0.01f);
            bombObject.transform.localScale = Vector3.one * unit * 0.72f;
            SpriteRenderer bomb = bombObject.AddComponent<SpriteRenderer>();
            bomb.sprite = GetCircleSprite();
            bomb.color = new Color(0.1f, 0.09f, 0.08f, 1f);
            bomb.sortingOrder = 30;

            AddBadgeLine(
                badge.transform,
                "Bomb Fuse",
                new Vector2(-unit * 0.62f, unit * 0.2f),
                new Vector2(-unit * 0.38f, unit * 0.48f),
                unit * 0.12f,
                new Color(0.18f, 0.12f, 0.08f, 1f),
                31);
            AddBadgeLine(
                badge.transform,
                "Bomb Spark A",
                new Vector2(-unit * 0.38f, unit * 0.48f),
                new Vector2(-unit * 0.2f, unit * 0.61f),
                unit * 0.1f,
                new Color(1f, 0.35f, 0.05f, 1f),
                32);
            AddBadgeLine(
                badge.transform,
                "Bomb Spark B",
                new Vector2(-unit * 0.38f, unit * 0.48f),
                new Vector2(-unit * 0.48f, unit * 0.68f),
                unit * 0.08f,
                new Color(1f, 0.35f, 0.05f, 1f),
                32);

            GameObject textObject = new GameObject("Remaining Bomb Count");
            textObject.transform.SetParent(badge.transform, false);
            textObject.transform.localPosition = new Vector3(unit * 0.58f, -unit * 0.03f, -0.02f);
            remainingBombText = textObject.AddComponent<TextMesh>();
            remainingBombText.anchor = TextAnchor.MiddleCenter;
            remainingBombText.alignment = TextAlignment.Center;
            remainingBombText.fontSize = 52;
            remainingBombText.characterSize = unit * 0.17f;
            remainingBombText.fontStyle = FontStyle.Bold;
            remainingBombText.color = new Color(0.18f, 0.08f, 0.04f, 1f);
            Font font = FindHandwrittenFont();
            if (font != null)
            {
                remainingBombText.font = font;
                textObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            textObject.GetComponent<MeshRenderer>().sortingOrder = 33;
            RefreshBombRequirementBadge();
        }

        private void RefreshBombRequirementBadge()
        {
            if (remainingBombText != null)
            {
                remainingBombText.text = "×" + Mathf.Max(0, requiredHits - currentHits);
            }
        }

        private static void AddBadgeLine(
            Transform parent,
            string lineName,
            Vector2 from,
            Vector2 to,
            float width,
            Color color,
            int sortingOrder)
        {
            GameObject lineObject = new GameObject(lineName);
            lineObject.transform.SetParent(parent, false);
            lineObject.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.startWidth = width;
            line.endWidth = width * 0.72f;
            line.numCapVertices = 3;
            line.sharedMaterial = GetCrackMaterial();
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = sortingOrder;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
        }

        private void CreateCrackStage(int stage)
        {
            Vector2[][] patterns =
            {
                new[] { new Vector2(-0.08f, 0.48f), new Vector2(0.08f, 0.2f), new Vector2(-0.04f, -0.04f), new Vector2(0.16f, -0.34f) },
                new[] { new Vector2(0.48f, 0.34f), new Vector2(0.16f, 0.15f), new Vector2(-0.1f, 0.02f), new Vector2(-0.42f, -0.18f) },
                new[] { new Vector2(-0.42f, 0.36f), new Vector2(-0.14f, 0.12f), new Vector2(0.12f, -0.08f), new Vector2(0.4f, -0.4f) },
                new[] { new Vector2(0.34f, 0.48f), new Vector2(0.12f, 0.22f), new Vector2(0.24f, -0.06f), new Vector2(-0.18f, -0.46f) },
                new[] { new Vector2(-0.48f, -0.4f), new Vector2(-0.18f, -0.16f), new Vector2(0.02f, 0.06f), new Vector2(0.44f, 0.2f) }
            };
            Vector2[] selected = patterns[Mathf.Clamp(stage, 0, patterns.Length - 1)];
            CreateCrackLine("Bomb Wall Crack " + (stage + 1), selected, 0.05f);

            Vector2 branchStart = selected[1];
            Vector2 branchEnd = branchStart + new Vector2(stage % 2 == 0 ? 0.22f : -0.22f, 0.15f - stage * 0.025f);
            CreateCrackLine(
                "Bomb Wall Crack Branch " + (stage + 1),
                new[] { branchStart, branchEnd },
                0.038f);
        }

        private void CreateCrackLine(string lineName, Vector2[] normalizedPoints, float width)
        {
            GameObject lineObject = new GameObject(lineName);
            lineObject.transform.SetParent(transform, false);
            lineObject.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = normalizedPoints.Length;
            line.startWidth = width;
            line.endWidth = width * 0.76f;
            line.numCapVertices = 3;
            line.numCornerVertices = 3;
            line.sharedMaterial = GetCrackMaterial();
            line.startColor = new Color(0.5f, 0.09f, 0.04f, 1f);
            line.endColor = new Color(0.24f, 0.035f, 0.02f, 1f);
            line.sortingOrder = 28;
            for (int i = 0; i < normalizedPoints.Length; i++)
            {
                line.SetPosition(i, new Vector3(
                    normalizedPoints[i].x * wallSize.x,
                    normalizedPoints[i].y * wallSize.y,
                    0f));
            }
        }

        private IEnumerator PlayDamageFlash()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            Color[] original = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                original[i] = renderers[i].color;
                renderers[i].color = Color.Lerp(original[i], new Color(1f, 0.22f, 0.08f, 1f), 0.65f);
            }
            yield return new WaitForSeconds(0.12f);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].color = original[i];
                }
            }
        }

        private IEnumerator PlayBreakAnimation(Vector2 blastCenter)
        {
            Transform[] pieces = new Transform[4];
            Sprite square = GetSquareSprite();
            SpriteRenderer sourceRenderer = GetComponentInChildren<SpriteRenderer>();
            Color color = sourceRenderer != null ? sourceRenderer.color : new Color(0.72f, 0.68f, 0.58f, 1f);
            Vector2 debrisSize = GetComponent<BoxCollider2D>() != null
                ? GetComponent<BoxCollider2D>().bounds.size
                : wallSize;

            for (int i = 0; i < pieces.Length; i++)
            {
                GameObject piece = new GameObject("Bomb Wall Debris " + i);
                piece.transform.position = transform.position + new Vector3(
                    (i % 2 == 0 ? -1f : 1f) * debrisSize.x * 0.22f,
                    (i < 2 ? -1f : 1f) * debrisSize.y * 0.22f,
                    0f);
                piece.transform.localScale = new Vector3(debrisSize.x * 0.42f, debrisSize.y * 0.42f, 1f);
                SpriteRenderer renderer = piece.AddComponent<SpriteRenderer>();
                renderer.sprite = square;
                renderer.color = color;
                renderer.sortingOrder = 35;
                pieces[i] = piece.transform;
            }

            Renderer[] original = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < original.Length; i++)
            {
                original[i].enabled = false;
            }

            float elapsed = 0f;
            while (elapsed < 0.42f)
            {
                elapsed += Time.deltaTime;
                for (int i = 0; i < pieces.Length; i++)
                {
                    if (pieces[i] == null)
                    {
                        continue;
                    }
                    Vector2 away = ((Vector2)pieces[i].position - blastCenter).normalized;
                    pieces[i].position += (Vector3)((away * 5f + Vector2.up * 1.2f) * Time.deltaTime);
                    pieces[i].Rotate(0f, 0f, (i % 2 == 0 ? -240f : 240f) * Time.deltaTime);
                    pieces[i].localScale *= 1f - Time.deltaTime * 1.8f;
                }
                yield return null;
            }

            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] != null)
                {
                    Destroy(pieces[i].gameObject);
                }
            }
            gameObject.SetActive(false);
        }

        private static Material GetCrackMaterial()
        {
            if (crackMaterial == null)
            {
                crackMaterial = new Material(Shader.Find("Sprites/Default"));
            }
            return crackMaterial;
        }

        private static Sprite GetSquareSprite()
        {
            if (squareSprite == null)
            {
                Texture2D texture = Texture2D.whiteTexture;
                squareSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    texture.width);
            }
            return squareSprite;
        }

        private static Sprite GetCircleSprite()
        {
            if (circleSprite != null)
            {
                return circleSprite;
            }

            const int size = 48;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Bomb Wall Mark Circle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[size * size];
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            float radius = size * 0.46f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius - distance + 0.85f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            circleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);
            return circleSprite;
        }

        private static Font FindHandwrittenFont()
        {
            Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();
            for (int i = 0; i < fonts.Length; i++)
            {
                Font font = fonts[i];
                if (font != null && font.name.IndexOf("Yomogi", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return font;
                }
            }

            Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return fallback != null ? fallback : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
