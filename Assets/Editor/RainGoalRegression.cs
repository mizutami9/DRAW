using System;
using System.Collections.Generic;
using System.Reflection;
using DrawBody.Prototype;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DrawBody.EditorTools
{
    // In-process multiplayer regression: real goal/controller/message handlers,
    // serialized packets, with a transport stub (not an EOS connectivity test).
    public static class RainGoalRegression
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static int assertions;

        public static void RunChecks()
        {
            StageData data = JsonUtility.FromJson<StageData>(Resources.Load<TextAsset>("Stages/14-2").text);
            Check(data.ruleMode == StageRuleMode.Survival, "test the shipped survival rule, not a normal-stage fixture");
            for (int count = 1; count <= 4; count++)
                for (int deadMask = 0; deadMask < (1 << count); deadMask++)
                    for (int order = 0; order < 2; order++)
                    {
                        CheckCase(data, count, deadMask, order == 1, false);
                        if (count >= 2) CheckCase(data, count, deadMask, order == 1, true);
                    }
            Debug.Log("[RAIN GOAL REGRESSION] PASS " + assertions + " assertions");
        }

        private static void CheckCase(StageData data, int count, int deadMask, bool deathAfterArrival, bool online)
        {
            GameObject root = new GameObject("Rain Goal Regression");
            StageManager host = MakeManager(root, data);
            StageManager peer = online ? MakeManager(root, data) : null;
            int hostCount = online ? Mathf.Max(1, count - 2) : count;
            var actors = new PlayerController2D[count];
            var ids = new string[count];
            var lobbyPlayers = new OnlinePlayerInfo[count];
            for (int i = 0; i < count; i++)
            {
                StageManager owner = i < hostCount ? host : peer;
                GameObject obj = new GameObject("Player " + i);
                obj.transform.SetParent(root.transform);
                obj.transform.position = new Vector3(80f + i * 1.2f, -2.7f);
                obj.AddComponent<Rigidbody2D>().gravityScale = 0f;
                obj.AddComponent<BoxCollider2D>().size = Vector2.one * 0.6f;
                actors[i] = obj.AddComponent<PlayerController2D>();
                Set(actors[i], "stageManager", owner);
                ((List<PlayerController2D>)Get(owner, "localPlayers")).Add(actors[i]);
                ids[i] = online ? OnlinePlayerIdentity.Compose(i < hostCount ? "host" : "peer",
                    i < hostCount ? i : i - hostCount) : "local_" + actors[i].GetInstanceID();
                lobbyPlayers[i] = new OnlinePlayerInfo { PlayerId = ids[i], IsHost = i == 0 };
            }

            GameObject rainObj = new GameObject("Rain Controller");
            rainObj.transform.SetParent(root.transform);
            StageUmbrellaRainController rain = rainObj.AddComponent<StageUmbrellaRainController>();
            Set(rain, "stageManager", host);
            foreach (string id in ids) ((HashSet<string>)Get(rain, "participantIds")).Add(id);
            if (online)
            {
                var lobby = new OnlineLobbyInfo { StageId = "14-2", Players = lobbyPlayers };
                TestBackend hostBackend = Connect(root, host, "host", lobby);
                TestBackend peerBackend = Connect(root, peer, "peer", lobby);
                Set(rain, "onlineManager", Get(host, "onlineManager"));
                for (int i = 0; i < count; i++)
                    ((Dictionary<string, PlayerController2D>)Get(i < hostCount ? peer : host, "onlineRemotePlayers"))[ids[i]] = actors[i];
                hostBackend.Deliver = message => Call(peer, "HandleOnlineGimmickData", message);
                peerBackend.Deliver = message => Call(host, "HandleOnlineGimmickData", message);
            }

            StageObjectFactory factory = root.AddComponent<StageObjectFactory>();
            StageObjectData door = Array.Find(data.objects, item => item.objectId == "14-2_goal");
            GameObject doorObject = factory.Create(door, root.transform);
            Goal goal = doorObject.GetComponent<Goal>();
            Physics2D.SyncTransforms();
            Action kill = () =>
            {
                for (int i = 0; i < count; i++)
                    if ((deadMask & (1 << i)) != 0) Call(rain, "ConfirmElimination", ids[i], online);
            };
            if (!deathAfterArrival) kill();
            for (int i = 0; i < count; i++)
            {
                if ((deadMask & (1 << i)) != 0) continue;
                Check(goal.GetComponent<Collider2D>().Distance(actors[i].GetComponent<Collider2D>()).isOverlapped,
                    "actual door collider includes the player's floor position");
                Set(goal, "stageManager", i < hostCount ? host : peer);
                Call(goal, "OnTriggerEnter2D", actors[i].GetComponent<Collider2D>());
            }
            if (deathAfterArrival) kill();
            bool survivors = deadMask != (1 << count) - 1;
            Check((bool)Get(host, "cleared") == survivors,
                $"host clear: count={count}, dead={deadMask}, lateDeath={deathAfterArrival}, online={online}");
            if (online) Check((bool)Get(peer, "cleared") == survivors, "host clear reaches participant");
            if (!survivors) Check((bool)Get(rain, "failed"), "total wipe fails instead of clearing");
            Set(host, "stageStarted", false);
            if (peer != null) Set(peer, "stageStarted", false);
            Object.DestroyImmediate(root);
        }

        private static StageManager MakeManager(GameObject root, StageData data)
        {
            GameObject obj = new GameObject("Inactive Test StageManager");
            obj.transform.SetParent(root.transform);
            obj.SetActive(false);
            StageManager manager = obj.AddComponent<StageManager>();
            Set(manager, "currentStageId", data.id);
            Set(manager, "stageRuleMode", data.ruleMode);
            Set(manager, "stageStarted", true);
            Set(manager, "challengeRunStarted", true);
            Set(manager, "titleMode", false);
            Set(manager, "testingEditedStage", true); // Never modify the user's clear records.
            return manager;
        }

        private static TestBackend Connect(GameObject root, StageManager manager, string id, OnlineLobbyInfo lobby)
        {
            GameObject obj = new GameObject("Inactive Test OnlineManager");
            obj.transform.SetParent(root.transform);
            obj.SetActive(false);
            OnlineManager online = obj.AddComponent<OnlineManager>();
            var backend = new TestBackend { LocalPlayerId = id, CurrentLobby = lobby };
            Set(online, "backend", backend);
            Set(manager, "onlineManager", online);
            return backend;
        }

        private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("[RAIN GOAL REGRESSION] " + message);
            assertions++;
        }

        internal sealed class TestBackend : IOnlineBackend
        {
            public OnlineConnectionState State => OnlineConnectionState.Playing;
            public OnlineLobbyInfo CurrentLobby { get; set; }
            public string LocalPlayerId { get; set; }
            public Action<OnlineGimmickData> Deliver;
            public event Action<OnlineConnectionState, OnlineLobbyInfo, string> StateChanged { add { } remove { } }
            public event Action<OnlinePlayerState> PlayerStateReceived { add { } remove { } }
            public event Action<OnlineBodyData> BodyDataReceived { add { } remove { } }
            public event Action<OnlineCarryData> CarryDataReceived { add { } remove { } }
            public event Action<OnlineGimmickData> GimmickDataReceived { add { } remove { } }
            public void SendGimmickData(OnlineGimmickData data)
            {
                if (string.IsNullOrEmpty(data.PlayerId)) data.PlayerId = LocalPlayerId;
                Deliver?.Invoke(JsonUtility.FromJson<OnlineGimmickData>(JsonUtility.ToJson(data)));
            }
            public void Initialize() { }
            public void Login() { }
            public void Tick() { }
            public void Shutdown() { }
            public void StartRandomMatch() { }
            public void CreateRoom(string name, int max, bool priv) { }
            public void JoinRoom(string id) { }
            public void LeaveLobby() { }
            public void SetReady(bool ready) { }
            public void OpenStageSelect() { }
            public void CloseStageSelect() { }
            public void StartGame(string stage) { }
            public void SendBodyData(OnlineBodyData data) { }
            public void SendInput(OnlineInputData data) { }
            public void SendPlayerState(OnlinePlayerState data) { }
            public void SendCarryData(OnlineCarryData data) { }
        }
    }
}
