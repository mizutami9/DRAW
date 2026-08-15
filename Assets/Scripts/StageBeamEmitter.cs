using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageBeamEmitter : MonoBehaviour
    {
        private const float MaximumRange = 120f;
        private const float PulseDuration = 0.28f;

        private readonly HashSet<PlayerController2D> hitPlayers = new HashSet<PlayerController2D>();
        private Transform muzzle;
        private LineRenderer beamLine;
        private Transform chargeFill;
        private SpriteRenderer chargeFillRenderer;
        private SpriteRenderer readyLamp;
        private StageManager stageManager;
        private float chargeGaugeWidth;
        private float interval = 2f;
        private float nextShotTime;
        private float hideTime;

        public void Configure(
            Transform beamMuzzle,
            LineRenderer line,
            Transform gaugeFill,
            SpriteRenderer gaugeRenderer,
            SpriteRenderer lampRenderer,
            float gaugeWidth,
            float seconds)
        {
            muzzle = beamMuzzle;
            beamLine = line;
            chargeFill = gaugeFill;
            chargeFillRenderer = gaugeRenderer;
            readyLamp = lampRenderer;
            chargeGaugeWidth = Mathf.Max(0.1f, gaugeWidth);
            interval = Mathf.Clamp(seconds > 0f ? seconds : 2f, 0.5f, 10f);
        }

        private void Start()
        {
            RuntimeStageEditor editor = FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing)
            {
                enabled = false;
                return;
            }

            stageManager = FindFirstObjectByType<StageManager>();
            nextShotTime = Time.time + interval;
            SetBeamVisible(false);
            UpdateChargeVisual(0f);
        }

        private void Update()
        {
            if (beamLine != null && beamLine.enabled)
            {
                if (Time.time >= hideTime)
                {
                    SetBeamVisible(false);
                }
                else
                {
                    UpdateActiveBeam();
                }
            }

            float charge = 1f - Mathf.Clamp01((nextShotTime - Time.time) / interval);
            UpdateChargeVisual(charge);

            if (muzzle == null || Time.time < nextShotTime)
            {
                return;
            }

            nextShotTime = Time.time + interval;
            Fire();
            UpdateChargeVisual(0f);
        }

        private void OnDisable()
        {
            SetBeamVisible(false);
        }

        private void Fire()
        {
            hitPlayers.Clear();
            hideTime = Time.time + PulseDuration;
            SetBeamVisible(true);
            GameSfx.PlayAt(SfxId.BeamFire, muzzle != null ? muzzle.position : transform.position);
            UpdateActiveBeam();
        }

        private void UpdateActiveBeam()
        {
            if (muzzle == null)
            {
                SetBeamVisible(false);
                return;
            }

            Vector2 origin = muzzle.position;
            Vector2 direction = transform.right.normalized;
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, MaximumRange);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            float beamDistance = MaximumRange;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hitCollider = hits[i].collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                PlayerController2D player = hitCollider.GetComponentInParent<PlayerController2D>();
                if (player != null)
                {
                    if (player.IsTurtleShelled)
                    {
                        beamDistance = Mathf.Min(beamDistance, hits[i].distance);
                        break;
                    }
                    continue;
                }

                if (hitCollider.isTrigger)
                {
                    continue;
                }

                beamDistance = hits[i].distance;
                break;
            }

            // Resolve damage only after the closest wall/shell blocker is known.
            // This prevents a player collider behind a wall from ever winning due
            // to collider ordering or overlapping hand-drawn segments.
            for (int i = 0; i < hits.Length && hits[i].distance <= beamDistance + 0.001f; i++)
            {
                Collider2D hitCollider = hits[i].collider;
                if (hitCollider == null
                    || hitCollider.transform.IsChildOf(transform)
                    || !IsVisiblePlayerCollider(hitCollider))
                {
                    continue;
                }

                PlayerController2D player = hitCollider.GetComponentInParent<PlayerController2D>();
                if (player == null || player.IsTurtleShelled || !hitPlayers.Add(player))
                {
                    continue;
                }

                stageManager?.RespawnFromHazard(player);
            }

            if (beamLine != null)
            {
                beamLine.SetPosition(0, origin);
                beamLine.SetPosition(1, origin + direction * Mathf.Max(0.05f, beamDistance));
                beamLine.enabled = true;
            }
        }

        private void SetBeamVisible(bool visible)
        {
            if (beamLine != null)
            {
                beamLine.enabled = visible;
            }
        }

        private static bool IsVisiblePlayerCollider(Collider2D collider)
        {
            PlayerController2D player = collider != null
                ? collider.GetComponentInParent<PlayerController2D>()
                : null;
            if (player == null)
            {
                return false;
            }

            LineRenderer line = collider.GetComponent<LineRenderer>();
            if (line != null && !line.enabled)
            {
                return false;
            }

            SpriteRenderer sprite = collider.GetComponent<SpriteRenderer>();
            return sprite == null || sprite.enabled;
        }

        private void UpdateChargeVisual(float charge)
        {
            float normalized = Mathf.Clamp01(charge);
            if (chargeFill != null)
            {
                Vector3 scale = chargeFill.localScale;
                scale.x = chargeGaugeWidth * normalized;
                chargeFill.localScale = scale;
                Vector3 position = chargeFill.localPosition;
                position.x = -chargeGaugeWidth * 0.5f + scale.x * 0.5f;
                chargeFill.localPosition = position;
            }

            Color color = normalized < 0.55f
                ? Color.Lerp(new Color(0.1f, 0.65f, 0.95f, 1f), new Color(1f, 0.82f, 0.08f, 1f), normalized / 0.55f)
                : Color.Lerp(new Color(1f, 0.82f, 0.08f, 1f), new Color(1f, 0.1f, 0.04f, 1f), (normalized - 0.55f) / 0.45f);
            bool nearlyReady = normalized >= 0.82f;
            if (nearlyReady)
            {
                float flash = 0.72f + (Mathf.Sin(Time.unscaledTime * 20f) + 1f) * 0.14f;
                color = Color.Lerp(color, Color.white, flash * 0.32f);
            }

            if (chargeFillRenderer != null)
            {
                chargeFillRenderer.color = color;
            }
            if (readyLamp != null)
            {
                readyLamp.color = normalized < 0.25f
                    ? new Color(0.14f, 0.34f, 0.4f, 1f)
                    : nearlyReady
                        ? color
                        : new Color(1f, 0.58f, 0.06f, 1f);
                float pulse = nearlyReady ? 1f + Mathf.Sin(Time.unscaledTime * 20f) * 0.18f : 1f;
                readyLamp.transform.localScale = Vector3.one * pulse * 0.13f;
            }
        }
    }
}
