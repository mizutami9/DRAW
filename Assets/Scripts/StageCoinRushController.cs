using UnityEngine;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Builds the large collectible field used by stage 12-2.  The stage geometry
    /// remains regular editable stage data, while the 1000 identical coins use a
    /// single cached sprite to avoid thousands of LineRenderer child objects.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageCoinRushController : MonoBehaviour
    {
        private const int HallColumns = 40;
        private const int HallRows = 22;
        private const int RoomColumns = 5;
        private const int RoomRows = 4;
        private static Sprite coinSprite;
        private static bool creatingEditorPreview;
        private bool previewOnly;

        internal static void CreateEditorPreview(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            GameObject preview = new GameObject("12-2 Coin Field Preview");
            preview.transform.SetParent(parent, false);
            creatingEditorPreview = true;
            try
            {
                preview.AddComponent<StageCoinRushController>();
            }
            finally
            {
                creatingEditorPreview = false;
            }
        }

        private void Awake()
        {
            previewOnly = creatingEditorPreview;
            BuildHallCoins();
            BuildRoomCoins();
        }

        private void BuildHallCoins()
        {
            int index = 0;
            for (int row = 0; row < HallRows; row++)
            {
                float y = Mathf.Lerp(-12.25f, 12.1f, row / (HallRows - 1f));
                for (int column = 0; column < HallColumns; column++)
                {
                    float x = Mathf.Lerp(-27.4f, 27.4f, column / (HallColumns - 1f));
                    float jitterX = HashSigned(index * 2 + 11) * 0.13f;
                    float jitterY = HashSigned(index * 2 + 37) * 0.1f;
                    CreateCoin($"12-2_hall_coin_{index:000}", new Vector2(x + jitterX, y + jitterY), index);
                    index++;
                }
            }
        }

        private void BuildRoomCoins()
        {
            int index = HallColumns * HallRows;
            float[] roomY = { -10f, 0f, 10f };
            for (int side = -1; side <= 1; side += 2)
            {
                for (int room = 0; room < roomY.Length; room++)
                {
                    for (int row = 0; row < RoomRows; row++)
                    {
                        float y = roomY[room] + Mathf.Lerp(-2.15f, 2.15f, row / (RoomRows - 1f));
                        for (int column = 0; column < RoomColumns; column++)
                        {
                            float inwardToOutward = Mathf.Lerp(39.0f, 45.8f, column / (RoomColumns - 1f));
                            float x = side * inwardToOutward;
                            float jitterX = HashSigned(index * 2 + 59) * 0.1f;
                            float jitterY = HashSigned(index * 2 + 83) * 0.08f;
                            CreateCoin($"12-2_room_coin_{index:000}", new Vector2(x + jitterX, y + jitterY), index);
                            index++;
                        }
                    }
                }
            }
        }

        private void CreateCoin(string id, Vector2 position, int index)
        {
            GameObject coin = new GameObject(id);
            coin.transform.SetParent(transform, false);
            coin.transform.position = new Vector3(position.x, position.y, -0.15f);
            float scale = 0.52f + (index % 5) * 0.012f;
            coin.transform.localScale = new Vector3(scale, scale, 1f);

            SpriteRenderer renderer = coin.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSharedCoinSprite();
            renderer.color = Color.white;
            renderer.sortingOrder = 16;

            if (!previewOnly)
            {
                CircleCollider2D trigger = coin.AddComponent<CircleCollider2D>();
                trigger.radius = 0.56f;
                trigger.isTrigger = true;

                StageCollectible collectible = coin.AddComponent<StageCollectible>();
                collectible.Configure(id, StageObjectType.CollectibleCoin);
            }
        }

        private static float HashSigned(int value)
        {
            uint hash = (uint)value;
            hash ^= hash >> 16;
            hash *= 0x7feb352du;
            hash ^= hash >> 15;
            hash *= 0x846ca68bu;
            hash ^= hash >> 16;
            return (hash & 0xffffu) / 32767.5f - 1f;
        }

        internal static Sprite GetSharedCoinSprite()
        {
            if (coinSprite != null)
            {
                return coinSprite;
            }

            const int size = 48;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "12-2 Crayon Coin",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color transparent = new Color(0f, 0f, 0f, 0f);
            Color rim = new Color(0.78f, 0.35f, 0.035f, 1f);
            Color gold = new Color(1f, 0.72f, 0.06f, 1f);
            Color inner = new Color(1f, 0.88f, 0.22f, 1f);
            Color shine = new Color(1f, 0.98f, 0.72f, 1f);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 delta = new Vector2(x, y) - center;
                    float distance = delta.magnitude;
                    Color color = transparent;
                    if (distance <= 21.5f)
                    {
                        color = distance > 18.4f ? rim : distance > 14.7f ? gold : inner;
                        float scribble = Mathf.Sin(x * 1.73f + y * 0.61f) * 0.025f;
                        color.r = Mathf.Clamp01(color.r + scribble);
                        color.g = Mathf.Clamp01(color.g + scribble);
                    }
                    if ((delta - new Vector2(-6f, 7f)).sqrMagnitude < 8f)
                    {
                        color = shine;
                    }
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply(false, true);
            coinSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            coinSprite.name = "12-2 Crayon Coin Sprite";
            coinSprite.hideFlags = HideFlags.HideAndDontSave;
            return coinSprite;
        }
    }
}
