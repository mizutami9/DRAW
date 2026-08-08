using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DrawBody.Prototype
{
    public enum StageObjectCategory
    {
        Terrain,
        StartGoal,
        Switch,
        DoorGate,
        Movable,
        Action,
        Trap,
        Gimmick,
        Decoration
    }

    public enum StageObjectPlacement
    {
        Point,
        Rect
    }

    public enum StageObjectKind
    {
        Solid,
        Marker,
        Goal,
        Balance,
        Pushable,
        Decoration,
        Trigger,
        Hazard
    }

    public readonly struct StageObjectCatalogEntry
    {
        public StageObjectCatalogEntry(StageObjectType type, StageObjectCategory category, StageObjectPlacement placement, StageObjectKind kind)
        {
            Type = type;
            Category = category;
            LabelKey = StageObjectCatalog.GetObjectKey(type);
            Placement = placement;
            Kind = kind;
        }

        public StageObjectType Type { get; }
        public StageObjectCategory Category { get; }
        public string LabelKey { get; }
        public string Label => LocalizationManager.T(LabelKey);
        public StageObjectPlacement Placement { get; }
        public StageObjectKind Kind { get; }
    }

    public static class StageObjectCatalog
    {
        private static readonly StageObjectCatalogEntry[] Entries =
        {
            E(StageObjectType.Platform, StageObjectCategory.Terrain, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.Wall, StageObjectCategory.Terrain, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.StageBoundary, StageObjectCategory.Terrain, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.Ceiling, StageObjectCategory.Terrain, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.HalfPlatform, StageObjectCategory.Terrain, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.OneWayPlatform, StageObjectCategory.Terrain, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.IceFloor, StageObjectCategory.Terrain, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.SlipperySlope, StageObjectCategory.Terrain, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.ClimbableWall, StageObjectCategory.Terrain, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.Rope, StageObjectCategory.Terrain, StageObjectPlacement.Rect, StageObjectKind.Trigger),
            E(StageObjectType.Ladder, StageObjectCategory.Terrain, StageObjectPlacement.Rect, StageObjectKind.Trigger),
            E(StageObjectType.CloudPlatform, StageObjectCategory.Terrain, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.BreakableFloor, StageObjectCategory.Terrain, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.FallingFloor, StageObjectCategory.Terrain, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.MovingPlatform, StageObjectCategory.Terrain, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.RotatingPlatform, StageObjectCategory.Terrain, StageObjectPlacement.Rect, StageObjectKind.Solid),

            E(StageObjectType.Spawn, StageObjectCategory.StartGoal, StageObjectPlacement.Point, StageObjectKind.Marker),
            E(StageObjectType.Goal, StageObjectCategory.StartGoal, StageObjectPlacement.Point, StageObjectKind.Goal),
            E(StageObjectType.Checkpoint, StageObjectCategory.StartGoal, StageObjectPlacement.Point, StageObjectKind.Marker),
            E(StageObjectType.WarpEntrance, StageObjectCategory.StartGoal, StageObjectPlacement.Point, StageObjectKind.Marker),
            E(StageObjectType.WarpExit, StageObjectCategory.StartGoal, StageObjectPlacement.Point, StageObjectKind.Marker),
            E(StageObjectType.RespawnPoint, StageObjectCategory.StartGoal, StageObjectPlacement.Point, StageObjectKind.Marker),
            E(StageObjectType.MidGoal, StageObjectCategory.StartGoal, StageObjectPlacement.Point, StageObjectKind.Marker),
            E(StageObjectType.GoalEffect, StageObjectCategory.StartGoal, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.CollectibleFish, StageObjectCategory.StartGoal, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.CollectibleCoin, StageObjectCategory.StartGoal, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.CollectibleStar, StageObjectCategory.StartGoal, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.ChallengeClock, StageObjectCategory.StartGoal, StageObjectPlacement.Point, StageObjectKind.Decoration),

            E(StageObjectType.Button, StageObjectCategory.Switch, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.WeightButton, StageObjectCategory.Switch, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.SimultaneousButton, StageObjectCategory.Switch, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.HoldButton, StageObjectCategory.Switch, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.Lever, StageObjectCategory.Switch, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.ToggleSwitch, StageObjectCategory.Switch, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.TimerSwitch, StageObjectCategory.Switch, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.Sensor, StageObjectCategory.Switch, StageObjectPlacement.Rect, StageObjectKind.Trigger),
            E(StageObjectType.RedSwitch, StageObjectCategory.Switch, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.BlueSwitch, StageObjectCategory.Switch, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.GreenSwitch, StageObjectCategory.Switch, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.YellowSwitch, StageObjectCategory.Switch, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.PressurePlate, StageObjectCategory.Switch, StageObjectPlacement.Rect, StageObjectKind.Trigger),
            E(StageObjectType.RemoteControl, StageObjectCategory.Switch, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.InkScale, StageObjectCategory.Switch, StageObjectPlacement.Point, StageObjectKind.Trigger),

            E(StageObjectType.Door, StageObjectCategory.DoorGate, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.LockedDoor, StageObjectCategory.DoorGate, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.Shutter, StageObjectCategory.DoorGate, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.Fence, StageObjectCategory.DoorGate, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.LaserGate, StageObjectCategory.DoorGate, StageObjectPlacement.Rect, StageObjectKind.Trigger),
            E(StageObjectType.ColorGate, StageObjectCategory.DoorGate, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.OneWayGate, StageObjectCategory.DoorGate, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.TimedGate, StageObjectCategory.DoorGate, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.BreakableWall, StageObjectCategory.DoorGate, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.HiddenWall, StageObjectCategory.DoorGate, StageObjectPlacement.Rect, StageObjectKind.Solid),

            E(StageObjectType.WoodBox, StageObjectCategory.Movable, StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.IronBox, StageObjectCategory.Movable, StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.Ball, StageObjectCategory.Movable, StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.Barrel, StageObjectCategory.Movable, StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.Rock, StageObjectCategory.Movable, StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.IceBlock, StageObjectCategory.Movable, StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.Weight, StageObjectCategory.Movable, StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.FloatingBox, StageObjectCategory.Movable, StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.RubberBox, StageObjectCategory.Movable, StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.Bomb, StageObjectCategory.Movable, StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.Key, StageObjectCategory.Movable, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.Coin, StageObjectCategory.Movable, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.Star, StageObjectCategory.Movable, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.Battery, StageObjectCategory.Movable, StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.Bucket, StageObjectCategory.Movable, StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.TriangleBox, StageObjectCategory.Movable, StageObjectPlacement.Point, StageObjectKind.Pushable),

            E(StageObjectType.JumpPad, StageObjectCategory.Action, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.Spring, StageObjectCategory.Action, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.ConveyorLeft, StageObjectCategory.Action, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.ConveyorRight, StageObjectCategory.Action, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.Elevator, StageObjectCategory.Action, StageObjectPlacement.Point, StageObjectKind.Solid),
            E(StageObjectType.Fan, StageObjectCategory.Action, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.Magnet, StageObjectCategory.Action, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.Belt, StageObjectCategory.Action, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.BoxDropper, StageObjectCategory.Action, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.Seesaw, StageObjectCategory.Action, StageObjectPlacement.Point, StageObjectKind.Balance),
            E(StageObjectType.BalanceScale, StageObjectCategory.Action, StageObjectPlacement.Point, StageObjectKind.Balance),
            E(StageObjectType.Turntable, StageObjectCategory.Action, StageObjectPlacement.Point, StageObjectKind.Solid),
            E(StageObjectType.Cannon, StageObjectCategory.Action, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.Catapult, StageObjectCategory.Action, StageObjectPlacement.Point, StageObjectKind.Balance),

            E(StageObjectType.Spike, StageObjectCategory.Trap, StageObjectPlacement.Rect, StageObjectKind.Hazard),
            E(StageObjectType.SpikeDropper, StageObjectCategory.Trap, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.BeamEmitter, StageObjectCategory.Trap, StageObjectPlacement.Point, StageObjectKind.Hazard),
            E(StageObjectType.Fire, StageObjectCategory.Trap, StageObjectPlacement.Rect, StageObjectKind.Hazard),
            E(StageObjectType.Water, StageObjectCategory.Trap, StageObjectPlacement.Rect, StageObjectKind.Trigger),
            E(StageObjectType.Poison, StageObjectCategory.Trap, StageObjectPlacement.Rect, StageObjectKind.Hazard),
            E(StageObjectType.Laser, StageObjectCategory.Trap, StageObjectPlacement.Rect, StageObjectKind.Hazard),
            E(StageObjectType.FallingRock, StageObjectCategory.Trap, StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.PressMachine, StageObjectCategory.Trap, StageObjectPlacement.Rect, StageObjectKind.Hazard),
            E(StageObjectType.Electricity, StageObjectCategory.Trap, StageObjectPlacement.Rect, StageObjectKind.Hazard),
            E(StageObjectType.Saw, StageObjectCategory.Trap, StageObjectPlacement.Point, StageObjectKind.Hazard),
            E(StageObjectType.BlackHole, StageObjectCategory.Trap, StageObjectPlacement.Point, StageObjectKind.Hazard),

            E(StageObjectType.Gear, StageObjectCategory.Gimmick, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BigGear, StageObjectCategory.Gimmick, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.RopePulley, StageObjectCategory.Gimmick, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.Slider, StageObjectCategory.Gimmick, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.RotatingBar, StageObjectCategory.Gimmick, StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.Pendulum, StageObjectCategory.Gimmick, StageObjectPlacement.Point, StageObjectKind.Hazard),
            E(StageObjectType.Keyhole, StageObjectCategory.Gimmick, StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.Clock, StageObjectCategory.Gimmick, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.Counter, StageObjectCategory.Gimmick, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.TrafficLight, StageObjectCategory.Gimmick, StageObjectPlacement.Point, StageObjectKind.Decoration),

            // Nature and scenery
            E(StageObjectType.BackgroundTree, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundGrass, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundFlower, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundBush, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundFourLeafClover, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundMushroom, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundMountain, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundCloud, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundRain, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundLightning, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundRainbow, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundSun, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundMoon, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundStar, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),

            // Characters and friendly marks
            E(StageObjectType.BackgroundCatFace, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundDogFace, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundStickFigure, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundSmiley, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundHeart, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),

            // Food
            E(StageObjectType.BackgroundApple, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundBanana, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundWatermelon, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundDonut, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundIceCream, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundCoffeeCup, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundPizza, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundBread, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),

            // Vehicles and places
            E(StageObjectType.BackgroundPaperAirplane, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundAirplane, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundRocket, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundUfo, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundHotAirBalloon, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundHouse, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundCastle, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundTreasureChest, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundMole, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundFossil, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundCrystal, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundAncientPot, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),

            // Items
            E(StageObjectType.BackgroundKey, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundSword, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundCrown, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundShield, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundGem, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundCoin, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundBone, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundLightBulb, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundGear, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundSpring, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundMagnet, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundDice, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),

            // Visual puzzle hints and symbols
            E(StageObjectType.BackgroundKeyNeeded, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundArrow, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundLoopArrow, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundSpeechBubble, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundCheckMark, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundQuestionMark, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundExclamationMark, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),

            // Word-based hints stay at the end of the decoration palette
            E(StageObjectType.BackgroundPush, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundJump, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundThrow, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundStart, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BackgroundGoal, StageObjectCategory.Decoration, StageObjectPlacement.Point, StageObjectKind.Decoration),
        };

        private static readonly Dictionary<StageObjectType, StageObjectCatalogEntry> ByType = BuildLookup();

        public static IReadOnlyList<StageObjectCatalogEntry> All => Entries;

        public static StageObjectCatalogEntry Get(StageObjectType type)
        {
            return ByType.TryGetValue(type, out StageObjectCatalogEntry entry)
                ? entry
                : new StageObjectCatalogEntry(type, StageObjectCategory.Terrain, StageObjectPlacement.Rect, StageObjectKind.Solid);
        }

        public static bool IsRectPlacement(StageObjectType type)
        {
            return Get(type).Placement == StageObjectPlacement.Rect;
        }

        public static bool IsPaletteVisible(StageObjectType type)
        {
            // Keep loading existing decorative keys, but expose only the functional key
            // in the editor so two identically named objects are not shown.
            return type != StageObjectType.BackgroundKey;
        }

        public static string GetCategoryLabel(StageObjectCategory category)
        {
            return LocalizationManager.T(GetCategoryKey(category));
        }

        public static string GetCategoryKey(StageObjectCategory category)
        {
            return "stage_category_" + ToSnakeCase(category.ToString());
        }

        public static string GetObjectKey(StageObjectType type)
        {
            return "stage_object_" + ToSnakeCase(type.ToString());
        }

        public static StageObjectCategory[] Categories { get; } =
        {
            StageObjectCategory.Terrain,
            StageObjectCategory.StartGoal,
            StageObjectCategory.Switch,
            StageObjectCategory.DoorGate,
            StageObjectCategory.Movable,
            StageObjectCategory.Action,
            StageObjectCategory.Trap,
            StageObjectCategory.Gimmick,
            StageObjectCategory.Decoration
        };

        private static StageObjectCatalogEntry E(StageObjectType type, StageObjectCategory category, StageObjectPlacement placement, StageObjectKind kind)
        {
            return new StageObjectCatalogEntry(type, category, placement, kind);
        }

        private static Dictionary<StageObjectType, StageObjectCatalogEntry> BuildLookup()
        {
            Dictionary<StageObjectType, StageObjectCatalogEntry> lookup = new Dictionary<StageObjectType, StageObjectCatalogEntry>();
            foreach (StageObjectCatalogEntry entry in Entries)
            {
                lookup[entry.Type] = entry;
            }

            return lookup;
        }

        private static string ToSnakeCase(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (char.IsUpper(current) && i > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
            }

            return builder.ToString();
        }
    }
}
