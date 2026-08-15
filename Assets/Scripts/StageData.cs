using System;
using UnityEngine;

namespace DrawBody.Prototype
{
    public enum StageRuleMode
    {
        Normal,
        TimedCollection,
        Survival,
        BlockBreaker
    }

    public static class StageObjectId
    {
        public static string New()
        {
            return "obj_" + Guid.NewGuid().ToString("N").Substring(0, 16);
        }
    }

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
        TrafficLight,
        BackgroundTree,
        BackgroundGrass,
        BackgroundFlower,
        BackgroundBush,
        BackgroundCloud,
        BackgroundPush,
        BackgroundArrow,
        BackgroundCatFace,
        BackgroundDogFace,
        BackgroundStickFigure,
        BackgroundSmiley,
        BackgroundHeart,
        BackgroundStar,
        BackgroundMoon,
        BackgroundSun,
        BackgroundRain,
        BackgroundLightning,
        BackgroundRainbow,
        BackgroundMountain,
        BackgroundFourLeafClover,
        BackgroundMushroom,
        BackgroundApple,
        BackgroundBanana,
        BackgroundWatermelon,
        BackgroundDonut,
        BackgroundIceCream,
        BackgroundCoffeeCup,
        BackgroundPizza,
        BackgroundBread,
        BackgroundPaperAirplane,
        BackgroundAirplane,
        BackgroundRocket,
        BackgroundUfo,
        BackgroundHotAirBalloon,
        BackgroundHouse,
        BackgroundCastle,
        BackgroundTreasureChest,
        BackgroundKey,
        BackgroundSword,
        BackgroundCrown,
        BackgroundShield,
        BackgroundGem,
        BackgroundCoin,
        BackgroundBone,
        BackgroundLightBulb,
        BackgroundGear,
        BackgroundSpring,
        BackgroundMagnet,
        BackgroundDice,
        BackgroundSpeechBubble,
        BackgroundCheckMark,
        BackgroundQuestionMark,
        BackgroundExclamationMark,
        BackgroundLoopArrow,
        BackgroundJump,
        BackgroundThrow,
        BackgroundStart,
        BackgroundGoal,
        StageBoundary,
        BackgroundKeyNeeded,
        BackgroundMole,
        BackgroundFossil,
        BackgroundCrystal,
        BackgroundAncientPot,
        InkScale,
        SimultaneousButton,
        HoldButton,
        TriangleBox,
        BoxDropper,
        SpikeDropper,
        CollectibleFish,
        CollectibleCoin,
        CollectibleStar,
        ChallengeClock,
        BeamEmitter,
        PickupFuseBomb,
        BombDropper,
        Dynamite,
        EnemyWalker,
        EnemyJumper,
        EnemyCharger,
        EnemyFlyer,
        EnemyShooter
    }

    [Serializable]
    public sealed class StageData
    {
        public string id = "1-1";
        public string displayName = "New Stage";
        public string backgroundColorHex = "#FBF9EDFF";
        public StageRuleMode ruleMode;
        public float timeLimitSeconds = 60f;
        public StageObjectType collectionTarget = StageObjectType.CollectibleFish;
        public int requiredCollectionCount = 1;
        public StageObjectData[] objects = Array.Empty<StageObjectData>();
    }

    public static class StageBackgroundAppearance
    {
        public static readonly Color DefaultColor = new Color(0.985f, 0.975f, 0.93f, 1f);

        public static Color Parse(string hex)
        {
            return !string.IsNullOrEmpty(hex)
                && ColorUtility.TryParseHtmlString(hex, out Color color)
                    ? color
                    : DefaultColor;
        }

        public static string ToHex(Color color)
        {
            Color opaque = new Color(
                Mathf.Clamp01(color.r),
                Mathf.Clamp01(color.g),
                Mathf.Clamp01(color.b),
                Mathf.Clamp01(color.a));
            return "#" + ColorUtility.ToHtmlStringRGBA(opaque);
        }

        public static void Apply(Color color)
        {
            GameObject paper = GameObject.Find("Notebook Paper");
            SpriteRenderer renderer = paper != null ? paper.GetComponent<SpriteRenderer>() : null;
            if (renderer != null)
            {
                renderer.color = color;
            }
        }

        public static void Reset()
        {
            Apply(DefaultColor);
        }
    }

    [Serializable]
    public sealed class StageRectPartData
    {
        public Vector2 position;
        public Vector2 size = Vector2.one;
    }

    [Serializable]
    public sealed class StageObjectData
    {
        public string objectId;
        public StageObjectType type;
        public Vector2 position;
        public Vector2 size = Vector2.one;
        public float rotation;
        public Vector2[] pathPoints = Array.Empty<Vector2>();
        public float pathThickness;
        public StageRectPartData[] connectedRects = Array.Empty<StageRectPartData>();
        public bool keepSeparate;
        public float actionStrength;
        public float movementAngle;
        public float movementSpeed;
        public int spawnPattern;
        public float spawnBoxSize;
        public float bombFuseSeconds;
        public string linkTargetId;
        public string linkAction;
    }
}
