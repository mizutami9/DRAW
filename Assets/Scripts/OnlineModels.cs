using System;
using UnityEngine;

namespace DrawBody.Prototype
{
    public enum OnlineConnectionState
    {
        Offline,
        LoggingIn,
        Online,
        Matching,
        InLobby,
        Playing,
        Error
    }

    public enum OnlineLobbyMode
    {
        None,
        Random,
        Room
    }

    [Serializable]
    public sealed class OnlinePlayerInfo
    {
        public string PlayerId;
        public string DisplayName;
        public bool IsHost;
        public bool IsReady;
    }

    [Serializable]
    public sealed class OnlineLobbyInfo
    {
        public string LobbyId;
        public string RoomCode;
        public string RoomName;
        public string StageId = "1-1";
        public int StageRevision;
        public int RetryRevision;
        public int MaxPlayers = 4;
        public OnlineLobbyMode Mode;
        public OnlinePlayerInfo[] Players = Array.Empty<OnlinePlayerInfo>();
    }

    [Serializable]
    public sealed class OnlineBodyData
    {
        public string PlayerId;
        public int Revision;
        public string Json;
    }

    [Serializable]
    public sealed class SerializableBodyDrawing
    {
        public string Species;
        public int CoordinateVersion;
        public SerializableBodyPartDrawing[] Parts = Array.Empty<SerializableBodyPartDrawing>();
    }

    [Serializable]
    public sealed class SerializableBodyPartDrawing
    {
        public string Part;
        public Vector2[] Points = Array.Empty<Vector2>();
        public float Ink;
    }

    [Serializable]
    public sealed class OnlineInputData
    {
        public string PlayerId;
        public float MoveX;
        public bool JumpPressed;
        public bool DrawOpen;
        public double ClientTime;
    }

    [Serializable]
    public sealed class OnlinePlayerState
    {
        public string PlayerId;
        public int Sequence;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Rotation;
        public bool Redrawing;
        public bool TurtleShelled;
        public string SlimeAttachedToPlayerId;
        public string CarriedPlayerId;
        public string CarryAction;
        public Vector2 CarryOffset;
    }

    [Serializable]
    public sealed class OnlineCarryData
    {
        public string CarrierPlayerId;
        public string TargetPlayerId;
        public string Action;
        public Vector2 ReleaseVelocity;
        public Vector2 LocalOffset;
    }

    [Serializable]
    public sealed class OnlineGimmickData
    {
        public string PlayerId;
        public string ObjectId;
        public string Kind;
        public string Json;
        public int Sequence;
    }

    [Serializable]
    public sealed class OnlineTransformGimmickState
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Rotation;
        public float AngularVelocity;
        public bool Active;
    }

    [Serializable]
    public sealed class OnlineLinkGimmickState
    {
        public string TargetId;
        public string Action;
        public float Progress;
        public bool Active;
        public bool Pressed;
    }
}
