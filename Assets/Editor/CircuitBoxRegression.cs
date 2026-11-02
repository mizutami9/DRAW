using System;
using System.Collections;
using System.Reflection;
using DrawBody.Prototype;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DrawBody.EditorTools
{
    public static class CircuitBoxRegression
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static int assertions;

        public static void RunChecks()
        {
            StageData data = JsonUtility.FromJson<StageData>(Resources.Load<TextAsset>("Stages/13-3").text);
            for (int count = 1; count <= 4; count++) CheckStage(data, count);
            Debug.Log("[CIRCUIT BOX REGRESSION] PASS " + assertions + " assertions");
        }

        private static void CheckStage(StageData data, int count)
        {
            GameObject root = new GameObject("Circuit Regression");
            StageObjectFactory factory = root.AddComponent<StageObjectFactory>();
            foreach (StageObjectData item in data.objects)
            {
                if (!item.objectId.StartsWith("13-3_box_", StringComparison.Ordinal)) continue;
                GameObject obj = factory.Create(item, root.transform);
                StageBoxDropper dropper = obj.GetComponent<StageBoxDropper>();
                if (dropper == null) continue;
                Call(dropper, "Start");
                Set(dropper, "nextSpawnTime", -100f);
                Call(dropper, "Update");
                Check((int)Get(dropper, "sequence") == 0, "no automatic box even before controller initialization");
            }
            StageHumanCircuitController controller = root.AddComponent<StageHumanCircuitController>();
            Set(controller, "playerCount", count);
            Call(controller, "BuildStageCircuits");
            Check(((IList)Get(controller, "rooms")).Count == 3, "exactly three circuits for every player count");
            foreach (object room in (IList)Get(controller, "rooms"))
                foreach (object path in (IList)Get(room, "Paths"))
                    foreach (object gap in (IList)Get(path, "Gaps"))
                    {
                        float width = Vector2.Distance((Vector2)Get(Get(gap, "Left"), "BasePosition"),
                            (Vector2)Get(Get(gap, "Right"), "BasePosition"));
                        Check(Mathf.Abs(width - (count >= 3 ? 2.2f : 2.6f)) < 0.001f, "gap narrowed in every round");
                    }
            Call(controller, "PrepareSequentialStage");
            Call(controller, "BuildBoxStations");
            IList stations = (IList)Get(controller, "boxStations");
            Check(stations.Count == 3, "all three round buttons paired");
            GameObject playerObject = new GameObject("Button Test Player");
            playerObject.transform.SetParent(root.transform);
            playerObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
            playerObject.AddComponent<BoxCollider2D>().size = Vector2.one * 0.5f;
            playerObject.AddComponent<PlayerController2D>();
            for (int round = 0; round < 3; round++)
            {
                Call(controller, "ActivateRound", round, false);
                object station = stations[round];
                StageBoxDropper dropper = (StageBoxDropper)Get(station, "Dropper");
                Collider2D button = (Collider2D)Get(station, "ButtonCollider");
                Check(button.gameObject.activeInHierarchy && button.transform.position.x > 18f,
                    "current round button active at bottom right");
                playerObject.transform.position = new Vector2(-18f, -6f);
                Physics2D.SyncTransforms();
                int before = (int)Get(dropper, "sequence");
                Set(controller, "nextBoxPreviewAt", -1f);
                int previousPreview = (int)Get(station, "PreviewIndex");
                Call(controller, "UpdateBoxStations");
                Check((int)Get(station, "PreviewIndex") != previousPreview, "box selection cycles with time");
                Check((int)Get(dropper, "sequence") == before, "preview cycling never dispenses a box");
                Set(controller, "nextBoxPreviewAt", float.PositiveInfinity);
                for (int selection = 0; selection < 5; selection++)
                {
                    playerObject.transform.position = new Vector2(-18f, -6f);
                    Physics2D.SyncTransforms();
                    Call(controller, "UpdateBoxStations");
                    Set(station, "PreviewIndex", selection);
                    Set(station, "NextDispenseAt", 0f);
                    Set(dropper, "nextManualDispenseTime", 0f);
                    playerObject.transform.position = button.bounds.center;
                    Physics2D.SyncTransforms();
                    Call(controller, "UpdateBoxStations");
                    Check((int)Get(dropper, "sequence") == ++before, "press dispenses exactly one selected box");
                    var sizes = (Vector2[])typeof(StageHumanCircuitController).GetField("BoxSizes", Flags | BindingFlags.Static).GetValue(null);
                    GameObject latest = null;
                    foreach (GameObject box in (IEnumerable)Get(dropper, "spawnedBoxes")) latest = box;
                    Check(latest != null && Vector2.Distance(latest.transform.localScale, sizes[selection]) < 0.001f,
                        "actual spawned box matches selected size/shape");
                    Call(controller, "UpdateBoxStations");
                    Check((int)Get(dropper, "sequence") == before, "holding the button does not repeat");
                }
            }
            // Exercise the production transition: round 3 ends the stage instead
            // of opening a fourth room. Suppress clear-progress persistence.
            GameObject managerObject = new GameObject("Inactive Circuit Test Manager");
            managerObject.transform.SetParent(root.transform);
            managerObject.SetActive(false);
            StageManager manager = managerObject.AddComponent<StageManager>();
            Set(manager, "currentStageId", "13-3");
            Set(manager, "stageStarted", true);
            Set(manager, "testingEditedStage", true);
            Set(controller, "stageManager", manager);
            Call(controller, "ActivateRound", 0, false);
            for (int round = 0; round < 3; round++)
            {
                Call(controller, "CompleteRoom", round);
                Check(!(bool)Get(manager, "cleared"), "completion waits for transition presentation");
                Set(controller, "transitionEndsAt", -1f);
                Call(controller, "AdvanceRoundTransition");
                Check((bool)Get(manager, "cleared") == (round == 2), "only third completed circuit clears stage");
                Check((int)Get(controller, "currentRound") == Math.Min(round + 1, 2), "progress never enters removed rounds");
            }
            Check(((TextMesh)Get(controller, "monitorText")).text.Contains("3"), "final monitor displays third stage");
            GameObject onlineObject = new GameObject("Inactive Circuit Peer");
            onlineObject.transform.SetParent(root.transform);
            onlineObject.SetActive(false);
            OnlineManager online = onlineObject.AddComponent<OnlineManager>();
            Set(online, "backend", new RainGoalRegression.TestBackend
            {
                LocalPlayerId = "peer",
                CurrentLobby = new OnlineLobbyInfo
                {
                    StageId = "13-3",
                    Players = new[] { new OnlinePlayerInfo { PlayerId = "host", IsHost = true },
                        new OnlinePlayerInfo { PlayerId = "peer" } }
                }
            });
            Set(manager, "onlineManager", online);
            Set(manager, "cleared", false);
            Set(controller, "onlineManager", online);
            Call(controller, "HandleNetworkData", new OnlineGimmickData
            {
                ObjectId = "13-3", Kind = "human_circuit_state", PlayerId = "host",
                Json = "{\"Sequence\":1,\"CurrentRound\":2,\"CompletedMask\":3,\"TransitionRemaining\":0}"
            });
            Check((int)Get(controller, "currentRound") == 2, "participant restores third round from host snapshot");
            Check(((TextMesh)Get(controller, "monitorText")).text.Contains("3/3"), "participant monitor uses three-round denominator");
            Call(controller, "HandleNetworkData", new OnlineGimmickData
            {
                ObjectId = "13-3", Kind = "human_circuit_state", PlayerId = "host",
                Json = "{\"Sequence\":2,\"CurrentRound\":2,\"CompletedMask\":7,\"TransitionRemaining\":1}"
            });
            Set(controller, "transitionEndsAt", -1f);
            Call(controller, "AdvanceRoundTransition");
            Check(!(bool)Get(manager, "cleared"), "participant waits for authoritative stage-clear message");
            Check((int)Get(controller, "currentRound") == 2, "participant cannot advance to a fourth round");
            Set(manager, "stageStarted", false);
            Object.DestroyImmediate(root);
        }

        private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("[CIRCUIT BOX REGRESSION] " + message);
            assertions++;
        }
    }
}
