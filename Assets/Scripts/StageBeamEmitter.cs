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
        private StageManager stageManager;
        private float interval = 2f;
        private float nextShotTime;
        private float hideTime;

        public void Configure(Transform beamMuzzle, LineRenderer line, float seconds)
        {
            muzzle = beamMuzzle;
            beamLine = line;
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
        }

        private void Update()
        {
            if (beamLine != null && beamLine.enabled && Time.time >= hideTime)
            {
                SetBeamVisible(false);
            }

            if (muzzle == null || Time.time < nextShotTime)
            {
                return;
            }

            nextShotTime = Time.time + interval;
            Fire();
        }

        private void OnDisable()
        {
            SetBeamVisible(false);
        }

        private void Fire()
        {
            Vector2 origin = muzzle.position;
            Vector2 direction = transform.right.normalized;
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, MaximumRange);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            hitPlayers.Clear();
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
                    if (!hitPlayers.Add(player))
                    {
                        continue;
                    }

                    if (player.IsTurtleShelled)
                    {
                        beamDistance = hits[i].distance;
                        break;
                    }

                    stageManager?.RespawnFromHazard(player);
                    continue;
                }

                if (hitCollider.isTrigger)
                {
                    continue;
                }

                beamDistance = hits[i].distance;
                break;
            }

            if (beamLine != null)
            {
                beamLine.SetPosition(0, origin);
                beamLine.SetPosition(1, origin + direction * Mathf.Max(0.05f, beamDistance));
                beamLine.enabled = true;
                hideTime = Time.time + PulseDuration;
            }
        }

        private void SetBeamVisible(bool visible)
        {
            if (beamLine != null)
            {
                beamLine.enabled = visible;
            }
        }
    }
}
