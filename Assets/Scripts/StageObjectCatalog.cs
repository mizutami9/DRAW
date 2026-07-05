using System;
using System.Collections.Generic;
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
        Gimmick
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
        public StageObjectCatalogEntry(StageObjectType type, StageObjectCategory category, string label, StageObjectPlacement placement, StageObjectKind kind)
        {
            Type = type;
            Category = category;
            Label = label;
            Placement = placement;
            Kind = kind;
        }

        public StageObjectType Type { get; }
        public StageObjectCategory Category { get; }
        public string Label { get; }
        public StageObjectPlacement Placement { get; }
        public StageObjectKind Kind { get; }
    }

    public static class StageObjectCatalog
    {
        private static readonly StageObjectCatalogEntry[] Entries =
        {
            E(StageObjectType.Platform, StageObjectCategory.Terrain, "床", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.Wall, StageObjectCategory.Terrain, "壁", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.Ceiling, StageObjectCategory.Terrain, "天井", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.HalfPlatform, StageObjectCategory.Terrain, "半床", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.OneWayPlatform, StageObjectCategory.Terrain, "一方通行床", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.IceFloor, StageObjectCategory.Terrain, "氷床", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.SlipperySlope, StageObjectCategory.Terrain, "滑る坂", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.ClimbableWall, StageObjectCategory.Terrain, "登れる壁", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.Rope, StageObjectCategory.Terrain, "ロープ", StageObjectPlacement.Rect, StageObjectKind.Trigger),
            E(StageObjectType.Ladder, StageObjectCategory.Terrain, "はしご", StageObjectPlacement.Rect, StageObjectKind.Trigger),
            E(StageObjectType.CloudPlatform, StageObjectCategory.Terrain, "雲床", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.BreakableFloor, StageObjectCategory.Terrain, "壊れる床", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.FallingFloor, StageObjectCategory.Terrain, "落下床", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.MovingPlatform, StageObjectCategory.Terrain, "動く床", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.RotatingPlatform, StageObjectCategory.Terrain, "回転床", StageObjectPlacement.Rect, StageObjectKind.Solid),

            E(StageObjectType.Spawn, StageObjectCategory.StartGoal, "プレイヤースタート", StageObjectPlacement.Point, StageObjectKind.Marker),
            E(StageObjectType.Goal, StageObjectCategory.StartGoal, "ゴール", StageObjectPlacement.Point, StageObjectKind.Goal),
            E(StageObjectType.Checkpoint, StageObjectCategory.StartGoal, "チェックポイント", StageObjectPlacement.Point, StageObjectKind.Marker),
            E(StageObjectType.WarpEntrance, StageObjectCategory.StartGoal, "ワープ入口", StageObjectPlacement.Point, StageObjectKind.Marker),
            E(StageObjectType.WarpExit, StageObjectCategory.StartGoal, "ワープ出口", StageObjectPlacement.Point, StageObjectKind.Marker),
            E(StageObjectType.RespawnPoint, StageObjectCategory.StartGoal, "リスポーン地点", StageObjectPlacement.Point, StageObjectKind.Marker),
            E(StageObjectType.MidGoal, StageObjectCategory.StartGoal, "中間ゴール", StageObjectPlacement.Point, StageObjectKind.Marker),
            E(StageObjectType.GoalEffect, StageObjectCategory.StartGoal, "ゴール演出", StageObjectPlacement.Point, StageObjectKind.Decoration),

            E(StageObjectType.Button, StageObjectCategory.Switch, "ボタン", StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.WeightButton, StageObjectCategory.Switch, "重さボタン", StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.Lever, StageObjectCategory.Switch, "レバー", StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.ToggleSwitch, StageObjectCategory.Switch, "トグルスイッチ", StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.TimerSwitch, StageObjectCategory.Switch, "タイマースイッチ", StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.Sensor, StageObjectCategory.Switch, "センサー", StageObjectPlacement.Rect, StageObjectKind.Trigger),
            E(StageObjectType.RedSwitch, StageObjectCategory.Switch, "赤スイッチ", StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.BlueSwitch, StageObjectCategory.Switch, "青スイッチ", StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.GreenSwitch, StageObjectCategory.Switch, "緑スイッチ", StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.YellowSwitch, StageObjectCategory.Switch, "黄スイッチ", StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.PressurePlate, StageObjectCategory.Switch, "圧力板", StageObjectPlacement.Rect, StageObjectKind.Trigger),
            E(StageObjectType.RemoteControl, StageObjectCategory.Switch, "リモコン", StageObjectPlacement.Point, StageObjectKind.Trigger),

            E(StageObjectType.Door, StageObjectCategory.DoorGate, "ドア", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.LockedDoor, StageObjectCategory.DoorGate, "鍵付きドア", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.Shutter, StageObjectCategory.DoorGate, "シャッター", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.Fence, StageObjectCategory.DoorGate, "柵", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.LaserGate, StageObjectCategory.DoorGate, "レーザーゲート", StageObjectPlacement.Rect, StageObjectKind.Trigger),
            E(StageObjectType.ColorGate, StageObjectCategory.DoorGate, "色ゲート", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.OneWayGate, StageObjectCategory.DoorGate, "一方通行ゲート", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.TimedGate, StageObjectCategory.DoorGate, "時間制限ゲート", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.BreakableWall, StageObjectCategory.DoorGate, "壊れる壁", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.HiddenWall, StageObjectCategory.DoorGate, "隠し壁", StageObjectPlacement.Rect, StageObjectKind.Solid),

            E(StageObjectType.WoodBox, StageObjectCategory.Movable, "木箱", StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.IronBox, StageObjectCategory.Movable, "鉄箱", StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.Ball, StageObjectCategory.Movable, "ボール", StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.Barrel, StageObjectCategory.Movable, "樽", StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.Rock, StageObjectCategory.Movable, "岩", StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.IceBlock, StageObjectCategory.Movable, "氷ブロック", StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.Weight, StageObjectCategory.Movable, "重り", StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.FloatingBox, StageObjectCategory.Movable, "浮遊箱", StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.RubberBox, StageObjectCategory.Movable, "ゴム箱", StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.Bomb, StageObjectCategory.Movable, "爆弾", StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.Key, StageObjectCategory.Movable, "鍵", StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.Coin, StageObjectCategory.Movable, "コイン", StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.Star, StageObjectCategory.Movable, "星", StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.Battery, StageObjectCategory.Movable, "バッテリー", StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.Bucket, StageObjectCategory.Movable, "バケツ", StageObjectPlacement.Point, StageObjectKind.Pushable),

            E(StageObjectType.JumpPad, StageObjectCategory.Action, "ジャンプ台", StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.Spring, StageObjectCategory.Action, "バネ", StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.ConveyorLeft, StageObjectCategory.Action, "コンベア左", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.ConveyorRight, StageObjectCategory.Action, "コンベア右", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.Elevator, StageObjectCategory.Action, "エレベーター", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.Fan, StageObjectCategory.Action, "扇風機", StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.Magnet, StageObjectCategory.Action, "磁石", StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.Belt, StageObjectCategory.Action, "ベルト", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.Seesaw, StageObjectCategory.Action, "シーソー", StageObjectPlacement.Point, StageObjectKind.Balance),
            E(StageObjectType.BalanceScale, StageObjectCategory.Action, "天秤", StageObjectPlacement.Point, StageObjectKind.Balance),
            E(StageObjectType.Turntable, StageObjectCategory.Action, "回転台", StageObjectPlacement.Point, StageObjectKind.Solid),
            E(StageObjectType.Cannon, StageObjectCategory.Action, "大砲", StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.Catapult, StageObjectCategory.Action, "投石機", StageObjectPlacement.Point, StageObjectKind.Balance),

            E(StageObjectType.Spike, StageObjectCategory.Trap, "トゲ", StageObjectPlacement.Rect, StageObjectKind.Hazard),
            E(StageObjectType.Fire, StageObjectCategory.Trap, "火", StageObjectPlacement.Rect, StageObjectKind.Hazard),
            E(StageObjectType.Water, StageObjectCategory.Trap, "水", StageObjectPlacement.Rect, StageObjectKind.Trigger),
            E(StageObjectType.Poison, StageObjectCategory.Trap, "毒", StageObjectPlacement.Rect, StageObjectKind.Hazard),
            E(StageObjectType.Laser, StageObjectCategory.Trap, "レーザー", StageObjectPlacement.Rect, StageObjectKind.Hazard),
            E(StageObjectType.FallingRock, StageObjectCategory.Trap, "落石", StageObjectPlacement.Point, StageObjectKind.Pushable),
            E(StageObjectType.PressMachine, StageObjectCategory.Trap, "プレス機", StageObjectPlacement.Rect, StageObjectKind.Hazard),
            E(StageObjectType.Electricity, StageObjectCategory.Trap, "電気", StageObjectPlacement.Rect, StageObjectKind.Hazard),
            E(StageObjectType.Saw, StageObjectCategory.Trap, "ノコギリ", StageObjectPlacement.Point, StageObjectKind.Hazard),
            E(StageObjectType.BlackHole, StageObjectCategory.Trap, "ブラックホール", StageObjectPlacement.Point, StageObjectKind.Hazard),

            E(StageObjectType.Gear, StageObjectCategory.Gimmick, "歯車", StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.BigGear, StageObjectCategory.Gimmick, "歯車(大)", StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.RopePulley, StageObjectCategory.Gimmick, "ロープ滑車", StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.Slider, StageObjectCategory.Gimmick, "スライダー", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.RotatingBar, StageObjectCategory.Gimmick, "回転棒", StageObjectPlacement.Rect, StageObjectKind.Solid),
            E(StageObjectType.Pendulum, StageObjectCategory.Gimmick, "振り子", StageObjectPlacement.Point, StageObjectKind.Hazard),
            E(StageObjectType.Keyhole, StageObjectCategory.Gimmick, "カギ穴", StageObjectPlacement.Point, StageObjectKind.Trigger),
            E(StageObjectType.Clock, StageObjectCategory.Gimmick, "時計", StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.Counter, StageObjectCategory.Gimmick, "カウンター", StageObjectPlacement.Point, StageObjectKind.Decoration),
            E(StageObjectType.TrafficLight, StageObjectCategory.Gimmick, "信号機", StageObjectPlacement.Point, StageObjectKind.Decoration),
        };

        private static readonly Dictionary<StageObjectType, StageObjectCatalogEntry> ByType = BuildLookup();

        public static IReadOnlyList<StageObjectCatalogEntry> All => Entries;

        public static StageObjectCatalogEntry Get(StageObjectType type)
        {
            return ByType.TryGetValue(type, out StageObjectCatalogEntry entry)
                ? entry
                : new StageObjectCatalogEntry(type, StageObjectCategory.Terrain, type.ToString(), StageObjectPlacement.Rect, StageObjectKind.Solid);
        }

        public static bool IsRectPlacement(StageObjectType type)
        {
            return Get(type).Placement == StageObjectPlacement.Rect;
        }

        public static string GetCategoryLabel(StageObjectCategory category)
        {
            switch (category)
            {
                case StageObjectCategory.StartGoal:
                    return "スタート・ゴール";
                case StageObjectCategory.Switch:
                    return "スイッチ";
                case StageObjectCategory.DoorGate:
                    return "ドア・ゲート";
                case StageObjectCategory.Movable:
                    return "可動";
                case StageObjectCategory.Action:
                    return "アクション";
                case StageObjectCategory.Trap:
                    return "トラップ";
                case StageObjectCategory.Gimmick:
                    return "ギミック";
                default:
                    return "地形";
            }
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
            StageObjectCategory.Gimmick
        };

        private static StageObjectCatalogEntry E(StageObjectType type, StageObjectCategory category, string label, StageObjectPlacement placement, StageObjectKind kind)
        {
            return new StageObjectCatalogEntry(type, category, label, placement, kind);
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
    }
}
