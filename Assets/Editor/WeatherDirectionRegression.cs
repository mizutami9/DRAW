using System;
using System.Collections;
using System.Reflection;
using DrawBody.Prototype;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DrawBody.EditorTools
{
    public static class WeatherDirectionRegression
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static int assertions;
        public static void RunChecks()
        {
            CheckCenterIcicleSchedule();
            GameObject root = new GameObject("Weather Regression");
            var storm = root.AddComponent<StageIcicleSeesawSurvivalController>();
            GameObject actor = new GameObject("Wind Test Player");
            actor.transform.SetParent(root.transform);
            Rigidbody2D body = actor.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            actor.AddComponent<BoxCollider2D>();
            var player = actor.AddComponent<PlayerController2D>();
            Set(storm, "blizzardStrength", 2f);
            Call(storm, "BuildBlizzardVisual");
            foreach (Vector2 direction in new[] { Vector2.left, new Vector2(-0.94f, -0.34f).normalized, Vector2.right })
            {
                Set(storm, "blizzardDirection", direction);
                foreach (bool motorFirst in new[] { false, true })
                {
                    player.ResetMotion();
                    body.linearVelocity = Vector2.right * 7f;
                    for (int step = 0; step < 30; step++)
                    {
                        if (motorFirst) Call(player, "Move");
                        Call(storm, "ApplyBlizzardPush");
                        if (!motorFirst) Call(player, "Move");
                    }
                    Check(Mathf.Sign(body.linearVelocity.x) == Mathf.Sign(direction.x), "released input drifts downwind, regardless of previous run or update order");
                    Check(Mathf.Abs(body.linearVelocity.x) <= 2.2f, "wind drift stays bounded");
                    player.SetScriptedInput(-Mathf.Sign(direction.x), false);
                    for (int step = 0; step < 15; step++) { Call(storm, "ApplyBlizzardPush"); Call(player, "Move"); }
                    Check(Mathf.Sign(body.linearVelocity.x) == -Mathf.Sign(direction.x), "player can still move against the wind");
                    player.SetScriptedInput(0f, false);
                    Set(player, "environmentalWindUntil", -100f);
                    for (int step = 0; step < 15; step++) Call(player, "Move");
                    Check(Mathf.Abs(body.linearVelocity.x) < 0.001f, "expired wind leaves no stale drift");
                }
                SetEnum(storm, "blizzardPhase", "Active");
                Call(storm, "RefreshBlizzardPresentation");
                var streak = (LineRenderer)((IList)Get(storm, "blizzardLines"))[0];
                Check(Vector2.Dot(streak.GetPosition(1) - streak.GetPosition(0), direction) < 0f,
                    "fading streak trails behind the downwind moving head");
                Call(storm, "SpawnSnowParticle", 1);
                IList snow = (IList)Get(storm, "snowParticles");
                var particle = (Component)snow[snow.Count - 1];
                Check(Mathf.Sign(particle.GetComponent<Rigidbody2D>().linearVelocity.x) == Mathf.Sign(direction.x),
                    "actual snow particle velocity matches force direction");
            }
            Object.DestroyImmediate(root);

            root = new GameObject("Equipment Regression");
            var factory = root.AddComponent<StageObjectFactory>();
            var rain = root.AddComponent<StageUmbrellaRainController>();
            Set(rain, "objectFactory", factory);
            Call(rain, "CreateSpawnEquipment");
            StageData data = JsonUtility.FromJson<StageData>(Resources.Load<TextAsset>("Stages/14-2").text);
            StageObjectData floor = Array.Find(data.objects, item => item.objectId == "14-2_lower_01");
            StageObjectData pad = Array.Find(data.objects, item => item.objectId == "14-2_pad_00");
            Physics2D.SyncTransforms();
            StageEditorObject[] weapons = root.GetComponentsInChildren<StageEditorObject>();
            Check(weapons.Length == 2, "both weapons created");
            foreach (StageEditorObject weapon in weapons)
            {
                Bounds bounds = weapon.GetComponent<Collider2D>().bounds;
                Check(bounds.min.x > pad.position.x + pad.size.x / 2f
                    && bounds.min.x > floor.position.x - floor.size.x / 2f
                    && bounds.max.x < floor.position.x + floor.size.x / 2f,
                    "weapon fully over first platform after spring");
                Check(bounds.min.y >= floor.position.y + floor.size.y / 2f, "weapon starts above the floor without penetration");
            }
            Call(rain, "CreateSpawnEquipment");
            Check(root.GetComponentsInChildren<StageEditorObject>().Length == 2, "setup never duplicates weapons");
            Object.DestroyImmediate(root);
            Debug.Log("[WEATHER REGRESSION] PASS " + assertions + " assertions");
        }

        private static void CheckCenterIcicleSchedule()
        {
            GameObject root = new GameObject("Center Icicle Regression");
            var storm = root.AddComponent<StageIcicleSeesawSurvivalController>();
            Set(storm, "elapsedSeconds", 15f);
            Call(storm, "DirectCenterIcicle");
            Check((int)Get(storm, "hazardSequence") == 0, "no center attack in waiting room");
            SetEnum(storm, "phase", "Running");
            Set(storm, "elapsedSeconds", 8.99f);
            Call(storm, "DirectCenterIcicle");
            Check((int)Get(storm, "hazardSequence") == 0, "first warning waits until nine seconds");
            for (int drop = 1; drop <= 5; drop++)
            {
                Set(storm, "elapsedSeconds", drop * 10f - 1f);
                int before = (int)Get(storm, "hazardSequence");
                Call(storm, "DirectCenterIcicle");
                Check((int)Get(storm, "hazardSequence") == before + 1, "one center warning per ten-second interval");
                LineRenderer[] lines = root.GetComponentsInChildren<LineRenderer>();
                Check(Mathf.Abs(lines[lines.Length - 1].GetPosition(0).x) < 0.001f, "warning targets exact seesaw center");
                Check((float)Get(storm, "nextCenterIcicleAt") == (drop + 1) * 10f, "next center deadline stays aligned");
                Call(storm, "DirectCenterIcicle");
                Check((int)Get(storm, "hazardSequence") == before + 1, "same frame never duplicates center drop");
                Call(storm, "IssueNextHazard", 0.5f, false);
                Check((int)Get(storm, "randomHazardSequence") == drop, "center attack does not consume random attack sequence");
                Check((int)Get(storm, "hazardSequence") == before + 2, "random attack remains additional with unique network ID");
            }
            Set(storm, "elapsedSeconds", 105f);
            Call(storm, "DirectCenterIcicle");
            int afterStall = (int)Get(storm, "hazardSequence");
            Call(storm, "DirectCenterIcicle");
            Check((int)Get(storm, "hazardSequence") == afterStall, "stall never queues a burst of missed attacks");
            SetEnum(storm, "phase", "Finished");
            Set(storm, "elapsedSeconds", 120f);
            Call(storm, "DirectCenterIcicle");
            Check((int)Get(storm, "hazardSequence") == afterStall, "no center attack after clear");
            Object.DestroyImmediate(root);
        }
        private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        private static void SetEnum(object target, string name, string value)
        {
            FieldInfo field = target.GetType().GetField(name, Flags);
            field.SetValue(target, Enum.Parse(field.FieldType, value));
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("[WEATHER REGRESSION] " + message);
            assertions++;
        }
    }
}
