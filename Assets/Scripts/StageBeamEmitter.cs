using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageBeamEmitter : MonoBehaviour, IStageLinkActivatable
    {
        private const float MaximumRange = 120f;
        private const float PulseDuration = 0.28f;

        private readonly HashSet<PlayerController2D> hitPlayers = new HashSet<PlayerController2D>();
        private readonly SpriteRenderer[] streamPackets = new SpriteRenderer[6];
        private Transform muzzle;
        private LineRenderer beamLine;
        private Transform chargeFill;
        private SpriteRenderer chargeFillRenderer;
        private SpriteRenderer readyLamp;
        private StageManager stageManager;
        private float chargeGaugeWidth;
        private float readyLampBaseSize = 0.13f;
        private float interval = 2f;
        private float beamRange = MaximumRange;
        private float beamWidth = 0.14f;
        private float nextShotTime;
        private float hideTime;
        private bool linkedMode;
        private bool piercesTerrain;
        private bool continuousBeam;
        private float nextContinuousHitReset;
        private float currentBeamDistance;
        private SpriteRenderer muzzleFlash;

        public void Configure(
            Transform beamMuzzle,
            LineRenderer line,
            Transform gaugeFill,
            SpriteRenderer gaugeRenderer,
            SpriteRenderer lampRenderer,
            float gaugeWidth,
            float lampBaseSize,
            float seconds,
            bool terrainPiercing,
            float configuredRange,
            float configuredBeamWidth,
            bool continuous)
        {
            muzzle = beamMuzzle;
            beamLine = line;
            chargeFill = gaugeFill;
            chargeFillRenderer = gaugeRenderer;
            readyLamp = lampRenderer;
            chargeGaugeWidth = Mathf.Max(0.1f, gaugeWidth);
            readyLampBaseSize = Mathf.Max(0.08f, lampBaseSize);
            interval = Mathf.Clamp(seconds > 0f ? seconds : 2f, 0.5f, 10f);
            piercesTerrain = terrainPiercing;
            continuousBeam = continuous;
            beamRange = configuredRange > 0f
                ? Mathf.Clamp(configuredRange, 1f, MaximumRange)
                : MaximumRange;
            beamWidth = Mathf.Clamp(configuredBeamWidth > 0f ? configuredBeamWidth : 0.14f, 0.08f, 1.2f);
            if (beamLine != null)
            {
                beamLine.startWidth = beamWidth;
                beamLine.endWidth = beamWidth * 0.72f;
            }
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
            if (continuousBeam)
            {
                CreateContinuousVisuals();
                nextContinuousHitReset = Time.time + 0.35f;
                SetBeamVisible(true);
                UpdateChargeVisual(1f);
                UpdateActiveBeam();
                UpdateContinuousVisuals();
            }
            else
            {
                SetBeamVisible(false);
                UpdateChargeVisual(0f);
            }
        }

        private void Update()
        {
            if (continuousBeam)
            {
                if (Time.time >= nextContinuousHitReset)
                {
                    nextContinuousHitReset = Time.time + 0.35f;
                    hitPlayers.Clear();
                }
                SetBeamVisible(true);
                UpdateChargeVisual(1f);
                UpdateActiveBeam();
                UpdateContinuousVisuals();
                return;
            }

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

            if (linkedMode)
            {
                UpdateChargeVisual(1f);
                return;
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

        public void PrepareForLink()
        {
            linkedMode = true;
            SetBeamVisible(false);
            UpdateChargeVisual(1f);
        }

        public void ActivateFromLink()
        {
            Fire();
            UpdateChargeVisual(1f);
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
            RaycastHit2D[] hits = beamWidth > 0.2f
                ? Physics2D.CircleCastAll(origin, beamWidth * 0.45f, direction, beamRange)
                : Physics2D.RaycastAll(origin, direction, beamRange);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            float beamDistance = beamRange;
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

                if (hitCollider.GetComponentInParent<StageEscortFriend>() != null)
                {
                    continue;
                }

                if (hitCollider.isTrigger)
                {
                    continue;
                }

                if (piercesTerrain)
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
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }


                StageEscortFriend escortFriend = hitCollider.GetComponentInParent<StageEscortFriend>();
                if (escortFriend != null)
                {
                    escortFriend.Defeat();
                    continue;
                }

                if (!IsVisiblePlayerCollider(hitCollider)) continue;

                PlayerController2D player = hitCollider.GetComponentInParent<PlayerController2D>();
                if (player == null || player.IsTurtleShelled || !hitPlayers.Add(player))
                {
                    continue;
                }

                stageManager?.RespawnFromHazard(player);
            }

            if (beamLine != null)
            {
                currentBeamDistance = Mathf.Max(0.05f, beamDistance);
                beamLine.SetPosition(0, origin);
                beamLine.SetPosition(1, origin + direction * currentBeamDistance);
                beamLine.enabled = true;
            }
        }

        private void CreateContinuousVisuals()
        {
            if (muzzle == null || streamPackets[0] != null) return;
            for (int i = 0; i < streamPackets.Length; i++)
            {
                GameObject packet = new GameObject($"Beam Stream Packet {i + 1}");
                packet.transform.SetParent(transform, false);
                SpriteRenderer renderer = packet.AddComponent<SpriteRenderer>();
                renderer.sprite = DoodleRuntimeAssets.CircleSprite;
                renderer.color = i % 2 == 0
                    ? new Color(1f, 0.9f, 0.32f, 0.96f)
                    : new Color(1f, 0.34f, 0.08f, 0.92f);
                renderer.sortingOrder = 42;
                streamPackets[i] = renderer;
            }

            GameObject flash = new GameObject("Continuous Beam Muzzle Flash");
            flash.transform.SetParent(muzzle, false);
            flash.transform.localPosition = Vector3.zero;
            muzzleFlash = flash.AddComponent<SpriteRenderer>();
            muzzleFlash.sprite = DoodleRuntimeAssets.CircleSprite;
            muzzleFlash.color = new Color(1f, 0.8f, 0.16f, 0.9f);
            muzzleFlash.sortingOrder = 43;
        }

        private void UpdateContinuousVisuals()
        {
            if (muzzle == null || beamLine == null) return;
            float visualTime = SharedVisualTime();
            float rapidPulse = 0.5f + Mathf.Sin(visualTime * 18f + transform.position.x * 0.17f) * 0.5f;
            float width = beamWidth * Mathf.Lerp(0.82f, 1.08f, rapidPulse);
            beamLine.startWidth = width;
            beamLine.endWidth = width * Mathf.Lerp(0.62f, 0.82f, rapidPulse);
            beamLine.startColor = Color.Lerp(
                new Color(1f, 0.12f, 0.04f, 0.92f),
                new Color(1f, 0.78f, 0.16f, 1f),
                rapidPulse);
            beamLine.endColor = Color.Lerp(
                new Color(1f, 0.05f, 0.02f, 0.82f),
                new Color(1f, 0.38f, 0.06f, 0.96f),
                rapidPulse);

            Vector2 origin = muzzle.position;
            Vector2 direction = transform.right.normalized;
            float usableDistance = Mathf.Max(0.1f, currentBeamDistance - 0.15f);
            for (int i = 0; i < streamPackets.Length; i++)
            {
                SpriteRenderer packet = streamPackets[i];
                if (packet == null) continue;
                float phase = Mathf.Repeat(visualTime * 2.35f + i / (float)streamPackets.Length, 1f);
                packet.transform.position = origin + direction * (0.1f + phase * usableDistance);
                packet.transform.rotation = transform.rotation;
                float packetLength = beamWidth * Mathf.Lerp(1.05f, 1.65f, 1f - phase);
                packet.transform.localScale = new Vector3(packetLength, beamWidth * 0.26f, 1f);
                packet.color = i % 2 == 0
                    ? new Color(1f, 0.94f, 0.45f, Mathf.Lerp(1f, 0.25f, phase))
                    : new Color(1f, 0.3f, 0.06f, Mathf.Lerp(0.9f, 0.18f, phase));
                packet.enabled = beamLine.enabled;
            }

            if (muzzleFlash != null)
            {
                float flashSize = beamWidth * Mathf.Lerp(0.75f, 1.35f, rapidPulse);
                muzzleFlash.transform.localScale = new Vector3(flashSize * 0.7f, flashSize, 1f);
                muzzleFlash.enabled = beamLine.enabled;
            }
        }

        private static float SharedVisualTime()
        {
            // UTC keeps the cosmetic stream phase approximately aligned across
            // clients. Gameplay does not depend on this value: continuous beams
            // are active every frame on every peer.
            long ticks = DateTime.UtcNow.Ticks % TimeSpan.TicksPerMinute;
            return ticks / (float)TimeSpan.TicksPerSecond;
        }

        private void SetBeamVisible(bool visible)
        {
            if (beamLine != null)
            {
                beamLine.enabled = visible;
            }
            for (int i = 0; i < streamPackets.Length; i++)
                if (streamPackets[i] != null) streamPackets[i].enabled = visible;
            if (muzzleFlash != null) muzzleFlash.enabled = visible;
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
                float flash = 0.72f + (Mathf.Sin(SharedVisualTime() * 20f) + 1f) * 0.14f;
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
                float pulse = nearlyReady ? 1f + Mathf.Sin(SharedVisualTime() * 20f) * 0.18f : 1f;
                readyLamp.transform.localScale = Vector3.one * pulse * readyLampBaseSize;
            }
        }
    }
}
