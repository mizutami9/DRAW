using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageLauncherTrajectoryPreview : MonoBehaviour
    {
        private sealed class Channel
        {
            public Transform Root;
            public readonly List<SpriteRenderer> Dots = new List<SpriteRenderer>();
            public SpriteRenderer Impact;
        }

        private StageBombDropper bombLauncher;
        private StageMissileLauncher missileLauncher;
        private Channel bombChannel;
        private Channel missileChannel;
        private float horizontalLimit = 28f;
        private float minimumY = -13f;
        private float maximumY = 13f;

        public void Configure(
            StageBombDropper bomb,
            StageMissileLauncher missile,
            float halfWidth,
            float minY,
            float maxY)
        {
            bombLauncher = bomb;
            missileLauncher = missile;
            horizontalLimit = Mathf.Max(5f, halfWidth);
            minimumY = minY;
            maximumY = maxY;
            if (bombChannel == null)
                bombChannel = CreateChannel("Bomb Trajectory Preview", new Color(1f, 0.38f, 0.08f, 0.86f));
            if (missileChannel == null)
                missileChannel = CreateChannel("Missile Trajectory Preview", new Color(0.12f, 0.72f, 1f, 0.86f));
        }

        private void Update()
        {
            if (bombLauncher != null
                && bombLauncher.TryGetLaunchPrediction(out Vector2 bombOrigin, out Vector2 bombVelocity))
                RefreshChannel(bombChannel, bombLauncher.transform, bombOrigin, bombVelocity, true);
            else SetVisible(bombChannel, false);

            if (missileLauncher != null
                && missileLauncher.TryGetLaunchPrediction(out Vector2 missileOrigin, out Vector2 missileVelocity))
                RefreshChannel(missileChannel, missileLauncher.transform, missileOrigin, missileVelocity, false);
            else SetVisible(missileChannel, false);
        }

        private Channel CreateChannel(string objectName, Color color)
        {
            GameObject root = new GameObject(objectName);
            root.transform.SetParent(transform, false);
            Channel channel = new Channel { Root = root.transform };
            for (int i = 0; i < 56; i++)
            {
                GameObject dot = new GameObject("Prediction Dot " + (i + 1));
                dot.transform.SetParent(root.transform, false);
                dot.transform.localScale = Vector3.one * (i % 4 == 0 ? 0.145f : 0.105f);
                SpriteRenderer renderer = dot.AddComponent<SpriteRenderer>();
                renderer.sprite = DoodleRuntimeAssets.CircleSprite;
                renderer.color = color;
                renderer.sortingOrder = 54;
                channel.Dots.Add(renderer);
            }

            GameObject impact = new GameObject("Predicted Impact");
            impact.transform.SetParent(root.transform, false);
            channel.Impact = impact.AddComponent<SpriteRenderer>();
            channel.Impact.sprite = DoodleRuntimeAssets.CircleSprite;
            channel.Impact.color = new Color(color.r, color.g, color.b, 0.34f);
            channel.Impact.sortingOrder = 53;
            impact.SetActive(false);
            return channel;
        }

        private void RefreshChannel(
            Channel channel,
            Transform launcher,
            Vector2 origin,
            Vector2 initialVelocity,
            bool ballistic)
        {
            if (channel == null) return;
            SetVisible(channel, true);
            Vector2 position = origin;
            Vector2 velocity = initialVelocity;
            const float stepSeconds = 0.04f;
            int dotIndex = 0;
            bool impacted = false;
            Vector2 impactPoint = position;

            for (int step = 0; step < 110 && dotIndex < channel.Dots.Count; step++)
            {
                Vector2 nextVelocity = ballistic ? velocity + Physics2D.gravity * stepSeconds : velocity;
                if (ballistic) nextVelocity /= 1f + 0.12f * stepSeconds;
                Vector2 nextPosition = position + (velocity + nextVelocity) * (0.5f * stepSeconds);
                if (TryFindTerrainHit(position, nextPosition, launcher, out Vector2 hitPoint))
                {
                    impactPoint = hitPoint;
                    impacted = true;
                    break;
                }

                SpriteRenderer dot = channel.Dots[dotIndex++];
                dot.transform.position = new Vector3(nextPosition.x, nextPosition.y, -0.46f);
                dot.enabled = true;
                position = nextPosition;
                velocity = nextVelocity;
                if (Mathf.Abs(position.x) > horizontalLimit || position.y < minimumY || position.y > maximumY) break;
            }

            for (int i = dotIndex; i < channel.Dots.Count; i++) channel.Dots[i].enabled = false;
            channel.Impact.gameObject.SetActive(impacted);
            if (!impacted) return;
            channel.Impact.transform.position = new Vector3(impactPoint.x, impactPoint.y, -0.45f);
            float pulse = 0.62f + Mathf.Sin(Time.unscaledTime * 7f) * 0.1f;
            channel.Impact.transform.localScale = new Vector3(pulse * 1.55f, pulse * 0.42f, 1f);
        }

        private static bool TryFindTerrainHit(Vector2 from, Vector2 to, Transform launcher, out Vector2 point)
        {
            RaycastHit2D[] hits = Physics2D.LinecastAll(from, to);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D collider = hits[i].collider;
                if (collider == null || collider.isTrigger || collider.gameObject.layer != 6) continue;
                if (launcher != null && (collider.transform == launcher || collider.transform.IsChildOf(launcher))) continue;
                point = hits[i].point;
                return true;
            }
            point = to;
            return false;
        }

        private static void SetVisible(Channel channel, bool visible)
        {
            if (channel?.Root != null) channel.Root.gameObject.SetActive(visible);
        }
    }
}
