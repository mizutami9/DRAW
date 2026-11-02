using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DrawBody.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DrawBody.EditorTools
{
    // Run with Unity -batchmode -projectPath ... -executeMethod
    // DrawBody.EditorTools.CosmicRevivalRegression.Run -logFile ... (without -quit).
    [InitializeOnLoad]
    public static class CosmicRevivalRegression
    {
        private const string Pending = "PICO.CosmicRevivalRegression.Pending";
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static;
        private static int assertions;

        static CosmicRevivalRegression()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            try
            {
                TestPawPositions();
                TestRevival();
                RainGoalRegression.RunChecks();
                CircuitBoxRegression.RunChecks();
                WeatherDirectionRegression.RunChecks();
                Shader shader = Resources.Load<Shader>("Shaders/PlayerCrayonTint");
                Check(shader != null && !ShaderUtil.ShaderHasError(shader), "crayon shader imports without errors");
                Debug.Log("[COSMIC REGRESSION] PASS " + assertions + " assertions");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("[COSMIC REGRESSION] FAIL");
                EditorApplication.Exit(1);
            }
        }

        private static void TestPawPositions()
        {
            for (int count = 1; count <= 8; count++)
            {
                Vector2[] positions = (Vector2[])InvokeStatic("CreatePawStampTargets", count, 761 + count, 3.4f);
                Vector2[] replay = (Vector2[])InvokeStatic("CreatePawStampTargets", count, 761 + count, 3.4f);
                Check(positions.Length == count, "paw volley size " + count);
                for (int i = 0; i < positions.Length; i++)
                {
                    Check(positions[i] == replay[i], "paw targets reproducible on peers");
                    Check(Mathf.Abs(positions[i].x) <= 15.1f && positions[i].y >= -6.6f
                        && positions[i].y <= 6.1f, "paw stays inside arena");
                    for (int j = 0; j < i; j++)
                        Check(Vector2.Distance(positions[i], positions[j]) > 2f, "paw targets distinct");
                }
            }
        }

        private static void TestRevival()
        {
            GameObject fixture = new GameObject("Cosmic Regression Fixture");
            GameObject managerObject = new GameObject("Inactive Test Manager");
            managerObject.transform.SetParent(fixture.transform);
            managerObject.SetActive(false);
            StageManager manager = managerObject.AddComponent<StageManager>();
            Set(manager, "currentStageId", "15-3");
            Set(manager, "stageStarted", true);
            Set(manager, "titleMode", false);
            Set(manager, "drawing", false);
            Set(manager, "challengeRunStarted", true);
            SetEnum(manager, "stageRuleMode", "Survival");

            PlayerController2D[] players = new PlayerController2D[3];
            List<PlayerController2D> locals = (List<PlayerController2D>)Get(manager, "localPlayers");
            for (int i = 0; i < players.Length; i++)
            {
                GameObject obj = new GameObject("Test Player " + i);
                obj.transform.SetParent(fixture.transform);
                obj.AddComponent<Rigidbody2D>().gravityScale = 0f;
                obj.AddComponent<BoxCollider2D>().size = Vector2.one;
                obj.AddComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>("StageObjects/NicoDraw/power-crayon-triple");
                players[i] = obj.AddComponent<PlayerController2D>();
                Set(players[i], "stageManager", manager);
                locals.Add(players[i]);
            }
            Set(manager, "primaryPlayer", players[0]);
            Set(manager, "player", players[0]);
            GameObject bossObject = new GameObject("Test Boss Controller");
            bossObject.transform.SetParent(fixture.transform);
            StageCosmicMimicBossController controller = bossObject.AddComponent<StageCosmicMimicBossController>();
            Set(controller, "stageManager", manager);
            SetEnum(controller, "state", "Active");
            var roster = (List<string>)Get(controller, "orderedPlayerIds");
            string[] ids = new string[players.Length];
            for (int i = 0; i < ids.Length; i++) { ids[i] = "local_" + players[i].GetInstanceID(); roster.Add(ids[i]); }

            GameObject bossAnchor = new GameObject("Test Cat Boss");
            bossAnchor.transform.SetParent(bossObject.transform);
            Set(controller, "bossRoot", bossAnchor.transform);
            SetEnum(controller, "bossSpecies", "Cat");
            Set(controller, "remaining", 15f);
            Type attackType = Nested("AttackType");
            for (int volley = 1; volley <= 4; volley++)
            {
                Invoke(controller, "ScheduleEnvironmentalAttack", Enum.Parse(attackType, "CatPawStamp"));
                object packet = Get(controller, "currentAttack");
                Check(((Vector2[])Get(packet, "Targets")).Length == volley, "scheduled cat volley grows " + volley);
            }

            Invoke(controller, "UpdateReviveDrops");
            Check(Drops(controller).Count == 0, "no crayon while everybody lives");
            Invoke(controller, "Eliminate", ids[1], players[1]);
            Invoke(controller, "Eliminate", ids[2], players[2]);
            Check(manager.IsPlayerEliminated(players[1]), "death registered for Q selection");

            object drop = NewNested("ReviveDropSnapshot");
            Set(drop, "Id", 100);
            Set(drop, "TargetId", ids[1]);
            Set(drop, "Position", new Vector2(10f, 0f));
            Set(drop, "Color", PlayerColorPalette.GetColor(1));
            Invoke(controller, "CreateReviveDrop", drop);
            Transform crayon = (Transform)Get(Drops(controller)[100], "Root");
            Check(crayon.GetComponent<SpriteRenderer>().sprite == Resources.Load<Sprite>(
                "StageObjects/NicoDraw/power-crayon-triple"), "same 15-1 crayon artwork reused");
            Check(crayon.GetComponent<SpriteRenderer>().color == PlayerColorPalette.GetColor(1), "crayon matches target player color");
            Check(!(bool)Invoke(controller, "TryCollectReviveDrop", 100, ids[0], 0.85f), "distant pickup rejected");
            Check(!(bool)Invoke(controller, "TryCollectReviveDrop", 100, ids[2], 0.85f), "dead collector rejected");
            players[0].transform.position = new Vector2(10f, 0f);
            Physics2D.SyncTransforms();
            Check((bool)Invoke(controller, "TryCollectReviveDrop", 100, ids[0], 0.85f), "living player collects and revives teammate");
            Check(!manager.IsPlayerEliminated(players[1]) && manager.IsPlayerEliminated(players[2]), "only matching color target revived");
            Check(players[1].GetComponent<Rigidbody2D>().simulated && players[1].GetComponent<BoxCollider2D>().enabled,
                "revived physics and colliders restored");
            Check(manager.ActivePlayerController == players[0] && !players[1].ControlsEnabled,
                "revival preserves single-player control selection");
            Check(Drops(controller).Count == 0, "collected crayon removed");
            Check(!(bool)Invoke(controller, "TryCollectReviveDrop", 100, ids[0], 0.85f), "duplicate pickup has no effect");
            Invoke(controller, "Eliminate", ids[1], players[1]);
            Check(!manager.IsPlayerEliminated(players[1]), "revival grace prevents instant death");
            ((IDictionary)Get(controller, "reviveProtectionUntil")).Clear();
            object staleHit = NewNested("HitRequest");
            Set(staleHit, "PlayerId", ids[1]);
            Set(staleHit, "LifeRevision", 0);
            Invoke(controller, "HandleNetworkData", new OnlineGimmickData
            {
                ObjectId = "15-3", Kind = "cosmic_mimic_hit", PlayerId = ids[1], Json = JsonUtility.ToJson(staleHit)
            });
            Check(!manager.IsPlayerEliminated(players[1]), "old-life hit rejected even after grace expires");

            Array snapshots = (Array)Invoke(controller, "CaptureRevivals");
            Type reviveType = Nested("ReviveSnapshot");
            object serialized = JsonUtility.FromJson(JsonUtility.ToJson(snapshots.GetValue(0)), reviveType);
            Array wire = Array.CreateInstance(reviveType, 1);
            wire.SetValue(serialized, 0);
            players[1].transform.position = new Vector2(2f, 1f);
            Invoke(controller, "ApplyRevivals", wire, new string[0]);
            Check((Vector2)players[1].transform.position == new Vector2(2f, 1f), "duplicate snapshot does not teleport survivor");

            // Simulate a receiver which missed the pickup packet and only has the
            // next complete snapshot, using the production JSON payload and apply path.
            ((IDictionary)Get(controller, "reviveProtectionUntil")).Clear();
            Invoke(controller, "Eliminate", ids[1], players[1]);
            ((IDictionary)Get(controller, "appliedLifeRevisions")).Clear();
            Invoke(controller, "ApplyRevivals", wire, new string[0]);
            Check(!manager.IsPlayerEliminated(players[1]), "serialized snapshot recovers missed revival");
            Check((int)Invoke(controller, "GetLifeRevision", ids[1]) == 1, "new life revision rejects old death requests");

            ((IDictionary)Get(controller, "reviveProtectionUntil")).Clear();
            Invoke(controller, "Eliminate", ids[1], players[1]);
            ((IDictionary)Get(controller, "appliedLifeRevisions")).Clear();
            Invoke(controller, "ApplyRevivals", wire, new[] { ids[1] });
            Check(manager.IsPlayerEliminated(players[1]), "later death snapshot never resurrects from old revival");

            Set(drop, "Id", 101);
            Invoke(controller, "CreateReviveDrop", drop);
            players[0].transform.position = new Vector2(-10f, 0f);
            Invoke(controller, "Eliminate", ids[0], players[0]);
            Invoke(controller, "UpdateReviveDrops");
            Check(Drops(controller).Count == 0, "total wipe clears rescue items");
            Set(manager, "stageStarted", false);
            Object.DestroyImmediate(fixture);
        }

        private static IDictionary Drops(object controller) => (IDictionary)Get(controller, "reviveDrops");
        private static Type Nested(string name) => typeof(StageCosmicMimicBossController).GetNestedType(name, BindingFlags.NonPublic);
        private static object NewNested(string name) => Activator.CreateInstance(Nested(name));
        private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private static void SetEnum(object target, string name, string value)
        {
            FieldInfo field = target.GetType().GetField(name, Flags);
            field.SetValue(target, Enum.Parse(field.FieldType, value));
        }
        private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        private static object InvokeStatic(string name, params object[] args) => typeof(StageCosmicMimicBossController).GetMethod(name, Flags).Invoke(null, args);
        private static void Check(bool condition, string description)
        {
            if (!condition) throw new Exception("[COSMIC REGRESSION] " + description);
            assertions++;
        }
    }
}
