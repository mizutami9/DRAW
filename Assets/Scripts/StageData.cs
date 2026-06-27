using System;
using UnityEngine;

namespace DrawBody.Prototype
{
    public enum StageObjectType
    {
        Platform,
        Wall,
        Spawn,
        Goal,
        BalanceScale,
        Weight
    }

    [Serializable]
    public sealed class StageData
    {
        public string id = "1-1";
        public string displayName = "New Stage";
        public StageObjectData[] objects = Array.Empty<StageObjectData>();
    }

    [Serializable]
    public sealed class StageObjectData
    {
        public string objectId;
        public StageObjectType type;
        public Vector2 position;
        public Vector2 size = Vector2.one;
        public float rotation;
    }
}
