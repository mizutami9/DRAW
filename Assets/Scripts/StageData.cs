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
        Weight,
        Ceiling,
        HalfPlatform,
        OneWayPlatform,
        IceFloor,
        SlipperySlope,
        ClimbableWall,
        Rope,
        Ladder,
        CloudPlatform,
        BreakableFloor,
        FallingFloor,
        MovingPlatform,
        RotatingPlatform,
        Checkpoint,
        WarpEntrance,
        WarpExit,
        RespawnPoint,
        MidGoal,
        GoalEffect,
        Button,
        WeightButton,
        Lever,
        ToggleSwitch,
        TimerSwitch,
        Sensor,
        RedSwitch,
        BlueSwitch,
        GreenSwitch,
        YellowSwitch,
        PressurePlate,
        RemoteControl,
        Door,
        LockedDoor,
        Shutter,
        Fence,
        LaserGate,
        ColorGate,
        OneWayGate,
        TimedGate,
        BreakableWall,
        HiddenWall,
        WoodBox,
        IronBox,
        Ball,
        Barrel,
        Rock,
        IceBlock,
        FloatingBox,
        RubberBox,
        Bomb,
        Key,
        Coin,
        Star,
        Battery,
        Bucket,
        JumpPad,
        Spring,
        ConveyorLeft,
        ConveyorRight,
        Elevator,
        Fan,
        Magnet,
        Belt,
        Seesaw,
        Turntable,
        Cannon,
        Catapult,
        Spike,
        Fire,
        Water,
        Poison,
        Laser,
        FallingRock,
        PressMachine,
        Electricity,
        Saw,
        BlackHole,
        Gear,
        BigGear,
        RopePulley,
        Slider,
        RotatingBar,
        Pendulum,
        Keyhole,
        Clock,
        Counter,
        TrafficLight
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
        public string linkTargetId;
        public string linkAction;
    }
}
