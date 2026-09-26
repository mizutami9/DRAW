using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class StageObjectFactory : MonoBehaviour
    {
        [SerializeField] private int groundLayer = 6;
        [SerializeField] private int goalLayer = 8;
        [SerializeField] private int pushableLayer = 9;

        private static Material lineMaterial;
        private static Sprite circleSprite;
        private static Sprite scaleBodySprite;
        private static Sprite[] natureWallVineSprites;
        private static Sprite natureHangingVineSprite;
        private static Sprite natureGrassSprite;
        private static Sprite natureBushSprite;
        private static Sprite natureFlowerSprite;
        private static Font monitorFont;
        private static readonly Color TitleTerrainPaperColor = new Color(0.905f, 0.965f, 0.845f, 1f);
        private static readonly Color TitleTerrainStrokeColor = new Color(0.16f, 0.57f, 0.22f, 0.98f);
        private static readonly Color TitleTerrainAccentColor = new Color(0.34f, 0.72f, 0.34f, 0.72f);
        private static readonly Color CaveTerrainPaperColor = new Color(0.78f, 0.805f, 0.82f, 1f);
        private static readonly Color CaveTerrainStrokeColor = new Color(0.19f, 0.205f, 0.23f, 0.98f);
        private static readonly Color CaveTerrainAccentColor = new Color(0.42f, 0.45f, 0.49f, 0.72f);
        private static readonly Color CaveBackdropPaperColor = new Color(0.63f, 0.72f, 0.78f, 0.18f);
        private static readonly Color CaveBackdropRockColor = new Color(0.29f, 0.34f, 0.39f, 0.25f);
        private static readonly Color CaveBackdropGraphiteColor = new Color(0.17f, 0.21f, 0.25f, 0.38f);
        private static readonly Color UnderwaterSandPaperColor = new Color(0.94f, 0.84f, 0.59f, 1f);
        private static readonly Color UnderwaterSandStrokeColor = new Color(0.62f, 0.43f, 0.2f, 0.96f);
        private static readonly Color UnderwaterRockPaperColor = new Color(0.61f, 0.71f, 0.72f, 1f);
        private static readonly Color UnderwaterRockStrokeColor = new Color(0.15f, 0.31f, 0.36f, 0.98f);
        private static readonly Color UnderwaterRockAccentColor = new Color(0.35f, 0.57f, 0.59f, 0.68f);
        private static readonly Color NightCityTerrainPaperColor = new Color(0.46f, 0.53f, 0.64f, 1f);
        private static readonly Color NightCityTerrainStrokeColor = new Color(0.08f, 0.12f, 0.2f, 0.98f);
        private static readonly Color NightCityTerrainAccentColor = new Color(0.31f, 0.43f, 0.62f, 0.76f);
        private static readonly Color NightCitySkyColor = new Color(0.075f, 0.12f, 0.24f, 0.74f);
        private static readonly Color FactoryTerrainPaperColor = new Color(0.67f, 0.7f, 0.71f, 1f);
        private static readonly Color FactoryTerrainStrokeColor = new Color(0.13f, 0.15f, 0.17f, 0.98f);
        private static readonly Color FactoryTerrainAccentColor = new Color(0.39f, 0.43f, 0.46f, 0.78f);
        private static readonly Color FactoryBackdropWashColor = new Color(0.42f, 0.48f, 0.54f, 0.28f);
        private static readonly Color SpaceTerrainPaperColor = new Color(0.57f, 0.63f, 0.7f, 1f);
        private static readonly Color SpaceTerrainStrokeColor = new Color(0.055f, 0.08f, 0.14f, 0.99f);
        private static readonly Color SpaceTerrainAccentColor = new Color(0.27f, 0.4f, 0.57f, 0.82f);
        private static readonly Color SpaceTerrainGlowColor = new Color(0.26f, 0.78f, 1f, 0.84f);
        private static readonly Color SpaceBackdropColor = new Color(0.035f, 0.055f, 0.17f, 0.94f);
        private Transform visualThemeRoot;
        private string visualThemeStageId;

        public void ConfigureStageVisualTheme(string stageId, Transform root)
        {
            visualThemeStageId = stageId;
            visualThemeRoot = root;
        }

        public void CreateCaveStageBackdrop(
            string stageId,
            IList<StageObjectData> objects,
            Transform parent)
        {
            if (parent == null || !IsCaveStageId(stageId))
            {
                return;
            }

            Transform existing = parent.Find("Cave Stage Backdrop");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            Rect bounds = GetCaveBackdropBounds(objects);
            GameObject backdrop = new GameObject("Cave Stage Backdrop");
            backdrop.transform.SetParent(parent, false);
            backdrop.transform.localPosition = Vector3.zero;

            AddCaveBackdropPaper(backdrop.transform, bounds);
            AddCaveBackdropDrawing(
                backdrop.transform,
                bounds,
                objects,
                GetStableNatureVisualSeed(stageId + "|cave-backdrop"));
        }

        public void CreateUnderwaterStageBackdrop(
            string stageId,
            IList<StageObjectData> objects,
            Transform parent)
        {
            if (parent == null || !IsUnderwaterStageId(stageId))
            {
                return;
            }

            Transform existing = parent.Find("Underwater Stage Backdrop");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            Rect bounds = GetCaveBackdropBounds(objects);
            GameObject backdrop = new GameObject("Underwater Stage Backdrop");
            backdrop.transform.SetParent(parent, false);
            backdrop.transform.localPosition = Vector3.zero;
            int seed = GetStableNatureVisualSeed(stageId + "|underwater-backdrop");
            AddUnderwaterWaterBackdrop(backdrop.transform, bounds, seed);
            AddUnderwaterSeaweedField(backdrop.transform, objects, seed + 1709);
        }

        public void CreateNightCityStageBackdrop(
            string stageId,
            IList<StageObjectData> objects,
            Transform parent)
        {
            if (parent == null || !IsNightCityStageId(stageId))
            {
                return;
            }

            Transform existing = parent.Find("Night City Stage Backdrop");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            Rect bounds = GetCaveBackdropBounds(objects);
            GameObject backdrop = new GameObject("Night City Stage Backdrop");
            backdrop.transform.SetParent(parent, false);
            backdrop.transform.localPosition = Vector3.zero;

            int seed = GetStableNatureVisualSeed(stageId + "|night-city-backdrop");
            AddNightCityBackdrop(backdrop.transform, bounds, seed);
            AddNightCityInfrastructure(backdrop.transform, bounds, objects, seed + 2917);
        }

        public void CreateFactoryStageBackdrop(
            string stageId,
            IList<StageObjectData> objects,
            Transform parent)
        {
            if (parent == null || !IsFactoryStageId(stageId))
            {
                return;
            }

            Transform existing = parent.Find("Factory Stage Backdrop");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            Rect bounds = GetFactoryBackdropBounds(stageId, objects);
            GameObject backdrop = new GameObject("Factory Stage Backdrop");
            backdrop.transform.SetParent(parent, false);
            backdrop.transform.localPosition = Vector3.zero;

            int seed = GetStableNatureVisualSeed(stageId + "|factory-backdrop");
            bool keepArenaInteriorClear = HasAlternatePlayerLayoutDefinitions(stageId, objects);
            AddFactoryBackdrop(backdrop.transform, bounds, seed, !keepArenaInteriorClear);
            if (!keepArenaInteriorClear)
            {
                AddFactoryInfrastructure(backdrop.transform, bounds, objects, stageId, seed + 4513);
            }
        }

        public void CreateSpaceStageBackdrop(
            string stageId,
            IList<StageObjectData> objects,
            Transform parent)
        {
            if (parent == null || !IsSpaceStageId(stageId))
            {
                return;
            }

            Transform existing = parent.Find("Space Stage Backdrop");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            Rect bounds = GetSpaceBackdropBounds(stageId, objects);
            GameObject backdrop = new GameObject("Space Stage Backdrop");
            backdrop.transform.SetParent(parent, false);
            backdrop.transform.localPosition = Vector3.zero;

            int seed = GetStableNatureVisualSeed(stageId + "|space-backdrop");
            AddSpaceBackdrop(backdrop.transform, bounds, seed);
            AddSpaceInfrastructure(backdrop.transform, bounds, objects, stageId, seed + 6173);
        }

        public GameObject Create(StageObjectData data, Transform parent)
        {
            if (data == null)
            {
                return null;
            }

            if (IsBackgroundDecorationType(data.type))
            {
                return CreateBackgroundDecoration(data, parent);
            }

            switch (data.type)
            {
                case StageObjectType.StageBoundary:
                    return CreateStageBoundary(data, parent);
                case StageObjectType.BackgroundTree:
                case StageObjectType.BackgroundGrass:
                case StageObjectType.BackgroundFlower:
                case StageObjectType.BackgroundBush:
                case StageObjectType.BackgroundCloud:
                case StageObjectType.BackgroundPush:
                case StageObjectType.BackgroundArrow:
                    return CreateBackgroundDecoration(data, parent);
                case StageObjectType.JumpPad:
                case StageObjectType.Spring:
                    return CreateJumpPad(data, parent);
                case StageObjectType.Key:
                    return CreateKey(data, parent);
                case StageObjectType.Keyhole:
                    return CreateKeyhole(data, parent);
                case StageObjectType.InkScale:
                    return CreateInkScale(data, parent);
                case StageObjectType.BoxDropper:
                case StageObjectType.SpikeDropper:
                case StageObjectType.BombDropper:
                case StageObjectType.EnemyDropper:
                    return CreateBoxDropper(data, parent);
                case StageObjectType.Spike:
                    return CreateSpike(data, parent);
                case StageObjectType.Spawn:
                    return CreateMarker(data, parent, new Color(0.1f, 0.3f, 1f), "START");
                case StageObjectType.Goal:
                    return CreateGoal(data, parent);
                case StageObjectType.CollectibleFish:
                case StageObjectType.CollectibleCoin:
                case StageObjectType.CollectibleStar:
                    return CreateCollectible(data, parent);
                case StageObjectType.ChallengeClock:
                    return CreateChallengeClock(data, parent);
                case StageObjectType.BeamEmitter:
                    return CreateBeamEmitter(data, parent);
                case StageObjectType.MissileLauncher:
                    return CreateMissileLauncher(data, parent);
                case StageObjectType.Handgun:
                    return StageGun.CreateObject(data, parent, pushableLayer);
                case StageObjectType.Bazooka:
                    return StageBazooka.CreateObject(data, parent, pushableLayer);
                case StageObjectType.SpikePlanet:
                    return StageSpikePlanet.CreateObject(data, parent);
                case StageObjectType.MovingSpikePlanet:
                    return StageAerialHazardFactory.CreateMovingSpikePlanet(data, parent);
                case StageObjectType.EnemyBomber:
                    return StageAerialHazardFactory.CreateBombingEnemy(data, parent, this);
                case StageObjectType.PoseCharacterKey:
                    return StagePoseKeyFactory.CreateKey(data, parent, pushableLayer);
                case StageObjectType.PoseCharacterKeyhole:
                    return StagePoseKeyFactory.CreateKeyhole(data, parent);
                case StageObjectType.UpdraftZone:
                    return StagePoseKeyFactory.CreateUpdraft(data, parent);
                case StageObjectType.RedrawZone:
                    return StageRedrawZoneFactory.CreateRedrawZone(data, parent);
                case StageObjectType.SpeedRing2X:
                case StageObjectType.SpeedRing3X:
                    return StageSpeedBoostRing.CreateObject(data, parent);
                case StageObjectType.GrainEmitter:
                case StageObjectType.GrainScale:
                case StageObjectType.GrainGate:
                    return CreateGrainCarryObject(data, parent);
                case StageObjectType.EscortSpawner:
                case StageObjectType.EscortGoal:
                case StageObjectType.EscortPlayerOnlyFloor:
                case StageObjectType.EscortHeadBridge:
                    return StageEscortEditableObjectFactory.Create(data, parent);
                case StageObjectType.Dynamite:
                    return CreateDynamite(data, parent);
                case StageObjectType.EnemyWalker:
                case StageObjectType.EnemyJumper:
                case StageObjectType.EnemyCharger:
                case StageObjectType.EnemyFlyer:
                case StageObjectType.EnemyShooter:
                case StageObjectType.EnemyFlyerZigzag:
                case StageObjectType.EnemyFlyerOrbit:
                    return CreateEnemy(data, parent);
                case StageObjectType.Elevator:
                    return CreateElevator(data, parent);
                case StageObjectType.BalanceScale:
                case StageObjectType.Seesaw:
                case StageObjectType.Catapult:
                    return CreateBalanceScale(data, parent);
                case StageObjectType.Weight:
                case StageObjectType.WoodBox:
                case StageObjectType.IronBox:
                case StageObjectType.Ball:
                case StageObjectType.Barrel:
                case StageObjectType.Rock:
                case StageObjectType.IceBlock:
                case StageObjectType.FloatingBox:
                case StageObjectType.RubberBox:
                case StageObjectType.Bomb:
                case StageObjectType.PickupFuseBomb:
                case StageObjectType.Battery:
                case StageObjectType.Bucket:
                case StageObjectType.FallingRock:
                case StageObjectType.TriangleBox:
                    return CreateWeight(data, parent);
                case StageObjectType.BreakableWall:
                    return CreateBombBreakableWall(data, parent);
                case StageObjectType.BulletBreakableWall:
                    return CreateBulletBreakableWall(data, parent);
                case StageObjectType.Checkpoint:
                case StageObjectType.WarpEntrance:
                case StageObjectType.WarpExit:
                case StageObjectType.RespawnPoint:
                case StageObjectType.MidGoal:
                case StageObjectType.Button:
                case StageObjectType.WeightButton:
                case StageObjectType.SimultaneousButton:
                case StageObjectType.HoldButton:
                case StageObjectType.PressurePlate:
                case StageObjectType.EscortFriendButton:
                    return CreateButtonSwitch(data, parent);
                case StageObjectType.Lever:
                case StageObjectType.ToggleSwitch:
                case StageObjectType.TimerSwitch:
                case StageObjectType.RedSwitch:
                case StageObjectType.BlueSwitch:
                case StageObjectType.GreenSwitch:
                case StageObjectType.YellowSwitch:
                case StageObjectType.RemoteControl:
                case StageObjectType.Coin:
                case StageObjectType.Star:
                case StageObjectType.Fan:
                case StageObjectType.Magnet:
                case StageObjectType.Cannon:
                case StageObjectType.Gear:
                case StageObjectType.BigGear:
                case StageObjectType.RopePulley:
                case StageObjectType.Saw:
                case StageObjectType.BlackHole:
                case StageObjectType.Pendulum:
                case StageObjectType.Clock:
                case StageObjectType.Counter:
                case StageObjectType.TrafficLight:
                case StageObjectType.GoalEffect:
                    return CreateProp(data, parent);
                case StageObjectType.Wall:
                case StageObjectType.Platform:
                default:
                    return CreateSolid(data, parent);
            }
        }

        public static StageObjectData CreateDefaultData(StageObjectType type, Vector2 position)
        {
            Vector2 size;
            if (type == StageObjectType.BackgroundKeyNeeded)
            {
                size = new Vector2(3.6f, 2.4f);
            }
            else if (IsBackgroundDecorationType(type))
            {
                size = new Vector2(2.4f, 2.4f);
            }
            else
            {
                switch (type)
                {
                    case StageObjectType.StageBoundary:
                        size = new Vector2(30f, 18f);
                        break;
                    case StageObjectType.Wall:
                    case StageObjectType.ClimbableWall:
                    case StageObjectType.Door:
                    case StageObjectType.LockedDoor:
                    case StageObjectType.Shutter:
                    case StageObjectType.Fence:
                    case StageObjectType.LaserGate:
                    case StageObjectType.ColorGate:
                    case StageObjectType.OneWayGate:
                    case StageObjectType.TimedGate:
                    case StageObjectType.BreakableWall:
                    case StageObjectType.BulletBreakableWall:
                    case StageObjectType.HiddenWall:
                        size = new Vector2(0.5f, 2f);
                        break;
                    case StageObjectType.Spawn:
                    case StageObjectType.Checkpoint:
                    case StageObjectType.WarpEntrance:
                    case StageObjectType.WarpExit:
                    case StageObjectType.RespawnPoint:
                    case StageObjectType.MidGoal:
                        size = new Vector2(0.7f, 0.7f);
                        break;
                    case StageObjectType.Dynamite:
                        size = new Vector2(1.4f, 1.25f);
                        break;
                    case StageObjectType.EnemyWalker:
                    case StageObjectType.EnemyJumper:
                    case StageObjectType.EnemyCharger:
                    case StageObjectType.EnemyFlyer:
                    case StageObjectType.EnemyShooter:
                    case StageObjectType.EnemyFlyerZigzag:
                    case StageObjectType.EnemyFlyerOrbit:
                        size = new Vector2(1.25f, 1.3f);
                        break;
                    case StageObjectType.Handgun:
                        size = new Vector2(0.85f, 0.48f);
                        break;
                    case StageObjectType.Bazooka:
                        size = new Vector2(1.45f, 0.72f);
                        break;
                    case StageObjectType.SpikePlanet:
                    case StageObjectType.MovingSpikePlanet:
                        size = new Vector2(2.5f, 2.5f);
                        break;
                    case StageObjectType.EnemyBomber:
                        size = new Vector2(1.7f, 1.35f);
                        break;
                    case StageObjectType.PoseCharacterKey:
                        size = new Vector2(1.25f, 1.55f);
                        break;
                    case StageObjectType.PoseCharacterKeyhole:
                        size = new Vector2(1.6f, 1.9f);
                        break;
                    case StageObjectType.UpdraftZone:
                        size = new Vector2(20f, 12f);
                        break;
                    case StageObjectType.RedrawZone:
                        size = new Vector2(20f, 12f);
                        break;
                    case StageObjectType.SpeedRing2X:
                    case StageObjectType.SpeedRing3X:
                        size = new Vector2(2.2f, 2.2f);
                        break;
                    case StageObjectType.Goal:
                        size = new Vector2(1.15f, 2.05f);
                        break;
                    case StageObjectType.CollectibleFish:
                    case StageObjectType.CollectibleCoin:
                    case StageObjectType.CollectibleStar:
                        size = Vector2.one * 0.8f;
                        break;
                    case StageObjectType.ChallengeClock:
                        size = new Vector2(3.2f, 1.25f);
                        break;
                    case StageObjectType.Key:
                        size = new Vector2(1.35f, 1.6f);
                        break;
                    case StageObjectType.Keyhole:
                        size = new Vector2(1.35f, 1.6f);
                        break;
                    case StageObjectType.BalanceScale:
                    case StageObjectType.Seesaw:
                    case StageObjectType.Catapult:
                        size = new Vector2(4.5f, 0.5f);
                        break;
                    case StageObjectType.InkScale:
                        size = new Vector2(3f, 0.9f);
                        break;
                    case StageObjectType.OneWayPlatform:
                    case StageObjectType.MovingOneWayPlatform:
                    case StageObjectType.EscortPlayerOneWayFloor:
                        size = new Vector2(3f, 0.25f);
                        break;
                    case StageObjectType.BoxDropper:
                    case StageObjectType.SpikeDropper:
                    case StageObjectType.BombDropper:
                    case StageObjectType.EnemyDropper:
                        size = new Vector2(1.8f, 1.4f);
                        break;
                    case StageObjectType.BeamEmitter:
                    case StageObjectType.MissileLauncher:
                        size = new Vector2(1.25f, 0.9f);
                        break;
                    case StageObjectType.EscortSpawner:
                        size = new Vector2(2.2f, 1.35f);
                        break;
                    case StageObjectType.EscortGoal:
                        size = new Vector2(1.7f, 2.1f);
                        break;
                    case StageObjectType.EscortPlayerOnlyFloor:
                        size = new Vector2(8f, 0.72f);
                        break;
                    case StageObjectType.EscortHeadBridge:
                        size = new Vector2(1.25f, 3f);
                        break;
                    case StageObjectType.Button:
                    case StageObjectType.WeightButton:
                    case StageObjectType.SimultaneousButton:
                    case StageObjectType.HoldButton:
                    case StageObjectType.PressurePlate:
                    case StageObjectType.EscortFriendButton:
                        size = new Vector2(1f, 0.5f);
                        break;
                    case StageObjectType.GrainEmitter:
                        size = new Vector2(2f, 2.4f);
                        break;
                    case StageObjectType.GrainScale:
                        size = new Vector2(2.4f, 1f);
                        break;
                    case StageObjectType.GrainGate:
                        size = new Vector2(0.8f, 5f);
                        break;
                    case StageObjectType.Weight:
                    case StageObjectType.WoodBox:
                    case StageObjectType.IronBox:
                    case StageObjectType.Ball:
                    case StageObjectType.Barrel:
                    case StageObjectType.Rock:
                    case StageObjectType.IceBlock:
                    case StageObjectType.FloatingBox:
                    case StageObjectType.RubberBox:
                    case StageObjectType.Bomb:
                    case StageObjectType.PickupFuseBomb:
                    case StageObjectType.Battery:
                    case StageObjectType.Bucket:
                    case StageObjectType.FallingRock:
                    case StageObjectType.TriangleBox:
                        size = new Vector2(0.9f, 0.9f);
                        break;
                    case StageObjectType.Rope:
                    case StageObjectType.Ladder:
                    case StageObjectType.Laser:
                    case StageObjectType.Water:
                    case StageObjectType.Poison:
                    case StageObjectType.Fire:
                    case StageObjectType.Electricity:
                        size = new Vector2(0.5f, 2f);
                        break;
                    default:
                        size = new Vector2(3f, 0.5f);
                        break;
                }
            }

            return new StageObjectData
            {
                objectId = StageObjectId.New(),
                type = type,
                position = position,
                size = size,
                rotation = 0f,
                actionStrength = type == StageObjectType.InkScale
                    ? 300f
                    : type == StageObjectType.GrainScale
                        ? 80f
                    : type == StageObjectType.BreakableWall
                        ? 1f
                    : type == StageObjectType.BulletBreakableWall
                        ? 3f
                    : type == StageObjectType.MovingSpikePlanet
                        ? 8f
                    : type == StageObjectType.EnemyBomber
                        ? 3.2f
                    : type == StageObjectType.SpeedRing2X
                        ? 2f
                    : type == StageObjectType.SpeedRing3X
                        ? 3f
                    : type == StageObjectType.JumpPad || type == StageObjectType.Spring
                        ? 27f
                        : type == StageObjectType.MovingPlatform || type == StageObjectType.MovingOneWayPlatform
                            ? 6f
                            : type == StageObjectType.Elevator
                                ? 8f
                            : type == StageObjectType.FallingFloor
                                ? 0.4f
                                : type == StageObjectType.Belt
                                    || type == StageObjectType.ConveyorLeft
                                    || type == StageObjectType.ConveyorRight
                                        ? 3f
                                        : type == StageObjectType.BoxDropper || type == StageObjectType.SpikeDropper || type == StageObjectType.BombDropper || type == StageObjectType.EnemyDropper || type == StageObjectType.BeamEmitter || type == StageObjectType.MissileLauncher ? 2f : 0f,
                movementAngle = type == StageObjectType.ConveyorLeft ? 180f : 0f,
                movementSpeed = type == StageObjectType.MovingPlatform || type == StageObjectType.MovingOneWayPlatform ? 3.2f
                    : type == StageObjectType.EnemyWalker ? 2.4f
                    : type == StageObjectType.EnemyJumper ? 2.1f
                    : type == StageObjectType.EnemyCharger ? 1.7f
                    : type == StageObjectType.EnemyFlyer ? 2.7f
                    : type == StageObjectType.EnemyFlyerZigzag ? 2.45f
                    : type == StageObjectType.EnemyFlyerOrbit ? 1.9f
                    : type == StageObjectType.EnemyBomber ? 2.6f
                    : type == StageObjectType.MovingSpikePlanet ? 2.4f
                    : type == StageObjectType.EnemyShooter ? 0.5f
                    : type == StageObjectType.MissileLauncher ? 8f
                    : 0f,
                spawnPattern = type == StageObjectType.BombDropper ? 1 : 0,
                spawnBoxSize = type == StageObjectType.BoxDropper || type == StageObjectType.SpikeDropper || type == StageObjectType.BombDropper || type == StageObjectType.EnemyDropper ? 0.9f : 0f,
                bombFuseSeconds = type == StageObjectType.Bomb
                    || type == StageObjectType.PickupFuseBomb
                    || type == StageObjectType.BombDropper
                    || type == StageObjectType.Dynamite ? 5f
                    : type == StageObjectType.SpeedRing2X || type == StageObjectType.SpeedRing3X ? 1.5f : 0f
            };
        }

        private GameObject CreateGrainCarryObject(StageObjectData data, Transform parent)
        {
            GameObject obj = StageGrainCarryObjectFactory.Create(data, parent, groundLayer);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private GameObject CreateDynamite(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = StageObjectType.Dynamite.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            Vector2 size = new Vector2(Mathf.Max(0.65f, data.size.x), Mathf.Max(0.65f, data.size.y));
            BoxCollider2D trigger = obj.AddComponent<BoxCollider2D>();
            trigger.size = size;
            trigger.isTrigger = true;

            StageDynamite dynamite = obj.AddComponent<StageDynamite>();
            dynamite.Configure(data.bombFuseSeconds > 0f ? data.bombFuseSeconds : 5f, size);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private GameObject CreateEnemy(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = data.type.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            // Enemy rotation is used as its initial facing direction. Keeping the
            // physics body upright avoids a 180-degree left-facing enemy becoming
            // visually upside down.
            obj.transform.rotation = Quaternion.identity;
            obj.layer = pushableLayer;

            Vector2 size = new Vector2(Mathf.Max(0.7f, data.size.x), Mathf.Max(0.7f, data.size.y));
            Rigidbody2D body = obj.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = data.type == StageObjectType.EnemyFlyer
                || data.type == StageObjectType.EnemyFlyerZigzag
                || data.type == StageObjectType.EnemyFlyerOrbit
                || data.type == StageObjectType.EnemyBomber ? 0f : 1.65f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            CapsuleCollider2D enemyCollider = obj.AddComponent<CapsuleCollider2D>();
            enemyCollider.size = size;
            enemyCollider.direction = CapsuleDirection2D.Vertical;

            StageEnemyCharacter enemy = obj.AddComponent<StageEnemyCharacter>();
            float facing = Mathf.Cos(data.rotation * Mathf.Deg2Rad);
            enemy.Configure(data.type, size, data.movementSpeed > 0f ? data.movementSpeed : 0f, facing);
            AddEditorMetadata(obj, data);
            return obj;
        }

        public GameObject CreateBombingEnemyBase(StageObjectData data, Transform parent)
        {
            return CreateEnemy(data, parent);
        }

        public GameObject CreateSpawnedEnemy(
            StageObjectType type,
            string objectId,
            Vector2 position,
            float size,
            Transform parent,
            float speed,
            float facing)
        {
            StageObjectData data = CreateDefaultData(type, position);
            data.objectId = string.IsNullOrEmpty(objectId) ? StageObjectId.New() : objectId;
            data.size = Vector2.one * Mathf.Clamp(size, 0.7f, 2f);
            data.movementSpeed = Mathf.Clamp(speed > 0f ? speed : 2.4f, 0.5f, 8f);
            data.rotation = facing < 0f ? 180f : 0f;
            GameObject enemyObject = CreateEnemy(data, parent);
            StageEnemyCharacter enemy = enemyObject != null ? enemyObject.GetComponent<StageEnemyCharacter>() : null;
            enemy?.SetSpawnedByDevice();
            bool movingGauntletGhost = data.objectId.StartsWith(
                "11-1_ghost_", System.StringComparison.Ordinal);
            bool phasingGauntletGhost = data.objectId.StartsWith(
                "11-1_phase_ghost_", System.StringComparison.Ordinal);
            if (enemyObject != null && (movingGauntletGhost || phasingGauntletGhost))
            {
                StageMovingGauntletGhost ghost = enemyObject.AddComponent<StageMovingGauntletGhost>();
                ghost.Configure(data.movementSpeed, phasingGauntletGhost);
            }
            return enemyObject;
        }

        private GameObject CreateStageBoundary(StageObjectData data, Transform parent)
        {
            GameObject root = new GameObject(data.objectId);
            root.name = StageObjectType.StageBoundary.ToString();
            root.transform.SetParent(parent, false);
            root.transform.position = data.position;
            root.transform.rotation = Quaternion.identity;

            float width = Mathf.Max(4f, data.size.x);
            float height = Mathf.Max(4f, data.size.y);
            float storedThickness = Mathf.Clamp(
                data.pathThickness > 0f ? data.pathThickness : 0.5f,
                0.5f,
                1.5f);
            float thickness = GetStageBoundaryThickness(data);
            float outwardGrowth = thickness - storedThickness;
            bool useNatureTerrainStyle = UsesNatureStageTheme(parent);
            bool useCaveTerrainStyle = UsesCaveStageTheme(parent);
            bool useUnderwaterTerrainStyle = UsesUnderwaterStageTheme(parent);
            bool useNightCityTerrainStyle = UsesNightCityStageTheme(parent);
            bool useFactoryTerrainStyle = UsesFactoryStageTheme(parent);
            bool useSpaceTerrainStyle = UsesSpaceStageTheme(parent);
            Color stroke = useCaveTerrainStyle
                ? CaveTerrainStrokeColor
                : useUnderwaterTerrainStyle
                    ? UnderwaterRockStrokeColor
                : useNightCityTerrainStyle
                    ? NightCityTerrainStrokeColor
                : useFactoryTerrainStyle
                    ? FactoryTerrainStrokeColor
                : useSpaceTerrainStyle
                    ? SpaceTerrainStrokeColor
                : useNatureTerrainStyle
                    ? TitleTerrainStrokeColor
                    : new Color(0.16f, 0.17f, 0.2f, 1f);

            CreateBoundarySide(
                "Boundary Ceiling",
                new Vector2(0f, height * 0.5f - storedThickness + thickness * 0.5f),
                new Vector2(width + outwardGrowth * 2f, thickness),
                stroke,
                root.transform);

            // Preserve every old inner face and the open lower edge. The extra
            // thickness is placed outside the authored room, never over its contents.
            float wallHeight = Mathf.Max(0.35f, height - storedThickness);
            float wallCenterY = -storedThickness * 0.5f;
            CreateBoundarySide(
                "Boundary Left Wall",
                new Vector2(-width * 0.5f + storedThickness - thickness * 0.5f, wallCenterY),
                new Vector2(thickness, wallHeight),
                stroke,
                root.transform);
            CreateBoundarySide(
                "Boundary Right Wall",
                new Vector2(width * 0.5f - storedThickness + thickness * 0.5f, wallCenterY),
                new Vector2(thickness, wallHeight),
                stroke,
                root.transform);

            if (useNatureTerrainStyle || useCaveTerrainStyle || useUnderwaterTerrainStyle || useNightCityTerrainStyle || useFactoryTerrainStyle || useSpaceTerrainStyle)
            {
                float seamY = height * 0.5f - storedThickness;
                float leftX = -width * 0.5f + storedThickness - thickness * 0.5f;
                float rightX = width * 0.5f - storedThickness + thickness * 0.5f;
                AddTerrainSeamMask(
                    root.transform,
                    false,
                    root.transform.TransformPoint(new Vector3(leftX, seamY, 0f)),
                    thickness,
                    useNatureTerrainStyle,
                    useCaveTerrainStyle,
                    useUnderwaterTerrainStyle,
                    useNightCityTerrainStyle,
                    useFactoryTerrainStyle,
                    useSpaceTerrainStyle);
                AddTerrainSeamMask(
                    root.transform,
                    false,
                    root.transform.TransformPoint(new Vector3(rightX, seamY, 0f)),
                    thickness,
                    useNatureTerrainStyle,
                    useCaveTerrainStyle,
                    useUnderwaterTerrainStyle,
                    useNightCityTerrainStyle,
                    useFactoryTerrainStyle,
                    useSpaceTerrainStyle);
            }

            data.size = new Vector2(width, height);
            // Keep the authored value stable. GetStageBoundaryThickness applies
            // the visual/physical 3x scale so rebuilding or saving cannot compound it.
            data.pathThickness = storedThickness;
            AddEditorMetadata(root, data);
            return root;
        }

        internal static float GetStageBoundaryThickness(StageObjectData data)
        {
            return Mathf.Clamp(GetStageBoundaryInteriorThickness(data) * 3f, 1.5f, 4.5f);
        }

        internal static float GetStageBoundaryInteriorThickness(StageObjectData data)
        {
            return Mathf.Clamp(
                data != null && data.pathThickness > 0f ? data.pathThickness : 0.5f,
                0.5f,
                1.5f);
        }

        internal static bool TryGetStageBoundaryInnerEdges(
            StageEditorObject boundary,
            out float left,
            out float right,
            out float top)
        {
            left = 0f;
            right = 0f;
            top = 0f;
            if (boundary == null || boundary.type != StageObjectType.StageBoundary)
            {
                return false;
            }

            bool foundLeft = false;
            bool foundRight = false;
            bool foundTop = false;
            Collider2D[] colliders = boundary.GetComponentsInChildren<Collider2D>(false);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger) continue;

                string sideName = collider.gameObject.name;
                if (sideName == "Boundary Left Wall")
                {
                    left = collider.bounds.max.x;
                    foundLeft = true;
                }
                else if (sideName == "Boundary Right Wall")
                {
                    right = collider.bounds.min.x;
                    foundRight = true;
                }
                else if (sideName == "Boundary Ceiling")
                {
                    top = collider.bounds.min.y;
                    foundTop = true;
                }
            }

            return foundLeft && foundRight && foundTop;
        }

        private GameObject CreateSpike(StageObjectData data, Transform parent)
        {
            GameObject root = new GameObject(data.objectId);
            root.name = StageObjectType.Spike.ToString();
            root.transform.SetParent(parent, false);
            root.transform.position = data.position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            Vector2 size = new Vector2(
                Mathf.Max(0.35f, data.size.x),
                Mathf.Max(0.25f, data.size.y));
            BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
            trigger.size = new Vector2(size.x, size.y * 0.82f);
            trigger.offset = new Vector2(0f, -size.y * 0.09f);
            trigger.isTrigger = true;
            root.AddComponent<StageSpikeHazard>();

            int spikeCount = Mathf.Clamp(Mathf.RoundToInt(size.x / Mathf.Max(0.28f, size.y * 0.72f)), 1, 48);
            float spikeWidth = size.x / spikeCount;
            bool hasColoredPencilSpike = Resources.Load<Sprite>("StageObjects/NicoDraw/spike") != null;
            Color fill = new Color(0.94f, 0.22f, 0.18f, 0.88f);
            Color pencil = new Color(0.43f, 0.04f, 0.035f, 1f);
            for (int i = 0; i < spikeCount; i++)
            {
                float left = -size.x * 0.5f + i * spikeWidth;
                float right = left + spikeWidth;
                float center = (left + right) * 0.5f;
                if (hasColoredPencilSpike
                    && AddResourceSprite(
                        root.transform,
                        "StageObjects/NicoDraw/spike",
                        new Vector2(spikeWidth, size.y),
                        19,
                        "Colored Pencil Spike",
                        new Vector2(center, 0f)))
                {
                    continue;
                }

                Vector3[] vertices =
                {
                    new Vector3(left, -size.y * 0.5f, -0.01f),
                    new Vector3(right, -size.y * 0.5f, -0.01f),
                    new Vector3(center, size.y * 0.5f, -0.01f)
                };
                Mesh mesh = new Mesh
                {
                    name = "Spike Fill Mesh",
                    vertices = vertices,
                    triangles = new[] { 0, 2, 1 },
                    colors = new[] { fill, fill, fill }
                };
                mesh.RecalculateBounds();

                GameObject visual = new GameObject("Spike Fill");
                visual.transform.SetParent(root.transform, false);
                MeshFilter filter = visual.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = GetLineMaterial();
                renderer.sortingOrder = 18;

                AddDoodleLine(
                    "Spike Outline",
                    root.transform,
                    new[] { vertices[0], vertices[2], vertices[1], vertices[0] },
                    pencil,
                    0.045f,
                    19);
            }

            AddEditorMetadata(root, data);
            return root;
        }

        private void CreateBoundarySide(
            string name,
            Vector2 localPosition,
            Vector2 size,
            Color stroke,
            Transform parent)
        {
            GameObject side = new GameObject(name);
            side.transform.SetParent(parent, false);
            side.transform.localPosition = localPosition;
            side.layer = groundLayer;
            side.tag = "Ground";

            BoxCollider2D collider = side.AddComponent<BoxCollider2D>();
            collider.size = size;

            bool useNatureTerrainStyle = UsesNatureStageTheme(parent);
            bool useCaveTerrainStyle = UsesCaveStageTheme(parent);
            bool useUnderwaterTerrainStyle = UsesUnderwaterStageTheme(parent);
            bool useNightCityTerrainStyle = UsesNightCityStageTheme(parent);
            bool useFactoryTerrainStyle = UsesFactoryStageTheme(parent);
            bool useSpaceTerrainStyle = UsesSpaceStageTheme(parent);
            if (useCaveTerrainStyle)
            {
                AddSolidPaperBase(side.transform, size, CaveTerrainPaperColor);
            }
            else if (useUnderwaterTerrainStyle)
            {
                AddSolidPaperBase(side.transform, size, UnderwaterRockPaperColor);
            }
            else if (useNightCityTerrainStyle)
            {
                AddSolidPaperBase(side.transform, size, NightCityTerrainPaperColor);
            }
            else if (useFactoryTerrainStyle)
            {
                AddSolidPaperBase(side.transform, size, FactoryTerrainPaperColor);
            }
            else if (useSpaceTerrainStyle)
            {
                AddSolidPaperBase(side.transform, size, SpaceTerrainPaperColor);
            }
            else if (useNatureTerrainStyle)
            {
                AddSolidPaperBase(side.transform, size, TitleTerrainPaperColor);
            }
            else
            {
                AddSolidPaperBase(side.transform, size);
            }
            AddSolidWash(side.transform, size, stroke);
            int visualSeed = GetStableNatureVisualSeed(name);
            if (useCaveTerrainStyle)
            {
                AddCaveTerrainFill(side.transform, size, stroke, visualSeed);
            }
            else if (useUnderwaterTerrainStyle)
            {
                AddUnderwaterRockFill(side.transform, size, visualSeed);
            }
            else if (useNightCityTerrainStyle)
            {
                AddNightCityTerrainFill(side.transform, size, visualSeed);
            }
            else if (useFactoryTerrainStyle)
            {
                AddFactoryTerrainFill(side.transform, size, visualSeed);
            }
            else if (useSpaceTerrainStyle)
            {
                AddSpaceTerrainFill(side.transform, size, visualSeed);
            }
            else if (useNatureTerrainStyle)
            {
                AddNatureTerrainFill(
                    side.transform,
                    size,
                    stroke,
                    name != "Boundary Ceiling",
                    visualSeed);
            }
            else
            {
                AddSolidPencilFill(side.transform, size, stroke);
            }
            if (useCaveTerrainStyle)
            {
                AddCaveTerrainBoxOutline(side.transform, size);
                AddCaveStalactitesOnWorldBottomEdge(side.transform, size, visualSeed + 701);
            }
            else if (useUnderwaterTerrainStyle)
            {
                AddUnderwaterTerrainBoxOutline(side.transform, size, UnderwaterRockStrokeColor);
                if (string.Equals(name, "Boundary Ceiling", StringComparison.Ordinal))
                {
                    AddUnderwaterRockUnderside(side.transform, size, visualSeed + 701);
                }
            }
            else if (useNightCityTerrainStyle)
            {
                AddNightCityTerrainBoxOutline(side.transform, size, visualSeed);
            }
            else if (useFactoryTerrainStyle)
            {
                AddFactoryTerrainBoxOutline(side.transform, size, visualSeed);
            }
            else if (useSpaceTerrainStyle)
            {
                AddSpaceTerrainBoxOutline(side.transform, size, visualSeed);
            }
            else if (useNatureTerrainStyle)
            {
                AddNatureTerrainBoxOutline(side.transform, size);
            }
            else
            {
                AddSolidStraightBoxOutline(side.transform, size);
            }
        }

        private GameObject CreateSolid(StageObjectData data, Transform parent)
        {
            if (data.connectedRects != null && data.connectedRects.Length > 0)
            {
                return CreateConnectedRectSolid(data, parent);
            }

            if (data.pathPoints != null && data.pathPoints.Length >= 2)
            {
                return CreatePathSolid(data, parent);
            }

            bool useNatureTerrainStyle = UsesNatureTerrainStyle(data, parent);
            bool useCaveTerrainStyle = UsesCaveTerrainStyle(data, parent);
            bool useUnderwaterTerrainStyle = UsesUnderwaterTerrainStyle(data, parent);
            bool useNightCityTerrainStyle = UsesNightCityTerrainStyle(data, parent);
            bool useFactoryTerrainStyle = UsesFactoryTerrainStyle(data, parent);
            bool useSpaceTerrainStyle = UsesSpaceTerrainStyle(data, parent);
            bool useUnderwaterSandBody = useUnderwaterTerrainStyle && IsUnderwaterMainSandFloor(data);
            bool addUnderwaterSandCap = useUnderwaterTerrainStyle
                && !useUnderwaterSandBody
                && IsUnderwaterHorizontalPlatform(data);
            Color stroke = useCaveTerrainStyle
                ? CaveTerrainStrokeColor
                : useUnderwaterTerrainStyle
                    ? (useUnderwaterSandBody ? UnderwaterSandStrokeColor : UnderwaterRockStrokeColor)
                : useNightCityTerrainStyle
                    ? NightCityTerrainStrokeColor
                : useFactoryTerrainStyle
                    ? FactoryTerrainStrokeColor
                : useSpaceTerrainStyle
                    ? SpaceTerrainStrokeColor
                : useNatureTerrainStyle
                    ? TitleTerrainStrokeColor
                    : GetObjectColor(data.type);
            GameObject obj = new GameObject(data.objectId);
            obj.name = data.type.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            obj.layer = groundLayer;
            obj.tag = "Ground";

            BoxCollider2D collider = obj.AddComponent<BoxCollider2D>();
            collider.size = data.size;
            bool isIceSurface = data.type == StageObjectType.IceFloor
                || data.type == StageObjectType.SlipperySlope;
            if (isIceSurface)
            {
                collider.sharedMaterial = StageIceSurface.GetMaterial();
                obj.AddComponent<StageIceSurface>();
            }
            if (StageObjectCatalog.Get(data.type).Kind == StageObjectKind.Trigger || StageObjectCatalog.Get(data.type).Kind == StageObjectKind.Hazard)
            {
                collider.isTrigger = true;
            }
            bool isOneWayPlatform = data.type == StageObjectType.OneWayPlatform
                || data.type == StageObjectType.MovingOneWayPlatform
                || data.type == StageObjectType.EscortPlayerOneWayFloor;
            bool isMovingPlatform = data.type == StageObjectType.MovingPlatform
                || data.type == StageObjectType.MovingOneWayPlatform;
            if (isOneWayPlatform)
            {
                // PlatformEffector2D uses the object's local up direction, so a
                // rotated one-way floor still supports characters from its drawn
                // top side while allowing them to pass through from below.
                collider.usedByEffector = true;
                PlatformEffector2D effector = obj.AddComponent<PlatformEffector2D>();
                effector.useOneWay = true;
                effector.useOneWayGrouping = true;
                effector.surfaceArc = 165f;
                effector.useSideFriction = false;
                effector.useSideBounce = false;
            }

            if (useCaveTerrainStyle)
            {
                AddSolidPaperBase(obj.transform, data.size, CaveTerrainPaperColor);
            }
            else if (useUnderwaterTerrainStyle)
            {
                AddSolidPaperBase(
                    obj.transform,
                    data.size,
                    useUnderwaterSandBody ? UnderwaterSandPaperColor : UnderwaterRockPaperColor);
            }
            else if (useNightCityTerrainStyle)
            {
                AddSolidPaperBase(obj.transform, data.size, NightCityTerrainPaperColor);
            }
            else if (useFactoryTerrainStyle)
            {
                AddSolidPaperBase(obj.transform, data.size, FactoryTerrainPaperColor);
            }
            else if (useSpaceTerrainStyle)
            {
                AddSolidPaperBase(obj.transform, data.size, SpaceTerrainPaperColor);
            }
            else if (useNatureTerrainStyle)
            {
                AddSolidPaperBase(obj.transform, data.size, TitleTerrainPaperColor);
            }
            else
            {
                AddSolidPaperBase(obj.transform, data.size);
            }
            if (isOneWayPlatform)
            {
                AddOneWayPlatformTint(obj.transform, data.size);
            }
            AddSolidWash(obj.transform, data.size, stroke);
            int visualSeed = GetStableNatureVisualSeed(data.objectId);
            if (useCaveTerrainStyle)
            {
                AddCaveTerrainFill(obj.transform, data.size, stroke, visualSeed);
            }
            else if (useUnderwaterTerrainStyle)
            {
                if (useUnderwaterSandBody)
                {
                    AddUnderwaterSandFill(obj.transform, data.size, visualSeed);
                }
                else
                {
                    AddUnderwaterRockFill(obj.transform, data.size, visualSeed);
                    if (addUnderwaterSandCap)
                    {
                        AddUnderwaterSandCap(obj.transform, data.size, visualSeed + 409);
                    }
                }
            }
            else if (useNightCityTerrainStyle)
            {
                AddNightCityTerrainFill(obj.transform, data.size, visualSeed);
            }
            else if (useFactoryTerrainStyle)
            {
                AddFactoryTerrainFill(obj.transform, data.size, visualSeed);
            }
            else if (useSpaceTerrainStyle)
            {
                AddSpaceTerrainFill(obj.transform, data.size, visualSeed);
            }
            else if (useNatureTerrainStyle)
            {
                AddNatureTerrainFill(
                    obj.transform,
                    data.size,
                    stroke,
                    UsesNatureStageTheme(parent),
                    visualSeed);
            }
            else
            {
                AddSolidPencilFill(obj.transform, data.size, stroke);
            }
            if (isOneWayPlatform)
            {
                AddOneWayPlatformSurfaceVisual(obj.transform, data.size);
                if (useCaveTerrainStyle)
                {
                    AddCaveStalactitesOnWorldBottomEdge(obj.transform, data.size, visualSeed + 701);
                }
            }
            else
            {
                if (useCaveTerrainStyle)
                {
                    AddCaveTerrainBoxOutline(obj.transform, data.size);
                    AddCaveStalactitesOnWorldBottomEdge(obj.transform, data.size, visualSeed + 701);
                }
                else if (useUnderwaterTerrainStyle)
                {
                    AddUnderwaterTerrainBoxOutline(
                        obj.transform,
                        data.size,
                        useUnderwaterSandBody ? UnderwaterSandStrokeColor : UnderwaterRockStrokeColor);
                    if (!useUnderwaterSandBody && IsUnderwaterHorizontalPlatform(data))
                    {
                        AddUnderwaterRockUnderside(obj.transform, data.size, visualSeed + 701);
                    }
                }
                else if (useNightCityTerrainStyle)
                {
                    AddNightCityTerrainBoxOutline(obj.transform, data.size, visualSeed);
                }
                else if (useFactoryTerrainStyle)
                {
                    AddFactoryTerrainBoxOutline(obj.transform, data.size, visualSeed);
                }
                else if (useSpaceTerrainStyle)
                {
                    AddSpaceTerrainBoxOutline(obj.transform, data.size, visualSeed);
                }
                else if (useNatureTerrainStyle)
                {
                    AddNatureTerrainBoxOutline(obj.transform, data.size);
                }
                else
                {
                    AddSolidStraightBoxOutline(obj.transform, data.size);
                }
            }
            bool isConveyor = data.type == StageObjectType.Belt
                || data.type == StageObjectType.ConveyorLeft
                || data.type == StageObjectType.ConveyorRight;
            if (isConveyor)
            {
                AddConveyorBeltVisual(obj.transform, data);
            }
            if (data.type == StageObjectType.MovingOneWayPlatform)
            {
                AddStickyMovingPlatformVisual(obj.transform, data.size);
                obj.AddComponent<StageEscortStickySurface>();
            }
            if (data.type == StageObjectType.EscortPlayerOneWayFloor)
            {
                obj.AddComponent<StageEscortPlayerOnlyFloor>();
            }
            if (isMovingPlatform)
            {
                if (parent != null && parent.name == "RuntimeStageEditorRoot")
                {
                    AddMovingPlatformDirectionIndicator(obj.transform, data);
                }
                Rigidbody2D body = obj.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
            }
            AddEditorMetadata(obj, data);
            if (isConveyor)
            {
                obj.AddComponent<StageConveyorBelt>();
            }
            if (data.type == StageObjectType.FallingFloor)
            {
                obj.AddComponent<StageCrumblingFloor>();
            }
            return obj;
        }

        private GameObject CreateElevator(StageObjectData data, Transform parent)
        {
            float travel = Mathf.Clamp(data.actionStrength > 0f ? data.actionStrength : 8f, 1f, 30f);
            // An elevator is a thin rideable platform. Older editor data could
            // contain a tall drag-created rectangle; do not turn that entire
            // shaft area into one enormous moving block.
            Vector2 cabinSize = new Vector2(
                Mathf.Max(1.2f, data.size.x),
                Mathf.Clamp(data.size.y, 0.35f, 0.8f));
            data.actionStrength = travel;
            data.size = cabinSize;

            GameObject root = new GameObject(data.objectId);
            root.name = StageObjectType.Elevator.ToString();
            root.transform.SetParent(parent, false);
            root.transform.position = data.position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            root.layer = groundLayer;

            float railInset = Mathf.Max(0.35f, cabinSize.x * 0.32f);
            Color railInk = new Color(0.18f, 0.34f, 0.55f, 0.72f);
            AddDoodleLine(
                "Elevator Left Rail",
                root.transform,
                new[] { new Vector3(-railInset, 0f, 0.06f), new Vector3(-railInset, travel, 0.06f) },
                railInk,
                0.065f,
                4);
            AddDoodleLine(
                "Elevator Right Rail",
                root.transform,
                new[] { new Vector3(railInset, 0f, 0.06f), new Vector3(railInset, travel, 0.06f) },
                railInk,
                0.065f,
                4);

            float arrowX = railInset + 0.22f;
            for (float y = 1.2f; y < travel - 0.3f; y += 1.5f)
            {
                AddDoodleLine(
                    "Elevator Up Arrow",
                    root.transform,
                    new[]
                    {
                        new Vector3(arrowX, y - 0.22f, 0.05f),
                        new Vector3(arrowX, y + 0.22f, 0.05f),
                        new Vector3(arrowX - 0.14f, y + 0.07f, 0.05f),
                        new Vector3(arrowX, y + 0.22f, 0.05f),
                        new Vector3(arrowX + 0.14f, y + 0.07f, 0.05f)
                    },
                    railInk,
                    0.04f,
                    5);
            }

            GameObject cabin = new GameObject("Elevator Cabin");
            cabin.transform.SetParent(root.transform, false);
            cabin.transform.localPosition = Vector3.zero;
            cabin.layer = groundLayer;
            cabin.tag = "Ground";

            BoxCollider2D collider = cabin.AddComponent<BoxCollider2D>();
            collider.size = cabinSize;
            if (UsesCaveStageTheme(parent))
            {
                int visualSeed = GetStableNatureVisualSeed(data.objectId) + 977;
                AddSolidPaperBase(cabin.transform, cabinSize, CaveTerrainPaperColor);
                AddSolidWash(cabin.transform, cabinSize, CaveTerrainStrokeColor);
                AddCaveTerrainFill(cabin.transform, cabinSize, CaveTerrainStrokeColor, visualSeed);
                AddCaveTerrainBoxOutline(cabin.transform, cabinSize);
                AddCaveStalactitesOnWorldBottomEdge(cabin.transform, cabinSize, visualSeed + 701);
            }
            else if (UsesFactoryStageTheme(parent))
            {
                int visualSeed = GetStableNatureVisualSeed(data.objectId) + 977;
                AddSolidPaperBase(cabin.transform, cabinSize, FactoryTerrainPaperColor);
                AddSolidWash(cabin.transform, cabinSize, FactoryTerrainStrokeColor);
                AddFactoryTerrainFill(cabin.transform, cabinSize, visualSeed);
                AddFactoryTerrainBoxOutline(cabin.transform, cabinSize, visualSeed);
            }
            else if (UsesSpaceStageTheme(parent))
            {
                int visualSeed = GetStableNatureVisualSeed(data.objectId) + 977;
                AddSolidPaperBase(cabin.transform, cabinSize, SpaceTerrainPaperColor);
                AddSolidWash(cabin.transform, cabinSize, SpaceTerrainStrokeColor);
                AddSpaceTerrainFill(cabin.transform, cabinSize, visualSeed);
                AddSpaceTerrainBoxOutline(cabin.transform, cabinSize, visualSeed);
            }
            else
            {
                AddSolidPaperBase(cabin.transform, cabinSize);
                AddSolidWash(cabin.transform, cabinSize, new Color(0.12f, 0.48f, 0.86f, 1f));
                AddSolidPencilFill(cabin.transform, cabinSize, new Color(0.12f, 0.48f, 0.86f, 1f));
                AddSolidStraightBoxOutline(cabin.transform, cabinSize);
            }

            Rigidbody2D body = cabin.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            AddEditorMetadata(root, data);
            if (parent == null || parent.name != "RuntimeStageEditorRoot")
            {
                StageElevator elevator = root.AddComponent<StageElevator>();
                elevator.Configure(body, travel, 2.2f);
            }
            return root;
        }

        private static void AddSolidStraightBoxOutline(
            Transform parent, Vector2 size, float outlineWidth = 0.055f)
        {
            AddSolidStraightBoxOutline(
                parent,
                size,
                Color.black,
                new Color(0.1f, 0.48f, 0.95f, 0.42f),
                outlineWidth);
        }

        private static void AddSolidStraightBoxOutline(
            Transform parent,
            Vector2 size,
            Color outlineColor,
            Color accentColor,
            float outlineWidth = 0.055f)
        {
            float x = size.x * 0.5f;
            float y = size.y * 0.5f;
            Vector3[] outline =
            {
                new Vector3(-x, -y, 0f),
                new Vector3(x, -y, 0f),
                new Vector3(x, y, 0f),
                new Vector3(-x, y, 0f),
                new Vector3(-x, -y, 0f)
            };
            AddDoodleLine("Solid Straight Outline", parent, outline, outlineColor, outlineWidth, 12);
            Vector3 accent = new Vector3(0.015f, -0.015f, 0f);
            Vector3[] accentOutline = new Vector3[outline.Length];
            for (int i = 0; i < outline.Length; i++) accentOutline[i] = outline[i] + accent;
            AddDoodleLine("Solid Straight Accent", parent, accentOutline, accentColor, outlineWidth * 0.47f, 11);
        }

        private static void AddNatureTerrainBoxOutline(Transform parent, Vector2 size)
        {
            Vector2[] corners =
            {
                new Vector2(-size.x * 0.5f, -size.y * 0.5f),
                new Vector2(size.x * 0.5f, -size.y * 0.5f),
                new Vector2(size.x * 0.5f, size.y * 0.5f),
                new Vector2(-size.x * 0.5f, size.y * 0.5f)
            };
            List<Vector3> main = new List<Vector3>(37);
            List<Vector3> echo = new List<Vector3>(37);
            List<Vector3> dryPencil = new List<Vector3>(37);
            for (int edgeIndex = 0; edgeIndex < corners.Length; edgeIndex++)
            {
                Vector2 from = corners[edgeIndex];
                Vector2 to = corners[(edgeIndex + 1) % corners.Length];
                Vector2 direction = to - from;
                Vector2 inward = new Vector2(-direction.y, direction.x).normalized;
                const int segmentCount = 8;
                for (int pointIndex = 0; pointIndex <= segmentCount; pointIndex++)
                {
                    if (edgeIndex > 0 && pointIndex == 0)
                    {
                        continue;
                    }

                    float t = pointIndex / (float)segmentCount;
                    float envelope = Mathf.Sin(t * Mathf.PI);
                    float wobble = (
                        Mathf.Sin(edgeIndex * 2.17f + pointIndex * 1.73f) * 0.012f
                        + Mathf.Sin(edgeIndex * 5.31f + pointIndex * 3.07f) * 0.006f) * envelope;
                    Vector2 point = Vector2.Lerp(from, to, t);
                    main.Add(point + inward * wobble);
                    echo.Add(point + inward * (0.025f + wobble * 0.65f));
                    dryPencil.Add(point + inward * (-0.018f - wobble * 0.42f));
                }
            }
            main.Add(main[0]);
            echo.Add(echo[0]);
            dryPencil.Add(dryPencil[0]);

            AddDoodleLine(
                "Nature Green Pencil Outline",
                parent,
                main.ToArray(),
                TitleTerrainStrokeColor,
                0.082f,
                12);
            AddDoodleLine(
                "Nature Dry Pencil Outer Edge",
                parent,
                dryPencil.ToArray(),
                new Color(TitleTerrainStrokeColor.r, TitleTerrainStrokeColor.g, TitleTerrainStrokeColor.b, 0.5f),
                0.028f,
                13);
            AddDoodleLine(
                "Nature Green Pencil Echo",
                parent,
                echo.ToArray(),
                TitleTerrainAccentColor,
                0.034f,
                11);
        }

        private static void AddOneWayPlatformTint(Transform parent, Vector2 size)
        {
            GameObject tintObject = new GameObject("One Way Platform Blue Fill");
            tintObject.transform.SetParent(parent, false);
            tintObject.transform.localPosition = new Vector3(0f, 0f, 0.025f);
            tintObject.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer = tintObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = new Color(0.56f, 0.86f, 1f, 0.72f);
            renderer.sortingOrder = 3;
        }

        private static void AddOneWayPlatformSurfaceVisual(Transform parent, Vector2 size)
        {
            float halfWidth = size.x * 0.5f;
            float bottom = -size.y * 0.5f;
            float top = size.y * 0.5f;
            Color surfaceBlue = new Color(0.02f, 0.42f, 0.9f, 0.95f);
            AddDoodleLine(
                "One Way Platform Top Surface",
                parent,
                new[]
                {
                    new Vector3(-halfWidth, top + 0.015f, 0f),
                    new Vector3(halfWidth, top + 0.015f, 0f)
                },
                surfaceBlue,
                0.075f,
                14);

            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            Color dashedBlue = new Color(0.02f, 0.42f, 0.9f, 0.78f);
            AppendDashedPlatformEdge(
                vertices, colors, triangles,
                new Vector3(-halfWidth, bottom, 0f),
                new Vector3(halfWidth, bottom, 0f),
                dashedBlue);
            AppendDashedPlatformEdge(
                vertices, colors, triangles,
                new Vector3(-halfWidth, bottom, 0f),
                new Vector3(-halfWidth, top, 0f),
                dashedBlue);
            AppendDashedPlatformEdge(
                vertices, colors, triangles,
                new Vector3(halfWidth, bottom, 0f),
                new Vector3(halfWidth, top, 0f),
                dashedBlue);
            CreatePencilMesh(parent, "One Way Platform Dashed Outline", vertices, colors, triangles, 13);
        }

        private static void AddStickyMovingPlatformVisual(Transform parent, Vector2 size)
        {
            GameObject gel = new GameObject("Sticky Moving Platform Gel");
            gel.transform.SetParent(parent, false);
            gel.transform.localPosition = new Vector3(0f, size.y * 0.1f, 0.03f);
            gel.transform.localScale = new Vector3(size.x * 0.96f, size.y * 0.72f, 1f);
            SpriteRenderer gelRenderer = gel.AddComponent<SpriteRenderer>();
            gelRenderer.sprite = GetSquareSprite();
            gelRenderer.color = new Color(0.25f, 0.82f, 0.43f, 0.62f);
            gelRenderer.sortingOrder = 8;

            Color stickyInk = new Color(0.04f, 0.52f, 0.25f, 0.95f);
            float halfWidth = size.x * 0.48f;
            float top = size.y * 0.5f + 0.035f;
            int segments = Mathf.Max(6, Mathf.CeilToInt(size.x / 0.42f));
            Vector3[] wave = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float y = top + Mathf.Sin(t * Mathf.PI * segments) * 0.055f;
                wave[i] = new Vector3(Mathf.Lerp(-halfWidth, halfWidth, t), y, 0f);
            }
            AddDoodleLine("Sticky Gel Surface", parent, wave, stickyInk, 0.075f, 16);

            float bottom = -size.y * 0.5f;
            for (int i = 1; i <= 3; i++)
            {
                float x = Mathf.Lerp(-halfWidth, halfWidth, i / 4f);
                float drip = 0.08f + 0.045f * (i % 2);
                AddDoodleLine(
                    "Sticky Gel Drip",
                    parent,
                    new[] { new Vector3(x, bottom + 0.08f, 0f), new Vector3(x, bottom - drip, 0f) },
                    stickyInk,
                    0.06f,
                    16);
            }
        }

        private static void AppendDashedPlatformEdge(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector3 from,
            Vector3 to,
            Color color)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length <= 0.001f)
            {
                return;
            }

            Vector3 direction = delta / length;
            const float dashLength = 0.18f;
            const float gapLength = 0.11f;
            for (float cursor = 0f; cursor < length; cursor += dashLength + gapLength)
            {
                float dashEnd = Mathf.Min(cursor + dashLength, length);
                AppendPencilQuad(
                    vertices,
                    colors,
                    triangles,
                    from + direction * cursor,
                    from + direction * dashEnd,
                    0.05f,
                    color);
            }
        }

        private static void AddMovingPlatformDirectionIndicator(Transform parent, StageObjectData data)
        {
            // movementAngle is stored in stage space, while this guide is a child
            // of the (possibly rotated) platform. Cancel the platform rotation so
            // the preview points at the same destination as the runtime movement.
            float radians = (data.movementAngle - data.rotation) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            float previewLength = Mathf.Clamp(data.actionStrength > 0f ? data.actionStrength : 6f, 1f, 100f);
            Vector2 start = Vector2.zero;
            Vector2 end = direction * previewLength;
            Color guide = new Color(0.1f, 0.48f, 0.95f, 0.5f);

            AddDoodleLine(
                "Movement Distance Guide",
                parent,
                new[] { (Vector3)start, (Vector3)end },
                guide,
                0.045f,
                20);

            Vector2 side = new Vector2(-direction.y, direction.x);
            float arrowSize = Mathf.Clamp(0.26f + previewLength * 0.025f, 0.3f, 0.62f);
            Vector2 arrowBase = end - direction * arrowSize;
            AddDoodleLine(
                "Movement Arrow",
                parent,
                new[]
                {
                    (Vector3)(arrowBase + side * arrowSize * 0.58f),
                    (Vector3)end,
                    (Vector3)(arrowBase - side * arrowSize * 0.58f)
                },
                new Color(0.06f, 0.34f, 0.9f, 0.78f),
                0.055f,
                21);

            AddDoodleCircleAt(
                parent,
                end,
                Mathf.Clamp(0.14f + previewLength * 0.008f, 0.16f, 0.3f),
                new Color(0.06f, 0.34f, 0.9f, 0.68f),
                0.04f,
                20);
        }

        private static void AddConveyorBeltVisual(Transform parent, StageObjectData data)
        {
            float halfWidth = Mathf.Max(0.2f, data.size.x * 0.5f);
            float halfHeight = Mathf.Max(0.1f, data.size.y * 0.5f);
            float direction = Mathf.Cos(data.movementAngle * Mathf.Deg2Rad) >= 0f ? 1f : -1f;
            float beltHalfHeight = halfHeight * 0.62f;
            float rollerRadius = Mathf.Clamp(halfHeight * 0.42f, 0.065f, 0.24f);
            float travelHalfWidth = Mathf.Max(0.05f, halfWidth - rollerRadius * 1.35f);
            Color graphite = new Color(0.09f, 0.1f, 0.12f, 0.98f);
            Color beltBlue = new Color(0.12f, 0.34f, 0.48f, 0.96f);
            Color metal = new Color(0.78f, 0.56f, 0.18f, 0.96f);

            AddMovableBase(
                parent,
                GetSquareSprite(),
                new Color(0.13f, 0.2f, 0.24f, 0.94f),
                new Vector2(data.size.x * 0.96f, beltHalfHeight * 2f));
            AddDoodleLine("Conveyor Upper Belt Edge", parent, new[]
            {
                new Vector3(-halfWidth * 0.97f, beltHalfHeight, -0.04f),
                new Vector3(halfWidth * 0.97f, beltHalfHeight, -0.04f)
            }, graphite, 0.065f, 21);
            AddDoodleLine("Conveyor Lower Belt Edge", parent, new[]
            {
                new Vector3(-halfWidth * 0.97f, -beltHalfHeight, -0.04f),
                new Vector3(halfWidth * 0.97f, -beltHalfHeight, -0.04f)
            }, graphite, 0.065f, 21);

            int treadCount = Mathf.Clamp(Mathf.CeilToInt(data.size.x / Mathf.Max(0.24f, data.size.y * 0.55f)), 4, 28);
            Transform[] treads = new Transform[treadCount];
            for (int i = 0; i < treadCount; i++)
            {
                GameObject tread = new GameObject("Conveyor Moving Tread");
                tread.transform.SetParent(parent, false);
                tread.transform.localPosition = new Vector3(
                    Mathf.Lerp(-travelHalfWidth, travelHalfWidth, i / (float)treadCount),
                    0f,
                    -0.045f);
                float lean = Mathf.Min(0.09f, data.size.y * 0.16f) * direction;
                AddDoodleLine("Tread Pencil Stroke", tread.transform, new[]
                {
                    new Vector3(-lean, -beltHalfHeight * 0.74f, 0f),
                    new Vector3(lean, beltHalfHeight * 0.74f, 0f)
                }, beltBlue, 0.045f, 22);
                treads[i] = tread.transform;
            }

            // Keep the travel direction readable even when the animated treads
            // are momentarily between frames or the belt is viewed from afar.
            Color directionInk = new Color(1f, 0.78f, 0.12f, 1f);
            const float directionMarkInterval = 1.2f;
            float directionMarkSpan = travelHalfWidth * 1.82f;
            int directionMarkCount = Mathf.Clamp(
                Mathf.FloorToInt(directionMarkSpan / directionMarkInterval) + 1,
                1,
                128);
            float markHalfWidth = Mathf.Clamp(data.size.x / (directionMarkCount * 5.2f), 0.1f, 0.24f);
            float markHalfHeight = Mathf.Clamp(beltHalfHeight * 0.5f, 0.045f, 0.13f);
            float occupiedMarkWidth = (directionMarkCount - 1) * directionMarkInterval;
            for (int i = 0; i < directionMarkCount; i++)
            {
                float x = -occupiedMarkWidth * 0.5f + i * directionMarkInterval;
                float tipX = x + direction * markHalfWidth;
                float tailX = x - direction * markHalfWidth;
                AddDoodleLine("Conveyor Direction Mark", parent, new[]
                {
                    new Vector3(tailX, 0f, -0.07f),
                    new Vector3(tipX, 0f, -0.07f),
                    new Vector3(x, markHalfHeight, -0.07f),
                    new Vector3(tipX, 0f, -0.07f),
                    new Vector3(x, -markHalfHeight, -0.07f)
                }, directionInk, 0.055f, 26);
            }

            Transform[] rollers = new Transform[2];
            rollers[0] = AddConveyorRoller(parent, -travelHalfWidth, rollerRadius, metal, graphite);
            rollers[1] = AddConveyorRoller(parent, travelHalfWidth, rollerRadius, metal, graphite);

            StageConveyorVisualAnimator animator = parent.gameObject.AddComponent<StageConveyorVisualAnimator>();
            animator.Configure(
                treads,
                rollers,
                travelHalfWidth,
                rollerRadius,
                direction,
                data.actionStrength > 0f ? data.actionStrength : 3f);
        }

        private static Transform AddConveyorRoller(
            Transform parent,
            float x,
            float radius,
            Color fill,
            Color outline)
        {
            GameObject roller = new GameObject("Conveyor Turning Roller");
            roller.transform.SetParent(parent, false);
            roller.transform.localPosition = new Vector3(x, 0f, -0.055f);
            AddMovableBase(roller.transform, GetCircleSprite(), fill, Vector2.one * radius * 1.72f);
            AddDoodleCircleAt(roller.transform, Vector2.zero, radius, outline, 0.04f, 24);
            AddDoodleLine("Roller Spokes", roller.transform, new[]
            {
                new Vector3(-radius * 0.72f, 0f, -0.02f),
                new Vector3(radius * 0.72f, 0f, -0.02f),
                new Vector3(0f, 0f, -0.02f),
                new Vector3(0f, -radius * 0.72f, -0.02f),
                new Vector3(0f, radius * 0.72f, -0.02f)
            }, outline, 0.035f, 25);
            return roller.transform;
        }

        public void RefreshBridgeConnectionVisuals(IList<StageObjectData> objects, Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            Transform existing = parent.Find("Bridge Terrain Connections");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            if (objects == null || objects.Count == 0)
            {
                return;
            }

            GameObject connectionRoot = new GameObject("Bridge Terrain Connections");
            connectionRoot.transform.SetParent(parent, false);
            bool useNatureTerrainStyle = UsesNatureStageTheme(parent);
            bool useCaveTerrainStyle = UsesCaveStageTheme(parent);
            for (int childIndex = 0; childIndex < parent.childCount; childIndex++)
            {
                Transform oldMask = parent.GetChild(childIndex).Find("Terrain Connection Masks");
                if (oldMask != null) DestroyImmediate(oldMask.gameObject);
            }

            HashSet<string> dynamicTargets = new HashSet<string>();
            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData source = objects[i];
                if (source == null || string.IsNullOrEmpty(source.linkTargetId))
                {
                    continue;
                }

                if (source.linkAction == "RevealGrowRightToLeft"
                    || source.linkAction == "RevealGrow"
                    || source.linkAction == "Hide")
                {
                    dynamicTargets.Add(source.linkTargetId);
                }
            }

            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData a = objects[i];
                if (!CanJoinTerrainVisual(a))
                {
                    continue;
                }

                List<Rect> rectsA = new List<Rect>();
                AppendStageRects(a, rectsA);
                for (int j = i + 1; j < objects.Count; j++)
                {
                    StageObjectData b = objects[j];
                    if (!CanJoinTerrainVisual(b))
                    {
                        continue;
                    }

                    List<Rect> rectsB = new List<Rect>();
                    AppendStageRects(b, rectsB);
                    StageObjectData dynamicPart = dynamicTargets.Contains(a.objectId) ? a : dynamicTargets.Contains(b.objectId) ? b : null;
                    Transform maskParent = connectionRoot.transform;
                    if (dynamicPart != null)
                    {
                        Transform target = FindStageObjectTransform(parent, dynamicPart.objectId);
                        if (target != null)
                        {
                            Transform maskRoot = target.Find("Terrain Connection Masks");
                            if (maskRoot == null)
                            {
                                GameObject maskObject = new GameObject("Terrain Connection Masks");
                                maskObject.transform.SetParent(target, false);
                                maskRoot = maskObject.transform;
                            }
                            maskParent = maskRoot;
                        }
                    }

                    for (int rectAIndex = 0; rectAIndex < rectsA.Count; rectAIndex++)
                    {
                        for (int rectBIndex = 0; rectBIndex < rectsB.Count; rectBIndex++)
                        {
                            if (TryGetSharedEdge(rectsA[rectAIndex], rectsB[rectBIndex], out bool vertical, out Vector2 seamCenter, out float seamLength))
                            {
                                AddTerrainSeamMask(
                                    maskParent,
                                    vertical,
                                    seamCenter,
                                    seamLength,
                                    useNatureTerrainStyle,
                                    useCaveTerrainStyle);
                            }
                        }
                    }
                }
            }
        }

        private static bool CanJoinTerrainVisual(StageObjectData data)
        {
            return data != null
                && (data.type == StageObjectType.Platform || data.type == StageObjectType.Wall)
                && (data.pathPoints == null || data.pathPoints.Length < 2)
                && IsAxisAligned(data.rotation);
        }

        private static bool TryGetSharedEdge(Rect a, Rect b, out bool vertical, out Vector2 center, out float length)
        {
            const float tolerance = 0.24f;
            float yMin = Mathf.Max(a.yMin, b.yMin);
            float yMax = Mathf.Min(a.yMax, b.yMax);
            if (yMax - yMin > 0.08f && (Mathf.Abs(a.xMax - b.xMin) <= tolerance || Mathf.Abs(b.xMax - a.xMin) <= tolerance))
            {
                float x = Mathf.Abs(a.xMax - b.xMin) <= tolerance ? (a.xMax + b.xMin) * 0.5f : (b.xMax + a.xMin) * 0.5f;
                vertical = true;
                center = new Vector2(x, (yMin + yMax) * 0.5f);
                length = yMax - yMin;
                return true;
            }

            float xMin = Mathf.Max(a.xMin, b.xMin);
            float xMax = Mathf.Min(a.xMax, b.xMax);
            if (xMax - xMin > 0.08f && (Mathf.Abs(a.yMax - b.yMin) <= tolerance || Mathf.Abs(b.yMax - a.yMin) <= tolerance))
            {
                float y = Mathf.Abs(a.yMax - b.yMin) <= tolerance ? (a.yMax + b.yMin) * 0.5f : (b.yMax + a.yMin) * 0.5f;
                vertical = false;
                center = new Vector2((xMin + xMax) * 0.5f, y);
                length = xMax - xMin;
                return true;
            }

            vertical = false;
            center = Vector2.zero;
            length = 0f;
            return false;
        }

        private static void AddTerrainSeamMask(
            Transform maskParent,
            bool vertical,
            Vector2 worldCenter,
            float length,
            bool useNatureTerrainStyle,
            bool useCaveTerrainStyle = false,
            bool useUnderwaterTerrainStyle = false,
            bool useNightCityTerrainStyle = false,
            bool useFactoryTerrainStyle = false,
            bool useSpaceTerrainStyle = false)
        {
            float visibleLength = Mathf.Max(0.04f, length - 0.07f);
            GameObject mask = new GameObject("Connected Terrain Seam Mask");
            mask.transform.SetParent(maskParent, false);
            mask.transform.localPosition = maskParent.InverseTransformPoint(new Vector3(worldCenter.x, worldCenter.y, 0f));
            mask.transform.localScale = vertical ? new Vector3(0.11f, visibleLength, 1f) : new Vector3(visibleLength, 0.11f, 1f);
            SpriteRenderer renderer = mask.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = useCaveTerrainStyle
                ? CaveTerrainPaperColor
                : useUnderwaterTerrainStyle
                    ? UnderwaterRockPaperColor
                : useNightCityTerrainStyle
                    ? NightCityTerrainPaperColor
                : useFactoryTerrainStyle
                    ? FactoryTerrainPaperColor
                : useSpaceTerrainStyle
                    ? SpaceTerrainPaperColor
                : useNatureTerrainStyle
                    ? TitleTerrainPaperColor
                    : new Color(0.985f, 0.975f, 0.93f, 1f);
            renderer.sortingOrder = 18;

            Color pencil = useCaveTerrainStyle
                ? new Color(CaveTerrainStrokeColor.r, CaveTerrainStrokeColor.g, CaveTerrainStrokeColor.b, 0.34f)
                : useUnderwaterTerrainStyle
                    ? new Color(UnderwaterRockStrokeColor.r, UnderwaterRockStrokeColor.g, UnderwaterRockStrokeColor.b, 0.34f)
                : useNightCityTerrainStyle
                    ? new Color(NightCityTerrainStrokeColor.r, NightCityTerrainStrokeColor.g, NightCityTerrainStrokeColor.b, 0.38f)
                : useFactoryTerrainStyle
                    ? new Color(FactoryTerrainStrokeColor.r, FactoryTerrainStrokeColor.g, FactoryTerrainStrokeColor.b, 0.4f)
                : useSpaceTerrainStyle
                    ? new Color(SpaceTerrainStrokeColor.r, SpaceTerrainStrokeColor.g, SpaceTerrainStrokeColor.b, 0.42f)
                : useNatureTerrainStyle
                    ? new Color(TitleTerrainStrokeColor.r, TitleTerrainStrokeColor.g, TitleTerrainStrokeColor.b, 0.3f)
                    : new Color(0.22f, 0.2f, 0.16f, 0.22f);
            int strokes = Mathf.Max(2, Mathf.CeilToInt(visibleLength / 0.18f));
            for (int i = 0; i < strokes; i++)
            {
                float along = -visibleLength * 0.5f + visibleLength * (i + 0.5f) / strokes;
                Vector3 from = vertical ? new Vector3(-0.075f, along - 0.045f, 0f) : new Vector3(along - 0.045f, -0.075f, 0f);
                Vector3 to = vertical ? new Vector3(0.075f, along + 0.045f, 0f) : new Vector3(along + 0.045f, 0.075f, 0f);
                AddDoodleLine("Connected Terrain Seam Pencil", maskParent, new[] { mask.transform.localPosition + from, mask.transform.localPosition + to }, pencil, 0.012f, 19);
            }
        }

        private static Transform FindStageObjectTransform(Transform parent, string objectId)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                StageEditorObject marker = parent.GetChild(i).GetComponent<StageEditorObject>();
                if (marker != null && marker.objectId == objectId) return marker.transform;
            }
            return null;
        }

        public void FitSeparateBridges(IList<StageObjectData> objects)
        {
            if (objects == null)
            {
                return;
            }

            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData data = objects[i];
                if (data != null && Mathf.Abs(Mathf.DeltaAngle(0f, data.rotation)) < 2f)
                {
                    data.rotation = 0f;
                }
            }

            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData bridge = objects[i];
                if (!IsSeparateHorizontalBridge(bridge))
                {
                    continue;
                }

                Rect bridgeRect = RectFromStageData(bridge.position, bridge.size);
                float leftEdge = 0f;
                float rightEdge = 0f;
                float bestLeftGap = float.MaxValue;
                float bestRightGap = float.MaxValue;
                bool hasLeft = false;
                bool hasRight = false;
                const float fitDistance = 1.25f;

                for (int candidateIndex = 0; candidateIndex < objects.Count; candidateIndex++)
                {
                    StageObjectData candidate = objects[candidateIndex];
                    if (!CanProvideBridgeBank(candidate, bridge))
                    {
                        continue;
                    }

                    List<Rect> parts = new List<Rect>();
                    AppendStageRects(candidate, parts);
                    for (int partIndex = 0; partIndex < parts.Count; partIndex++)
                    {
                        Rect part = parts[partIndex];
                        if (part.yMax <= bridgeRect.yMin || part.yMin >= bridgeRect.yMax)
                        {
                            continue;
                        }

                        float leftGap = Mathf.Abs(part.xMax - bridgeRect.xMin);
                        if (part.center.x < bridgeRect.center.x && leftGap <= fitDistance && leftGap < bestLeftGap)
                        {
                            bestLeftGap = leftGap;
                            leftEdge = part.xMax;
                            hasLeft = true;
                        }

                        float rightGap = Mathf.Abs(part.xMin - bridgeRect.xMax);
                        if (part.center.x > bridgeRect.center.x && rightGap <= fitDistance && rightGap < bestRightGap)
                        {
                            bestRightGap = rightGap;
                            rightEdge = part.xMin;
                            hasRight = true;
                        }
                    }
                }

                if (!hasLeft || !hasRight || rightEdge - leftEdge < 0.2f)
                {
                    continue;
                }

                bridge.position = new Vector2((leftEdge + rightEdge) * 0.5f, bridge.position.y);
                bridge.size = new Vector2(rightEdge - leftEdge, bridge.size.y);
            }
        }

        private static bool IsSeparateHorizontalBridge(StageObjectData data)
        {
            return data != null
                && data.keepSeparate
                && data.type == StageObjectType.Platform
                && data.size.x > data.size.y * 1.5f
                && Mathf.Abs(Mathf.DeltaAngle(0f, data.rotation)) < 2f;
        }

        private static bool CanProvideBridgeBank(StageObjectData candidate, StageObjectData bridge)
        {
            return candidate != null
                && candidate != bridge
                && StageObjectCatalog.Get(candidate.type).Category == StageObjectCategory.Terrain
                && StageObjectCatalog.Get(candidate.type).Kind == StageObjectKind.Solid
                && (candidate.pathPoints == null || candidate.pathPoints.Length < 2)
                && IsAxisAligned(candidate.rotation);
        }

        private static void AppendStageRects(StageObjectData data, List<Rect> results)
        {
            if (data.connectedRects != null && data.connectedRects.Length > 0)
            {
                for (int i = 0; i < data.connectedRects.Length; i++)
                {
                    StageRectPartData part = data.connectedRects[i];
                    if (part != null)
                    {
                        results.Add(RectFromStageData(data.position + part.position, part.size));
                    }
                }
                return;
            }

            results.Add(RectFromStageData(data.position, data.size, data.rotation));
        }

        private static Rect RectFromStageData(Vector2 position, Vector2 size)
        {
            Vector2 half = size * 0.5f;
            return Rect.MinMaxRect(position.x - half.x, position.y - half.y, position.x + half.x, position.y + half.y);
        }

        private static Rect RectFromStageData(Vector2 position, Vector2 size, float rotation)
        {
            Vector2 sourceHalf = size * 0.5f;
            float radians = rotation * Mathf.Deg2Rad;
            float cos = Mathf.Abs(Mathf.Cos(radians));
            float sin = Mathf.Abs(Mathf.Sin(radians));
            Vector2 half = new Vector2(
                sourceHalf.x * cos + sourceHalf.y * sin,
                sourceHalf.x * sin + sourceHalf.y * cos);
            return Rect.MinMaxRect(position.x - half.x, position.y - half.y, position.x + half.x, position.y + half.y);
        }

        private static bool IsAxisAligned(float rotation)
        {
            float horizontal = Mathf.Abs(Mathf.DeltaAngle(0f, rotation));
            float vertical = Mathf.Abs(Mathf.Abs(Mathf.DeltaAngle(0f, rotation)) - 90f);
            return horizontal < 2f || vertical < 2f;
        }

        private GameObject CreateConnectedRectSolid(StageObjectData data, Transform parent)
        {
            bool useNatureTerrainStyle = UsesNatureTerrainStyle(data, parent);
            bool useCaveTerrainStyle = UsesCaveTerrainStyle(data, parent);
            bool useNightCityTerrainStyle = UsesNightCityTerrainStyle(data, parent);
            bool useFactoryTerrainStyle = UsesFactoryTerrainStyle(data, parent);
            bool useSpaceTerrainStyle = UsesSpaceTerrainStyle(data, parent);
            bool addNatureInteriorPlants = UsesNatureStageTheme(parent);
            Color stroke = useCaveTerrainStyle
                ? CaveTerrainStrokeColor
                : useNightCityTerrainStyle
                    ? NightCityTerrainStrokeColor
                : useFactoryTerrainStyle
                    ? FactoryTerrainStrokeColor
                : useSpaceTerrainStyle
                    ? SpaceTerrainStrokeColor
                : useNatureTerrainStyle
                    ? TitleTerrainStrokeColor
                    : GetObjectColor(data.type);
            Color outline = useCaveTerrainStyle
                ? CaveTerrainStrokeColor
                : useNightCityTerrainStyle
                    ? NightCityTerrainStrokeColor
                : useFactoryTerrainStyle
                    ? FactoryTerrainStrokeColor
                : useSpaceTerrainStyle
                    ? SpaceTerrainStrokeColor
                : useNatureTerrainStyle
                    ? TitleTerrainStrokeColor
                    : Color.black;
            Color outlineAccent = useCaveTerrainStyle
                ? CaveTerrainAccentColor
                : useNightCityTerrainStyle
                    ? NightCityTerrainAccentColor
                : useFactoryTerrainStyle
                    ? FactoryTerrainAccentColor
                : useSpaceTerrainStyle
                    ? SpaceTerrainAccentColor
                : useNatureTerrainStyle
                    ? TitleTerrainAccentColor
                    : new Color(0.1f, 0.48f, 0.95f, 0.42f);
            GameObject obj = new GameObject(data.objectId);
            obj.name = data.type + " Connected";
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            obj.layer = groundLayer;
            obj.tag = "Ground";

            List<float> xs = new List<float>();
            List<float> ys = new List<float>();
            for (int i = 0; i < data.connectedRects.Length; i++)
            {
                StageRectPartData part = data.connectedRects[i];
                if (part == null) continue;
                Vector2 half = part.size * 0.5f;
                AddUniqueCoordinate(xs, part.position.x - half.x);
                AddUniqueCoordinate(xs, part.position.x + half.x);
                AddUniqueCoordinate(ys, part.position.y - half.y);
                AddUniqueCoordinate(ys, part.position.y + half.y);

                BoxCollider2D collider = obj.AddComponent<BoxCollider2D>();
                collider.offset = part.position;
                collider.size = part.size;

                GameObject fillRoot = new GameObject($"Connected Fill {i}");
                fillRoot.transform.SetParent(obj.transform, false);
                fillRoot.transform.localPosition = part.position;
                int partVisualSeed = GetStableNatureVisualSeed(data.objectId) + i * 977;
                if (useCaveTerrainStyle)
                {
                    AddSolidPaperBase(fillRoot.transform, part.size, CaveTerrainPaperColor);
                }
                else if (useNightCityTerrainStyle)
                {
                    AddSolidPaperBase(fillRoot.transform, part.size, NightCityTerrainPaperColor);
                }
                else if (useFactoryTerrainStyle)
                {
                    AddSolidPaperBase(fillRoot.transform, part.size, FactoryTerrainPaperColor);
                }
                else if (useSpaceTerrainStyle)
                {
                    AddSolidPaperBase(fillRoot.transform, part.size, SpaceTerrainPaperColor);
                }
                else if (useNatureTerrainStyle)
                {
                    AddSolidPaperBase(fillRoot.transform, part.size, TitleTerrainPaperColor);
                }
                else
                {
                    AddSolidPaperBase(fillRoot.transform, part.size);
                }
                AddSolidWash(fillRoot.transform, part.size, stroke);
                if (useCaveTerrainStyle)
                {
                    AddCaveTerrainFill(fillRoot.transform, part.size, stroke, partVisualSeed);
                }
                else if (useNightCityTerrainStyle)
                {
                    AddNightCityTerrainFill(fillRoot.transform, part.size, partVisualSeed);
                }
                else if (useFactoryTerrainStyle)
                {
                    AddFactoryTerrainFill(fillRoot.transform, part.size, partVisualSeed);
                }
                else if (useSpaceTerrainStyle)
                {
                    AddSpaceTerrainFill(fillRoot.transform, part.size, partVisualSeed);
                }
                else if (useNatureTerrainStyle)
                {
                    AddNatureTerrainCoreTexture(fillRoot.transform, part.size, stroke);
                }
                else
                {
                    AddSolidPencilFill(fillRoot.transform, part.size, stroke);
                }
            }

            xs.Sort();
            ys.Sort();
            bool[,] occupied = new bool[Mathf.Max(0, xs.Count - 1), Mathf.Max(0, ys.Count - 1)];
            for (int x = 0; x < xs.Count - 1; x++)
            {
                for (int y = 0; y < ys.Count - 1; y++)
                {
                    Vector2 center = new Vector2((xs[x] + xs[x + 1]) * 0.5f, (ys[y] + ys[y + 1]) * 0.5f);
                    occupied[x, y] = IsInsideConnectedRect(data.connectedRects, center);
                }
            }

            for (int x = 0; x < xs.Count - 1; x++)
            {
                for (int y = 0; y < ys.Count - 1; y++)
                {
                    if (!occupied[x, y]) continue;
                    float verticalDepth = Mathf.Clamp((ys[y + 1] - ys[y]) * 0.24f, 0.08f, 0.65f);
                    float horizontalDepth = Mathf.Clamp((xs[x + 1] - xs[x]) * 0.24f, 0.08f, 0.65f);
                    if (y == 0 || !occupied[x, y - 1])
                    {
                        Vector2 from = new Vector2(xs[x], ys[y]);
                        Vector2 to = new Vector2(xs[x + 1], ys[y]);
                        AddConnectedEdge(obj.transform, from, to, outline, outlineAccent, useNatureTerrainStyle || useCaveTerrainStyle || useNightCityTerrainStyle || useFactoryTerrainStyle || useSpaceTerrainStyle);
                        if (useNatureTerrainStyle) AddNatureConnectedEdgeGradient(obj.transform, from, to, verticalDepth);
                        if (useCaveTerrainStyle)
                        {
                            AddCaveConnectedEdgeGradient(obj.transform, from, to, verticalDepth);
                            AddCaveStalactitesAlongExposedEdge(
                                obj.transform,
                                from,
                                to,
                                verticalDepth,
                                GetStableNatureVisualSeed(data.objectId) + x * 31 + y * 17);
                        }
                        if (addNatureInteriorPlants) AddNaturePlantsOnWorldBottomEdge(obj.transform, from, to, verticalDepth, GetStableNatureVisualSeed(data.objectId) + x * 31 + y * 17);
                    }
                    if (y == ys.Count - 2 || !occupied[x, y + 1])
                    {
                        Vector2 from = new Vector2(xs[x + 1], ys[y + 1]);
                        Vector2 to = new Vector2(xs[x], ys[y + 1]);
                        AddConnectedEdge(obj.transform, from, to, outline, outlineAccent, useNatureTerrainStyle || useCaveTerrainStyle || useNightCityTerrainStyle || useFactoryTerrainStyle || useSpaceTerrainStyle);
                        if (useNatureTerrainStyle) AddNatureConnectedEdgeGradient(obj.transform, from, to, verticalDepth);
                        if (useCaveTerrainStyle)
                        {
                            AddCaveConnectedEdgeGradient(obj.transform, from, to, verticalDepth);
                            AddCaveStalactitesAlongExposedEdge(
                                obj.transform,
                                from,
                                to,
                                verticalDepth,
                                GetStableNatureVisualSeed(data.objectId) + x * 37 + y * 19);
                        }
                        if (addNatureInteriorPlants) AddNaturePlantsOnWorldBottomEdge(obj.transform, from, to, verticalDepth, GetStableNatureVisualSeed(data.objectId) + x * 37 + y * 19);
                    }
                    if (x == 0 || !occupied[x - 1, y])
                    {
                        Vector2 from = new Vector2(xs[x], ys[y + 1]);
                        Vector2 to = new Vector2(xs[x], ys[y]);
                        AddConnectedEdge(obj.transform, from, to, outline, outlineAccent, useNatureTerrainStyle || useCaveTerrainStyle || useNightCityTerrainStyle || useFactoryTerrainStyle || useSpaceTerrainStyle);
                        if (useNatureTerrainStyle) AddNatureConnectedEdgeGradient(obj.transform, from, to, horizontalDepth);
                        if (useCaveTerrainStyle) AddCaveConnectedEdgeGradient(obj.transform, from, to, horizontalDepth);
                        if (addNatureInteriorPlants) AddNaturePlantsOnWorldBottomEdge(obj.transform, from, to, horizontalDepth, GetStableNatureVisualSeed(data.objectId) + x * 41 + y * 23);
                    }
                    if (x == xs.Count - 2 || !occupied[x + 1, y])
                    {
                        Vector2 from = new Vector2(xs[x + 1], ys[y]);
                        Vector2 to = new Vector2(xs[x + 1], ys[y + 1]);
                        AddConnectedEdge(obj.transform, from, to, outline, outlineAccent, useNatureTerrainStyle || useCaveTerrainStyle || useNightCityTerrainStyle || useFactoryTerrainStyle || useSpaceTerrainStyle);
                        if (useNatureTerrainStyle) AddNatureConnectedEdgeGradient(obj.transform, from, to, horizontalDepth);
                        if (useCaveTerrainStyle) AddCaveConnectedEdgeGradient(obj.transform, from, to, horizontalDepth);
                        if (addNatureInteriorPlants) AddNaturePlantsOnWorldBottomEdge(obj.transform, from, to, horizontalDepth, GetStableNatureVisualSeed(data.objectId) + x * 43 + y * 29);
                    }
                }
            }

            if (IsTitleRoomFrame(data))
            {
                AddTitleHangingVines(obj.transform);
            }

            AddEditorMetadata(obj, data);
            return obj;
        }

        private static void AddUniqueCoordinate(List<float> values, float value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (Mathf.Abs(values[i] - value) < 0.001f) return;
            }
            values.Add(value);
        }

        private static bool IsInsideConnectedRect(StageRectPartData[] parts, Vector2 point)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                StageRectPartData part = parts[i];
                if (part == null) continue;
                Vector2 half = part.size * 0.5f;
                if (point.x >= part.position.x - half.x && point.x <= part.position.x + half.x
                    && point.y >= part.position.y - half.y && point.y <= part.position.y + half.y)
                {
                    return true;
                }
            }
            return false;
        }

        private static void AddConnectedEdge(
            Transform parent,
            Vector2 from,
            Vector2 to,
            Color outlineColor,
            Color accentColor,
            bool natureStyle = false)
        {
            AddDoodleLine(
                "Connected Outer Edge",
                parent,
                new[] { (Vector3)from, (Vector3)to },
                outlineColor,
                natureStyle ? 0.082f : 0.055f,
                12);
            if (!natureStyle)
            {
                AddDoodleLine("Connected Accent Edge", parent, new[] { (Vector3)(from + new Vector2(0.015f, -0.015f)), (Vector3)(to + new Vector2(0.015f, -0.015f)) }, accentColor, 0.026f, 11);
                return;
            }

            Vector2 direction = to - from;
            Vector2 inward = direction.sqrMagnitude > 0.000001f
                ? new Vector2(-direction.y, direction.x).normalized
                : Vector2.zero;
            Vector3[] sketch = new Vector3[7];
            for (int i = 0; i < sketch.Length; i++)
            {
                float t = i / (sketch.Length - 1f);
                float wobble = i == 0 || i == sketch.Length - 1
                    ? 0.012f
                    : 0.018f + Mathf.Sin(i * 2.19f + from.x * 0.31f + from.y * 0.23f) * 0.011f;
                sketch[i] = Vector2.Lerp(from, to, t) + inward * wobble;
            }
            AddDoodleLine("Connected Green Pencil Echo", parent, sketch, accentColor, 0.034f, 11);

            Vector3[] dryPencil = new Vector3[sketch.Length];
            for (int i = 0; i < dryPencil.Length; i++)
            {
                float t = i / (dryPencil.Length - 1f);
                float scratch = -0.018f + Mathf.Sin(i * 3.11f + from.x * 0.17f + to.y * 0.29f) * 0.008f;
                dryPencil[i] = Vector2.Lerp(from, to, t) + inward * scratch;
            }
            AddDoodleLine(
                "Connected Dry Pencil Edge",
                parent,
                dryPencil,
                new Color(outlineColor.r, outlineColor.g, outlineColor.b, 0.5f),
                0.028f,
                13);
        }

        private static bool IsTitleRoomFrame(StageObjectData data)
        {
            return data != null
                && !string.IsNullOrEmpty(data.objectId)
                && data.objectId.EndsWith("-RoomFrame", StringComparison.Ordinal);
        }

        private static void AddTitleHangingVines(Transform parent)
        {
            float[] anchors = { -12.75f, -11.95f, -3.7f, 3.85f, 12.65f };
            float[] lengths = { 0.78f, 1.28f, 0.72f, 0.95f, 1.35f };
            for (int vineIndex = 0; vineIndex < anchors.Length; vineIndex++)
            {
                AddHangingVine(parent, new Vector2(anchors[vineIndex], 6.48f), lengths[vineIndex], vineIndex);
            }
        }

        public void AddNatureHangingVine(Transform parent, Vector2 worldAnchor, float length, int seed)
        {
            if (parent == null)
            {
                return;
            }

            Vector3 localAnchor = parent.InverseTransformPoint(new Vector3(worldAnchor.x, worldAnchor.y, 0f));
            AddHangingVine(parent, localAnchor, Mathf.Max(0.35f, length), seed);
        }

        public void AddNatureWallVine(Transform parent, Vector2 worldBottom, float height, int seed)
        {
            if (parent == null)
            {
                return;
            }

            Vector3 localBottom = parent.InverseTransformPoint(new Vector3(worldBottom.x, worldBottom.y, 0f));
            float clampedHeight = Mathf.Max(0.5f, height);
            if (!TryAddNatureWallVineSprite(parent, localBottom, clampedHeight, seed))
            {
                AddWallVine(parent, localBottom, clampedHeight, seed);
            }
        }

        private static bool TryAddNatureWallVineSprite(Transform parent, Vector2 bottom, float height, int seed)
        {
            if (natureWallVineSprites == null)
            {
                natureWallVineSprites = new[]
                {
                    Resources.Load<Sprite>("StageDecorations/CrayonSet/vine-climbing"),
                    Resources.Load<Sprite>("StageDecorations/CrayonSet/vine-climbing-sparse"),
                    Resources.Load<Sprite>("StageDecorations/CrayonSet/vine-climbing-leafy")
                };
            }

            int variant = (seed & 0x7fffffff) % natureWallVineSprites.Length;
            Sprite sprite = natureWallVineSprites[variant];
            if (sprite == null)
            {
                for (int spriteIndex = 0; spriteIndex < natureWallVineSprites.Length; spriteIndex++)
                {
                    if (natureWallVineSprites[spriteIndex] != null)
                    {
                        sprite = natureWallVineSprites[spriteIndex];
                        break;
                    }
                }
            }
            if (sprite == null || sprite.bounds.size.x <= 0f || sprite.bounds.size.y <= 0f)
            {
                return false;
            }

            Vector2[] spriteVertices = sprite.vertices;
            Vector2 bottomVertex = spriteVertices.Length > 0
                ? spriteVertices[0]
                : new Vector2(sprite.bounds.center.x, sprite.bounds.min.y);
            for (int vertexIndex = 1; vertexIndex < spriteVertices.Length; vertexIndex++)
            {
                Vector2 candidate = spriteVertices[vertexIndex];
                if (candidate.y < bottomVertex.y
                    || (Mathf.Approximately(candidate.y, bottomVertex.y)
                        && Mathf.Abs(candidate.x - sprite.bounds.center.x) < Mathf.Abs(bottomVertex.x - sprite.bounds.center.x)))
                {
                    bottomVertex = candidate;
                }
            }

            float scale = height / sprite.bounds.size.y;
            float variantWidth = variant == 2 ? 0.66f : (variant == 1 ? 0.74f : 0.86f);
            float horizontalScale = scale * (variantWidth + Mathf.Abs(Mathf.Sin(seed * 0.61f)) * 0.12f);
            if ((seed & 1) != 0)
            {
                horizontalScale = -horizontalScale;
            }

            GameObject vine = new GameObject($"Nature Hand Drawn Wall Vine {variant + 1}");
            vine.transform.SetParent(parent, false);
            vine.transform.localScale = new Vector3(horizontalScale, scale, 1f);
            vine.transform.localPosition = new Vector3(
                bottom.x - bottomVertex.x * horizontalScale,
                bottom.y - bottomVertex.y * scale,
                0f);

            SpriteRenderer renderer = vine.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.94f, 1f, 0.92f, 0.97f);
            renderer.sortingOrder = 15;

            AddNatureGrassSpriteCluster(
                parent,
                bottom,
                Vector2.up,
                0.66f + Mathf.Abs(Mathf.Sin(seed * 0.43f)) * 0.32f,
                0.25f + Mathf.Abs(Mathf.Cos(seed * 0.37f)) * 0.12f,
                seed + 101);
            return true;
        }

        private static void AddHangingVine(Transform parent, Vector2 anchor, float length, int seed)
        {
            if (TryAddNatureHangingVineSprite(parent, anchor, length, seed))
            {
                return;
            }

            Color vine = new Color(0.2f, 0.58f, 0.25f, 0.68f);
            Color leaf = new Color(0.26f, 0.66f, 0.29f, 0.6f);
            const int pointCount = 8;
            Vector3[] points = new Vector3[pointCount];
            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                float t = pointIndex / (pointCount - 1f);
                float sway = Mathf.Sin(t * Mathf.PI * 2.2f + seed * 1.35f) * (0.07f + seed % 2 * 0.025f);
                points[pointIndex] = new Vector3(
                    anchor.x + sway,
                    anchor.y - length * t,
                    0f);
            }

            AddDoodleLine("Nature Hanging Vine", parent, points, vine, 0.035f, -62);
            for (int pointIndex = 2; pointIndex < pointCount - 1; pointIndex += 2)
            {
                Vector3 stem = points[pointIndex];
                float side = ((pointIndex + seed) & 1) == 0 ? 1f : -1f;
                AddDoodleLine(
                    "Nature Vine Leaf",
                    parent,
                    new[]
                    {
                        stem,
                        stem + new Vector3(0.14f * side, 0.06f, 0f),
                        stem + new Vector3(0.07f * side, -0.035f, 0f),
                        stem
                    },
                    leaf,
                    0.026f,
                    -61);
            }
        }

        private static bool TryAddNatureHangingVineSprite(Transform parent, Vector2 anchor, float length, int seed)
        {
            if (natureHangingVineSprite == null)
            {
                natureHangingVineSprite = Resources.Load<Sprite>("StageDecorations/CrayonSet/vine-hanging");
            }

            Sprite sprite = natureHangingVineSprite;
            if (sprite == null || sprite.bounds.size.x <= 0f || sprite.bounds.size.y <= 0f)
            {
                return false;
            }

            Vector2[] spriteVertices = sprite.vertices;
            Vector2 topVertex = spriteVertices.Length > 0
                ? spriteVertices[0]
                : new Vector2(sprite.bounds.center.x, sprite.bounds.max.y);
            for (int vertexIndex = 1; vertexIndex < spriteVertices.Length; vertexIndex++)
            {
                Vector2 candidate = spriteVertices[vertexIndex];
                if (candidate.y > topVertex.y
                    || (Mathf.Approximately(candidate.y, topVertex.y)
                        && Mathf.Abs(candidate.x - sprite.bounds.center.x) < Mathf.Abs(topVertex.x - sprite.bounds.center.x)))
                {
                    topVertex = candidate;
                }
            }

            float scale = length / sprite.bounds.size.y;
            float horizontalScale = scale * (0.7f + Mathf.Abs(Mathf.Sin(seed * 0.53f)) * 0.16f);
            if ((seed & 1) != 0)
            {
                horizontalScale = -horizontalScale;
            }

            GameObject vine = new GameObject("Nature Hand Drawn Hanging Vine");
            vine.transform.SetParent(parent, false);
            vine.transform.localScale = new Vector3(horizontalScale, scale, 1f);
            vine.transform.localPosition = new Vector3(
                anchor.x - topVertex.x * horizontalScale,
                anchor.y - topVertex.y * scale,
                0f);
            SpriteRenderer renderer = vine.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.94f, 1f, 0.92f, 0.96f);
            renderer.sortingOrder = -61;
            return true;
        }

        private static void AddWallVine(Transform parent, Vector2 bottom, float height, int seed)
        {
            Color stemDark = new Color(0.08f, 0.43f, 0.14f, 0.94f);
            Color stemLight = new Color(0.28f, 0.67f, 0.27f, 0.68f);
            Color leafOutline = new Color(0.08f, 0.47f, 0.15f, 0.92f);
            Color leafFill = new Color(0.34f, 0.72f, 0.3f, 0.16f);
            const int pointCount = 22;
            Vector2[] points = new Vector2[pointCount];
            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                float t = pointIndex / (pointCount - 1f);
                float sway = Mathf.Sin(t * Mathf.PI * 2.15f + seed * 1.17f) * 0.095f
                    + Mathf.Sin(t * Mathf.PI * 7.1f + seed * 0.43f) * 0.043f
                    + Mathf.Sin(t * Mathf.PI * 13.4f + seed * 0.19f) * 0.015f;
                points[pointIndex] = new Vector2(bottom.x + sway, bottom.y + height * t);
            }

            List<Vector3> vertices = new List<Vector3>(420);
            List<Color> colors = new List<Color>(420);
            List<int> triangles = new List<int>(630);
            for (int pointIndex = 0; pointIndex < pointCount - 1; pointIndex++)
            {
                Vector2 from = points[pointIndex];
                Vector2 to = points[pointIndex + 1];
                AppendPencilQuad(vertices, colors, triangles, from, to, 0.046f, stemDark);
                AppendPencilQuad(
                    vertices,
                    colors,
                    triangles,
                    from + new Vector2(0.018f + Mathf.Sin(pointIndex * 1.7f) * 0.006f, 0f),
                    to + new Vector2(0.018f + Mathf.Sin((pointIndex + 1) * 1.7f) * 0.006f, 0f),
                    0.016f,
                    stemLight);
            }

            for (int pointIndex = 2; pointIndex < pointCount - 1; pointIndex++)
            {
                if ((pointIndex + seed) % 5 == 0)
                {
                    continue;
                }

                Vector2 stem = points[pointIndex];
                float side = Mathf.Sin((pointIndex + seed) * 2.37f) >= 0f ? 1f : -1f;
                float branchLength = 0.075f + Mathf.Abs(Mathf.Sin((pointIndex + seed) * 1.31f)) * 0.105f;
                Vector2 branchTip = stem + new Vector2(
                    side * branchLength,
                    0.035f + Mathf.Abs(Mathf.Cos((pointIndex + seed) * 0.91f)) * 0.065f);
                AppendPencilQuad(vertices, colors, triangles, stem, branchTip, 0.022f, stemDark);

                float leafLength = 0.12f + Mathf.Abs(Mathf.Sin(pointIndex * 1.49f + seed)) * 0.11f;
                Vector2 leafTip = branchTip + new Vector2(
                    side * leafLength,
                    0.07f + Mathf.Abs(Mathf.Cos(pointIndex * 1.13f + seed)) * 0.1f);
                AppendVineLeaf(
                    vertices,
                    colors,
                    triangles,
                    branchTip,
                    leafTip,
                    0.042f + Mathf.Abs(Mathf.Sin(pointIndex * 1.81f + seed)) * 0.035f,
                    leafFill,
                    leafOutline);

                if ((pointIndex + seed) % 7 == 2)
                {
                    Vector2 oppositeTip = stem + new Vector2(-side * 0.12f, 0.09f);
                    AppendPencilQuad(vertices, colors, triangles, stem, oppositeTip, 0.019f, stemDark);
                    AppendVineLeaf(
                        vertices,
                        colors,
                        triangles,
                        oppositeTip,
                        oppositeTip + new Vector2(-side * 0.11f, 0.09f),
                        0.04f,
                        leafFill,
                        leafOutline);
                }

                if ((pointIndex + seed) % 6 == 1)
                {
                    AppendVineCurl(vertices, colors, triangles, stem, side, seed + pointIndex, stemDark);
                }
            }

            Vector2 terminalBase = points[pointCount - 1];
            AppendVineLeaf(
                vertices,
                colors,
                triangles,
                terminalBase,
                terminalBase + new Vector2(Mathf.Sin(seed) * 0.12f, 0.22f),
                0.065f,
                leafFill,
                leafOutline);

            CreatePencilMesh(parent, "Nature Wall Vine", vertices, colors, triangles, 15);
            AddNatureGrassSpriteCluster(
                parent,
                bottom,
                Vector2.up,
                0.62f + Mathf.Abs(Mathf.Sin(seed * 0.43f)) * 0.28f,
                0.24f + Mathf.Abs(Mathf.Cos(seed * 0.37f)) * 0.1f,
                seed + 101);
        }

        private static void AppendVineLeaf(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 leafBase,
            Vector2 leafTip,
            float halfWidth,
            Color fill,
            Color outline)
        {
            Vector2 axis = leafTip - leafBase;
            if (axis.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector2 normal = new Vector2(-axis.y, axis.x).normalized;
            Vector2 nearCenter = Vector2.Lerp(leafBase, leafTip, 0.34f);
            Vector2 farCenter = Vector2.Lerp(leafBase, leafTip, 0.67f);
            Vector2 upperNear = nearCenter + normal * halfWidth * 0.78f;
            Vector2 upperFar = farCenter + normal * halfWidth;
            Vector2 lowerFar = farCenter - normal * halfWidth;
            Vector2 lowerNear = nearCenter - normal * halfWidth * 0.78f;
            int first = vertices.Count;
            vertices.Add(leafBase);
            vertices.Add(upperNear);
            vertices.Add(upperFar);
            vertices.Add(leafTip);
            vertices.Add(lowerFar);
            vertices.Add(lowerNear);
            colors.Add(fill);
            colors.Add(fill);
            colors.Add(fill);
            colors.Add(fill);
            colors.Add(fill);
            colors.Add(fill);
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
            triangles.Add(first);
            triangles.Add(first + 3);
            triangles.Add(first + 4);
            triangles.Add(first);
            triangles.Add(first + 4);
            triangles.Add(first + 5);

            AppendPencilQuad(vertices, colors, triangles, leafBase, upperNear, 0.021f, outline);
            AppendPencilQuad(vertices, colors, triangles, upperNear, upperFar, 0.021f, outline);
            AppendPencilQuad(vertices, colors, triangles, upperFar, leafTip, 0.021f, outline);
            AppendPencilQuad(vertices, colors, triangles, leafTip, lowerFar, 0.021f, outline);
            AppendPencilQuad(vertices, colors, triangles, lowerFar, lowerNear, 0.021f, outline);
            AppendPencilQuad(vertices, colors, triangles, lowerNear, leafBase, 0.021f, outline);
            AppendPencilQuad(vertices, colors, triangles, leafBase, leafTip, 0.013f, outline * 0.8f);
            Vector2 veinCenter = Vector2.Lerp(leafBase, leafTip, 0.56f);
            AppendPencilQuad(vertices, colors, triangles, veinCenter, upperFar, 0.009f, outline * 0.58f);
            AppendPencilQuad(vertices, colors, triangles, veinCenter, lowerFar, 0.009f, outline * 0.58f);
        }

        private static void AppendVineCurl(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 origin,
            float side,
            int seed,
            Color color)
        {
            Vector2 previous = origin;
            for (int pointIndex = 1; pointIndex <= 9; pointIndex++)
            {
                float t = pointIndex / 9f;
                float angle = t * Mathf.PI * 2.05f + seed * 0.31f;
                float radius = 0.025f + t * 0.105f;
                Vector2 next = origin + new Vector2(
                    side * (0.06f + Mathf.Cos(angle) * radius),
                    0.035f + t * 0.17f + Mathf.Sin(angle) * radius * 0.55f);
                AppendPencilQuad(vertices, colors, triangles, previous, next, 0.018f, color * 0.82f);
                previous = next;
            }
        }

        private GameObject CreatePathSolid(StageObjectData data, Transform parent)
        {
            Color stroke = GetObjectColor(data.type);
            GameObject obj = new GameObject(data.objectId);
            obj.name = data.type + " Path";
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            obj.layer = groundLayer;
            obj.tag = "Ground";

            Vector2[] center = data.pathPoints;
            float thickness = Mathf.Max(0.2f, data.pathThickness > 0f ? data.pathThickness : 0.5f);
            float half = thickness * 0.5f;
            Vector2[] left = new Vector2[center.Length];
            Vector2[] right = new Vector2[center.Length];
            for (int i = 0; i < center.Length; i++)
            {
                Vector2 previous = center[Mathf.Max(0, i - 1)];
                Vector2 next = center[Mathf.Min(center.Length - 1, i + 1)];
                Vector2 tangent = (next - previous).normalized;
                if (tangent.sqrMagnitude < 0.001f)
                {
                    tangent = Vector2.right;
                }

                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                left[i] = center[i] + normal * half;
                right[i] = center[i] - normal * half;
            }

            Vector2[] polygon = new Vector2[center.Length * 2];
            Vector3[] outline = new Vector3[polygon.Length + 1];
            for (int i = 0; i < center.Length; i++)
            {
                polygon[i] = left[i];
                polygon[center.Length + i] = right[center.Length - 1 - i];
            }

            for (int i = 0; i < polygon.Length; i++)
            {
                outline[i] = polygon[i];
            }
            outline[outline.Length - 1] = polygon[0];

            PolygonCollider2D collider = obj.AddComponent<PolygonCollider2D>();
            collider.points = polygon;
            if (StageObjectCatalog.Get(data.type).Kind == StageObjectKind.Trigger || StageObjectCatalog.Get(data.type).Kind == StageObjectKind.Hazard)
            {
                collider.isTrigger = true;
            }

            Vector3[] centerLine = new Vector3[center.Length];
            for (int i = 0; i < center.Length; i++)
            {
                centerLine[i] = center[i];
            }

            AddDoodleLine("Continuous Path Paper Base", obj.transform, centerLine, new Color(0.985f, 0.975f, 0.93f, 1f), thickness, 2);
            AddDoodleLine("Continuous Path Fill", obj.transform, centerLine, new Color(stroke.r, stroke.g, stroke.b, 0.045f), thickness, 3);
            int pencilLineCount = Mathf.Clamp(Mathf.CeilToInt(thickness / 0.13f), 2, 28);
            for (int lineIndex = 1; lineIndex < pencilLineCount; lineIndex++)
            {
                float offset = Mathf.Lerp(-half * 0.82f, half * 0.82f, lineIndex / (float)pencilLineCount);
                Vector3[] pencilPath = new Vector3[center.Length];
                for (int pointIndex = 0; pointIndex < center.Length; pointIndex++)
                {
                    Vector2 normal = (left[pointIndex] - center[pointIndex]).normalized;
                    float jitter = Mathf.Sin(pointIndex * 1.73f + lineIndex * 2.11f) * 0.018f;
                    pencilPath[pointIndex] = center[pointIndex] + normal * (offset + jitter);
                }

                float alpha = 0.11f + (lineIndex % 3) * 0.035f;
                AddDoodleLine(
                    $"Continuous Pencil {lineIndex}",
                    obj.transform,
                    pencilPath,
                    new Color(stroke.r, stroke.g, stroke.b, alpha),
                    0.012f + (lineIndex % 2) * 0.004f,
                    5);
            }
            AddDoodleLine("Continuous Path Outline", obj.transform, outline, Color.black, 0.055f, 12);
            AddDoodleLine("Continuous Path Accent", obj.transform, outline, new Color(0.1f, 0.48f, 0.95f, 0.42f), 0.026f, 11);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private GameObject CreateBalanceScale(StageObjectData data, Transform parent)
        {
            GameObject root = new GameObject(data.objectId);
            root.name = "BalanceScale";
            root.transform.SetParent(parent, false);
            root.transform.position = data.position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            Vector2 size = data.size;
            float height = Mathf.Max(3.2f, size.y);
            float halfSpan = Mathf.Max(1.0f, size.x * 0.42f);
            float trayWidth = Mathf.Max(2.9f, size.x * 0.76f);

            AddDoodleLine("Top Beam", root.transform, new[] { new Vector3(-halfSpan, height * 0.38f, 0f), new Vector3(halfSpan, height * 0.38f, 0f) }, Color.black, 0.055f, 18);

            GameObject leftTray = CreateBalanceTray("Left Tray", root.transform, new Vector2(-halfSpan, -height * 0.08f), trayWidth);
            GameObject rightTray = CreateBalanceTray("Right Tray", root.transform, new Vector2(halfSpan, -height * 0.08f), trayWidth);
            AddDoodleLine("Left Rope", root.transform, new[] { new Vector3(-halfSpan, height * 0.38f, 0f), new Vector3(-halfSpan, -height * 0.08f, 0f) }, Color.black, 0.026f, 17);
            AddDoodleLine("Right Rope", root.transform, new[] { new Vector3(halfSpan, height * 0.38f, 0f), new Vector3(halfSpan, -height * 0.08f, 0f) }, Color.black, 0.026f, 17);

            VerticalBalanceScale scale = root.AddComponent<VerticalBalanceScale>();
            scale.Configure(leftTray.GetComponent<Rigidbody2D>(), rightTray.GetComponent<Rigidbody2D>(), height * 0.36f);
            ConfigureBalanceTrayReporters(leftTray, scale, -1);
            ConfigureBalanceTrayReporters(rightTray, scale, 1);

            BoxCollider2D editorCollider = root.AddComponent<BoxCollider2D>();
            editorCollider.size = new Vector2(Mathf.Max(1f, data.size.x), Mathf.Max(1f, height + 1.2f));
            editorCollider.isTrigger = true;
            AddEditorMetadata(root, data);
            return root;
        }

        private GameObject CreateBalanceTray(string name, Transform parent, Vector2 localPosition, float width)
        {
            const float trayHeight = 0.68f;
            Color trayColor = new Color(0.08f, 0.42f, 0.95f);
            Vector2 traySize = new Vector2(width, trayHeight);
            GameObject tray = CreateBox(name, Vector2.zero, traySize, new Color(trayColor.r, trayColor.g, trayColor.b, 0.08f), parent);
            tray.transform.localPosition = localPosition;
            tray.layer = groundLayer;
            tray.tag = "Ground";

            Rigidbody2D body = tray.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.useFullKinematicContacts = true;

            BoxCollider2D collider = tray.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;

            GameObject sensor = new GameObject("Load Sensor");
            sensor.transform.SetParent(tray.transform, false);
            sensor.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            BoxCollider2D sensorCollider = sensor.AddComponent<BoxCollider2D>();
            sensorCollider.isTrigger = true;
            sensorCollider.size = new Vector2(1.05f, 1.15f);
            sensor.AddComponent<VerticalBalanceTray>();

            AddPencilFillLocal(tray.transform, traySize, trayColor);
            AddSketchBoxOutline(tray.transform, traySize, Color.black, 0.06f);
            tray.AddComponent<VerticalBalanceTray>();
            return tray;
        }

        private static void ConfigureBalanceTrayReporters(GameObject tray, VerticalBalanceScale scale, int side)
        {
            VerticalBalanceTray[] reporters = tray.GetComponentsInChildren<VerticalBalanceTray>(true);
            for (int i = 0; i < reporters.Length; i++)
            {
                reporters[i].Configure(scale, side);
            }
        }

        private GameObject CreateBoxDropper(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = data.type.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            obj.layer = groundLayer;

            BoxCollider2D selectionCollider = obj.AddComponent<BoxCollider2D>();
            selectionCollider.size = data.size;
            selectionCollider.isTrigger = true;

            string dropperResource = data.type == StageObjectType.BoxDropper
                ? "StageObjects/NicoDraw/box-dropper"
                : data.type == StageObjectType.SpikeDropper
                    ? "StageObjects/NicoDraw/spike-dropper"
                    : data.type == StageObjectType.BombDropper
                        ? "StageObjects/NicoDraw/bomb-dropper"
                        : "StageObjects/NicoDraw/enemy-dropper";
            string dropperArtworkName = data.type == StageObjectType.BoxDropper
                ? "Colored Pencil Box Dropper"
                : data.type == StageObjectType.SpikeDropper
                    ? "Colored Pencil Spike Dropper"
                    : data.type == StageObjectType.BombDropper
                        ? "Colored Pencil Bomb Dropper"
                        : "Colored Pencil Enemy Dropper";
            bool usesDropperArtwork = AddResourceSprite(
                obj.transform,
                dropperResource,
                data.size,
                24,
                dropperArtworkName);

            if (!usesDropperArtwork)
            {
            Color casing = data.type == StageObjectType.EnemyDropper
                ? new Color(0.68f, 0.28f, 0.78f, 1f)
                : new Color(0.94f, 0.56f, 0.16f, 1f);
            AddSolidPaperBase(obj.transform, data.size);
            AddSolidWash(obj.transform, data.size, casing);
            AddSolidPencilFill(obj.transform, data.size, casing);
            AddSolidStraightBoxOutline(obj.transform, data.size);

            float halfWidth = data.size.x * 0.5f;
            float halfHeight = data.size.y * 0.5f;
            Color ink = new Color(0.32f, 0.12f, 0.04f, 1f);
            AddDoodleLine("Dropper Funnel", obj.transform, new[]
            {
                new Vector3(-halfWidth * 0.62f, halfHeight * 0.42f, -0.05f),
                new Vector3(halfWidth * 0.62f, halfHeight * 0.42f, -0.05f),
                new Vector3(halfWidth * 0.23f, -halfHeight * 0.16f, -0.05f),
                new Vector3(-halfWidth * 0.23f, -halfHeight * 0.16f, -0.05f),
                new Vector3(-halfWidth * 0.62f, halfHeight * 0.42f, -0.05f)
            }, ink, 0.06f, 21);
            AddDoodleLine("Dropper Arrow", obj.transform, new[]
            {
                new Vector3(0f, -halfHeight * 0.08f, -0.05f),
                new Vector3(0f, -halfHeight * 0.55f, -0.05f),
                new Vector3(-halfWidth * 0.12f, -halfHeight * 0.4f, -0.05f),
                new Vector3(0f, -halfHeight * 0.55f, -0.05f),
                new Vector3(halfWidth * 0.12f, -halfHeight * 0.4f, -0.05f)
            }, new Color(0.9f, 0.16f, 0.08f, 1f), 0.065f, 22);
            if (data.type == StageObjectType.EnemyDropper)
            {
                AddEnemyDropperPreview(obj.transform, data.spawnPattern, data.size);
            }
            else if (data.type == StageObjectType.SpikeDropper)
            {
                AddSpikeDropperPreview(obj.transform, data.size);
            }
            else if (data.type == StageObjectType.BombDropper)
            {
                AddBombDropperPreview(obj.transform, data.spawnPattern, data.size);
            }
            else
            {
                AddBoxDropperPatternPreview(obj.transform, data.spawnPattern, data.size);
            }
            }

            if (!usesDropperArtwork)
            {
                WrapDropperArtwork(obj.transform, dropperArtworkName);
            }
            AddDropperDispenseAnimation(obj.transform, data.size, dropperArtworkName);

            AddEditorMetadata(obj, data);
            if (data.type == StageObjectType.EnemyDropper)
            {
                StageEnemyDropper dropper = obj.AddComponent<StageEnemyDropper>();
                dropper.Configure(
                    this,
                    parent,
                    data.size,
                    data.actionStrength,
                    data.spawnPattern,
                    data.spawnBoxSize);
            }
            else if (data.type == StageObjectType.SpikeDropper)
            {
                StageSpikeDropper dropper = obj.AddComponent<StageSpikeDropper>();
                dropper.Configure(this, parent, data.size, data.actionStrength, data.spawnBoxSize);
            }
            else if (data.type == StageObjectType.BombDropper)
            {
                StageBombDropper dropper = obj.AddComponent<StageBombDropper>();
                dropper.Configure(
                    this,
                    parent,
                    data.size,
                    data.actionStrength,
                    data.spawnPattern,
                    data.spawnBoxSize,
                    data.bombFuseSeconds);
            }
            else
            {
                StageBoxDropper dropper = obj.AddComponent<StageBoxDropper>();
                dropper.Configure(
                    this,
                    parent,
                    data.size,
                    data.actionStrength,
                    data.spawnPattern,
                    data.spawnBoxSize);
            }
            return obj;
        }

        private static void AddDropperDispenseAnimation(
            Transform parent,
            Vector2 size,
            string artworkName)
        {
            Transform artwork = parent != null ? parent.Find(artworkName) : null;
            if (artwork == null) return;

            GameObject puffRoot = new GameObject("Dropper Pencil Puff");
            puffRoot.transform.SetParent(parent, false);
            puffRoot.transform.localPosition = new Vector3(0f, -size.y * 0.54f, -0.09f);
            float radius = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.055f, 0.045f, 0.11f);
            Color puffInk = new Color(0.32f, 0.24f, 0.16f, 0.82f);
            for (int i = 0; i < 3; i++)
            {
                GameObject puff = new GameObject("Pencil Puff Stroke");
                puff.transform.SetParent(puffRoot.transform, false);
                puff.transform.localPosition = new Vector3((i - 1) * radius * 1.55f, -Mathf.Abs(i - 1) * radius * 0.28f, 0f);
                AddDoodleCircleAt(puff.transform, Vector2.zero, radius * (i == 1 ? 1.08f : 0.82f), puffInk, 0.035f, 30);
            }
            puffRoot.SetActive(false);

            StageDropperVisualAnimator animator = parent.gameObject.AddComponent<StageDropperVisualAnimator>();
            animator.Configure(artwork, puffRoot.transform, Mathf.Max(0.12f, size.y * 0.16f));
        }

        private static void WrapDropperArtwork(Transform parent, string artworkName)
        {
            if (parent == null || parent.Find(artworkName) != null) return;
            GameObject artworkObject = new GameObject(artworkName);
            artworkObject.transform.SetParent(parent, false);
            Transform artwork = artworkObject.transform;
            List<Transform> visualChildren = new List<Transform>();
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != artwork) visualChildren.Add(child);
            }
            for (int i = 0; i < visualChildren.Count; i++)
            {
                visualChildren[i].SetParent(artwork, false);
            }
        }

        private static void AddEnemyDropperPreview(Transform parent, int pattern, Vector2 size)
        {
            float radius = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.2f, 0.16f, 0.3f);
            float y = size.y * 0.25f;
            Color ink = new Color(0.34f, 0.08f, 0.46f, 1f);
            AddDoodleCircleAt(parent, new Vector2(0f, y), radius, ink, 0.055f, 24);
            float eyeOffset = radius * 0.38f;
            AddDoodleCircleAt(parent, new Vector2(-eyeOffset, y + radius * 0.12f), radius * 0.12f, ink, 0.04f, 25);
            AddDoodleCircleAt(parent, new Vector2(eyeOffset, y + radius * 0.12f), radius * 0.12f, ink, 0.04f, 25);
            if (pattern == 1)
            {
                AddDoodleLine("Enemy Spawner Jump Mark", parent, new[]
                {
                    new Vector3(-radius, y - radius, -0.06f),
                    new Vector3(-radius * 0.35f, y - radius * 1.45f, -0.06f),
                    new Vector3(radius * 0.35f, y - radius, -0.06f),
                    new Vector3(radius, y - radius * 1.45f, -0.06f)
                }, ink, 0.045f, 24);
            }
        }

        private GameObject CreateBeamEmitter(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = StageObjectType.BeamEmitter.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            obj.layer = groundLayer;

            BoxCollider2D selectionCollider = obj.AddComponent<BoxCollider2D>();
            selectionCollider.size = data.size;
            selectionCollider.isTrigger = true;

            Color casing = new Color(0.25f, 0.72f, 0.86f, 1f);
            AddSolidPaperBase(obj.transform, data.size);
            AddSolidWash(obj.transform, data.size, casing);
            AddSolidPencilFill(obj.transform, data.size, casing);
            AddSolidStraightBoxOutline(obj.transform, data.size);

            float halfWidth = data.size.x * 0.5f;
            float halfHeight = data.size.y * 0.5f;
            Color beamColor = new Color(1f, 0.12f, 0.08f, 1f);
            AddDoodleLine("Beam Direction", obj.transform, new[]
            {
                new Vector3(-halfWidth * 0.34f, 0f, -0.06f),
                new Vector3(halfWidth * 0.42f, 0f, -0.06f),
                new Vector3(halfWidth * 0.18f, halfHeight * 0.24f, -0.06f),
                new Vector3(halfWidth * 0.42f, 0f, -0.06f),
                new Vector3(halfWidth * 0.18f, -halfHeight * 0.24f, -0.06f)
            }, beamColor, 0.065f, 22);

            AddResourceSprite(
                obj.transform,
                "StageObjects/NicoDraw/beam-emitter",
                new Vector2(data.size.x * 1.12f, data.size.y * 1.12f),
                30,
                "Colored Pencil Beam Emitter");

            // The charge state is gameplay information, so give it most of the
            // device width instead of hiding it in a small decorative slit.
            float gaugeWidth = Mathf.Max(0.72f, data.size.x * 0.78f);
            float gaugeHeight = Mathf.Clamp(data.size.y * 0.2f, 0.14f, 0.23f);
            float gaugeY = -halfHeight * 0.58f;
            GameObject gaugeBackObject = new GameObject("Beam Charge Gauge Back");
            gaugeBackObject.transform.SetParent(obj.transform, false);
            gaugeBackObject.transform.localPosition = new Vector3(0f, gaugeY, -0.075f);
            gaugeBackObject.transform.localScale = new Vector3(gaugeWidth + 0.14f, gaugeHeight + 0.11f, 1f);
            SpriteRenderer gaugeBack = gaugeBackObject.AddComponent<SpriteRenderer>();
            gaugeBack.sprite = GetSquareSprite();
            gaugeBack.color = new Color(0.055f, 0.075f, 0.09f, 0.96f);
            gaugeBack.sortingOrder = 33;

            GameObject gaugeFillObject = new GameObject("Beam Charge Gauge Fill");
            gaugeFillObject.transform.SetParent(obj.transform, false);
            gaugeFillObject.transform.localPosition = new Vector3(-gaugeWidth * 0.5f, gaugeY, -0.08f);
            gaugeFillObject.transform.localScale = new Vector3(0.001f, gaugeHeight, 1f);
            SpriteRenderer gaugeFill = gaugeFillObject.AddComponent<SpriteRenderer>();
            gaugeFill.sprite = GetSquareSprite();
            gaugeFill.color = new Color(1f, 0.68f, 0.08f, 1f);
            gaugeFill.sortingOrder = 34;

            for (int tick = 1; tick < 4; tick++)
            {
                float tickX = Mathf.Lerp(-gaugeWidth * 0.5f, gaugeWidth * 0.5f, tick / 4f);
                AddDoodleLine($"Beam Charge Tick {tick}", obj.transform, new[]
                {
                    new Vector3(tickX, gaugeY - gaugeHeight * 0.6f, -0.085f),
                    new Vector3(tickX, gaugeY + gaugeHeight * 0.6f, -0.085f)
                }, new Color(0.04f, 0.055f, 0.065f, 0.82f), 0.014f, 35);
            }

            GameObject readyLampObject = new GameObject("Beam Ready Lamp");
            readyLampObject.transform.SetParent(obj.transform, false);
            readyLampObject.transform.localPosition = new Vector3(-halfWidth * 0.38f, halfHeight * 0.3f, -0.085f);
            float readyLampSize = Mathf.Clamp(data.size.y * 0.18f, 0.14f, 0.24f);
            readyLampObject.transform.localScale = Vector3.one * readyLampSize;
            SpriteRenderer readyLamp = readyLampObject.AddComponent<SpriteRenderer>();
            readyLamp.sprite = GetCircleSprite();
            readyLamp.color = new Color(0.18f, 0.4f, 0.46f, 1f);
            readyLamp.sortingOrder = 36;

            GameObject muzzle = new GameObject("Beam Muzzle");
            muzzle.transform.SetParent(obj.transform, false);
            muzzle.transform.localPosition = new Vector3(halfWidth + 0.08f, 0f, -0.08f);
            AddDoodleLine("Muzzle Top", muzzle.transform, new[]
            {
                new Vector3(-0.13f, halfHeight * 0.28f, 0f),
                new Vector3(0.13f, halfHeight * 0.18f, 0f)
            }, Color.black, 0.05f, 23);
            AddDoodleLine("Muzzle Bottom", muzzle.transform, new[]
            {
                new Vector3(-0.13f, -halfHeight * 0.28f, 0f),
                new Vector3(0.13f, -halfHeight * 0.18f, 0f)
            }, Color.black, 0.05f, 23);

            GameObject pulse = new GameObject("Beam Pulse");
            pulse.transform.SetParent(obj.transform, false);
            LineRenderer pulseLine = pulse.AddComponent<LineRenderer>();
            pulseLine.useWorldSpace = true;
            pulseLine.positionCount = 2;
            pulseLine.startWidth = 0.14f;
            pulseLine.endWidth = 0.1f;
            pulseLine.material = GetLineMaterial();
            pulseLine.startColor = new Color(1f, 0.18f, 0.08f, 0.98f);
            pulseLine.endColor = new Color(1f, 0.72f, 0.12f, 0.92f);
            pulseLine.sortingOrder = 40;
            pulseLine.enabled = false;

            StageBeamEmitter emitter = obj.AddComponent<StageBeamEmitter>();
            emitter.Configure(
                muzzle.transform,
                pulseLine,
                gaugeFillObject.transform,
                gaugeFill,
                readyLamp,
                gaugeWidth,
                readyLampSize,
                data.actionStrength,
                data.spawnPattern == 1,
                data.spawnBoxSize);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private GameObject CreateMissileLauncher(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = StageObjectType.MissileLauncher.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            obj.layer = groundLayer;

            BoxCollider2D selectionCollider = obj.AddComponent<BoxCollider2D>();
            selectionCollider.size = data.size;
            selectionCollider.isTrigger = true;

            Color casing = new Color(0.86f, 0.25f, 0.18f, 1f);
            AddSolidPaperBase(obj.transform, data.size);
            AddSolidWash(obj.transform, data.size, casing);
            AddSolidPencilFill(obj.transform, data.size, casing);
            AddSolidStraightBoxOutline(obj.transform, data.size);

            float halfWidth = data.size.x * 0.5f;
            float halfHeight = data.size.y * 0.5f;
            Color missileInk = new Color(0.55f, 0.04f, 0.035f, 1f);
            AddDoodleLine("Missile Body Preview", obj.transform, new[]
            {
                new Vector3(-halfWidth * 0.36f, -halfHeight * 0.13f, -0.06f),
                new Vector3(halfWidth * 0.22f, -halfHeight * 0.13f, -0.06f),
                new Vector3(halfWidth * 0.46f, 0f, -0.06f),
                new Vector3(halfWidth * 0.22f, halfHeight * 0.13f, -0.06f),
                new Vector3(-halfWidth * 0.36f, halfHeight * 0.13f, -0.06f),
                new Vector3(-halfWidth * 0.36f, -halfHeight * 0.13f, -0.06f)
            }, missileInk, 0.055f, 23);
            AddDoodleLine("Missile Fins Preview", obj.transform, new[]
            {
                new Vector3(-halfWidth * 0.2f, -halfHeight * 0.13f, -0.06f),
                new Vector3(-halfWidth * 0.34f, -halfHeight * 0.32f, -0.06f),
                new Vector3(0f, -halfHeight * 0.13f, -0.06f),
                new Vector3(-halfWidth * 0.2f, halfHeight * 0.13f, -0.06f),
                new Vector3(-halfWidth * 0.34f, halfHeight * 0.32f, -0.06f),
                new Vector3(0f, halfHeight * 0.13f, -0.06f)
            }, missileInk, 0.045f, 23);

            AddResourceSprite(
                obj.transform,
                "StageObjects/NicoDraw/missile-launcher",
                new Vector2(data.size.x * 1.12f, data.size.y * 1.18f),
                30,
                "Colored Pencil Missile Launcher");

            GameObject launcherTube = new GameObject("Missile Launcher Tube");
            launcherTube.transform.SetParent(obj.transform, false);
            launcherTube.transform.localPosition = new Vector3(halfWidth * 0.08f, 0f, -0.07f);
            launcherTube.transform.localScale = new Vector3(data.size.x * 0.74f, data.size.y * 0.34f, 1f);
            SpriteRenderer tubeRenderer = launcherTube.AddComponent<SpriteRenderer>();
            tubeRenderer.sprite = GetSquareSprite();
            tubeRenderer.color = new Color(0.22f, 0.13f, 0.18f, 0.92f);
            tubeRenderer.sortingOrder = 22;

            GameObject missileReadyLampObject = new GameObject("Missile Ready Lamp");
            missileReadyLampObject.transform.SetParent(obj.transform, false);
            missileReadyLampObject.transform.localPosition = new Vector3(-halfWidth * 0.35f, halfHeight * 0.33f, -0.09f);
            missileReadyLampObject.transform.localScale = Vector3.one * Mathf.Clamp(data.size.y * 0.17f, 0.13f, 0.22f);
            SpriteRenderer missileReadyLamp = missileReadyLampObject.AddComponent<SpriteRenderer>();
            missileReadyLamp.sprite = GetCircleSprite();
            missileReadyLamp.color = new Color(0.95f, 0.32f, 0.1f, 1f);
            missileReadyLamp.sortingOrder = 36;

            GameObject muzzle = new GameObject("Missile Muzzle");
            muzzle.transform.SetParent(obj.transform, false);
            muzzle.transform.localPosition = new Vector3(halfWidth + 0.18f, 0f, -0.08f);
            AddDoodleCircleAt(
                muzzle.transform,
                Vector2.zero,
                Mathf.Max(0.15f, halfHeight * 0.48f),
                Color.black,
                0.065f,
                24);

            StageMissileLauncher launcher = obj.AddComponent<StageMissileLauncher>();
            launcher.Configure(
                parent,
                muzzle.transform,
                missileReadyLamp,
                data.actionStrength,
                data.movementSpeed > 0f ? data.movementSpeed : 8f);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private static void AddSpikeDropperPreview(Transform parent, Vector2 size)
        {
            float radius = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.14f, 0.12f, 0.22f);
            float y = size.y * 0.29f;
            Color color = new Color(0.62f, 0.05f, 0.04f, 0.96f);
            AddDoodleLine("Dropper Spike", parent, new[]
            {
                new Vector3(-radius, y - radius, -0.06f),
                new Vector3(0f, y + radius, -0.06f),
                new Vector3(radius, y - radius, -0.06f),
                new Vector3(-radius, y - radius, -0.06f)
            }, color, 0.045f, 23);
        }

        private static void AddBombDropperPreview(Transform parent, int pattern, Vector2 size)
        {
            float radius = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.14f, 0.12f, 0.22f);
            float y = size.y * 0.29f;
            Color color = pattern == 2
                ? new Color(0.15f, 0.55f, 0.95f, 1f)
                : pattern == 1
                    ? new Color(0.95f, 0.2f, 0.12f, 1f)
                    : new Color(0.58f, 0.2f, 0.68f, 1f);
            AddDoodleCircleAt(parent, new Vector2(0f, y), radius, color, 0.045f, 23);
            AddDoodleLine("Bomb Dropper Fuse", parent, new[]
            {
                new Vector3(radius * 0.35f, y + radius * 0.72f, -0.06f),
                new Vector3(radius * 0.78f, y + radius * 1.35f, -0.06f)
            }, new Color(0.3f, 0.16f, 0.05f, 1f), 0.04f, 24);
        }

        private static void AddBoxDropperPatternPreview(Transform parent, int pattern, Vector2 size)
        {
            float radius = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.095f, 0.09f, 0.16f);
            float y = size.y * 0.29f;
            Color color = new Color(0.14f, 0.25f, 0.48f, 0.95f);
            int first = pattern == 0 ? 1 : Mathf.Clamp(pattern, 1, 3);
            int last = pattern == 0 ? 3 : first;
            int count = last - first + 1;
            for (int shape = first; shape <= last; shape++)
            {
                float x = count == 1
                    ? 0f
                    : Mathf.Lerp(-radius * 2.3f, radius * 2.3f, (shape - first) / (float)(count - 1));
                if (shape == 1)
                {
                    AddDoodleLine("Dropper Square", parent, new[]
                    {
                        new Vector3(x - radius, y - radius, -0.06f),
                        new Vector3(x + radius, y - radius, -0.06f),
                        new Vector3(x + radius, y + radius, -0.06f),
                        new Vector3(x - radius, y + radius, -0.06f),
                        new Vector3(x - radius, y - radius, -0.06f)
                    }, color, 0.035f, 23);
                }
                else if (shape == 2)
                {
                    AddDoodleCircleAt(parent, new Vector2(x, y), radius, color, 0.035f, 23);
                }
                else
                {
                    AddDoodleLine("Dropper Triangle", parent, new[]
                    {
                        new Vector3(x, y + radius, -0.06f),
                        new Vector3(x + radius, y - radius, -0.06f),
                        new Vector3(x - radius, y - radius, -0.06f),
                        new Vector3(x, y + radius, -0.06f)
                    }, color, 0.035f, 23);
                }
            }
        }

        public GameObject CreateDroppedBox(
            StageObjectType boxType,
            string objectId,
            Vector2 position,
            float size,
            Transform parent,
            float bombFuseSeconds = 5f)
        {
            if (boxType != StageObjectType.WoodBox
                && boxType != StageObjectType.Ball
                && boxType != StageObjectType.TriangleBox
                && boxType != StageObjectType.Spike
                && boxType != StageObjectType.Bomb
                && boxType != StageObjectType.PickupFuseBomb)
            {
                boxType = StageObjectType.WoodBox;
            }

            StageObjectData data = CreateDefaultData(boxType, position);
            data.objectId = string.IsNullOrEmpty(objectId) ? StageObjectId.New() : objectId;
            data.bombFuseSeconds = Mathf.Clamp(bombFuseSeconds > 0f ? bombFuseSeconds : 5f, 1f, 15f);
            // Runtime challenge spawners can use oversized bombs. Editor-authored
            // droppers still keep their own 0.5-2.0 input range.
            float clampedSize = Mathf.Clamp(size, 0.5f, 3f);
            data.size = boxType == StageObjectType.Spike
                ? new Vector2(clampedSize, clampedSize * 0.8f)
                : Vector2.one * clampedSize;
            return boxType == StageObjectType.Spike
                ? CreateDroppedSpike(data, parent)
                : CreateWeight(data, parent);
        }

        internal Transform CreateDroppedBoxPreview(
            StageObjectType boxType,
            Transform parent,
            int sortingOrder = 33)
        {
            if (parent == null) return null;
            if (boxType != StageObjectType.WoodBox
                && boxType != StageObjectType.Ball
                && boxType != StageObjectType.TriangleBox)
            {
                boxType = StageObjectType.WoodBox;
            }

            GameObject preview = new GameObject("Next " + boxType + " Preview");
            preview.transform.SetParent(parent, false);
            DrawMovableObject(preview.transform, boxType);
            UnityEngine.Rendering.SortingGroup group = preview.AddComponent<UnityEngine.Rendering.SortingGroup>();
            group.sortingOrder = sortingOrder;
            group.sortAtRoot = true;
            return preview.transform;
        }

        private GameObject CreateDroppedSpike(StageObjectData data, Transform parent)
        {
            GameObject spike = CreateSpike(data, parent);
            spike.layer = pushableLayer;

            BoxCollider2D solid = spike.AddComponent<BoxCollider2D>();
            solid.size = new Vector2(data.size.x * 0.9f, data.size.y * 0.22f);
            solid.offset = new Vector2(0f, -data.size.y * 0.38f);

            Rigidbody2D body = spike.AddComponent<Rigidbody2D>();
            body.mass = Mathf.Max(1.5f, data.size.x * data.size.y * 2f);
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearDamping = 0.08f;
            body.angularDamping = 0.35f;
            return spike;
        }

        private GameObject CreateWeight(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = data.type.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.localScale = new Vector3(
                Mathf.Max(0.2f, data.size.x),
                Mathf.Max(0.2f, data.size.y),
                1f);
            obj.layer = pushableLayer;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
            rb.mass = data.type == StageObjectType.Weight
                || data.type == StageObjectType.IronBox
                || data.type == StageObjectType.Rock
                || data.type == StageObjectType.FallingRock
                    ? 50f
                    : data.type == StageObjectType.Barrel ? 4f : 2.5f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            rb.interpolation = RigidbodyInterpolation2D.None;
            rb.sleepMode = RigidbodySleepMode2D.StartAsleep;
            rb.linearDamping = 0.12f;
            rb.angularDamping = 0.18f;
            obj.AddComponent<CarryableObject>();

            AddMovableCollider(obj, data.type);
            DrawMovableObject(obj.transform, data.type);
            AddEditorMetadata(obj, data);
            if (data.type == StageObjectType.Bomb || data.type == StageObjectType.PickupFuseBomb)
            {
                StageBomb bomb = obj.AddComponent<StageBomb>();
                bomb.Configure(
                    data.type == StageObjectType.PickupFuseBomb,
                    data.bombFuseSeconds > 0f ? data.bombFuseSeconds : 5f);
            }
            return obj;
        }

        private GameObject CreateBombBreakableWall(StageObjectData data, Transform parent)
        {
            GameObject wall = CreateSolid(data, parent);
            StageBombBreakableWall bombWall = wall.AddComponent<StageBombBreakableWall>();
            bombWall.Configure(Mathf.Clamp(Mathf.RoundToInt(data.actionStrength > 0f ? data.actionStrength : 1f), 1, 50), data.size);
            return wall;
        }

        private GameObject CreateBulletBreakableWall(StageObjectData data, Transform parent)
        {
            GameObject wall = CreateSolid(data, parent);
            StageBombBreakableWall damage = wall.AddComponent<StageBombBreakableWall>();
            damage.Configure(Mathf.Clamp(Mathf.RoundToInt(data.actionStrength > 0f ? data.actionStrength : 3f), 1, 50), data.size);
            damage.SetRequirementBadgeVisible(false);
            StageBulletBreakableWall bulletWall = wall.AddComponent<StageBulletBreakableWall>();
            bulletWall.Configure(damage, data.size);
            return wall;
        }

        private static void AddMovableCollider(GameObject obj, StageObjectType type)
        {
            switch (type)
            {
                case StageObjectType.Ball:
                case StageObjectType.Bomb:
                case StageObjectType.PickupFuseBomb:
                    CircleCollider2D circle = obj.AddComponent<CircleCollider2D>();
                    circle.radius = 0.48f;
                    break;
                case StageObjectType.Barrel:
                    CapsuleCollider2D capsule = obj.AddComponent<CapsuleCollider2D>();
                    capsule.size = new Vector2(0.82f, 0.98f);
                    capsule.direction = CapsuleDirection2D.Vertical;
                    break;
                case StageObjectType.Rock:
                case StageObjectType.FallingRock:
                    PolygonCollider2D rock = obj.AddComponent<PolygonCollider2D>();
                    rock.points = new[]
                    {
                        new Vector2(-0.48f, -0.4f),
                        new Vector2(-0.42f, 0.15f),
                        new Vector2(-0.16f, 0.46f),
                        new Vector2(0.27f, 0.42f),
                        new Vector2(0.49f, 0.05f),
                        new Vector2(0.38f, -0.42f)
                    };
                    break;
                case StageObjectType.Bucket:
                    PolygonCollider2D bucket = obj.AddComponent<PolygonCollider2D>();
                    bucket.points = new[]
                    {
                        new Vector2(-0.43f, 0.34f),
                        new Vector2(0.43f, 0.34f),
                        new Vector2(0.32f, -0.46f),
                        new Vector2(-0.32f, -0.46f)
                    };
                    break;
                case StageObjectType.TriangleBox:
                    PolygonCollider2D triangle = obj.AddComponent<PolygonCollider2D>();
                    triangle.points = new[]
                    {
                        new Vector2(-0.49f, -0.48f),
                        new Vector2(0.49f, -0.48f),
                        new Vector2(0f, 0.49f)
                    };
                    break;
                default:
                    BoxCollider2D box = obj.AddComponent<BoxCollider2D>();
                    box.size = new Vector2(0.96f, 0.96f);
                    break;
            }
        }

        private static void DrawMovableObject(Transform parent, StageObjectType type)
        {
            Color dark = new Color(0.12f, 0.09f, 0.06f, 0.96f);
            switch (type)
            {
                case StageObjectType.WoodBox:
                    if (AddResourceSprite(parent, "StageObjects/NicoDraw/wood-box", Vector2.one, 20, "Colored Pencil Wood Box"))
                    {
                        break;
                    }
                    // Keep the box opaque and readable without creating roughly one
                    // hundred tiny LineRenderer objects per crate. Large crate piles
                    // otherwise become CPU- and draw-call-heavy.
                    AddMovableBase(parent, GetSquareSprite(), new Color(0.7f, 0.42f, 0.18f, 1f));
                    AddSketchBoxOutline(parent, Vector2.one, new Color(0.28f, 0.14f, 0.045f), 0.055f);
                    AddDoodleLine("Wood Left Plank", parent, new[] { new Vector3(-0.27f, -0.48f), new Vector3(-0.27f, 0.48f) }, new Color(0.34f, 0.17f, 0.05f), 0.025f, 18);
                    AddDoodleLine("Wood Right Plank", parent, new[] { new Vector3(0.27f, -0.48f), new Vector3(0.27f, 0.48f) }, new Color(0.34f, 0.17f, 0.05f), 0.025f, 18);
                    AddDoodleLine("Wood Brace A", parent, new[] { new Vector3(-0.42f, -0.4f), new Vector3(0.42f, 0.4f) }, new Color(0.28f, 0.13f, 0.04f), 0.04f, 19);
                    AddDoodleLine("Wood Brace B", parent, new[] { new Vector3(-0.42f, 0.4f), new Vector3(0.42f, -0.4f) }, new Color(0.28f, 0.13f, 0.04f), 0.04f, 19);
                    break;
                case StageObjectType.IronBox:
                    if (AddResourceSprite(parent, "StageObjects/NicoDraw/iron-box",
                        new Vector2(1.13f, 1.08f), 22, "Colored Pencil Iron Box")) break;
                    AddMovableBase(parent, GetSquareSprite(), new Color(0.66f, 0.7f, 0.73f, 0.96f));
                    AddPencilFillLocal(parent, Vector2.one, new Color(0.18f, 0.27f, 0.34f, 1f));
                    AddSketchBoxOutline(parent, Vector2.one, new Color(0.1f, 0.14f, 0.17f), 0.068f);
                    AddDoodleLine("Iron Inset", parent, new[]
                    {
                        new Vector3(-0.34f, -0.34f), new Vector3(0.34f, -0.34f),
                        new Vector3(0.34f, 0.34f), new Vector3(-0.34f, 0.34f),
                        new Vector3(-0.34f, -0.34f)
                    }, new Color(0.22f, 0.27f, 0.31f), 0.042f, 18);
                    AddDoodleLine("Iron Diagonal Brace A", parent, new[]
                    {
                        new Vector3(-0.31f, -0.3f), new Vector3(0.29f, 0.31f)
                    }, new Color(0.25f, 0.3f, 0.34f, 0.88f), 0.032f, 18);
                    AddDoodleLine("Iron Diagonal Brace B", parent, new[]
                    {
                        new Vector3(-0.3f, 0.31f), new Vector3(0.31f, -0.29f)
                    }, new Color(0.25f, 0.3f, 0.34f, 0.88f), 0.032f, 18);
                    AddRivets(parent, new Color(0.12f, 0.16f, 0.2f));
                    break;
                case StageObjectType.Ball:
                    if (AddResourceSprite(parent, "StageObjects/NicoDraw/ball",
                        new Vector2(1.245f, 1.167f), 22, "Colored Pencil Ball")) break;
                    AddMovableBase(parent, GetCircleSprite(), new Color(0.28f, 0.66f, 0.94f, 0.94f));
                    AddDoodleCircle(parent, 0.48f, new Color(0.04f, 0.2f, 0.48f), 0.062f);
                    AddDoodleCircleAt(parent, new Vector2(0.012f, -0.008f), 0.445f,
                        new Color(0.12f, 0.43f, 0.76f, 0.76f), 0.025f, 17);
                    AddDoodleLine("Ball Curve", parent, new[]
                    {
                        new Vector3(-0.36f, -0.08f), new Vector3(-0.12f, 0.05f),
                        new Vector3(0.12f, 0.12f), new Vector3(0.35f, 0.06f)
                    }, new Color(0.82f, 0.92f, 1f, 0.9f), 0.04f, 18);
                    AddDoodleLine("Ball Pencil Shade A", parent, new[]
                    {
                        new Vector3(-0.3f, -0.26f), new Vector3(0.22f, 0.27f)
                    }, new Color(0.04f, 0.3f, 0.67f, 0.42f), 0.027f, 17);
                    AddDoodleLine("Ball Pencil Shade B", parent, new[]
                    {
                        new Vector3(-0.38f, -0.08f), new Vector3(0.08f, 0.38f)
                    }, new Color(0.04f, 0.3f, 0.67f, 0.34f), 0.025f, 17);
                    AddDoodleLine("Ball Pencil Shine", parent, new[]
                    {
                        new Vector3(-0.22f, 0.3f), new Vector3(-0.08f, 0.39f)
                    }, new Color(0.94f, 0.98f, 1f, 0.92f), 0.045f, 19);
                    break;
                case StageObjectType.Barrel:
                    if (AddResourceSprite(parent, "StageObjects/NicoDraw/barrel",
                        new Vector2(0.91f, 1.14f), 22, "Colored Pencil Barrel")) break;
                    AddMovableBase(parent, GetCircleSprite(), new Color(0.67f, 0.35f, 0.12f, 0.96f), new Vector2(0.82f, 1f));
                    AddDoodleLine("Barrel Outline", parent, new[]
                    {
                        new Vector3(-0.3f, -0.48f), new Vector3(-0.42f, -0.28f),
                        new Vector3(-0.42f, 0.28f), new Vector3(-0.3f, 0.48f),
                        new Vector3(0.3f, 0.48f), new Vector3(0.42f, 0.28f),
                        new Vector3(0.42f, -0.28f), new Vector3(0.3f, -0.48f),
                        new Vector3(-0.3f, -0.48f)
                    }, dark, 0.055f, 18);
                    AddDoodleLine("Barrel Loose Outline", parent, new[]
                    {
                        new Vector3(-0.28f, -0.47f), new Vector3(-0.4f, -0.25f),
                        new Vector3(-0.39f, 0.3f), new Vector3(-0.27f, 0.46f),
                        new Vector3(0.28f, 0.47f), new Vector3(0.4f, 0.26f),
                        new Vector3(0.39f, -0.3f), new Vector3(0.28f, -0.47f)
                    }, new Color(0.31f, 0.14f, 0.045f, 0.7f), 0.027f, 17);
                    AddDoodleLine("Barrel Top Band", parent, new[] { new Vector3(-0.38f, 0.28f), new Vector3(0.38f, 0.27f) }, new Color(0.18f, 0.19f, 0.2f), 0.082f, 19);
                    AddDoodleLine("Barrel Bottom Band", parent, new[] { new Vector3(-0.38f, -0.28f), new Vector3(0.38f, -0.27f) }, new Color(0.18f, 0.19f, 0.2f), 0.082f, 19);
                    AddDoodleLine("Barrel Wood Seam", parent, new[] { new Vector3(0f, -0.44f), new Vector3(0.015f, 0.44f) }, new Color(0.34f, 0.16f, 0.05f), 0.03f, 18);
                    AddDoodleLine("Barrel Pencil Shade A", parent, new[] { new Vector3(-0.3f, -0.2f), new Vector3(0.25f, 0.23f) }, new Color(0.35f, 0.15f, 0.04f, 0.4f), 0.025f, 17);
                    AddDoodleLine("Barrel Pencil Shade B", parent, new[] { new Vector3(-0.29f, 0.03f), new Vector3(0.24f, 0.43f) }, new Color(0.35f, 0.15f, 0.04f, 0.35f), 0.023f, 17);
                    break;
                case StageObjectType.Rock:
                case StageObjectType.FallingRock:
                    AddMovableBase(parent, GetCircleSprite(), new Color(0.46f, 0.45f, 0.43f, 0.9f), new Vector2(1f, 0.88f));
                    AddRockOutline(parent, type == StageObjectType.FallingRock);
                    break;
                case StageObjectType.IceBlock:
                    AddMovableBase(parent, GetSquareSprite(), new Color(0.55f, 0.86f, 1f, 0.7f));
                    AddSketchBoxOutline(parent, Vector2.one, new Color(0.12f, 0.54f, 0.82f), 0.05f);
                    AddDoodleLine("Ice Shine", parent, new[] { new Vector3(-0.34f, 0.36f), new Vector3(0.18f, -0.2f), new Vector3(0.36f, -0.08f) }, new Color(0.92f, 0.99f, 1f), 0.045f, 18);
                    break;
                case StageObjectType.FloatingBox:
                    AddMovableBase(parent, GetSquareSprite(), new Color(0.64f, 0.52f, 0.9f, 0.78f));
                    AddSketchBoxOutline(parent, Vector2.one, new Color(0.32f, 0.18f, 0.62f), 0.05f);
                    AddDoodleLine("Float Arrow", parent, new[] { new Vector3(0f, -0.25f), new Vector3(0f, 0.27f), new Vector3(-0.17f, 0.1f), new Vector3(0f, 0.27f), new Vector3(0.17f, 0.1f) }, Color.white, 0.045f, 19);
                    break;
                case StageObjectType.RubberBox:
                    if (AddResourceSprite(parent, "StageObjects/NicoDraw/rubber-box",
                        Vector2.one, 20, "Colored Pencil Rubber Box"))
                    {
                        break;
                    }
                    AddMovableBase(parent, GetSquareSprite(), new Color(0.95f, 0.46f, 0.28f, 0.86f));
                    AddSketchBoxOutline(parent, Vector2.one, new Color(0.55f, 0.15f, 0.08f), 0.055f);
                    AddDoodleLine("Rubber Zigzag", parent, new[] { new Vector3(-0.38f, 0.05f), new Vector3(-0.16f, 0.22f), new Vector3(0.05f, -0.14f), new Vector3(0.35f, 0.12f) }, new Color(1f, 0.86f, 0.5f), 0.06f, 19);
                    break;
                case StageObjectType.Bomb:
                    AddMovableBase(parent, GetCircleSprite(), new Color(0.16f, 0.15f, 0.18f, 0.98f), new Vector2(0.88f, 0.88f));
                    AddDoodleCircleAt(parent, new Vector2(0f, -0.05f), 0.405f, new Color(0.035f, 0.03f, 0.045f), 0.067f, 18);
                    AddDoodleCircleAt(parent, new Vector2(0.012f, -0.035f), 0.37f, new Color(0.36f, 0.34f, 0.4f, 0.66f), 0.024f, 17);
                    AddDoodleLine("Bomb Pencil Shade A", parent, new[] { new Vector3(-0.3f, -0.18f), new Vector3(0.2f, 0.25f) }, new Color(0.72f, 0.7f, 0.78f, 0.22f), 0.03f, 17);
                    AddDoodleLine("Bomb Pencil Shade B", parent, new[] { new Vector3(-0.34f, 0.02f), new Vector3(0.08f, 0.36f) }, new Color(0.72f, 0.7f, 0.78f, 0.18f), 0.025f, 17);
                    AddDoodleLine("Bomb Fuse Collar", parent, new[]
                    {
                        new Vector3(0.1f, 0.3f), new Vector3(0.26f, 0.27f),
                        new Vector3(0.31f, 0.38f), new Vector3(0.16f, 0.42f)
                    }, new Color(0.18f, 0.13f, 0.08f), 0.075f, 20);
                    AddDoodleLine("Bomb Fuse", parent, new[] { new Vector3(0.21f, 0.39f), new Vector3(0.32f, 0.52f), new Vector3(0.46f, 0.45f) }, new Color(0.42f, 0.22f, 0.07f), 0.06f, 20);
                    AddDoodleLine("Bomb Spark", parent, new[] { new Vector3(0.42f, 0.42f), new Vector3(0.51f, 0.55f), new Vector3(0.47f, 0.4f), new Vector3(0.57f, 0.45f) }, new Color(1f, 0.57f, 0.04f), 0.055f, 21);
                    break;
                case StageObjectType.PickupFuseBomb:
                    AddMovableBase(parent, GetCircleSprite(), new Color(0.08f, 0.3f, 0.54f, 0.97f), new Vector2(0.88f, 0.88f));
                    AddDoodleCircleAt(parent, new Vector2(0f, -0.05f), 0.405f, new Color(0.02f, 0.1f, 0.2f), 0.067f, 18);
                    AddDoodleCircleAt(parent, new Vector2(0.012f, -0.035f), 0.37f, new Color(0.2f, 0.62f, 0.9f, 0.54f), 0.024f, 17);
                    AddDoodleLine("Pickup Bomb Pencil Shade", parent, new[] { new Vector3(-0.32f, -0.16f), new Vector3(0.16f, 0.27f) }, new Color(0.45f, 0.82f, 1f, 0.32f), 0.03f, 17);
                    AddDoodleLine("Pickup Bomb Fuse", parent, new[] { new Vector3(0.18f, 0.34f), new Vector3(0.31f, 0.51f), new Vector3(0.46f, 0.44f) }, new Color(0.42f, 0.22f, 0.07f), 0.06f, 20);
                    AddDoodleCircleAt(parent, new Vector2(-0.19f, 0.02f), 0.105f, new Color(0.34f, 0.82f, 1f, 1f), 0.045f, 21);
                    break;
                case StageObjectType.Battery:
                    AddMovableBase(parent, GetSquareSprite(), new Color(0.54f, 0.78f, 0.25f, 0.88f), new Vector2(0.72f, 0.94f));
                    AddDoodleLine("Battery Outline", parent, new[] { new Vector3(-0.36f, -0.47f), new Vector3(0.36f, -0.47f), new Vector3(0.36f, 0.4f), new Vector3(-0.36f, 0.4f), new Vector3(-0.36f, -0.47f) }, dark, 0.05f, 18);
                    AddDoodleLine("Battery Terminal", parent, new[] { new Vector3(-0.14f, 0.43f), new Vector3(0.14f, 0.43f), new Vector3(0.14f, 0.5f), new Vector3(-0.14f, 0.5f) }, dark, 0.05f, 19);
                    AddDoodleLine("Battery Plus", parent, new[] { new Vector3(-0.12f, 0f), new Vector3(0.12f, 0f), new Vector3(0f, -0.12f), new Vector3(0f, 0.12f) }, Color.white, 0.05f, 20);
                    break;
                case StageObjectType.Bucket:
                    AddDoodleLine("Bucket Body", parent, new[]
                    {
                        new Vector3(-0.43f, 0.3f), new Vector3(-0.31f, -0.46f),
                        new Vector3(0.31f, -0.46f), new Vector3(0.43f, 0.3f),
                        new Vector3(-0.43f, 0.3f)
                    }, new Color(0.2f, 0.42f, 0.58f), 0.06f, 18);
                    AddDoodleLine("Bucket Handle", parent, new[] { new Vector3(-0.36f, 0.25f), new Vector3(-0.2f, 0.48f), new Vector3(0.2f, 0.48f), new Vector3(0.36f, 0.25f) }, new Color(0.18f, 0.22f, 0.24f), 0.045f, 19);
                    break;
                case StageObjectType.TriangleBox:
                    AddTriangleBoxVisual(parent);
                    break;
                case StageObjectType.Weight:
                default:
                    AddMovableBase(parent, GetCircleSprite(), new Color(0.24f, 0.25f, 0.27f, 0.92f), new Vector2(0.92f, 0.78f));
                    AddDoodleLine("Weight Base", parent, new[] { new Vector3(-0.45f, -0.4f), new Vector3(0.45f, -0.4f), new Vector3(0.34f, 0.22f), new Vector3(-0.34f, 0.22f), new Vector3(-0.45f, -0.4f) }, Color.black, 0.06f, 18);
                    AddDoodleLine("Weight Handle", parent, new[] { new Vector3(-0.18f, 0.22f), new Vector3(-0.1f, 0.45f), new Vector3(0.1f, 0.45f), new Vector3(0.18f, 0.22f) }, Color.black, 0.055f, 20);
                    break;
            }
        }

        private static void AddTriangleBoxVisual(Transform parent)
        {
            if (AddResourceSprite(
                parent,
                "StageObjects/NicoDraw/triangle-box",
                new Vector2(0.98f, 0.97f),
                20,
                "Colored Pencil Triangle Box"))
            {
                return;
            }

            Color fill = new Color(0.76f, 0.48f, 0.2f, 0.96f);
            Color outline = new Color(0.3f, 0.14f, 0.045f, 1f);
            Vector3 left = new Vector3(-0.49f, -0.48f, 0.02f);
            Vector3 right = new Vector3(0.49f, -0.48f, 0.02f);
            Vector3 top = new Vector3(0f, 0.49f, 0.02f);

            Mesh mesh = new Mesh
            {
                name = "Triangle Box Fill Mesh",
                vertices = new[] { left, top, right },
                triangles = new[] { 0, 2, 1 },
                colors = new[] { fill, fill, fill }
            };
            mesh.RecalculateBounds();

            GameObject visual = new GameObject("Triangle Box Fill");
            visual.transform.SetParent(parent, false);
            MeshFilter filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetLineMaterial();
            renderer.sortingOrder = 3;

            AddDoodleLine(
                "Triangle Box Outline",
                parent,
                new[] { left, top, right, left },
                outline,
                0.06f,
                18);
            AddDoodleLine(
                "Triangle Box Brace Left",
                parent,
                new[] { new Vector3(-0.38f, -0.39f), new Vector3(0f, 0.34f) },
                new Color(0.4f, 0.19f, 0.055f),
                0.035f,
                19);
            AddDoodleLine(
                "Triangle Box Brace Right",
                parent,
                new[] { new Vector3(0.38f, -0.39f), new Vector3(0f, 0.34f) },
                new Color(0.4f, 0.19f, 0.055f),
                0.035f,
                19);
        }

        private static void AddMovableBase(Transform parent, Sprite sprite, Color color, Vector2 scale = default)
        {
            GameObject visual = new GameObject("Movable Fill");
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            visual.transform.localScale = scale == default ? Vector3.one : new Vector3(scale.x, scale.y, 1f);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = 3;
        }

        private static void AddRivets(Transform parent, Color color)
        {
            Vector2[] positions =
            {
                new Vector2(-0.39f, -0.39f),
                new Vector2(0.39f, -0.39f),
                new Vector2(-0.39f, 0.39f),
                new Vector2(0.39f, 0.39f)
            };
            for (int i = 0; i < positions.Length; i++)
            {
                AddDoodleCircleAt(parent, positions[i], 0.045f, color, 0.025f, 20);
            }
        }

        private static void AddRockOutline(Transform parent, bool dangerous)
        {
            Color outline = dangerous ? new Color(0.5f, 0.12f, 0.08f) : new Color(0.22f, 0.21f, 0.2f);
            AddDoodleLine("Rock Outline", parent, new[]
            {
                new Vector3(-0.48f, -0.38f), new Vector3(-0.42f, 0.12f),
                new Vector3(-0.16f, 0.45f), new Vector3(0.25f, 0.41f),
                new Vector3(0.48f, 0.04f), new Vector3(0.37f, -0.4f),
                new Vector3(-0.48f, -0.38f)
            }, outline, 0.06f, 18);
            AddDoodleLine("Rock Facet", parent, new[] { new Vector3(-0.2f, 0.35f), new Vector3(0.05f, 0.05f), new Vector3(0.34f, 0.27f) }, new Color(outline.r, outline.g, outline.b, 0.7f), 0.035f, 19);
        }

        private GameObject CreateProp(StageObjectData data, Transform parent)
        {
            Color stroke = GetObjectColor(data.type);
            GameObject obj = new GameObject(data.objectId);
            obj.name = data.type.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            bool hasCoinArt = data.type == StageObjectType.Coin
                && AddResourceSprite(obj.transform, "StageObjects/NicoDraw/coin",
                    new Vector2(Mathf.Max(0.56f, data.size.x * 0.76f), Mathf.Max(0.56f, data.size.y * 0.76f)),
                    20, "Colored Pencil Coin");
            if (!hasCoinArt)
            {
                AddDoodleCircle(obj.transform, Mathf.Max(0.28f, data.size.x * 0.38f), stroke, 0.055f);
            }

            CircleCollider2D collider = obj.AddComponent<CircleCollider2D>();
            collider.radius = Mathf.Max(0.35f, data.size.x * 0.45f);
            collider.isTrigger = StageObjectCatalog.Get(data.type).Kind != StageObjectKind.Pushable;
            AddEditorMetadata(obj, data);
            return obj;
        }

        private GameObject CreateKey(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = StageObjectType.Key.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            obj.transform.localScale = new Vector3(
                Mathf.Max(0.2f, data.size.x),
                Mathf.Max(0.2f, data.size.y),
                1f);
            obj.layer = pushableLayer;

            Rigidbody2D body = obj.AddComponent<Rigidbody2D>();
            body.mass = 0.55f;
            body.gravityScale = 1.2f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            CapsuleCollider2D collider = obj.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.5f, 0.92f);

            obj.AddComponent<CarryableObject>();

            if (AddResourceSprite(obj.transform, "StageObjects/NicoDraw/key", new Vector2(0.72f, 1.02f), 22, "Colored Pencil Key"))
            {
                AddEditorMetadata(obj, data);
                return obj;
            }

            Color gold = new Color(1f, 0.7f, 0.02f, 1f);
            AddDoodleCircleAt(obj.transform, new Vector2(0f, 0.27f), 0.22f, gold, 0.065f, 22);
            AddDoodleCircleAt(obj.transform, new Vector2(0f, 0.27f), 0.105f, gold, 0.035f, 22);
            AddDoodleLine("Key Shaft", obj.transform, new[]
            {
                new Vector3(0f, 0.07f, -0.02f),
                new Vector3(0f, -0.39f, -0.02f)
            }, gold, 0.09f, 22);
            AddDoodleLine("Key Teeth", obj.transform, new[]
            {
                new Vector3(0f, -0.32f, -0.02f),
                new Vector3(0.2f, -0.32f, -0.02f),
                new Vector3(0.2f, -0.2f, -0.02f),
                new Vector3(0.11f, -0.2f, -0.02f),
                new Vector3(0.11f, -0.11f, -0.02f)
            }, gold, 0.075f, 22);
            AddDoodleLine("Key Tip", obj.transform, new[]
            {
                new Vector3(-0.075f, -0.4f, -0.02f),
                new Vector3(0.075f, -0.4f, -0.02f)
            }, gold, 0.075f, 22);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private GameObject CreateKeyhole(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = StageObjectType.Keyhole.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            obj.transform.localScale = new Vector3(
                Mathf.Max(0.2f, data.size.x),
                Mathf.Max(0.2f, data.size.y),
                1f);

            BoxCollider2D trigger = obj.AddComponent<BoxCollider2D>();
            // The key is often dropped at an angle, so the usable insertion area
            // needs a little room beyond the visible keyhole silhouette.
            trigger.size = new Vector2(0.95f, 1.1f);
            trigger.isTrigger = true;

            if (!AddResourceSprite(obj.transform, "StageObjects/NicoDraw/keyhole", new Vector2(0.72f, 0.98f), 22, "Graphite Keyhole"))
            {
                AddFilledKeyholeSilhouette(obj.transform);
            }
            AddEditorMetadata(obj, data);
            return obj;
        }

        private GameObject CreateInkScale(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = StageObjectType.InkScale.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            obj.transform.localScale = new Vector3(
                Mathf.Max(0.2f, data.size.x),
                Mathf.Max(0.2f, data.size.y),
                1f);
            obj.layer = groundLayer;

            SpriteRenderer body;
            Sprite scaleArt = Resources.Load<Sprite>("StageObjects/NicoDraw/ink-scale");
            bool hasColoredPencilScaleArt = scaleArt != null
                && scaleArt.bounds.size.x > 0f
                && scaleArt.bounds.size.y > 0f;
            if (hasColoredPencilScaleArt)
            {
                GameObject artObject = new GameObject("Colored Pencil Ink Scale");
                artObject.transform.SetParent(obj.transform, false);
                artObject.transform.localPosition = new Vector3(0f, 0f, -0.025f);
                artObject.transform.localScale = new Vector3(
                    1.12f / scaleArt.bounds.size.x,
                    1.06f / scaleArt.bounds.size.y,
                    1f);
                body = artObject.AddComponent<SpriteRenderer>();
                body.sprite = scaleArt;
                body.color = Color.white;
                body.sortingOrder = 8;
            }
            else
            {
                body = obj.AddComponent<SpriteRenderer>();
                body.sprite = GetScaleBodySprite();
                body.color = new Color(0.96f, 0.77f, 0.24f, 0.94f);
                body.sortingOrder = 8;
            }

            BoxCollider2D platform = obj.AddComponent<BoxCollider2D>();
            platform.size = new Vector2(1.08f, 0.16f);
            platform.offset = new Vector2(0f, 0.43f);

            BoxCollider2D weighingArea = obj.AddComponent<BoxCollider2D>();
            weighingArea.isTrigger = true;
            // A vertical weighing column also catches players stacked on top of
            // another player. InkWeightScale filters out airborne characters.
            weighingArea.size = new Vector2(0.94f, 8f);
            weighingArea.offset = new Vector2(0f, 4.48f);

            if (!hasColoredPencilScaleArt)
            {
                GameObject plateObject = new GameObject("Scale Top Plate");
                plateObject.transform.SetParent(obj.transform, false);
                plateObject.transform.localPosition = new Vector3(0f, 0.43f, -0.025f);
                plateObject.transform.localScale = new Vector3(1.08f, 0.16f, 1f);
                SpriteRenderer plate = plateObject.AddComponent<SpriteRenderer>();
                plate.sprite = GetSquareSprite();
                plate.color = new Color(0.84f, 0.86f, 0.82f, 1f);
                plate.sortingOrder = 18;

                AddDoodleLine("Scale Plate Outline", obj.transform, new[]
                {
                    new Vector3(-0.54f, 0.35f), new Vector3(-0.54f, 0.51f),
                    new Vector3(0.54f, 0.51f), new Vector3(0.54f, 0.35f),
                    new Vector3(-0.54f, 0.35f)
                }, new Color(0.1f, 0.1f, 0.09f), 0.035f, 20);
                AddDoodleLine("Scale Body Outline", obj.transform, new[]
                {
                    new Vector3(-0.49f, -0.48f), new Vector3(0.49f, -0.48f),
                    new Vector3(0.4f, 0.35f), new Vector3(-0.4f, 0.35f),
                    new Vector3(-0.49f, -0.48f)
                }, new Color(0.14f, 0.11f, 0.05f), 0.045f, 19);

                GameObject displayObject = new GameObject("Scale Display Window");
                displayObject.transform.SetParent(obj.transform, false);
                displayObject.transform.localPosition = new Vector3(0f, 0.08f, -0.035f);
                displayObject.transform.localScale = new Vector3(0.72f, 0.34f, 1f);
                SpriteRenderer display = displayObject.AddComponent<SpriteRenderer>();
                display.sprite = GetSquareSprite();
                display.color = new Color(0.98f, 0.97f, 0.84f, 1f);
                display.sortingOrder = 20;
                AddDoodleLine("Scale Display Outline", obj.transform, new[]
                {
                    new Vector3(-0.36f, -0.09f), new Vector3(0.36f, -0.09f),
                    new Vector3(0.36f, 0.25f), new Vector3(-0.36f, 0.25f),
                    new Vector3(-0.36f, -0.09f)
                }, new Color(0.12f, 0.11f, 0.08f), 0.025f, 22);

                GameObject gaugeBackObject = new GameObject("Scale Gauge Back");
                gaugeBackObject.transform.SetParent(obj.transform, false);
                gaugeBackObject.transform.localPosition = new Vector3(0f, -0.29f, -0.035f);
                gaugeBackObject.transform.localScale = new Vector3(0.64f, 0.1f, 1f);
                SpriteRenderer gaugeBack = gaugeBackObject.AddComponent<SpriteRenderer>();
                gaugeBack.sprite = GetSquareSprite();
                gaugeBack.color = new Color(0.12f, 0.11f, 0.09f, 0.92f);
                gaugeBack.sortingOrder = 20;
            }

            GameObject gaugeFillObject = new GameObject("Scale Gauge Fill");
            gaugeFillObject.transform.SetParent(obj.transform, false);
            gaugeFillObject.transform.localPosition = new Vector3(-0.31f, -0.29f, -0.045f);
            gaugeFillObject.transform.localScale = new Vector3(0f, 0.065f, 1f);
            SpriteRenderer gaugeFill = gaugeFillObject.AddComponent<SpriteRenderer>();
            gaugeFill.sprite = GetSquareSprite();
            gaugeFill.color = new Color(0.3f, 0.82f, 0.96f, 1f);
            gaugeFill.sortingOrder = 21;

            if (!hasColoredPencilScaleArt)
            {
                for (int tick = 0; tick <= 4; tick++)
                {
                    float x = Mathf.Lerp(-0.32f, 0.32f, tick / 4f);
                    AddDoodleLine($"Scale Gauge Tick {tick}", obj.transform, new[]
                    {
                        new Vector3(x, -0.35f, -0.05f), new Vector3(x, -0.23f, -0.05f)
                    }, new Color(0.12f, 0.11f, 0.09f, 0.7f), 0.012f, 23);
                }

                AddScaleFoot(obj.transform, -0.32f);
                AddScaleFoot(obj.transform, 0.32f);
            }

            GameObject textObject = new GameObject("Scale Meter Text");
            textObject.transform.SetParent(obj.transform, false);
            // TextMesh glyph metrics sit visually above their pivot. Lower the
            // pivot so the numbers are centered in the illustrated display window.
            textObject.transform.localPosition = new Vector3(0f, -0.02f, -0.055f);
            float meterScale = Mathf.Max(0.35f, Mathf.Min(
                Mathf.Max(0.2f, data.size.x) / 3f,
                Mathf.Max(0.2f, data.size.y) / 0.9f));
            textObject.transform.localScale = new Vector3(
                meterScale / Mathf.Max(0.2f, data.size.x),
                meterScale / Mathf.Max(0.2f, data.size.y),
                1f);
            TextMesh meterText = textObject.AddComponent<TextMesh>();
            meterText.text = $"0 / {Mathf.RoundToInt(data.actionStrength > 0f ? data.actionStrength : 300f)}";
            Font handwrittenFont = FindHandwrittenFont();
            if (handwrittenFont != null)
            {
                meterText.font = handwrittenFont;
            }
            meterText.fontSize = 42;
            meterText.characterSize = 0.08f;
            meterText.anchor = TextAnchor.MiddleCenter;
            meterText.alignment = TextAlignment.Center;
            meterText.color = new Color(0.08f, 0.07f, 0.05f, 1f);
            MeshRenderer textRenderer = textObject.GetComponent<MeshRenderer>();
            if (textRenderer != null)
            {
                if (handwrittenFont != null)
                {
                    textRenderer.sharedMaterial = handwrittenFont.material;
                }
                textRenderer.sortingOrder = 23;
            }

            InkWeightScale scale = obj.AddComponent<InkWeightScale>();
            scale.Configure(
                data.actionStrength > 0f ? data.actionStrength : 300f,
                meterText,
                hasColoredPencilScaleArt ? null : body,
                gaugeFillObject.transform,
                gaugeFill);

            AddEditorMetadata(obj, data);
            return obj;
        }

        private static void AddScaleFoot(Transform parent, float x)
        {
            GameObject footObject = new GameObject("Scale Foot");
            footObject.transform.SetParent(parent, false);
            footObject.transform.localPosition = new Vector3(x, -0.53f, -0.025f);
            footObject.transform.localScale = new Vector3(0.2f, 0.08f, 1f);
            SpriteRenderer foot = footObject.AddComponent<SpriteRenderer>();
            foot.sprite = GetSquareSprite();
            foot.color = new Color(0.16f, 0.14f, 0.1f, 1f);
            foot.sortingOrder = 18;
        }

        private GameObject CreateJumpPad(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = data.type.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.localScale = new Vector3(data.size.x, data.size.y, 1f);
            obj.layer = groundLayer;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            BoxCollider2D solid = obj.AddComponent<BoxCollider2D>();
            solid.size = new Vector2(0.85f, 0.16f);
            solid.offset = new Vector2(0f, -0.42f);

            GameObject trigger = new GameObject("Jump Trigger");
            trigger.transform.SetParent(obj.transform, false);
            trigger.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            BoxCollider2D triggerCollider = trigger.AddComponent<BoxCollider2D>();
            triggerCollider.isTrigger = true;
            triggerCollider.size = new Vector2(0.95f, 0.7f);
            JumpPad jumpPad = trigger.AddComponent<JumpPad>();
            jumpPad.Configure(obj.transform, data.actionStrength > 0f ? data.actionStrength : 27f);
            if (!string.IsNullOrEmpty(data.objectId)
                && data.objectId.StartsWith("6-1_", System.StringComparison.Ordinal))
            {
                // 6-1 already defines the launch velocity for the bird carrying
                // a teammate. The common x3 bird bonus made 40 become 120 and
                // launched the pair directly into the upper spikes.
                jumpPad.ConfigureBirdMultiplier(1f);
            }

            if (!AddResourceSprite(
                obj.transform,
                "StageObjects/NicoDraw/jump-pad",
                new Vector2(0.98f, 0.98f),
                24,
                "Colored Pencil Jump Pad"))
            {
            AddDoodleLine("Spring Left", obj.transform, new[]
            {
                new Vector3(-0.2f, -0.42f, -0.02f),
                new Vector3(-0.36f, -0.22f, -0.02f),
                new Vector3(-0.12f, -0.04f, -0.02f),
                new Vector3(-0.34f, 0.16f, -0.02f),
                new Vector3(-0.1f, 0.34f, -0.02f)
            }, Color.black, 0.028f, 20);
            AddDoodleLine("Spring Right", obj.transform, new[]
            {
                new Vector3(0.2f, -0.42f, -0.02f),
                new Vector3(0.36f, -0.22f, -0.02f),
                new Vector3(0.12f, -0.04f, -0.02f),
                new Vector3(0.34f, 0.16f, -0.02f),
                new Vector3(0.1f, 0.34f, -0.02f)
            }, Color.black, 0.028f, 20);
            AddDoodleLine("Spring Top", obj.transform, new[] { new Vector3(-0.46f, 0.4f, -0.02f), new Vector3(0.46f, 0.4f, -0.02f) }, Color.black, 0.055f, 21);
            AddDoodleLine("Spring Base", obj.transform, new[] { new Vector3(-0.42f, -0.44f, -0.02f), new Vector3(0.42f, -0.44f, -0.02f) }, Color.black, 0.055f, 21);
            }
            AddEditorMetadata(obj, data);
            return obj;
        }

        private GameObject CreateBackgroundDecoration(StageObjectData data, Transform parent)
        {
            GameObject root = new GameObject(data.objectId);
            root.name = data.type.ToString();
            root.transform.SetParent(parent, false);
            root.transform.position = data.position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            // Outdoor doodles left in older stage data fight the cave silhouette and
            // make the room read as a normal notebook stage. Keep their data intact
            // for editing/backward compatibility, but do not draw them in cave play.
            if (UsesCaveStageTheme(parent)
                && (data.type == StageObjectType.BackgroundCloud
                    || data.type == StageObjectType.BackgroundSun
                    || data.type == StageObjectType.BackgroundRainbow
                    || data.type == StageObjectType.BackgroundCastle))
            {
                AddEditorMetadata(root, data);
                return root;
            }

            // The generated night skyline owns the moon and ambient sky. Old
            // daytime suns left in authored stage data would otherwise appear
            // in front of the city and make the scene read as daytime.
            if (UsesNightCityStageTheme(parent)
                && data.type == StageObjectType.BackgroundSun)
            {
                AddEditorMetadata(root, data);
                return root;
            }

            if (UsesFactoryStageTheme(parent)
                && (data.type == StageObjectType.BackgroundCloud
                    || data.type == StageObjectType.BackgroundSun
                    || data.type == StageObjectType.BackgroundRainbow
                    || data.type == StageObjectType.BackgroundCastle
                    || data.type == StageObjectType.BackgroundTree
                    || data.type == StageObjectType.BackgroundGrass
                    || data.type == StageObjectType.BackgroundFlower
                    || data.type == StageObjectType.BackgroundBush
                    || data.type == StageObjectType.BackgroundMountain
                    || data.type == StageObjectType.BackgroundMushroom
                    || data.type == StageObjectType.BackgroundUfo
                    || data.type == StageObjectType.BackgroundHotAirBalloon
                    || data.type == StageObjectType.BackgroundHouse
                    || data.type == StageObjectType.BackgroundFossil))
            {
                AddEditorMetadata(root, data);
                return root;
            }

            // The generated space window owns the sky and celestial bodies.
            // Preserve legacy decoration data for editing, but avoid duplicate
            // moons/clouds or daytime scenery in normal play.
            if (UsesSpaceStageTheme(parent)
                && (data.type == StageObjectType.BackgroundTree
                    || data.type == StageObjectType.BackgroundGrass
                    || data.type == StageObjectType.BackgroundFlower
                    || data.type == StageObjectType.BackgroundBush
                    || data.type == StageObjectType.BackgroundCloud
                    || data.type == StageObjectType.BackgroundMoon
                    || data.type == StageObjectType.BackgroundSun
                    || data.type == StageObjectType.BackgroundRainbow
                    || data.type == StageObjectType.BackgroundMountain
                    || data.type == StageObjectType.BackgroundPlanet
                    || data.type == StageObjectType.BackgroundComet
                    || data.type == StageObjectType.BackgroundBlackHoleDecor
                    || data.type == StageObjectType.BackgroundRocket
                    || data.type == StageObjectType.BackgroundUfo
                    || data.type == StageObjectType.BackgroundAlien))
            {
                AddEditorMetadata(root, data);
                return root;
            }

            GameObject visual = new GameObject("Background Visual");
            visual.transform.SetParent(root.transform, false);
            bool useDistantNatureStyle = IsDistantNatureDecoration(data, parent);
            bool alignFloraToSurface = IsSurfaceAlignedFlora(data);
            float opacity = useDistantNatureStyle
                ? GetDistantTitleDecorationOpacity(data.type)
                : IsGeneratedNatureDecoration(data) ? 0.88f : 1f;
            if (UsesUnderwaterStageTheme(parent)
                && data.type == StageObjectType.BackgroundBubbles)
            {
                // 2-2 already contains several large legacy bubble cards. Keep
                // their authored placement, but make them a faint distant layer
                // behind the smaller animated bubbles generated by the theme.
                opacity = 0.12f;
            }
            int sortingOrder = useDistantNatureStyle ? -90 : -69;
            if (!TryApplyBackgroundSprite(
                visual,
                data.type,
                data.size,
                opacity,
                sortingOrder,
                alignFloraToSurface))
            {
                visual.transform.localScale = new Vector3(
                    Mathf.Max(0.2f, data.size.x),
                    Mathf.Max(0.2f, data.size.y),
                    1f);

                Color line = GetBackgroundDecorationColor(data.type);
                line.a *= opacity;
                DrawBackgroundDecoration(visual.transform, data.type, line);
            }

            bool needsEditorSelectionCollider = !IsGeneratedNatureDecoration(data)
                || (parent != null && parent.name == "RuntimeStageEditorRoot");
            if (needsEditorSelectionCollider)
            {
                BoxCollider2D selectionCollider = root.AddComponent<BoxCollider2D>();
                selectionCollider.size = new Vector2(Mathf.Max(0.2f, data.size.x), Mathf.Max(0.2f, data.size.y));
                selectionCollider.isTrigger = true;
            }
            AddEditorMetadata(root, data);
            return root;
        }

        private static bool TryApplyBackgroundSprite(
            GameObject visual,
            StageObjectType type,
            Vector2 requestedSize,
            float opacity,
            int sortingOrder,
            bool alignBottomToOrigin)
        {
            string resourcePath = GetCrayonDecorationResourcePath(type);
            Sprite sprite = string.IsNullOrEmpty(resourcePath) ? null : Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                switch (type)
                {
                    case StageObjectType.BackgroundTree:
                        resourcePath = "StageDecorations/tree-doodle";
                        break;
                    case StageObjectType.BackgroundGrass:
                        resourcePath = "StageDecorations/grass-doodle";
                        break;
                    case StageObjectType.BackgroundFlower:
                        resourcePath = "StageDecorations/flower-doodle";
                        break;
                    case StageObjectType.BackgroundBush:
                        resourcePath = "StageDecorations/bush-doodle";
                        break;
                    case StageObjectType.BackgroundCloud:
                        resourcePath = "StageDecorations/cloud-doodle";
                        break;
                    default:
                        resourcePath = null;
                        break;
                }

                sprite = string.IsNullOrEmpty(resourcePath) ? null : Resources.Load<Sprite>(resourcePath);
            }

            if (sprite == null)
            {
                return false;
            }

            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(opacity));
            renderer.sortingOrder = sortingOrder;

            Vector2 spriteSize = sprite.bounds.size;
            float fitScale = Mathf.Min(
                Mathf.Max(0.2f, requestedSize.x) / Mathf.Max(0.01f, spriteSize.x),
                Mathf.Max(0.2f, requestedSize.y) / Mathf.Max(0.01f, spriteSize.y));
            visual.transform.localScale = new Vector3(fitScale, fitScale, 1f);
            if (alignBottomToOrigin)
            {
                Vector2[] vertices = sprite.vertices;
                float lowestVisibleY = vertices.Length > 0 ? vertices[0].y : sprite.bounds.min.y;
                for (int i = 1; i < vertices.Length; i++)
                {
                    lowestVisibleY = Mathf.Min(lowestVisibleY, vertices[i].y);
                }
                visual.transform.localPosition = new Vector3(0f, -lowestVisibleY * fitScale, 0f);
            }
            return true;
        }

        private static float GetDistantTitleDecorationOpacity(StageObjectType type)
        {
            switch (type)
            {
                case StageObjectType.BackgroundTree:
                    return 0.38f;
                case StageObjectType.BackgroundMountain:
                    return 0.36f;
                default:
                    return 0.28f;
            }
        }

        private static bool IsGeneratedNatureDecoration(StageObjectData data)
        {
            return data != null
                && !string.IsNullOrEmpty(data.objectId)
                && (data.objectId.StartsWith("title-playground-", StringComparison.Ordinal)
                    || data.objectId.StartsWith("nature-", StringComparison.Ordinal));
        }

        private bool IsDistantNatureDecoration(StageObjectData data, Transform parent)
        {
            if (data == null || string.IsNullOrEmpty(data.objectId))
            {
                return false;
            }

            if (data.objectId.StartsWith("title-playground-distant-", StringComparison.Ordinal)
                || data.objectId.StartsWith("nature-distant-", StringComparison.Ordinal))
            {
                return true;
            }

            return UsesNatureStageTheme(parent)
                && (data.type == StageObjectType.BackgroundTree
                    || data.type == StageObjectType.BackgroundMountain
                    || data.type == StageObjectType.BackgroundCloud);
        }

        private static bool IsSurfaceAlignedFlora(StageObjectData data)
        {
            return data != null
                && !string.IsNullOrEmpty(data.objectId)
                && (data.objectId.StartsWith("title-playground-flora-", StringComparison.Ordinal)
                    || data.objectId.StartsWith("nature-flora-", StringComparison.Ordinal));
        }

        private static bool IsBackgroundDecorationType(StageObjectType type)
        {
            return type.ToString().StartsWith("Background", StringComparison.Ordinal);
        }

        private static string GetCrayonDecorationResourcePath(StageObjectType type)
        {
            if (!IsBackgroundDecorationType(type))
            {
                return null;
            }

            string name = type.ToString().Substring("Background".Length);
            StringBuilder slug = new StringBuilder(name.Length + 6);
            for (int i = 0; i < name.Length; i++)
            {
                char character = name[i];
                if (i > 0 && char.IsUpper(character))
                {
                    slug.Append('-');
                }

                slug.Append(char.ToLowerInvariant(character));
            }

            return "StageDecorations/CrayonSet/" + slug;
        }

        private static Color GetBackgroundDecorationColor(StageObjectType type)
        {
            switch (type)
            {
                case StageObjectType.BackgroundTree:
                case StageObjectType.BackgroundGrass:
                case StageObjectType.BackgroundBush:
                    return new Color(0.14f, 0.52f, 0.25f, 0.72f);
                case StageObjectType.BackgroundFlower:
                    return new Color(0.92f, 0.3f, 0.38f, 0.72f);
                case StageObjectType.BackgroundCloud:
                    return new Color(0.18f, 0.5f, 0.9f, 0.62f);
                default:
                    return new Color(0.55f, 0.32f, 0.74f, 0.72f);
            }
        }

        private static void AddBackgroundCrayonFill(Transform parent, StageObjectType type, Color color)
        {
            Color fill = new Color(color.r, color.g, color.b, 0.095f);
            float minY = type == StageObjectType.BackgroundGrass || type == StageObjectType.BackgroundBush ? -0.35f : -0.42f;
            float maxY = type == StageObjectType.BackgroundGrass || type == StageObjectType.BackgroundBush ? 0.2f : 0.42f;
            for (int i = 0; i < 7; i++)
            {
                float y = Mathf.Lerp(minY, maxY, i / 6f);
                float inset = Mathf.Abs(y) * 0.3f;
                AddDoodleLine(
                    "Decoration Crayon Fill",
                    parent,
                    new[]
                    {
                        new Vector3(-0.42f + inset, y, 0f),
                        new Vector3(0.42f - inset, y + Mathf.Sin(i * 1.8f) * 0.025f, 0f)
                    },
                    fill,
                    0.075f + (i % 2) * 0.025f,
                    -72);
            }
        }

        private static void DrawBackgroundDecoration(Transform parent, StageObjectType type, Color color)
        {
            const float width = 0.028f;
            switch (type)
            {
                case StageObjectType.BackgroundTree:
                    DrawDetailedTree(parent);
                    break;
                case StageObjectType.BackgroundGrass:
                    DrawDetailedGrass(parent, Vector2.zero, 1f);
                    break;
                case StageObjectType.BackgroundFlower:
                    DrawDetailedFlower(parent);
                    break;
                case StageObjectType.BackgroundBush:
                    DrawDetailedBush(parent);
                    break;
                case StageObjectType.BackgroundCloud:
                    DrawDetailedCloud(parent);
                    break;
                case StageObjectType.BackgroundPush:
                    AddBackgroundCrayonFill(parent, type, color);
                    DrawBackgroundWord(parent, "PUSH", color);
                    break;
                case StageObjectType.BackgroundArrow:
                    AddBackgroundCrayonFill(parent, type, color);
                    AddDoodleLine("Arrow", parent, new[] { new Vector3(-0.48f, 0f), new Vector3(0.42f, 0f), new Vector3(0.15f, 0.25f), new Vector3(0.42f, 0f), new Vector3(0.15f, -0.25f) }, color, width * 1.3f, -69);
                    break;
            }
        }

        private static void DrawDetailedTree(Transform parent)
        {
            Color leaf = new Color(0.24f, 0.56f, 0.25f, 0.76f);
            Color trunk = new Color(0.48f, 0.29f, 0.15f, 0.8f);
            Vector2[] centers =
            {
                new Vector2(-0.22f, 0.2f), new Vector2(0f, 0.31f), new Vector2(0.23f, 0.18f),
                new Vector2(-0.34f, 0.02f), new Vector2(0.34f, 0.01f), new Vector2(0f, 0.05f)
            };
            float[] radii = { 0.24f, 0.28f, 0.25f, 0.2f, 0.2f, 0.3f };
            for (int i = 0; i < centers.Length; i++)
            {
                AddCrayonBlob(parent, centers[i], radii[i], leaf, i);
            }

            for (int i = 0; i < 6; i++)
            {
                float x = Mathf.Lerp(-0.095f, 0.095f, i / 5f);
                AddDoodleLine(
                    "Tree Trunk Crayon",
                    parent,
                    new[]
                    {
                        new Vector3(x, -0.46f),
                        new Vector3(x * 0.45f + Mathf.Sin(i * 1.7f) * 0.02f, 0.13f)
                    },
                    new Color(trunk.r, trunk.g, trunk.b, 0.16f),
                    0.055f,
                    -71);
            }

            AddDoodleLine("Tree Trunk Outline", parent, new[] { new Vector3(-0.11f, -0.46f), new Vector3(-0.06f, 0.15f), new Vector3(0.07f, 0.15f), new Vector3(0.12f, -0.46f) }, trunk, 0.025f, -68);
            AddDoodleLine("Tree Branch", parent, new[] { new Vector3(-0.03f, -0.02f), new Vector3(-0.23f, 0.2f), new Vector3(-0.34f, 0.27f) }, trunk, 0.022f, -68);
            AddDoodleLine("Tree Branch", parent, new[] { new Vector3(0.04f, 0.03f), new Vector3(0.25f, 0.23f), new Vector3(0.35f, 0.29f) }, trunk, 0.022f, -68);

            DrawTreeCanopyOutline(parent, leaf);
            AddDoodleLine("Leaf Pencil Detail", parent, new[]
            {
                new Vector3(-0.32f, 0.11f), new Vector3(-0.19f, 0.18f), new Vector3(-0.09f, 0.13f)
            }, new Color(leaf.r, leaf.g, leaf.b, 0.38f), 0.014f, -68);
            AddDoodleLine("Leaf Pencil Detail", parent, new[]
            {
                new Vector3(0.08f, 0.31f), new Vector3(0.19f, 0.22f), new Vector3(0.34f, 0.27f)
            }, new Color(leaf.r, leaf.g, leaf.b, 0.38f), 0.014f, -68);
            AddDoodleLine("Leaf Pencil Detail", parent, new[]
            {
                new Vector3(-0.16f, -0.02f), new Vector3(-0.03f, 0.05f), new Vector3(0.12f, -0.01f)
            }, new Color(leaf.r, leaf.g, leaf.b, 0.34f), 0.014f, -68);

            AddDoodleLine("Tree Roots", parent, new[] { new Vector3(-0.22f, -0.48f), new Vector3(-0.03f, -0.43f), new Vector3(0.03f, -0.43f), new Vector3(0.23f, -0.48f) }, trunk, 0.022f, -68);
            DrawDetailedGrass(parent, new Vector2(0.3f, -0.33f), 0.38f);
        }

        private static void DrawTreeCanopyOutline(Transform parent, Color color)
        {
            Vector3[] points = new Vector3[65];
            Vector2 center = new Vector2(0f, 0.1f);
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / (points.Length - 1);
                float lobes =
                    1f +
                    Mathf.Sin(angle * 7f + 0.4f) * 0.075f +
                    Mathf.Sin(angle * 11f - 0.7f) * 0.045f +
                    Mathf.Cos(angle * 3f + 0.2f) * 0.035f;
                points[i] = center + new Vector2(
                    Mathf.Cos(angle) * 0.47f * lobes,
                    Mathf.Sin(angle) * 0.43f * lobes);
            }

            AddDoodleLine("Tree Canopy Outline", parent, points, color, 0.025f, -68);
        }

        private static void DrawDetailedGrass(Transform parent, Vector2 offset, float scale)
        {
            Color grass = new Color(0.2f, 0.58f, 0.24f, 0.76f);
            for (int i = -6; i <= 6; i++)
            {
                float t = (i + 6) / 12f;
                float x = Mathf.Lerp(-0.48f, 0.48f, t) * scale + offset.x;
                float height = (0.28f + Mathf.Abs(Mathf.Sin(i * 1.73f)) * 0.2f) * scale;
                float lean = Mathf.Sin(i * 2.21f) * 0.12f * scale;
                Vector3[] blade =
                {
                    new Vector3(x, offset.y - 0.14f * scale),
                    new Vector3(x + lean * 0.35f, offset.y + height * 0.45f),
                    new Vector3(x + lean, offset.y + height)
                };
                AddDoodleLine("Grass Crayon", parent, blade, new Color(grass.r, grass.g, grass.b, 0.13f), 0.055f * scale, -71);
                AddDoodleLine("Grass Blade", parent, blade, grass, 0.018f * scale, -68);
            }
            AddDoodleLine("Grass Ground", parent, new[] { new Vector3(offset.x - 0.5f * scale, offset.y - 0.15f * scale), new Vector3(offset.x + 0.5f * scale, offset.y - 0.15f * scale) }, new Color(grass.r, grass.g, grass.b, 0.38f), 0.018f, -69);
        }

        private static void DrawDetailedFlower(Transform parent)
        {
            Color petal = new Color(0.94f, 0.32f, 0.43f, 0.76f);
            Color center = new Color(0.96f, 0.68f, 0.12f, 0.82f);
            Color stem = new Color(0.18f, 0.55f, 0.25f, 0.78f);
            for (int i = 0; i < 7; i++)
            {
                float angle = i * Mathf.PI * 2f / 7f;
                Vector2 position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.21f + new Vector2(0f, 0.18f);
                AddCrayonBlob(parent, position, 0.14f, petal, i);
                BackgroundWobblyCircle(parent, position, 0.14f, petal, 0.02f, i);
            }
            AddCrayonBlob(parent, new Vector2(0f, 0.18f), 0.12f, center, 9);
            BackgroundWobblyCircle(parent, new Vector2(0f, 0.18f), 0.12f, center, 0.022f, 9);
            AddDoodleLine("Flower Stem", parent, new[] { new Vector3(0f, 0.08f), new Vector3(0.01f, -0.48f) }, stem, 0.025f, -68);
            AddDoodleLine("Flower Leaf", parent, new[] { new Vector3(0f, -0.17f), new Vector3(-0.25f, -0.3f), new Vector3(-0.02f, -0.35f) }, stem, 0.022f, -68);
        }

        private static void DrawDetailedBush(Transform parent)
        {
            Color bush = new Color(0.18f, 0.55f, 0.25f, 0.74f);
            Vector2[] centers =
            {
                new Vector2(-0.33f, -0.05f), new Vector2(-0.13f, 0.12f),
                new Vector2(0.1f, 0.15f), new Vector2(0.33f, -0.04f), new Vector2(0f, -0.1f)
            };
            for (int i = 0; i < centers.Length; i++)
            {
                AddCrayonBlob(parent, centers[i], 0.25f, bush, i);
                BackgroundWobblyCircle(parent, centers[i], 0.25f, bush, 0.022f, i);
            }
            AddDoodleLine("Bush Ground", parent, new[] { new Vector3(-0.48f, -0.3f), new Vector3(0.48f, -0.3f) }, bush, 0.022f, -68);
        }

        private static void DrawDetailedCloud(Transform parent)
        {
            Color cloud = new Color(0.22f, 0.56f, 0.92f, 0.66f);
            Vector2[] centers =
            {
                new Vector2(-0.3f, -0.03f), new Vector2(-0.08f, 0.16f),
                new Vector2(0.18f, 0.18f), new Vector2(0.34f, -0.02f), new Vector2(0f, -0.08f)
            };
            for (int i = 0; i < centers.Length; i++)
            {
                AddCrayonBlob(parent, centers[i], 0.24f, cloud, i);
                BackgroundWobblyCircle(parent, centers[i], 0.24f, cloud, 0.021f, i);
            }
            AddDoodleLine("Cloud Base", parent, new[] { new Vector3(-0.46f, -0.25f), new Vector3(0.48f, -0.25f) }, cloud, 0.022f, -68);
        }

        private static void AddCrayonBlob(Transform parent, Vector2 center, float radius, Color color, int seed)
        {
            for (int row = -3; row <= 3; row++)
            {
                float normalized = row / 3.5f;
                float halfWidth = Mathf.Sqrt(Mathf.Max(0f, 1f - normalized * normalized)) * radius;
                float y = center.y + normalized * radius + Mathf.Sin(seed * 1.7f + row) * 0.008f;
                AddDoodleLine(
                    "Organic Crayon Fill",
                    parent,
                    new[] { new Vector3(center.x - halfWidth, y), new Vector3(center.x + halfWidth, y + Mathf.Cos(seed + row) * 0.008f) },
                    new Color(color.r, color.g, color.b, 0.13f),
                    0.055f,
                    -71);
            }
        }

        private static void BackgroundWobblyCircle(Transform parent, Vector2 center, float radius, Color color, float width, int seed)
        {
            Vector3[] points = new Vector3[33];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / (points.Length - 1);
                float wobble = 1f + Mathf.Sin(i * 2.13f + seed * 1.77f) * 0.055f + Mathf.Cos(i * 1.17f + seed) * 0.025f;
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * wobble;
            }
            AddDoodleLine("Organic Pencil Outline", parent, points, color, width, -68);
        }

        private static void DrawBackgroundWord(Transform parent, string word, Color color)
        {
            float advance = 0.24f;
            float start = -(word.Length - 1) * advance * 0.5f;
            for (int i = 0; i < word.Length; i++)
            {
                DrawBackgroundLetter(parent, word[i], start + i * advance, color);
            }
        }

        private static void DrawBackgroundLetter(Transform parent, char letter, float x, Color color)
        {
            const float w = 0.025f;
            Vector3 P(float px, float py) => new Vector3(x + px * 0.17f, py * 0.42f, 0f);
            switch (letter)
            {
                case 'P':
                    AddDoodleLine("Letter P", parent, new[] { P(-0.45f, -0.75f), P(-0.45f, 0.75f), P(0.35f, 0.75f), P(0.48f, 0.15f), P(-0.42f, 0.15f) }, color, w, -69);
                    break;
                case 'U':
                    AddDoodleLine("Letter U", parent, new[] { P(-0.45f, 0.75f), P(-0.45f, -0.55f), P(0f, -0.78f), P(0.45f, -0.55f), P(0.45f, 0.75f) }, color, w, -69);
                    break;
                case 'S':
                    AddDoodleLine("Letter S", parent, new[] { P(0.45f, 0.65f), P(0f, 0.8f), P(-0.45f, 0.45f), P(0.35f, -0.05f), P(0.45f, -0.55f), P(0f, -0.8f), P(-0.45f, -0.62f) }, color, w, -69);
                    break;
                case 'H':
                    AddDoodleLine("Letter H", parent, new[] { P(-0.45f, -0.75f), P(-0.45f, 0.75f) }, color, w, -69);
                    AddDoodleLine("Letter H", parent, new[] { P(0.45f, -0.75f), P(0.45f, 0.75f) }, color, w, -69);
                    AddDoodleLine("Letter H", parent, new[] { P(-0.45f, 0f), P(0.45f, 0f) }, color, w, -69);
                    break;
            }
        }

        private static void BackgroundCircle(Transform parent, Vector2 center, float radius, Color color, float width)
        {
            Vector3[] points = new Vector3[25];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / (points.Length - 1);
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            AddDoodleLine("Decoration Circle", parent, points, color, width, -69);
        }

        private GameObject CreateButtonSwitch(StageObjectData data, Transform parent)
        {
            GameObject root = new GameObject(data.objectId);
            root.name = data.type.ToString();
            root.transform.SetParent(parent, false);
            root.transform.position = data.position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            Vector2 baseSize = new Vector2(data.size.x, data.size.y * 0.34f);
            GameObject baseBox = CreateBox("Button Base", Vector2.zero, baseSize, new Color(0.08f, 0.08f, 0.08f, 0.04f), root.transform);
            baseBox.transform.localPosition = new Vector3(0f, -data.size.y * 0.16f, 0f);
            AddPencilFillLocal(baseBox.transform, baseSize, new Color(0.08f, 0.08f, 0.08f));
            AddSketchBoxOutline(baseBox.transform, baseSize, Color.black, 0.045f);

            bool simultaneous = data.type == StageObjectType.SimultaneousButton;
            bool hold = data.type == StageObjectType.HoldButton;
            bool escortFriend = data.type == StageObjectType.EscortFriendButton;
            Color capColor = escortFriend
                ? new Color(0.16f, 0.78f, 0.9f)
                : simultaneous
                ? new Color(0.1f, 0.48f, 0.95f)
                : hold
                    ? new Color(0.95f, 0.62f, 0.08f)
                    : new Color(0.85f, 0.08f, 0.05f);
            Vector2 capSize = new Vector2(data.size.x * 0.72f, data.size.y * 0.28f);
            GameObject cap = CreateBox(
                "Button Cap",
                Vector2.zero,
                capSize,
                new Color(capColor.r, capColor.g, capColor.b, 0.2f),
                root.transform);
            cap.transform.localPosition = new Vector3(0f, data.size.y * 0.12f, -0.02f);
            AddPencilFillLocal(cap.transform, capSize, capColor);
            AddSketchBoxOutline(cap.transform, capSize, Color.black, 0.04f);

            BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = data.size;
            AddEditorMetadata(root, data);
            return root;
        }

        private static void AddBalanceScaleStopper(Transform beam, float localX)
        {
            GameObject stopper = new GameObject(localX < 0f ? "Left Stopper" : "Right Stopper");
            stopper.transform.SetParent(beam, false);
            stopper.transform.localPosition = new Vector3(localX, 1.08f, -0.01f);
            stopper.transform.localRotation = Quaternion.identity;

            BoxCollider2D collider = stopper.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.04f, 0.85f);
            collider.offset = Vector2.zero;

            AddDoodleLine(
                stopper.name + " Front",
                stopper.transform,
                new[] { new Vector3(-0.02f, -0.42f, 0f), new Vector3(-0.02f, 0.42f, 0f) },
                Color.black,
                0.05f,
                16);
            AddDoodleLine(
                stopper.name + " Back",
                stopper.transform,
                new[] { new Vector3(0.02f, -0.4f, 0f), new Vector3(0.02f, 0.4f, 0f) },
                new Color(0.12f, 0.12f, 0.12f, 0.8f),
                0.026f,
                17);
        }

        private GameObject CreateGoal(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = "Goal";
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.localScale = new Vector3(data.size.x, data.size.y, 1f);
            obj.layer = goalLayer;
            obj.tag = "Goal";
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            BoxCollider2D collider = obj.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(2.2f, 0.86f);
            collider.offset = new Vector2(0f, -0.02f);
            collider.isTrigger = true;
            obj.AddComponent<Goal>();

            if (AddResourceSprite(obj.transform, "StageObjects/NicoDraw/goal-door", new Vector2(2.3f, 0.92f), 20, "Colored Pencil Exit Door"))
            {
                AddEditorMetadata(obj, data);
                return obj;
            }

            GameObject beam = new GameObject("Goal Beam");
            beam.transform.SetParent(obj.transform, false);
            beam.transform.localPosition = new Vector3(0f, -0.08f, -0.02f);
            LineRenderer beamLine = beam.AddComponent<LineRenderer>();
            beamLine.useWorldSpace = false;
            beamLine.positionCount = 4;
            beamLine.loop = true;
            beamLine.SetPositions(new[]
            {
                new Vector3(-0.28f, 0.28f, 0f),
                new Vector3(0.28f, 0.28f, 0f),
                new Vector3(0.5f, -0.48f, 0f),
                new Vector3(-0.5f, -0.48f, 0f)
            });
            beamLine.startWidth = 0.028f;
            beamLine.endWidth = 0.028f;
            beamLine.material = GetLineMaterial();
            beamLine.startColor = new Color(0.2f, 0.75f, 1f, 0.55f);
            beamLine.endColor = new Color(0.2f, 0.75f, 1f, 0.55f);
            beamLine.sortingOrder = 16;

            GameObject ufo = new GameObject("UFO");
            ufo.transform.SetParent(obj.transform, false);
            ufo.transform.localPosition = new Vector3(0f, 0.46f, -0.03f);
            ufo.AddComponent<UfoGoalVisual>();
            AddDoodleLine("UFO Body", ufo.transform, new[]
            {
                new Vector3(-0.34f, 0f, 0f),
                new Vector3(-0.18f, 0.12f, 0f),
                new Vector3(0.18f, 0.12f, 0f),
                new Vector3(0.34f, 0f, 0f),
                new Vector3(0.12f, -0.09f, 0f),
                new Vector3(-0.12f, -0.09f, 0f),
                new Vector3(-0.34f, 0f, 0f)
            }, Color.black, 0.04f, 20);
            AddDoodleLine("UFO Dome", ufo.transform, new[]
            {
                new Vector3(-0.12f, 0.1f, 0f),
                new Vector3(-0.04f, 0.22f, 0f),
                new Vector3(0.08f, 0.22f, 0f),
                new Vector3(0.16f, 0.1f, 0f)
            }, new Color(0.1f, 0.45f, 1f), 0.032f, 21);
            AddDoodleLine("Beam Rays", obj.transform, new[]
            {
                new Vector3(-0.18f, 0.22f, -0.02f),
                new Vector3(-0.42f, -0.45f, -0.02f),
                new Vector3(0f, 0.2f, -0.02f),
                new Vector3(0.02f, -0.5f, -0.02f),
                new Vector3(0.18f, 0.22f, -0.02f),
                new Vector3(0.42f, -0.45f, -0.02f)
            }, new Color(0.2f, 0.75f, 1f, 0.42f), 0.022f, 17);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private GameObject CreateMarker(StageObjectData data, Transform parent, Color color, string label)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = data.type.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            bool showMarkerVisual = parent != null && parent.name == "RuntimeStageEditorRoot";
            if (showMarkerVisual && AddResourceSprite(obj.transform, "StageObjects/NicoDraw/start-flag",
                new Vector2(1.18f, 1.22f), 6, "Colored Pencil Start Flag", new Vector2(0f, 0.32f)))
            {
                Transform flagVisual = obj.transform.Find("Colored Pencil Start Flag");
                if (flagVisual != null)
                {
                    flagVisual.gameObject.AddComponent<StartFlagWave>();
                }
            }
            else if (showMarkerVisual)
            {
                AddDoodleCircleAt(obj.transform, Vector2.zero, 0.28f, color, 0.045f, 6);
                AddDoodleLine("Flag Pole", obj.transform, new[] { new Vector3(0.18f, -0.25f, 0f), new Vector3(0.18f, 0.36f, 0f) }, color, 0.04f, 6);
                AddDoodleLine("Flag", obj.transform, new[] { new Vector3(0.18f, 0.32f, 0f), new Vector3(0.52f, 0.22f, 0f), new Vector3(0.18f, 0.12f, 0f) }, color, 0.04f, 6);
            }

            CircleCollider2D collider = obj.AddComponent<CircleCollider2D>();
            collider.radius = 0.42f;
            collider.isTrigger = true;

            AddEditorMetadata(obj, data);
            return obj;
        }

        private GameObject CreateCollectible(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = data.type.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            float scale = Mathf.Clamp(Mathf.Min(data.size.x, data.size.y), 0.4f, 2f);
            obj.transform.localScale = Vector3.one * scale;

            Color color = data.type == StageObjectType.CollectibleFish
                ? new Color(0.12f, 0.58f, 0.9f, 1f)
                : data.type == StageObjectType.CollectibleCoin
                    ? new Color(1f, 0.68f, 0.08f, 1f)
                    : new Color(1f, 0.38f, 0.2f, 1f);
            if (data.type == StageObjectType.CollectibleFish)
            {
                DrawCollectibleFish(obj.transform);
            }
            else if (data.type == StageObjectType.CollectibleCoin)
            {
                if (!AddResourceSprite(obj.transform, "StageObjects/NicoDraw/coin",
                    Vector2.one * 0.78f, 24, "Colored Pencil Coin"))
                {
                    AddDoodleCircle(obj.transform, 0.38f, color, 0.07f);
                    AddDoodleCircle(obj.transform, 0.24f, color, 0.04f);
                }
            }
            else
            {
                Vector3[] points = new Vector3[11];
                for (int i = 0; i < 10; i++)
                {
                    float angle = (90f + i * 36f) * Mathf.Deg2Rad;
                    float radius = i % 2 == 0 ? 0.42f : 0.19f;
                    points[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                }
                points[10] = points[0];
                AddDoodleLine("Star", obj.transform, points, color, 0.06f, 20);
            }

            CircleCollider2D trigger = obj.AddComponent<CircleCollider2D>();
            trigger.radius = 0.48f;
            trigger.isTrigger = true;
            StageCollectible collectible = obj.AddComponent<StageCollectible>();
            collectible.Configure(data.objectId, data.type);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private void DrawCollectibleFish(Transform parent)
        {
            if (AddResourceSprite(
                parent,
                "StageObjects/NicoDraw/fish",
                new Vector2(1.04f, 0.59f),
                24,
                "Colored Pencil Fish"))
            {
                return;
            }

            Color bodyBlue = new Color(0.18f, 0.66f, 0.95f, 1f);
            Color tailBlue = new Color(0.08f, 0.47f, 0.86f, 1f);
            Color outlineBlue = new Color(0.025f, 0.23f, 0.55f, 1f);

            CreateColoredTriangle(
                parent,
                "Fish Tail Fill",
                new Vector3(-0.28f, 0f, -0.02f),
                new Vector3(-0.62f, 0.29f, -0.02f),
                new Vector3(-0.59f, -0.29f, -0.02f),
                tailBlue,
                18);
            AddDoodleLine("Fish Tail Outline", parent, new[]
            {
                new Vector3(-0.28f, 0f, -0.03f),
                new Vector3(-0.62f, 0.29f, -0.03f),
                new Vector3(-0.59f, -0.29f, -0.03f),
                new Vector3(-0.28f, 0f, -0.03f)
            }, outlineBlue, 0.045f, 21);

            GameObject body = new GameObject("Fish Blue Body");
            body.transform.SetParent(parent, false);
            body.transform.localPosition = new Vector3(0.03f, 0f, -0.04f);
            body.transform.localScale = new Vector3(0.78f, 0.5f, 1f);
            SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = GetCircleSprite();
            bodyRenderer.color = bodyBlue;
            bodyRenderer.sortingOrder = 19;
            AddDoodleCircle(body.transform, 0.5f, outlineBlue, 0.055f);

            CreateColoredTriangle(
                parent,
                "Fish Fin Fill",
                new Vector3(-0.02f, -0.02f, -0.06f),
                new Vector3(-0.16f, -0.31f, -0.06f),
                new Vector3(0.18f, -0.17f, -0.06f),
                new Color(0.06f, 0.42f, 0.8f, 0.95f),
                22);
            AddDoodleLine("Fish Fin Outline", parent, new[]
            {
                new Vector3(-0.02f, -0.02f, -0.07f),
                new Vector3(-0.16f, -0.31f, -0.07f),
                new Vector3(0.18f, -0.17f, -0.07f)
            }, outlineBlue, 0.032f, 23);

            CreateFishEye(parent, new Vector2(0.25f, 0.09f));
            AddDoodleLine("Fish Gill", parent, new[]
            {
                new Vector3(0.1f, 0.18f, -0.08f),
                new Vector3(0.05f, 0f, -0.08f),
                new Vector3(0.1f, -0.18f, -0.08f)
            }, new Color(0.04f, 0.38f, 0.7f, 0.9f), 0.025f, 24);
            AddDoodleLine("Fish Mouth", parent, new[]
            {
                new Vector3(0.39f, -0.05f, -0.08f),
                new Vector3(0.31f, -0.09f, -0.08f)
            }, outlineBlue, 0.026f, 24);
            AddDoodleLine("Fish Highlight", parent, new[]
            {
                new Vector3(-0.1f, 0.16f, -0.08f),
                new Vector3(0.08f, 0.2f, -0.08f)
            }, new Color(0.72f, 0.92f, 1f, 0.9f), 0.03f, 24);
        }

        private static void CreateFishEye(Transform parent, Vector2 position)
        {
            GameObject white = new GameObject("Fish Eye White");
            white.transform.SetParent(parent, false);
            white.transform.localPosition = new Vector3(position.x, position.y, -0.08f);
            white.transform.localScale = Vector3.one * 0.13f;
            SpriteRenderer whiteRenderer = white.AddComponent<SpriteRenderer>();
            whiteRenderer.sprite = GetCircleSprite();
            whiteRenderer.color = Color.white;
            whiteRenderer.sortingOrder = 24;

            GameObject pupil = new GameObject("Fish Eye Pupil");
            pupil.transform.SetParent(parent, false);
            pupil.transform.localPosition = new Vector3(position.x + 0.018f, position.y, -0.09f);
            pupil.transform.localScale = Vector3.one * 0.055f;
            SpriteRenderer pupilRenderer = pupil.AddComponent<SpriteRenderer>();
            pupilRenderer.sprite = GetCircleSprite();
            pupilRenderer.color = new Color(0.015f, 0.08f, 0.18f, 1f);
            pupilRenderer.sortingOrder = 25;
        }

        private void CreateColoredTriangle(
            Transform parent,
            string objectName,
            Vector3 first,
            Vector3 second,
            Vector3 third,
            Color color,
            int sortingOrder)
        {
            GameObject visual = new GameObject(objectName);
            visual.transform.SetParent(parent, false);
            Mesh mesh = new Mesh
            {
                name = objectName + " Mesh",
                vertices = new[] { first, second, third },
                triangles = new[] { 0, 1, 2 },
                colors = new[] { color, color, color }
            };
            mesh.RecalculateBounds();
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetLineMaterial();
            renderer.sortingOrder = sortingOrder;
        }

        private GameObject CreateChallengeClock(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = StageObjectType.ChallengeClock.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            DoodleMonitorVisuals.KeepBehindPlayers(obj.transform);

            Vector2 size = new Vector2(Mathf.Max(1.4f, data.size.x), Mathf.Max(0.65f, data.size.y));
            data.size = size;
            float clockScale = Mathf.Max(0.35f, Mathf.Min(size.x / 3.2f, size.y / 1.25f));
            Color graphite = new Color(0.16f, 0.2f, 0.26f, 0.94f);
            Color caseBlue = new Color(0.34f, 0.66f, 0.88f, 0.74f);
            Color screenPaper = new Color(0.91f, 0.97f, 0.91f, 0.98f);
            CreateClockPanelLayer(
                obj.transform,
                "Crayon Monitor Paper Case",
                size,
                new Color(0.98f, 0.95f, 0.82f, 0.98f),
                -24,
                new Vector3(0f, 0f, -0.02f));
            CreateClockPanelLayer(
                obj.transform,
                "Blue Pencil Case Wash",
                new Vector2(size.x - 0.12f * clockScale, size.y - 0.12f * clockScale),
                caseBlue,
                -23,
                new Vector3(0f, 0f, -0.03f));
            Vector2 screenSize = new Vector2(
                Mathf.Max(0.5f, size.x - 0.34f * clockScale),
                Mathf.Max(0.3f, size.y * 0.64f));
            Vector3 screenPosition = new Vector3(0f, -size.y * 0.075f, -0.04f);
            CreateClockPanelLayer(
                obj.transform,
                "Pale Paper Screen",
                screenSize,
                screenPaper,
                -21,
                screenPosition);
            AddClockPencilHatching(obj.transform, size, caseBlue, -22);
            AddDoodleLine("Timer Outer Outline", obj.transform, new[]
            {
                new Vector3(-size.x * 0.5f - 0.02f * clockScale, -size.y * 0.47f, -0.05f),
                new Vector3(size.x * 0.49f, -size.y * 0.5f, -0.05f),
                new Vector3(size.x * 0.5f + 0.015f * clockScale, size.y * 0.47f, -0.05f),
                new Vector3(-size.x * 0.47f, size.y * 0.5f, -0.05f),
                new Vector3(-size.x * 0.5f - 0.02f * clockScale, -size.y * 0.47f, -0.05f)
            }, graphite, 0.045f * clockScale, -18);
            AddDoodleLine("Timer Loose Blue Outline", obj.transform, new[]
            {
                new Vector3(-size.x * 0.48f, -size.y * 0.5f, -0.055f),
                new Vector3(size.x * 0.5f + 0.02f * clockScale, -size.y * 0.46f, -0.055f),
                new Vector3(size.x * 0.47f, size.y * 0.5f + 0.02f * clockScale, -0.055f),
                new Vector3(-size.x * 0.5f, size.y * 0.46f, -0.055f)
            }, new Color(0.1f, 0.42f, 0.72f, 0.7f), 0.025f * clockScale, -17);
            AddDoodleLine("Crooked Screen Outline", obj.transform, new[]
            {
                screenPosition + new Vector3(-screenSize.x * 0.5f, -screenSize.y * 0.48f, -0.02f),
                screenPosition + new Vector3(screenSize.x * 0.49f, -screenSize.y * 0.5f, -0.02f),
                screenPosition + new Vector3(screenSize.x * 0.5f, screenSize.y * 0.47f, -0.02f),
                screenPosition + new Vector3(-screenSize.x * 0.48f, screenSize.y * 0.5f, -0.02f),
                screenPosition + new Vector3(-screenSize.x * 0.5f, -screenSize.y * 0.48f, -0.02f)
            }, graphite, 0.028f * clockScale, -17);

            // Uneven antennae and feet make the silhouette immediately read as a
            // child's drawing of a monitor, even when the clock is very small.
            AddDoodleLine("Left Crayon Antenna", obj.transform, new[]
            {
                new Vector3(-0.1f * clockScale, size.y * 0.5f, -0.055f),
                new Vector3(-0.34f * clockScale, size.y * 0.5f + 0.25f * clockScale, -0.055f)
            }, graphite, 0.035f * clockScale, -17);
            AddDoodleLine("Right Crayon Antenna", obj.transform, new[]
            {
                new Vector3(0.08f * clockScale, size.y * 0.5f, -0.055f),
                new Vector3(0.39f * clockScale, size.y * 0.5f + 0.2f * clockScale, -0.055f)
            }, graphite, 0.035f * clockScale, -17);
            AddDoodleLine("Monitor Feet", obj.transform, new[]
            {
                new Vector3(-size.x * 0.28f, -size.y * 0.49f, -0.055f),
                new Vector3(-size.x * 0.32f, -size.y * 0.5f - 0.13f * clockScale, -0.055f),
                new Vector3(-size.x * 0.18f, -size.y * 0.5f - 0.13f * clockScale, -0.055f)
            }, graphite, 0.04f * clockScale, -17);
            AddDoodleLine("Monitor Right Foot", obj.transform, new[]
            {
                new Vector3(size.x * 0.27f, -size.y * 0.49f, -0.055f),
                new Vector3(size.x * 0.31f, -size.y * 0.5f - 0.12f * clockScale, -0.055f),
                new Vector3(size.x * 0.17f, -size.y * 0.5f - 0.12f * clockScale, -0.055f)
            }, graphite, 0.04f * clockScale, -17);

            GameObject statusLed = new GameObject("Timer Status LED");
            statusLed.transform.SetParent(obj.transform, false);
            statusLed.transform.localPosition = new Vector3(-size.x * 0.39f, size.y * 0.31f, -0.06f);
            statusLed.transform.localScale = Vector3.one * (0.14f * clockScale);
            SpriteRenderer ledRenderer = statusLed.AddComponent<SpriteRenderer>();
            ledRenderer.sprite = GetCircleSprite();
            ledRenderer.color = new Color(1f, 0.24f, 0.1f, 1f);
            ledRenderer.sortingOrder = -19;

            BoxCollider2D selection = obj.AddComponent<BoxCollider2D>();
            selection.size = size;
            selection.isTrigger = true;

            Font font = GetMonitorFont();
            bool compactClock = size.y < 1.45f;
            float characterSize = (compactClock ? 0.067f : 0.075f) * clockScale;
            float digitsY = compactClock ? size.y * 0.075f : -size.y * 0.075f;
            float progressY = compactClock ? -size.y * 0.275f : -size.y * 0.32f;
            if (!compactClock && (string.IsNullOrEmpty(data.objectId)
                || !data.objectId.StartsWith("9-2_", System.StringComparison.Ordinal)))
            {
                CreateClockText(
                    obj.transform,
                    "Timer Label",
                    font,
                    0.034f * clockScale,
                    new Color(0.08f, 0.22f, 0.38f, 0.95f),
                    -20,
                    new Vector3(0f, size.y * 0.3f, -0.065f),
                    "TIME");
            }
            TextMesh shadow = CreateClockText(
                obj.transform,
                "Clock Shadow",
                font,
                characterSize,
                new Color(0.18f, 0.27f, 0.3f, 0.28f),
                -20,
                new Vector3(0.02f * clockScale, digitsY - 0.02f * clockScale, -0.07f),
                "01:00.0");
            TextMesh digits = CreateClockText(
                obj.transform,
                "Clock Digits",
                font,
                characterSize,
                new Color(0.04f, 0.43f, 0.58f, 1f),
                -19,
                new Vector3(0f, digitsY, -0.08f),
                "01:00.0");
            TextMesh progress = CreateClockText(
                obj.transform,
                "Clock Fish Progress",
                font,
                (compactClock ? 0.024f : 0.029f) * clockScale,
                new Color(0.1f, 0.28f, 0.5f, 1f),
                -19,
                new Vector3(0f, progressY, -0.08f),
                "FISH  0 / 0");

            StageChallengeClock clock = obj.AddComponent<StageChallengeClock>();
            clock.Configure(digits, shadow, progress, ledRenderer, 60f);
            obj.AddComponent<DoodleMonitorTextReadability>().Configure(size);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private static TextMesh CreateClockText(
            Transform parent,
            string objectName,
            Font font,
            float characterSize,
            Color color,
            int sortingOrder,
            Vector3 localPosition)
        {
            return CreateClockText(parent, objectName, font, characterSize, color, sortingOrder, localPosition, "01:00.0");
        }

        private static TextMesh CreateClockText(
            Transform parent,
            string objectName,
            Font font,
            float characterSize,
            Color color,
            int sortingOrder,
            Vector3 localPosition,
            string initialText)
        {
            GameObject textObject = new GameObject(objectName);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = localPosition;
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = 64;
            text.fontStyle = FontStyle.Bold;
            text.characterSize = characterSize;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            text.text = initialText;
            MeshRenderer renderer = text.GetComponent<MeshRenderer>();
            renderer.sortingOrder = sortingOrder;
            if (font != null)
            {
                renderer.sharedMaterial = font.material;
            }
            return text;
        }

        private static void CreateClockPanelLayer(
            Transform parent,
            string objectName,
            Vector2 size,
            Color color,
            int sortingOrder,
            Vector3 localPosition)
        {
            GameObject layer = new GameObject(objectName);
            layer.transform.SetParent(parent, false);
            layer.transform.localPosition = localPosition;
            layer.transform.localScale = new Vector3(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y), 1f);
            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        private static void AddClockPencilHatching(Transform parent, Vector2 size, Color color, int sortingOrder)
        {
            int strokeCount = Mathf.Clamp(Mathf.RoundToInt((size.x + size.y) * 0.55f), 5, 18);
            Color pencil = new Color(color.r * 0.7f, color.g * 0.8f, color.b, 0.2f);
            for (int i = 0; i < strokeCount; i++)
            {
                float t = (i + 0.5f) / strokeCount;
                float y = Mathf.Lerp(-size.y * 0.44f, size.y * 0.44f, t);
                float wobble = Mathf.Sin(i * 2.31f) * 0.035f;
                AddDoodleLine("Blue Case Pencil Stroke", parent, new[]
                {
                    new Vector3(-size.x * 0.46f, y - 0.12f + wobble, -0.045f),
                    new Vector3(size.x * 0.46f, y + 0.12f - wobble, -0.045f)
                }, pencil, Mathf.Max(0.012f, Mathf.Min(size.x, size.y) * 0.008f), sortingOrder);
            }
        }

        private static Font GetMonitorFont()
        {
            if (monitorFont != null)
            {
                return monitorFont;
            }

            monitorFont = DoodleRuntimeAssets.HandwrittenFont;
            if (monitorFont == null)
            {
                monitorFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return monitorFont;
        }

        private static Color GetObjectColor(StageObjectType type)
        {
            if (type == StageObjectType.OneWayPlatform || type == StageObjectType.MovingOneWayPlatform)
            {
                return new Color(0.02f, 0.48f, 0.92f);
            }

            switch (StageObjectCatalog.Get(type).Category)
            {
                case StageObjectCategory.StartGoal:
                    return new Color(0.1f, 0.45f, 1f);
                case StageObjectCategory.Switch:
                    return new Color(0.95f, 0.2f, 0.2f);
                case StageObjectCategory.DoorGate:
                    return new Color(0.25f, 0.25f, 0.25f);
                case StageObjectCategory.Movable:
                    return new Color(0.52f, 0.34f, 0.18f);
                case StageObjectCategory.Action:
                    return new Color(0.1f, 0.65f, 0.25f);
                case StageObjectCategory.Trap:
                    return new Color(0.92f, 0.12f, 0.1f);
                case StageObjectCategory.Gimmick:
                    return new Color(0.55f, 0.25f, 0.9f);
                case StageObjectCategory.Enemy:
                    return new Color(0.72f, 0.18f, 0.58f);
                default:
                    if (type == StageObjectType.IceFloor || type == StageObjectType.SlipperySlope
                        || type == StageObjectType.IceBlock || type == StageObjectType.CloudPlatform)
                    {
                        return new Color(0.2f, 0.65f, 1f);
                    }

                    return new Color(0.05f, 0.05f, 0.05f);
            }
        }

        private bool UsesNatureTerrainStyle(StageObjectData data, Transform parent)
        {
            return data != null
                && (data.type == StageObjectType.Platform
                    || data.type == StageObjectType.Wall
                    || data.type == StageObjectType.FallingFloor
                    || data.type == StageObjectType.BreakableWall
                    || data.type == StageObjectType.BulletBreakableWall)
                && ((!string.IsNullOrEmpty(data.objectId)
                        && data.objectId.StartsWith("title-playground-", StringComparison.Ordinal))
                    || UsesNatureStageTheme(parent));
        }

        private bool UsesCaveTerrainStyle(StageObjectData data, Transform parent)
        {
            return data != null
                && IsCaveTerrainType(data.type)
                && UsesCaveStageTheme(parent);
        }

        private bool UsesUnderwaterTerrainStyle(StageObjectData data, Transform parent)
        {
            return data != null
                && IsCaveTerrainType(data.type)
                && UsesUnderwaterStageTheme(parent);
        }

        private bool UsesNightCityTerrainStyle(StageObjectData data, Transform parent)
        {
            return data != null
                && IsCaveTerrainType(data.type)
                && UsesNightCityStageTheme(parent);
        }

        private bool UsesFactoryTerrainStyle(StageObjectData data, Transform parent)
        {
            return data != null
                && (IsCaveTerrainType(data.type)
                    || data.type == StageObjectType.EscortPlayerOneWayFloor)
                && UsesFactoryStageTheme(parent);
        }

        private bool UsesSpaceTerrainStyle(StageObjectData data, Transform parent)
        {
            return data != null
                && (IsCaveTerrainType(data.type)
                    || data.type == StageObjectType.EscortPlayerOneWayFloor)
                && UsesSpaceStageTheme(parent);
        }

        private static bool IsCaveTerrainType(StageObjectType type)
        {
            return type == StageObjectType.Platform
                || type == StageObjectType.Wall
                || type == StageObjectType.Ceiling
                || type == StageObjectType.HalfPlatform
                || type == StageObjectType.OneWayPlatform
                || type == StageObjectType.FallingFloor
                || type == StageObjectType.MovingPlatform
                || type == StageObjectType.MovingOneWayPlatform
                || type == StageObjectType.BreakableFloor
                || type == StageObjectType.BreakableWall
                || type == StageObjectType.BulletBreakableWall;
        }

        private bool UsesNatureStageTheme(Transform parent)
        {
            if (parent == null || visualThemeRoot == null || !IsNatureStageId(visualThemeStageId))
            {
                return false;
            }

            return parent == visualThemeRoot || parent.IsChildOf(visualThemeRoot);
        }

        private bool UsesCaveStageTheme(Transform parent)
        {
            if (parent == null || visualThemeRoot == null || !IsCaveStageId(visualThemeStageId))
            {
                return false;
            }

            return parent == visualThemeRoot || parent.IsChildOf(visualThemeRoot);
        }

        private bool UsesUnderwaterStageTheme(Transform parent)
        {
            if (parent == null || visualThemeRoot == null || !IsUnderwaterStageId(visualThemeStageId))
            {
                return false;
            }

            return parent == visualThemeRoot || parent.IsChildOf(visualThemeRoot);
        }

        private bool UsesNightCityStageTheme(Transform parent)
        {
            if (parent == null || visualThemeRoot == null || !IsNightCityStageId(visualThemeStageId))
            {
                return false;
            }

            return parent == visualThemeRoot || parent.IsChildOf(visualThemeRoot);
        }

        private bool UsesFactoryStageTheme(Transform parent)
        {
            if (parent == null || visualThemeRoot == null || !IsFactoryStageId(visualThemeStageId))
            {
                return false;
            }

            return parent == visualThemeRoot || parent.IsChildOf(visualThemeRoot);
        }

        private bool UsesSpaceStageTheme(Transform parent)
        {
            if (parent == null || visualThemeRoot == null || !IsSpaceStageId(visualThemeStageId))
            {
                return false;
            }

            return parent == visualThemeRoot || parent.IsChildOf(visualThemeRoot);
        }

        private static bool IsNatureStageId(string stageId)
        {
            return stageId == "1-1"
                || stageId == "2-1"
                || stageId == "3-1"
                || stageId == "5-2"
                || stageId == "7-2"
                || stageId == "10-2"
                || stageId == "14-2";
        }

        private static bool IsCaveStageId(string stageId)
        {
            return stageId == "1-2"
                || stageId == "3-2"
                || stageId == "12-1"
                || stageId == "12-2";
        }

        private static bool IsUnderwaterStageId(string stageId)
        {
            return stageId == "2-2" || stageId == "6-3";
        }

        private static bool IsNightCityStageId(string stageId)
        {
            return stageId == "3-3"
                || stageId == "5-1"
                || stageId == "6-1"
                || stageId == "12-3"
                || stageId == "13-1";
        }

        internal static bool IsFactoryStageId(string stageId)
        {
            return stageId == "4-1"
                || stageId == "4-2"
                || stageId == "6-2"
                || stageId == "7-3"
                || stageId == "8-1"
                || stageId == "9-1"
                || stageId == "9-3"
                || stageId == "10-3"
                || stageId == "11-2"
                || stageId == "11-3"
                || stageId == "13-3"
                || stageId == "14-3";
        }

        internal static bool IsSpaceStageId(string stageId)
        {
            return stageId == "4-3"
                || stageId == "15-1"
                || stageId == "15-2"
                || stageId == "15-3";
        }

        private static bool IsUnderwaterHorizontalPlatform(StageObjectData data)
        {
            if (data == null || data.type != StageObjectType.Platform)
            {
                return false;
            }

            float worldAngle = Mathf.Abs(Mathf.DeltaAngle(0f, data.rotation));
            bool localTopFacesUp = worldAngle < 2f;
            return localTopFacesUp && data.size.x >= Mathf.Max(3f, data.size.y * 1.8f);
        }

        private static bool IsUnderwaterMainSandFloor(StageObjectData data)
        {
            return IsUnderwaterHorizontalPlatform(data)
                && (string.Equals(data.objectId, "Platform_Start", StringComparison.Ordinal)
                    || (!string.IsNullOrEmpty(data.objectId)
                        && data.objectId.IndexOf("preview_floor", StringComparison.OrdinalIgnoreCase) >= 0)
                    || data.size.x >= 24f);
        }

        private static void AddObjectGlyph(Transform parent, StageObjectData data)
        {
            string label = StageObjectCatalog.Get(data.type).Label;
            string glyph = string.IsNullOrEmpty(label) ? data.type.ToString() : label.Substring(0, 1);
            if (data.type == StageObjectType.Goal || data.type == StageObjectType.MidGoal)
            {
                glyph = "G";
            }
            else if (data.type == StageObjectType.Spawn || data.type == StageObjectType.RespawnPoint)
            {
                glyph = "S";
            }
            else if (data.type == StageObjectType.BalanceScale || data.type == StageObjectType.Seesaw)
            {
                glyph = "↔";
            }
            else if (data.type == StageObjectType.Key)
            {
                glyph = "鍵";
            }
            else if (data.type == StageObjectType.Coin)
            {
                glyph = "￥";
            }
            else if (data.type == StageObjectType.Star)
            {
                glyph = "☆";
            }

            GameObject textObject = new GameObject("Glyph");
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.text = glyph;
            text.fontSize = 30;
            text.characterSize = Mathf.Clamp(Mathf.Min(data.size.x, data.size.y) * 0.16f, 0.06f, 0.2f);
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0.02f, 0.02f, 0.02f, 0.82f);
        }

        private static void AddEditorMetadata(GameObject obj, StageObjectData data)
        {
            StageEditorObject marker = obj.AddComponent<StageEditorObject>();
            marker.objectId = data.objectId;
            marker.type = data.type;
            marker.size = data.size;
            marker.actionStrength = data.actionStrength;
            marker.movementAngle = data.movementAngle;
            marker.movementSpeed = data.movementSpeed;
            marker.spawnPattern = data.spawnPattern;
            marker.spawnBoxSize = data.spawnBoxSize;
            marker.bombFuseSeconds = data.bombFuseSeconds;
            marker.linkTargetId = data.linkTargetId;
            marker.linkAction = data.linkAction;
        }

        private static bool AddResourceSprite(
            Transform parent,
            string resourcePath,
            Vector2 localSize,
            int sortingOrder,
            string objectName,
            Vector2 localPosition = default)
        {
            if (parent == null || string.IsNullOrEmpty(resourcePath)) return false;
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null || sprite.bounds.size.x <= 0f || sprite.bounds.size.y <= 0f) return false;

            GameObject visual = new GameObject(string.IsNullOrEmpty(objectName) ? sprite.name : objectName);
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(localPosition.x, localPosition.y, -0.025f);
            visual.transform.localScale = new Vector3(
                Mathf.Max(0.001f, localSize.x) / sprite.bounds.size.x,
                Mathf.Max(0.001f, localSize.y) / sprite.bounds.size.y,
                1f);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = sortingOrder;
            return true;
        }

        private static GameObject CreateBox(string name, Vector2 position, Vector2 size, Color color, Transform parent)
        {
            GameObject obj = new GameObject(string.IsNullOrEmpty(name) ? "StageObject" : name);
            obj.transform.SetParent(parent, false);
            obj.transform.position = position;
            obj.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = color;
            renderer.sortingOrder = 3;
            return obj;
        }

        private static void AddSketchBoxOutline(Transform parent, Vector2 size, Color color, float width)
        {
            Vector3[] points =
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.48f, 0f),
                new Vector3(0.48f, 0.5f, 0f),
                new Vector3(-0.49f, 0.48f, 0f),
                new Vector3(-0.5f, -0.5f, 0f)
            };
            AddDoodleLine("Outline", parent, points, color, width / Mathf.Max(Mathf.Max(size.x, size.y), 0.1f), 12);
        }

        private static void AddSolidWash(Transform parent, Vector2 size, Color color)
        {
            GameObject wash = new GameObject("Solid Fill Wash");
            wash.transform.SetParent(parent, false);
            wash.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            wash.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer = wash.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = new Color(color.r, color.g, color.b, 0.025f);
            renderer.sortingOrder = 3;
        }

        private static void AddSolidPaperBase(Transform parent, Vector2 size)
        {
            AddSolidPaperBase(parent, size, new Color(0.985f, 0.975f, 0.93f, 1f));
        }

        private static void AddSolidPaperBase(Transform parent, Vector2 size, Color paperColor)
        {
            GameObject baseObject = new GameObject("Solid Opaque Paper Base");
            baseObject.transform.SetParent(parent, false);
            baseObject.transform.localPosition = new Vector3(0f, 0f, 0.03f);
            baseObject.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer = baseObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = paperColor;
            renderer.sortingOrder = 2;
        }

        private static void AddSolidSketchBoxOutline(Transform parent, Vector2 size, Color color, float width, int sortingOrder, Vector3 offset = default)
        {
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            Vector3[] points =
            {
                offset + new Vector3(-halfWidth - 0.02f, -halfHeight + 0.02f, 0f),
                offset + new Vector3(halfWidth, -halfHeight + 0.04f, 0f),
                offset + new Vector3(halfWidth - 0.03f, halfHeight, 0f),
                offset + new Vector3(-halfWidth + 0.01f, halfHeight - 0.02f, 0f),
                offset + new Vector3(-halfWidth - 0.02f, -halfHeight + 0.02f, 0f)
            };
            AddDoodleLine("Solid Sketch Outline A", parent, points, color, width, sortingOrder);

            Vector3[] loosePoints =
            {
                offset + new Vector3(-halfWidth, -halfHeight - 0.02f, 0f),
                offset + new Vector3(halfWidth + 0.03f, -halfHeight + 0.01f, 0f),
                offset + new Vector3(halfWidth + 0.01f, halfHeight + 0.02f, 0f),
                offset + new Vector3(-halfWidth - 0.03f, halfHeight - 0.01f, 0f),
                offset + new Vector3(-halfWidth, -halfHeight - 0.02f, 0f)
            };
            AddDoodleLine("Solid Sketch Outline B", parent, loosePoints, color * 0.9f, width, sortingOrder + 1);
        }

        private static void AddSolidPencilFill(
            Transform parent,
            Vector2 size,
            Color color,
            int sortingOrder = 4,
            float opacityScale = 1f)
        {
            float left = -size.x * 0.5f;
            float right = size.x * 0.5f;
            float bottom = -size.y * 0.5f;
            float top = size.y * 0.5f;
            Color pencil = new Color(color.r, color.g, color.b, 0.32f);
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();

            int index = 0;
            for (int layer = 0; layer < 3; layer++)
            {
                float row = bottom + 0.06f + layer * 0.05f;
                float rowSpacing = 0.22f + layer * 0.025f;
                while (row < top - 0.04f)
                {
                    float x = left - 0.6f + Mathf.Sin(index * 1.37f) * 0.08f + layer * 0.1f;
                    while (x < right)
                    {
                        float jitterX = Mathf.Sin(index * 2.17f) * 0.045f;
                        float jitterY = Mathf.Cos(index * 1.61f) * 0.035f;
                        float length = 0.7f + Mathf.Abs(Mathf.Sin(index * 1.11f)) * 0.45f;
                        float rise = 0.22f + Mathf.Abs(Mathf.Cos(index * 1.91f)) * 0.16f;
                        float startX = Mathf.Max(left, x + jitterX);
                        float startY = Mathf.Clamp(row + jitterY, bottom + 0.04f, top - 0.04f);
                        float endX = Mathf.Min(right, startX + length);
                        float endY = Mathf.Clamp(startY + rise, bottom + 0.04f, top - 0.04f);

                        if (endX > left && startX < right && endY > bottom)
                        {
                            Color layerColor = new Color(
                                pencil.r,
                                pencil.g,
                                pencil.b,
                                (0.14f + layer * 0.045f + Mathf.Abs(Mathf.Sin(index * 0.71f)) * 0.07f)
                                    * opacityScale);
                            AppendPencilQuad(
                                vertices, colors, triangles,
                                new Vector3(startX, startY, 0f),
                                new Vector3(endX, endY, 0f),
                                0.01f + layer * 0.002f,
                                layerColor);
                        }

                        x += 0.34f + Mathf.Sin(index * 3.23f) * 0.045f;
                        index++;
                    }

                    row += rowSpacing;
                }
            }

            for (int i = 0; i < 5; i++)
            {
                float y = Mathf.Lerp(bottom + 0.16f, top - 0.12f, (i + 1f) / 6f);
                AppendPencilQuad(
                    vertices, colors, triangles,
                    new Vector3(left + 0.1f, y + Mathf.Sin(i * 1.3f) * 0.025f, 0f),
                    new Vector3(right - 0.1f, y + Mathf.Cos(i * 1.9f) * 0.025f, 0f),
                    0.01f,
                    new Color(color.r, color.g, color.b, 0.13f * opacityScale));
            }

            CreatePencilMesh(parent, "Solid Pencil Fill Mesh", vertices, colors, triangles, sortingOrder);
        }

        private static void AddNightCityTerrainFill(Transform parent, Vector2 size, int seed)
        {
            AddSolidPencilFill(parent, size, NightCityTerrainStrokeColor, 4, 0.92f);

            float left = -size.x * 0.5f;
            float right = size.x * 0.5f;
            float bottom = -size.y * 0.5f;
            float top = size.y * 0.5f;
            uint state = CreateCaveVisualState(seed ^ 0x4E494748);
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            Color seam = new Color(
                NightCityTerrainStrokeColor.r,
                NightCityTerrainStrokeColor.g,
                NightCityTerrainStrokeColor.b,
                0.34f);
            Color scratch = new Color(0.73f, 0.8f, 0.9f, 0.16f);

            float verticalSpacing = CaveVisualRange(ref state, 1.45f, 2.35f);
            for (float x = left + verticalSpacing; x < right - 0.1f; x += verticalSpacing)
            {
                float wobble = CaveVisualRange(ref state, -0.045f, 0.045f);
                AppendCaveWobblyStroke(
                    vertices,
                    colors,
                    triangles,
                    new Vector2(x + wobble, bottom + 0.04f),
                    new Vector2(x - wobble * 0.5f, top - 0.04f),
                    0.016f,
                    seam,
                    0.025f,
                    ref state);
            }

            float horizontalSpacing = CaveVisualRange(ref state, 1.25f, 2.05f);
            for (float y = bottom + horizontalSpacing; y < top - 0.1f; y += horizontalSpacing)
            {
                AppendCaveWobblyStroke(
                    vertices,
                    colors,
                    triangles,
                    new Vector2(left + 0.04f, y + CaveVisualRange(ref state, -0.035f, 0.035f)),
                    new Vector2(right - 0.04f, y + CaveVisualRange(ref state, -0.035f, 0.035f)),
                    0.014f,
                    seam * 0.86f,
                    0.02f,
                    ref state);
            }

            int scratchCount = Mathf.Clamp(Mathf.RoundToInt(size.x * size.y * 0.1f), 4, 52);
            for (int i = 0; i < scratchCount; i++)
            {
                Vector2 from = new Vector2(
                    CaveVisualRange(ref state, left + 0.12f, right - 0.12f),
                    CaveVisualRange(ref state, bottom + 0.12f, top - 0.12f));
                Vector2 to = from + new Vector2(
                    CaveVisualRange(ref state, 0.18f, 0.62f),
                    CaveVisualRange(ref state, 0.08f, 0.26f));
                to.x = Mathf.Min(to.x, right - 0.08f);
                to.y = Mathf.Min(to.y, top - 0.08f);
                AppendPencilQuad(
                    vertices,
                    colors,
                    triangles,
                    from,
                    to,
                    CaveVisualRange(ref state, 0.009f, 0.018f),
                    scratch);
            }

            int rivetColumns = Mathf.Clamp(Mathf.FloorToInt(size.x / 2.2f) + 1, 2, 28);
            int rivetRows = Mathf.Clamp(Mathf.FloorToInt(size.y / 2.2f) + 1, 2, 18);
            for (int xIndex = 0; xIndex < rivetColumns; xIndex++)
            {
                for (int yIndex = 0; yIndex < rivetRows; yIndex++)
                {
                    if ((xIndex + yIndex) % 2 != 0 && CaveVisual01(ref state) < 0.56f)
                    {
                        continue;
                    }

                    float x = Mathf.Lerp(left + 0.1f, right - 0.1f, rivetColumns <= 1 ? 0.5f : xIndex / (rivetColumns - 1f));
                    float y = Mathf.Lerp(bottom + 0.1f, top - 0.1f, rivetRows <= 1 ? 0.5f : yIndex / (rivetRows - 1f));
                    float radius = CaveVisualRange(ref state, 0.025f, 0.045f);
                    AppendPencilQuad(vertices, colors, triangles, new Vector2(x - radius, y), new Vector2(x + radius, y), 0.018f, seam);
                    AppendPencilQuad(vertices, colors, triangles, new Vector2(x, y - radius), new Vector2(x, y + radius), 0.018f, seam);
                }
            }

            CreatePencilMesh(parent, "Night City Metal Panel Pencil", vertices, colors, triangles, 7);
        }

        private static void AddNightCityTerrainBoxOutline(Transform parent, Vector2 size, int seed)
        {
            float jitter = ((seed & 3) - 1.5f) * 0.006f;
            AddSolidSketchBoxOutline(
                parent,
                size,
                NightCityTerrainAccentColor,
                0.038f,
                11,
                new Vector3(0.018f + jitter, -0.014f, 0f));
            AddSolidSketchBoxOutline(
                parent,
                size,
                NightCityTerrainStrokeColor,
                0.072f,
                13);
        }

        private static void AddFactoryTerrainFill(Transform parent, Vector2 size, int seed)
        {
            AddSolidPencilFill(parent, size, FactoryTerrainStrokeColor, 4, 0.82f);
            AddFactoryPanelDetailMesh(parent, size, seed, 7);
        }

        private static void AddFactoryPanelDetailMesh(
            Transform parent,
            Vector2 size,
            int seed,
            int sortingOrder)
        {
            float left = -size.x * 0.5f;
            float right = size.x * 0.5f;
            float bottom = -size.y * 0.5f;
            float top = size.y * 0.5f;
            uint state = CreateCaveVisualState(seed ^ 0x46414354);
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            Color joint = new Color(
                FactoryTerrainStrokeColor.r,
                FactoryTerrainStrokeColor.g,
                FactoryTerrainStrokeColor.b,
                0.52f);
            Color scrape = new Color(0.9f, 0.92f, 0.91f, 0.15f);

            float panelSpanX = CaveVisualRange(ref state, 2.1f, 3.6f);
            for (float x = left + panelSpanX; x < right - 0.12f; x += panelSpanX)
            {
                AppendCaveWobblyStroke(
                    vertices,
                    colors,
                    triangles,
                    new Vector2(x + CaveVisualRange(ref state, -0.035f, 0.035f), bottom + 0.04f),
                    new Vector2(x + CaveVisualRange(ref state, -0.035f, 0.035f), top - 0.04f),
                    0.024f,
                    joint,
                    0.018f,
                    ref state);
            }

            float panelSpanY = CaveVisualRange(ref state, 1.8f, 2.9f);
            for (float y = bottom + panelSpanY; y < top - 0.12f; y += panelSpanY)
            {
                AppendCaveWobblyStroke(
                    vertices,
                    colors,
                    triangles,
                    new Vector2(left + 0.04f, y + CaveVisualRange(ref state, -0.03f, 0.03f)),
                    new Vector2(right - 0.04f, y + CaveVisualRange(ref state, -0.03f, 0.03f)),
                    0.022f,
                    joint * 0.88f,
                    0.016f,
                    ref state);
            }

            int rivetColumns = Mathf.Clamp(Mathf.CeilToInt(size.x / 2.7f) + 1, 2, 34);
            int rivetRows = Mathf.Clamp(Mathf.CeilToInt(size.y / 2.4f) + 1, 2, 24);
            for (int xIndex = 0; xIndex < rivetColumns; xIndex++)
            {
                for (int yIndex = 0; yIndex < rivetRows; yIndex++)
                {
                    bool edge = xIndex == 0 || xIndex == rivetColumns - 1
                        || yIndex == 0 || yIndex == rivetRows - 1;
                    if (!edge && ((xIndex * 3 + yIndex * 5) & 3) != 0)
                    {
                        continue;
                    }

                    float x = Mathf.Lerp(left + 0.11f, right - 0.11f, xIndex / (rivetColumns - 1f));
                    float y = Mathf.Lerp(bottom + 0.11f, top - 0.11f, yIndex / (rivetRows - 1f));
                    float radius = edge ? 0.045f : 0.035f;
                    AppendPencilQuad(vertices, colors, triangles, new Vector2(x - radius, y), new Vector2(x + radius, y), 0.024f, joint);
                    AppendPencilQuad(vertices, colors, triangles, new Vector2(x, y - radius), new Vector2(x, y + radius), 0.024f, joint);
                }
            }

            int scrapeCount = Mathf.Clamp(Mathf.RoundToInt(size.x * size.y * 0.075f), 3, 58);
            for (int i = 0; i < scrapeCount; i++)
            {
                Vector2 from = new Vector2(
                    CaveVisualRange(ref state, left + 0.14f, right - 0.14f),
                    CaveVisualRange(ref state, bottom + 0.14f, top - 0.14f));
                Vector2 to = from + new Vector2(
                    CaveVisualRange(ref state, 0.18f, 0.74f),
                    CaveVisualRange(ref state, -0.08f, 0.18f));
                to.x = Mathf.Clamp(to.x, left + 0.08f, right - 0.08f);
                to.y = Mathf.Clamp(to.y, bottom + 0.08f, top - 0.08f);
                AppendPencilQuad(vertices, colors, triangles, from, to, CaveVisualRange(ref state, 0.01f, 0.02f), scrape);
            }

            // A small amount of hand-drawn warning paint makes long industrial
            // beams readable without turning every platform into a road sign.
            if (size.x >= 7f && size.y <= 1.35f && (seed & 3) == 1)
            {
                float stripeWidth = 0.32f;
                float stripLeft = Mathf.Max(left + 0.22f, right - Mathf.Min(3.2f, size.x * 0.28f));
                Color yellow = new Color(0.95f, 0.69f, 0.12f, 0.44f);
                Color charcoal = new Color(0.11f, 0.12f, 0.13f, 0.45f);
                for (float x = stripLeft; x < right - 0.2f; x += stripeWidth)
                {
                    Color stripe = (Mathf.FloorToInt((x - stripLeft) / stripeWidth) & 1) == 0 ? yellow : charcoal;
                    AppendPencilQuad(
                        vertices,
                        colors,
                        triangles,
                        new Vector2(x, bottom + 0.08f),
                        new Vector2(Mathf.Min(x + 0.38f, right - 0.08f), top - 0.08f),
                        0.12f,
                        stripe);
                }
            }

            CreatePencilMesh(parent, "Factory Steel Plate Joints And Rivets", vertices, colors, triangles, sortingOrder);
        }

        private static void AddFactoryTerrainBoxOutline(
            Transform parent,
            Vector2 size,
            int seed,
            int sortingOrder = 13)
        {
            float jitter = ((seed & 7) - 3.5f) * 0.004f;
            AddSolidSketchBoxOutline(
                parent,
                size,
                FactoryTerrainAccentColor,
                0.042f,
                sortingOrder - 2,
                new Vector3(0.016f + jitter, -0.012f, 0f));
            AddSolidSketchBoxOutline(
                parent,
                size,
                FactoryTerrainStrokeColor,
                0.078f,
                sortingOrder);
        }

        internal static SpriteRenderer AddFactorySteelPanelVisual(
            Transform parent,
            Vector2 size,
            string seedKey,
            int sortingOrder)
        {
            if (parent == null)
            {
                return null;
            }

            GameObject baseObject = new GameObject("Factory Steel Plate Fill");
            baseObject.transform.SetParent(parent, false);
            baseObject.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = baseObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = FactoryTerrainPaperColor;
            renderer.sortingOrder = sortingOrder;

            int seed = GetStableNatureVisualSeed(seedKey);
            AddSolidPencilFill(parent, size, FactoryTerrainStrokeColor, sortingOrder + 1, 0.78f);
            AddFactoryPanelDetailMesh(parent, size, seed, sortingOrder + 2);
            AddFactoryTerrainBoxOutline(parent, size, seed, sortingOrder + 3);
            return renderer;
        }

        private static void AddSpaceTerrainFill(Transform parent, Vector2 size, int seed)
        {
            AddSolidPencilFill(parent, size, SpaceTerrainStrokeColor, 4, 0.74f);
            AddSpacePanelDetailMesh(parent, size, seed, 7, false);
        }

        private static void AddSpacePanelDetailMesh(
            Transform parent,
            Vector2 size,
            int seed,
            int sortingOrder,
            bool warningPanel)
        {
            float left = -size.x * 0.5f;
            float right = size.x * 0.5f;
            float bottom = -size.y * 0.5f;
            float top = size.y * 0.5f;
            uint state = CreateCaveVisualState(seed ^ 0x53504143);
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            Color joint = new Color(
                SpaceTerrainStrokeColor.r,
                SpaceTerrainStrokeColor.g,
                SpaceTerrainStrokeColor.b,
                0.58f);
            Color bevel = new Color(0.82f, 0.9f, 0.96f, 0.22f);
            Color glow = warningPanel
                ? new Color(1f, 0.68f, 0.14f, 0.9f)
                : SpaceTerrainGlowColor;

            float panelSpanX = CaveVisualRange(ref state, 2.7f, 4.3f);
            for (float x = left + panelSpanX; x < right - 0.12f; x += panelSpanX)
            {
                float wobble = CaveVisualRange(ref state, -0.035f, 0.035f);
                AppendCaveWobblyStroke(
                    vertices,
                    colors,
                    triangles,
                    new Vector2(x + wobble, bottom + 0.035f),
                    new Vector2(x - wobble * 0.5f, top - 0.035f),
                    0.028f,
                    joint,
                    0.016f,
                    ref state);
            }

            float panelSpanY = CaveVisualRange(ref state, 2.1f, 3.3f);
            for (float y = bottom + panelSpanY; y < top - 0.12f; y += panelSpanY)
            {
                AppendCaveWobblyStroke(
                    vertices,
                    colors,
                    triangles,
                    new Vector2(left + 0.035f, y),
                    new Vector2(right - 0.035f, y + CaveVisualRange(ref state, -0.025f, 0.025f)),
                    0.025f,
                    joint * 0.9f,
                    0.015f,
                    ref state);
            }

            if (size.x > 0.34f && size.y > 0.34f)
            {
                float insetX = Mathf.Min(0.16f, size.x * 0.12f);
                float insetY = Mathf.Min(0.16f, size.y * 0.12f);
                AppendCaveWobblyStroke(vertices, colors, triangles,
                    new Vector2(left + insetX, top - insetY),
                    new Vector2(right - insetX, top - insetY), 0.022f, bevel, 0.01f, ref state);
                AppendCaveWobblyStroke(vertices, colors, triangles,
                    new Vector2(left + insetX, bottom + insetY),
                    new Vector2(right - insetX, bottom + insetY), 0.018f, joint * 0.72f, 0.01f, ref state);
            }

            int rivetColumns = Mathf.Clamp(Mathf.CeilToInt(size.x / 3.1f) + 1, 2, 42);
            int rivetRows = Mathf.Clamp(Mathf.CeilToInt(size.y / 2.7f) + 1, 2, 24);
            for (int xIndex = 0; xIndex < rivetColumns; xIndex++)
            {
                for (int yIndex = 0; yIndex < rivetRows; yIndex++)
                {
                    bool edge = xIndex == 0 || xIndex == rivetColumns - 1
                        || yIndex == 0 || yIndex == rivetRows - 1;
                    if (!edge && ((xIndex * 5 + yIndex * 3) & 3) != 0)
                    {
                        continue;
                    }

                    float x = Mathf.Lerp(left + Mathf.Min(0.12f, size.x * 0.22f), right - Mathf.Min(0.12f, size.x * 0.22f), xIndex / (rivetColumns - 1f));
                    float y = Mathf.Lerp(bottom + Mathf.Min(0.12f, size.y * 0.22f), top - Mathf.Min(0.12f, size.y * 0.22f), yIndex / (rivetRows - 1f));
                    float radius = edge ? 0.044f : 0.033f;
                    AppendPencilQuad(vertices, colors, triangles,
                        new Vector2(x - radius, y), new Vector2(x + radius, y), 0.022f, joint);
                    AppendPencilQuad(vertices, colors, triangles,
                        new Vector2(x, y - radius), new Vector2(x, y + radius), 0.022f, joint);
                }
            }

            if (size.x >= 1.8f && size.x >= size.y * 1.25f)
            {
                float inset = Mathf.Min(0.42f, size.x * 0.12f);
                float y = Mathf.Clamp(top - Mathf.Min(0.21f, size.y * 0.34f), bottom + 0.06f, top - 0.06f);
                Vector2 from = new Vector2(left + inset, y);
                Vector2 to = new Vector2(right - inset, y + CaveVisualRange(ref state, -0.018f, 0.018f));
                AppendPencilQuad(vertices, colors, triangles, from, to, 0.095f, new Color(glow.r, glow.g, glow.b, 0.16f));
                AppendPencilQuad(vertices, colors, triangles, from, to, 0.035f, glow);
            }
            else if (size.y >= 3f)
            {
                float x = Mathf.Clamp(right - Mathf.Min(0.22f, size.x * 0.28f), left + 0.07f, right - 0.07f);
                float inset = Mathf.Min(0.42f, size.y * 0.1f);
                Vector2 from = new Vector2(x, bottom + inset);
                Vector2 to = new Vector2(x + CaveVisualRange(ref state, -0.018f, 0.018f), top - inset);
                AppendPencilQuad(vertices, colors, triangles, from, to, 0.095f, new Color(glow.r, glow.g, glow.b, 0.16f));
                AppendPencilQuad(vertices, colors, triangles, from, to, 0.035f, glow);
            }

            int scrapeCount = Mathf.Clamp(Mathf.RoundToInt(size.x * size.y * 0.045f), 2, 44);
            Color scrape = new Color(0.93f, 0.96f, 1f, 0.12f);
            for (int scrapeIndex = 0; scrapeIndex < scrapeCount; scrapeIndex++)
            {
                Vector2 from = new Vector2(
                    CaveVisualRange(ref state, left + 0.1f, right - 0.1f),
                    CaveVisualRange(ref state, bottom + 0.08f, top - 0.08f));
                Vector2 to = from + new Vector2(
                    CaveVisualRange(ref state, 0.14f, 0.58f),
                    CaveVisualRange(ref state, -0.05f, 0.13f));
                to.x = Mathf.Clamp(to.x, left + 0.06f, right - 0.06f);
                to.y = Mathf.Clamp(to.y, bottom + 0.05f, top - 0.05f);
                AppendPencilQuad(vertices, colors, triangles, from, to,
                    CaveVisualRange(ref state, 0.009f, 0.018f), scrape);
            }

            CreatePencilMesh(parent, "Space Futuristic Panel Details", vertices, colors, triangles, sortingOrder);
        }

        private static void AddSpaceTerrainBoxOutline(
            Transform parent,
            Vector2 size,
            int seed,
            int sortingOrder = 13)
        {
            float jitter = ((seed & 7) - 3.5f) * 0.0035f;
            AddSolidSketchBoxOutline(
                parent,
                size,
                SpaceTerrainAccentColor,
                0.044f,
                sortingOrder - 2,
                new Vector3(0.014f + jitter, -0.012f, 0f));
            AddSolidSketchBoxOutline(
                parent,
                size,
                SpaceTerrainStrokeColor,
                0.082f,
                sortingOrder);
        }

        internal static SpriteRenderer AddSpaceFuturisticPanelVisual(
            Transform parent,
            Vector2 size,
            string seedKey,
            int sortingOrder,
            bool warningPanel = false)
        {
            if (parent == null)
            {
                return null;
            }

            GameObject baseObject = new GameObject("Space Station Panel Fill");
            baseObject.transform.SetParent(parent, false);
            baseObject.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = baseObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = warningPanel
                ? new Color(0.64f, 0.59f, 0.48f, 1f)
                : SpaceTerrainPaperColor;
            renderer.sortingOrder = sortingOrder;

            int seed = GetStableNatureVisualSeed(seedKey);
            AddSolidPencilFill(parent, size, SpaceTerrainStrokeColor, sortingOrder + 1, 0.68f);
            AddSpacePanelDetailMesh(parent, size, seed, sortingOrder + 2, warningPanel);
            AddSpaceTerrainBoxOutline(parent, size, seed, sortingOrder + 3);
            return renderer;
        }

        private static Rect GetSpaceBackdropBounds(string stageId, IList<StageObjectData> objects)
        {
            // The chapter 15 arenas are assembled by their controllers after the
            // JSON has loaded. Fixed canvases keep the star field present across
            // their complete camera route instead of measuring the lone spawn.
            switch (stageId)
            {
                case "4-3":
                    return Rect.MinMaxRect(-23f, -11f, 31f, 17f);
                case "15-1":
                    return Rect.MinMaxRect(-23f, -11f, 23f, 15f);
                case "15-2":
                    return Rect.MinMaxRect(-20f, -12f, 202f, 14f);
                case "15-3":
                    return Rect.MinMaxRect(-22f, -12f, 22f, 14f);
                default:
                    return GetCaveBackdropBounds(objects);
            }
        }

        private static Rect GetBackdropColorFillBounds(Rect detailBounds)
        {
            // Decorative layouts stay close to the playable area, but their
            // solid color must cover every supported aspect ratio and the
            // CameraFollow2D group zoom. Keeping this overscan separate avoids
            // generating hundreds of off-screen stars, plants or machines.
            return Rect.MinMaxRect(
                detailBounds.xMin - 72f,
                detailBounds.yMin - 24f,
                detailBounds.xMax + 72f,
                detailBounds.yMax + 24f);
        }

        private static void AddSpaceBackdrop(Transform parent, Rect bounds, int seed)
        {
            Rect colorBounds = GetBackdropColorFillBounds(bounds);
            GameObject wash = new GameObject("Space Deep Navy Crayon Wash");
            wash.transform.SetParent(parent, false);
            wash.transform.localPosition = new Vector3(colorBounds.center.x, colorBounds.center.y, 0f);
            wash.transform.localScale = new Vector3(colorBounds.width, colorBounds.height, 1f);
            SpriteRenderer washRenderer = wash.AddComponent<SpriteRenderer>();
            washRenderer.sprite = GetSquareSprite();
            washRenderer.color = SpaceBackdropColor;
            washRenderer.sortingOrder = -99;

            Color[] depthBands =
            {
                new Color(0.025f, 0.035f, 0.12f, 0.5f),
                new Color(0.07f, 0.09f, 0.27f, 0.28f),
                new Color(0.08f, 0.18f, 0.38f, 0.18f)
            };
            float bandHeight = bounds.height / depthBands.Length;
            for (int band = 0; band < depthBands.Length; band++)
            {
                float bandTop = band == 0
                    ? colorBounds.yMax
                    : bounds.yMax - bandHeight * band;
                float bandBottom = band == depthBands.Length - 1
                    ? colorBounds.yMin
                    : bounds.yMax - bandHeight * (band + 1f);
                GameObject layer = new GameObject("Space Sky Depth " + band);
                layer.transform.SetParent(parent, false);
                layer.transform.localPosition = new Vector3(
                    colorBounds.center.x,
                    (bandTop + bandBottom) * 0.5f,
                    0f);
                layer.transform.localScale = new Vector3(colorBounds.width, bandTop - bandBottom + 0.12f, 1f);
                SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
                renderer.sprite = GetSquareSprite();
                renderer.color = depthBands[band];
                renderer.sortingOrder = -98;
            }

            uint state = CreateCaveVisualState(seed);
            List<Vector3> textureVertices = new List<Vector3>();
            List<Color> textureColors = new List<Color>();
            List<int> textureTriangles = new List<int>();
            int hatchCount = Mathf.Clamp(Mathf.RoundToInt(bounds.width * bounds.height / 18f), 85, 420);
            for (int hatch = 0; hatch < hatchCount; hatch++)
            {
                Vector2 from = new Vector2(
                    CaveVisualRange(ref state, bounds.xMin, bounds.xMax),
                    CaveVisualRange(ref state, bounds.yMin, bounds.yMax));
                float length = CaveVisualRange(ref state, 0.55f, 3.5f);
                Vector2 to = from + new Vector2(length, length * CaveVisualRange(ref state, 0.16f, 0.38f));
                to.x = Mathf.Min(to.x, bounds.xMax);
                to.y = Mathf.Min(to.y, bounds.yMax);
                Color hatchColor = (hatch & 3) == 0
                    ? new Color(0.33f, 0.48f, 0.86f, CaveVisualRange(ref state, 0.045f, 0.09f))
                    : new Color(0.48f, 0.38f, 0.82f, CaveVisualRange(ref state, 0.025f, 0.065f));
                AppendPencilQuad(
                    textureVertices,
                    textureColors,
                    textureTriangles,
                    from,
                    to,
                    CaveVisualRange(ref state, 0.012f, 0.029f),
                    hatchColor);
            }

            int nebulaCount = Mathf.Clamp(Mathf.CeilToInt(bounds.width / 38f), 2, 7);
            for (int nebula = 0; nebula < nebulaCount; nebula++)
            {
                float centerX = Mathf.Lerp(bounds.xMin, bounds.xMax, (nebula + 0.5f) / nebulaCount)
                    + CaveVisualRange(ref state, -2.5f, 2.5f);
                float centerY = CaveVisualRange(ref state, bounds.yMin + bounds.height * 0.28f, bounds.yMax - 1.4f);
                float span = CaveVisualRange(ref state, 5f, 10f);
                Color nebulaColor = nebula % 2 == 0
                    ? new Color(0.32f, 0.21f, 0.7f, 0.07f)
                    : new Color(0.08f, 0.56f, 0.72f, 0.055f);
                for (int stroke = 0; stroke < 5; stroke++)
                {
                    float offset = (stroke - 2f) * CaveVisualRange(ref state, 0.22f, 0.52f);
                    AppendCaveWobblyStroke(
                        textureVertices,
                        textureColors,
                        textureTriangles,
                        new Vector2(centerX - span * 0.5f, centerY + offset),
                        new Vector2(centerX + span * 0.5f, centerY - offset * 0.45f),
                        CaveVisualRange(ref state, 0.16f, 0.34f),
                        nebulaColor,
                        0.2f,
                        ref state);
                }
            }
            CreatePencilMesh(parent, "Space Crayon Sky Texture And Nebulae", textureVertices, textureColors, textureTriangles, -97);

            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            int starCount = Mathf.Clamp(Mathf.RoundToInt(bounds.width * bounds.height / 14f), 55, 320);
            Color[] starColors =
            {
                new Color(1f, 0.88f, 0.28f, 0.88f),
                new Color(0.83f, 0.93f, 1f, 0.84f),
                new Color(0.56f, 0.8f, 1f, 0.78f)
            };
            for (int star = 0; star < starCount; star++)
            {
                Vector2 center = new Vector2(
                    CaveVisualRange(ref state, bounds.xMin + 0.35f, bounds.xMax - 0.35f),
                    CaveVisualRange(ref state, bounds.yMin + 0.45f, bounds.yMax - 0.35f));
                float radius = CaveVisualRange(ref state, 0.025f, 0.105f);
                Color starColor = starColors[star % starColors.Length];
                AppendPencilQuad(vertices, colors, triangles, center - Vector2.right * radius, center + Vector2.right * radius, Mathf.Max(0.012f, radius * 0.27f), starColor);
                AppendPencilQuad(vertices, colors, triangles, center - Vector2.up * radius, center + Vector2.up * radius, Mathf.Max(0.012f, radius * 0.27f), starColor);
                if (star % 17 == 0)
                {
                    Vector2 diagonal = new Vector2(radius * 0.62f, radius * 0.62f);
                    AppendPencilQuad(vertices, colors, triangles, center - diagonal, center + diagonal, Mathf.Max(0.009f, radius * 0.18f), starColor * 0.76f);
                }
            }

            int shootingStarCount = Mathf.Clamp(Mathf.CeilToInt(bounds.width / 55f), 1, 5);
            for (int shootingStar = 0; shootingStar < shootingStarCount; shootingStar++)
            {
                float slot = (shootingStar + 0.63f) / shootingStarCount;
                Vector2 head = new Vector2(
                    Mathf.Lerp(bounds.xMin + 3f, bounds.xMax - 3f, slot),
                    CaveVisualRange(ref state, bounds.center.y + 1f, bounds.yMax - 1.2f));
                AppendSpaceShootingStar(vertices, colors, triangles, head, CaveVisualRange(ref state, 1.3f, 2.8f), ref state);
            }

            int asteroidCount = Mathf.Clamp(Mathf.RoundToInt(bounds.width / 4.8f), 7, 42);
            for (int asteroid = 0; asteroid < asteroidCount; asteroid++)
            {
                Vector2 center = new Vector2(
                    CaveVisualRange(ref state, bounds.xMin + 1f, bounds.xMax - 1f),
                    CaveVisualRange(ref state, bounds.yMin + 1f, bounds.yMax - 1f));
                AppendSpaceAsteroid(
                    vertices,
                    colors,
                    triangles,
                    center,
                    CaveVisualRange(ref state, 0.16f, 0.5f),
                    ref state);
            }

            int vistaCount = Mathf.Clamp(Mathf.CeilToInt(bounds.width / 48f), 1, 5);
            float vistaSpan = bounds.width / vistaCount;
            for (int vista = 0; vista < vistaCount; vista++)
            {
                float segmentLeft = bounds.xMin + vistaSpan * vista;
                int motif = vista % 4;
                if (motif == 0)
                {
                    AppendSpaceEarth(
                        vertices,
                        colors,
                        triangles,
                        new Vector2(segmentLeft + vistaSpan * 0.13f, bounds.yMin + bounds.height * 0.13f),
                        Mathf.Clamp(bounds.height * 0.24f, 3.3f, 6.2f),
                        ref state);
                }
                else if (motif == 1)
                {
                    AppendSpaceRingedPlanet(
                        vertices,
                        colors,
                        triangles,
                        new Vector2(segmentLeft + vistaSpan * 0.56f, bounds.yMin + bounds.height * 0.64f),
                        Mathf.Clamp(bounds.height * 0.11f, 1.5f, 3.2f),
                        ref state);
                }
                else if (motif == 2)
                {
                    AppendSpaceMoon(
                        vertices,
                        colors,
                        triangles,
                        new Vector2(segmentLeft + vistaSpan * 0.73f, bounds.yMin + bounds.height * 0.74f),
                        Mathf.Clamp(bounds.height * 0.13f, 1.7f, 3.5f),
                        ref state);
                }
                else
                {
                    AppendSpaceBluePlanet(
                        vertices,
                        colors,
                        triangles,
                        new Vector2(segmentLeft + vistaSpan * 0.52f, bounds.yMin + bounds.height * 0.38f),
                        Mathf.Clamp(bounds.height * 0.1f, 1.4f, 2.8f),
                        ref state);
                }
            }
            CreatePencilMesh(parent, "Space Hand Drawn Planets Stars And Asteroids", vertices, colors, triangles, -88);
        }

        private static void AppendSpaceDisc(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 center,
            float radius,
            Color fill,
            Color outline,
            ref uint state,
            float xScale = 1f,
            int segmentCount = 28)
        {
            segmentCount = Mathf.Clamp(segmentCount, 12, 40);
            Vector2[] edge = new Vector2[segmentCount];
            float phase = CaveVisualRange(ref state, -0.08f, 0.08f);
            for (int point = 0; point < segmentCount; point++)
            {
                float angle = phase + point * Mathf.PI * 2f / segmentCount;
                float wobble = 1f + CaveVisualRange(ref state, -0.035f, 0.035f);
                edge[point] = center + new Vector2(
                    Mathf.Cos(angle) * radius * xScale * wobble,
                    Mathf.Sin(angle) * radius * wobble);
            }
            AppendFilledCavePolygon(vertices, colors, triangles, edge, fill);
            for (int point = 0; point < edge.Length; point++)
            {
                AppendPencilQuad(vertices, colors, triangles, edge[point], edge[(point + 1) % edge.Length], Mathf.Max(0.025f, radius * 0.025f), outline);
            }
        }

        private static void AppendSpaceEllipseStroke(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 center,
            float radiusX,
            float radiusY,
            float width,
            Color color,
            ref uint state,
            float angleDegrees = 0f,
            int segmentCount = 30)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            Vector2 previous = Vector2.zero;
            for (int point = 0; point <= segmentCount; point++)
            {
                float angle = point * Mathf.PI * 2f / segmentCount;
                Vector2 local = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
                local *= 1f + CaveVisualRange(ref state, -0.018f, 0.018f);
                Vector2 current = center + new Vector2(local.x * cos - local.y * sin, local.x * sin + local.y * cos);
                if (point > 0)
                {
                    AppendPencilQuad(vertices, colors, triangles, previous, current, width * CaveVisualRange(ref state, 0.86f, 1.14f), color);
                }
                previous = current;
            }
        }

        private static void AppendSpaceEarth(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 center,
            float radius,
            ref uint state)
        {
            AppendSpaceDisc(vertices, colors, triangles, center, radius,
                new Color(0.16f, 0.56f, 0.94f, 0.66f),
                new Color(0.45f, 0.83f, 1f, 0.82f), ref state, 1f, 34);

            Color land = new Color(0.28f, 0.76f, 0.44f, 0.72f);
            Color landInk = new Color(0.12f, 0.45f, 0.3f, 0.72f);
            Vector2[][] continents =
            {
                new[]
                {
                    center + new Vector2(-0.78f, 0.32f) * radius,
                    center + new Vector2(-0.55f, 0.66f) * radius,
                    center + new Vector2(-0.18f, 0.55f) * radius,
                    center + new Vector2(-0.06f, 0.24f) * radius,
                    center + new Vector2(-0.37f, 0.02f) * radius,
                    center + new Vector2(-0.42f, -0.42f) * radius,
                    center + new Vector2(-0.67f, -0.2f) * radius
                },
                new[]
                {
                    center + new Vector2(0.06f, 0.63f) * radius,
                    center + new Vector2(0.53f, 0.53f) * radius,
                    center + new Vector2(0.76f, 0.23f) * radius,
                    center + new Vector2(0.42f, 0.05f) * radius,
                    center + new Vector2(0.35f, -0.38f) * radius,
                    center + new Vector2(0.04f, -0.57f) * radius,
                    center + new Vector2(-0.08f, -0.18f) * radius,
                    center + new Vector2(0.16f, 0.12f) * radius
                },
                new[]
                {
                    center + new Vector2(0.52f, -0.4f) * radius,
                    center + new Vector2(0.78f, -0.54f) * radius,
                    center + new Vector2(0.66f, -0.75f) * radius,
                    center + new Vector2(0.38f, -0.64f) * radius
                }
            };
            for (int continent = 0; continent < continents.Length; continent++)
            {
                AppendFilledCavePolygon(vertices, colors, triangles, continents[continent], land);
                for (int point = 0; point < continents[continent].Length; point++)
                {
                    AppendPencilQuad(vertices, colors, triangles,
                        continents[continent][point],
                        continents[continent][(point + 1) % continents[continent].Length],
                        Mathf.Max(0.018f, radius * 0.014f), landInk);
                }
            }

            for (int stroke = 0; stroke < 8; stroke++)
            {
                float y = CaveVisualRange(ref state, -0.72f, 0.72f) * radius;
                float half = Mathf.Sqrt(Mathf.Max(0.01f, radius * radius - y * y)) * CaveVisualRange(ref state, 0.42f, 0.86f);
                AppendCaveWobblyStroke(vertices, colors, triangles,
                    center + new Vector2(-half, y), center + new Vector2(half, y + CaveVisualRange(ref state, -0.08f, 0.08f) * radius),
                    Mathf.Max(0.012f, radius * 0.009f), new Color(0.72f, 0.94f, 1f, 0.16f), radius * 0.01f, ref state);
            }
        }

        private static void AppendSpaceRingedPlanet(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 center,
            float radius,
            ref uint state)
        {
            Color ring = new Color(0.86f, 0.52f, 1f, 0.68f);
            AppendSpaceEllipseStroke(vertices, colors, triangles, center, radius * 1.75f, radius * 0.45f, Mathf.Max(0.08f, radius * 0.1f), ring, ref state, -9f);
            AppendSpaceEllipseStroke(vertices, colors, triangles, center, radius * 1.5f, radius * 0.32f, Mathf.Max(0.025f, radius * 0.03f), new Color(1f, 0.71f, 0.96f, 0.62f), ref state, -9f);
            AppendSpaceDisc(vertices, colors, triangles, center, radius,
                new Color(0.57f, 0.35f, 0.9f, 0.78f),
                new Color(0.83f, 0.62f, 1f, 0.9f), ref state, 1f, 26);
            for (int band = -1; band <= 1; band++)
            {
                float y = band * radius * 0.32f;
                float half = radius * Mathf.Sqrt(Mathf.Max(0.05f, 1f - (y * y) / (radius * radius)));
                AppendCaveWobblyStroke(vertices, colors, triangles,
                    center + new Vector2(-half, y), center + new Vector2(half, y - radius * 0.05f),
                    Mathf.Max(0.018f, radius * 0.025f), new Color(0.95f, 0.7f, 1f, 0.3f), radius * 0.012f, ref state);
            }
        }

        private static void AppendSpaceMoon(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 center,
            float radius,
            ref uint state)
        {
            AppendSpaceDisc(vertices, colors, triangles, center, radius,
                new Color(0.96f, 0.78f, 0.32f, 0.76f),
                new Color(1f, 0.9f, 0.56f, 0.9f), ref state, 1f, 30);
            for (int crater = 0; crater < 7; crater++)
            {
                float angle = CaveVisualRange(ref state, -Mathf.PI, Mathf.PI);
                float distance = CaveVisualRange(ref state, 0.08f, 0.65f) * radius;
                float craterRadius = CaveVisualRange(ref state, 0.08f, 0.2f) * radius;
                AppendSpaceDisc(vertices, colors, triangles,
                    center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance,
                    craterRadius,
                    new Color(0.56f, 0.43f, 0.3f, 0.2f),
                    new Color(0.62f, 0.48f, 0.32f, 0.34f), ref state, 1.2f, 12);
            }
        }

        private static void AppendSpaceBluePlanet(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 center,
            float radius,
            ref uint state)
        {
            AppendSpaceDisc(vertices, colors, triangles, center, radius,
                new Color(0.28f, 0.52f, 0.92f, 0.72f),
                new Color(0.48f, 0.78f, 1f, 0.84f), ref state, 1f, 26);
            for (int band = 0; band < 4; band++)
            {
                float y = Mathf.Lerp(-0.58f, 0.58f, band / 3f) * radius;
                float half = radius * Mathf.Sqrt(Mathf.Max(0.05f, 1f - (y * y) / (radius * radius)));
                AppendCaveWobblyStroke(vertices, colors, triangles,
                    center + new Vector2(-half, y), center + new Vector2(half, y + CaveVisualRange(ref state, -0.08f, 0.08f) * radius),
                    Mathf.Max(0.016f, radius * 0.023f), new Color(0.58f, 0.9f, 1f, 0.34f), radius * 0.018f, ref state);
            }
        }

        private static void AppendSpaceAsteroid(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 center,
            float radius,
            ref uint state)
        {
            int pointCount = 8 + Mathf.FloorToInt(CaveVisual01(ref state) * 4f);
            Vector2[] edge = new Vector2[pointCount];
            for (int point = 0; point < pointCount; point++)
            {
                float angle = point * Mathf.PI * 2f / pointCount;
                float pointRadius = radius * CaveVisualRange(ref state, 0.7f, 1.18f);
                edge[point] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * pointRadius;
            }
            Color fill = new Color(0.52f, 0.55f, 0.62f, 0.5f);
            Color outline = new Color(0.14f, 0.17f, 0.26f, 0.72f);
            AppendFilledCavePolygon(vertices, colors, triangles, edge, fill);
            for (int point = 0; point < pointCount; point++)
            {
                AppendPencilQuad(vertices, colors, triangles, edge[point], edge[(point + 1) % pointCount], Mathf.Max(0.012f, radius * 0.055f), outline);
            }
            for (int crater = 0; crater < 2; crater++)
            {
                Vector2 offset = new Vector2(CaveVisualRange(ref state, -0.3f, 0.3f), CaveVisualRange(ref state, -0.3f, 0.3f)) * radius;
                AppendSpaceDisc(vertices, colors, triangles, center + offset, radius * CaveVisualRange(ref state, 0.11f, 0.2f),
                    new Color(0.16f, 0.18f, 0.25f, 0.24f), outline * 0.55f, ref state, 1.2f, 10);
            }
        }

        private static void AppendSpaceShootingStar(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 head,
            float length,
            ref uint state)
        {
            Vector2 direction = new Vector2(-1f, CaveVisualRange(ref state, 0.28f, 0.58f)).normalized;
            Vector2 tail = head + direction * length;
            Color warm = new Color(1f, 0.86f, 0.34f, 0.83f);
            AppendPencilQuad(vertices, colors, triangles, tail, head, 0.055f, warm);
            AppendPencilQuad(vertices, colors, triangles, tail + Vector2.up * 0.14f, head - direction * 0.22f, 0.022f, warm * 0.58f);
            AppendPencilQuad(vertices, colors, triangles, head - Vector2.right * 0.14f, head + Vector2.right * 0.14f, 0.045f, warm);
            AppendPencilQuad(vertices, colors, triangles, head - Vector2.up * 0.14f, head + Vector2.up * 0.14f, 0.045f, warm);
        }

        private static void AddSpaceInfrastructure(
            Transform parent,
            Rect bounds,
            IList<StageObjectData> objects,
            string stageId,
            int seed)
        {
            uint state = CreateCaveVisualState(seed);
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            float left = bounds.xMin + 0.65f;
            float right = bounds.xMax - 0.65f;
            float top = bounds.yMax - 0.65f;
            float bottom = bounds.yMin + 0.65f;
            if (stageId == "4-3")
            {
                TryGetNightCityBoundaryInterior(objects, ref left, ref right, ref top, ref bottom);
            }
            else if (stageId == "15-2")
            {
                // This stage scrolls horizontally with a deliberately shorter
                // vertical camera. Keep its window rails just inside that view.
                top = Mathf.Min(top, 9.55f);
                bottom = Mathf.Max(bottom, -8.15f);
            }

            Color panel = new Color(0.38f, 0.48f, 0.62f, 0.74f);
            Color edge = new Color(0.035f, 0.055f, 0.11f, 0.92f);
            Color highlight = new Color(0.72f, 0.84f, 0.94f, 0.32f);
            Color cyan = new Color(0.24f, 0.8f, 1f, 0.82f);
            Color amber = new Color(1f, 0.78f, 0.22f, 0.88f);

            AppendSpaceStationBeam(vertices, colors, triangles,
                new Vector2(left, top), new Vector2(right, top), 0.78f,
                panel, edge, highlight, true, ref state);
            AppendSpaceStationBeam(vertices, colors, triangles,
                new Vector2(left, bottom), new Vector2(right, bottom), 0.72f,
                panel * 0.92f, edge, highlight, true, ref state);
            AppendSpaceStationBeam(vertices, colors, triangles,
                new Vector2(left, bottom), new Vector2(left, top), 0.76f,
                panel, edge, highlight, false, ref state);
            AppendSpaceStationBeam(vertices, colors, triangles,
                new Vector2(right, bottom), new Vector2(right, top), 0.76f,
                panel, edge, highlight, false, ref state);

            int ribCount = Mathf.Clamp(Mathf.CeilToInt((right - left) / 28f), 1, 9);
            for (int rib = 1; rib < ribCount; rib++)
            {
                float x = Mathf.Lerp(left, right, rib / (float)ribCount)
                    + CaveVisualRange(ref state, -0.32f, 0.32f);
                AppendSpaceStationBeam(vertices, colors, triangles,
                    new Vector2(x, bottom + 0.25f), new Vector2(x, top - 0.25f),
                    CaveVisualRange(ref state, 0.38f, 0.54f),
                    panel * 0.62f, edge * 0.72f, highlight * 0.74f, false, ref state);
            }

            int lightCount = Mathf.Clamp(Mathf.RoundToInt((right - left) / 12f), 3, 22);
            for (int light = 0; light < lightCount; light++)
            {
                float t = (light + 0.5f) / lightCount;
                float x = Mathf.Lerp(left + 1.2f, right - 1.2f, t)
                    + CaveVisualRange(ref state, -0.28f, 0.28f);
                Color lightColor = light % 4 == 0 ? cyan : amber;
                AppendSpaceLightBar(
                    vertices,
                    colors,
                    triangles,
                    new Vector2(x, top - 0.47f),
                    CaveVisualRange(ref state, 0.55f, 1.05f),
                    lightColor,
                    ref state);
            }

            int cableSections = Mathf.Clamp(Mathf.CeilToInt((right - left) / 16f), 2, 16);
            for (int cable = 0; cable < cableSections; cable++)
            {
                float x0 = Mathf.Lerp(left + 0.45f, right - 0.45f, cable / (float)cableSections);
                float x1 = Mathf.Lerp(left + 0.45f, right - 0.45f, (cable + 1f) / cableSections);
                AppendSpaceCableArc(
                    vertices,
                    colors,
                    triangles,
                    new Vector2(x0, top - 0.38f),
                    new Vector2(x1, top - 0.38f),
                    CaveVisualRange(ref state, 0.28f, 0.8f),
                    edge,
                    ref state);
            }

            AppendSpaceJointPlate(vertices, colors, triangles, new Vector2(left, top), panel, edge, cyan, ref state);
            AppendSpaceJointPlate(vertices, colors, triangles, new Vector2(right, top), panel, edge, amber, ref state);
            AppendSpaceJointPlate(vertices, colors, triangles, new Vector2(left, bottom), panel, edge, amber, ref state);
            AppendSpaceJointPlate(vertices, colors, triangles, new Vector2(right, bottom), panel, edge, cyan, ref state);

            CreatePencilMesh(parent, "Space Station Window Frame Lights And Cables", vertices, colors, triangles, -42);
        }

        private static void AppendSpaceStationBeam(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 from,
            Vector2 to,
            float width,
            Color fill,
            Color outline,
            Color highlight,
            bool warningStripes,
            ref uint state)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length <= 0.05f)
            {
                return;
            }

            Vector2 direction = delta / length;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            Vector2[] body =
            {
                from + normal * width * 0.5f,
                to + normal * width * 0.5f,
                to - normal * width * 0.5f,
                from - normal * width * 0.5f
            };
            AppendFilledCavePolygon(vertices, colors, triangles, body, fill);
            for (int edgeIndex = 0; edgeIndex < body.Length; edgeIndex++)
            {
                AppendCaveWobblyStroke(vertices, colors, triangles,
                    body[edgeIndex], body[(edgeIndex + 1) % body.Length],
                    0.055f, outline, 0.018f, ref state);
            }
            AppendCaveWobblyStroke(vertices, colors, triangles,
                from + normal * width * 0.27f,
                to + normal * width * 0.27f,
                0.025f, highlight, 0.012f, ref state);

            int panelCount = Mathf.Clamp(Mathf.CeilToInt(length / 4.6f), 1, 52);
            for (int panelIndex = 0; panelIndex <= panelCount; panelIndex++)
            {
                float t = panelIndex / (float)panelCount;
                Vector2 center = Vector2.Lerp(from, to, t);
                AppendPencilQuad(vertices, colors, triangles,
                    center + normal * width * 0.46f,
                    center - normal * width * 0.46f,
                    0.026f,
                    new Color(outline.r, outline.g, outline.b, 0.58f));
                if (panelIndex < panelCount && warningStripes && (panelIndex & 3) == 1)
                {
                    Vector2 stripeCenter = Vector2.Lerp(from, to, (panelIndex + 0.5f) / panelCount);
                    float halfLength = Mathf.Min(length / panelCount * 0.28f, 0.75f);
                    Color warning = new Color(1f, 0.72f, 0.12f, 0.78f);
                    for (int stripe = -1; stripe <= 1; stripe++)
                    {
                        Vector2 centerOffset = direction * (stripe * halfLength * 0.55f);
                        AppendPencilQuad(vertices, colors, triangles,
                            stripeCenter + centerOffset - direction * halfLength * 0.18f + normal * width * 0.31f,
                            stripeCenter + centerOffset + direction * halfLength * 0.18f - normal * width * 0.31f,
                            0.06f,
                            warning);
                    }
                }
            }

            int rivetCount = Mathf.Clamp(panelCount * 2 + 1, 3, 75);
            for (int rivet = 0; rivet < rivetCount; rivet++)
            {
                float t = rivet / (rivetCount - 1f);
                Vector2 center = Vector2.Lerp(from, to, t)
                    + normal * width * ((rivet & 1) == 0 ? 0.36f : -0.36f);
                AppendPencilQuad(vertices, colors, triangles, center - direction * 0.035f, center + direction * 0.035f, 0.028f, outline * 0.78f);
            }
        }

        private static void AppendSpaceLightBar(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 center,
            float halfWidth,
            Color light,
            ref uint state)
        {
            Vector2 left = center - Vector2.right * halfWidth;
            Vector2 right = center + Vector2.right * halfWidth;
            AppendPencilQuad(vertices, colors, triangles, left, right, 0.22f,
                new Color(light.r, light.g, light.b, 0.12f));
            AppendCaveWobblyStroke(vertices, colors, triangles, left, right, 0.095f,
                new Color(0.025f, 0.04f, 0.09f, 0.94f), 0.012f, ref state);
            AppendCaveWobblyStroke(vertices, colors, triangles,
                left + Vector2.right * 0.08f, right - Vector2.right * 0.08f,
                0.052f, light, 0.009f, ref state);
        }

        private static void AppendSpaceCableArc(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 from,
            Vector2 to,
            float sag,
            Color color,
            ref uint state)
        {
            Vector2 previous = from;
            const int Samples = 8;
            for (int sample = 1; sample <= Samples; sample++)
            {
                float t = sample / (float)Samples;
                Vector2 next = Vector2.Lerp(from, to, t)
                    + Vector2.down * (Mathf.Sin(t * Mathf.PI) * sag)
                    + Vector2.up * CaveVisualRange(ref state, -0.018f, 0.018f);
                AppendPencilQuad(vertices, colors, triangles, previous, next,
                    CaveVisualRange(ref state, 0.025f, 0.044f), color);
                previous = next;
            }
        }

        private static void AppendSpaceJointPlate(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 center,
            Color fill,
            Color outline,
            Color indicator,
            ref uint state)
        {
            Vector2[] plate =
            {
                center + new Vector2(-0.58f, -0.5f),
                center + new Vector2(0.55f, -0.47f),
                center + new Vector2(0.59f, 0.48f),
                center + new Vector2(-0.54f, 0.52f)
            };
            AppendFilledCavePolygon(vertices, colors, triangles, plate, fill);
            for (int edgeIndex = 0; edgeIndex < plate.Length; edgeIndex++)
            {
                AppendPencilQuad(vertices, colors, triangles,
                    plate[edgeIndex], plate[(edgeIndex + 1) % plate.Length], 0.05f, outline);
            }
            AppendPencilQuad(vertices, colors, triangles,
                center + new Vector2(-0.25f, 0f), center + new Vector2(0.25f, 0f), 0.055f, indicator);
            Vector2[] rivets =
            {
                center + new Vector2(-0.39f, -0.32f),
                center + new Vector2(0.39f, -0.32f),
                center + new Vector2(-0.39f, 0.32f),
                center + new Vector2(0.39f, 0.32f)
            };
            for (int rivet = 0; rivet < rivets.Length; rivet++)
            {
                AppendPencilQuad(vertices, colors, triangles,
                    rivets[rivet] - Vector2.right * 0.035f,
                    rivets[rivet] + Vector2.right * 0.035f,
                    0.025f,
                    outline * CaveVisualRange(ref state, 0.72f, 0.9f));
            }
        }

        private static void AddNightCityBackdrop(Transform parent, Rect bounds, int seed)
        {
            Rect colorBounds = GetBackdropColorFillBounds(bounds);
            GameObject wash = new GameObject("Night City Crayon Sky Wash");
            wash.transform.SetParent(parent, false);
            wash.transform.localPosition = new Vector3(colorBounds.center.x, colorBounds.center.y, 0f);
            wash.transform.localScale = new Vector3(colorBounds.width, colorBounds.height, 1f);
            SpriteRenderer washRenderer = wash.AddComponent<SpriteRenderer>();
            washRenderer.sprite = GetSquareSprite();
            washRenderer.color = NightCitySkyColor;
            washRenderer.sortingOrder = -99;

            Color[] depthBands =
            {
                new Color(0.18f, 0.24f, 0.42f, 0.22f),
                new Color(0.11f, 0.17f, 0.32f, 0.2f),
                new Color(0.055f, 0.085f, 0.18f, 0.22f)
            };
            float bandHeight = bounds.height / depthBands.Length;
            for (int band = 0; band < depthBands.Length; band++)
            {
                float bandTop = band == 0
                    ? colorBounds.yMax
                    : bounds.yMax - bandHeight * band;
                float bandBottom = band == depthBands.Length - 1
                    ? colorBounds.yMin
                    : bounds.yMax - bandHeight * (band + 1f);
                GameObject layer = new GameObject("Night City Sky Depth " + band);
                layer.transform.SetParent(parent, false);
                layer.transform.localPosition = new Vector3(
                    colorBounds.center.x,
                    (bandTop + bandBottom) * 0.5f,
                    0f);
                layer.transform.localScale = new Vector3(colorBounds.width, bandTop - bandBottom + 0.12f, 1f);
                SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
                renderer.sprite = GetSquareSprite();
                renderer.color = depthBands[band];
                renderer.sortingOrder = -98;
            }

            uint state = CreateCaveVisualState(seed);
            List<Vector3> skyVertices = new List<Vector3>();
            List<Color> skyColors = new List<Color>();
            List<int> skyTriangles = new List<int>();
            int hatchCount = Mathf.Clamp(Mathf.RoundToInt(bounds.width * bounds.height / 20f), 80, 380);
            for (int i = 0; i < hatchCount; i++)
            {
                Vector2 from = new Vector2(
                    CaveVisualRange(ref state, bounds.xMin, bounds.xMax),
                    CaveVisualRange(ref state, bounds.yMin, bounds.yMax));
                float length = CaveVisualRange(ref state, 0.55f, 2.8f);
                Vector2 to = from + new Vector2(length, length * CaveVisualRange(ref state, 0.08f, 0.2f));
                to.x = Mathf.Min(to.x, bounds.xMax);
                to.y = Mathf.Min(to.y, bounds.yMax);
                AppendPencilQuad(
                    skyVertices,
                    skyColors,
                    skyTriangles,
                    from,
                    to,
                    CaveVisualRange(ref state, 0.012f, 0.026f),
                    new Color(0.36f, 0.47f, 0.7f, CaveVisualRange(ref state, 0.035f, 0.075f)));
            }

            int starCount = Mathf.Clamp(Mathf.RoundToInt(bounds.width / 4.2f), 12, 58);
            Color starColor = new Color(1f, 0.82f, 0.24f, 0.68f);
            for (int star = 0; star < starCount; star++)
            {
                Vector2 center = new Vector2(
                    CaveVisualRange(ref state, bounds.xMin + 0.7f, bounds.xMax - 0.7f),
                    CaveVisualRange(ref state, bounds.center.y, bounds.yMax - 0.55f));
                float radius = CaveVisualRange(ref state, 0.045f, 0.13f);
                AppendPencilQuad(skyVertices, skyColors, skyTriangles, center - Vector2.right * radius, center + Vector2.right * radius, 0.025f, starColor);
                AppendPencilQuad(skyVertices, skyColors, skyTriangles, center - Vector2.up * radius, center + Vector2.up * radius, 0.025f, starColor);
                if ((star & 3) == 0)
                {
                    Vector2 diagonal = new Vector2(radius * 0.72f, radius * 0.72f);
                    AppendPencilQuad(skyVertices, skyColors, skyTriangles, center - diagonal, center + diagonal, 0.015f, starColor * 0.78f);
                }
            }
            CreatePencilMesh(parent, "Night City Sky Pencil Texture", skyVertices, skyColors, skyTriangles, -97);

            AddNightCityCrescentMoon(parent, bounds, ref state);
            AddNightCitySkylineLayer(parent, bounds, 0.72f, 0.23f, -95, ref state);
            AddNightCitySkylineLayer(parent, bounds, 0.5f, 0.4f, -92, ref state);
        }

        private static void AddNightCityCrescentMoon(Transform parent, Rect bounds, ref uint state)
        {
            float radius = Mathf.Clamp(Mathf.Min(bounds.width, bounds.height) * 0.035f, 0.55f, 1.45f);
            Vector2 center = new Vector2(
                Mathf.Lerp(bounds.xMin, bounds.xMax, CaveVisualRange(ref state, 0.16f, 0.3f)),
                Mathf.Lerp(bounds.yMin, bounds.yMax, CaveVisualRange(ref state, 0.73f, 0.86f)));

            GameObject moon = new GameObject("Night City Crayon Moon");
            moon.transform.SetParent(parent, false);
            moon.transform.localPosition = center;
            moon.transform.localScale = Vector3.one * radius * 2f;
            SpriteRenderer moonRenderer = moon.AddComponent<SpriteRenderer>();
            moonRenderer.sprite = GetCircleSprite();
            moonRenderer.color = new Color(1f, 0.77f, 0.18f, 0.82f);
            moonRenderer.sortingOrder = -96;

            GameObject cutout = new GameObject("Night City Moon Pencil Cutout");
            cutout.transform.SetParent(parent, false);
            cutout.transform.localPosition = center + new Vector2(radius * 0.42f, radius * 0.2f);
            cutout.transform.localScale = Vector3.one * radius * 1.85f;
            SpriteRenderer cutoutRenderer = cutout.AddComponent<SpriteRenderer>();
            cutoutRenderer.sprite = GetCircleSprite();
            cutoutRenderer.color = new Color(0.095f, 0.145f, 0.27f, 0.98f);
            cutoutRenderer.sortingOrder = -95;
        }

        private static void AddNightCitySkylineLayer(
            Transform parent,
            Rect bounds,
            float maximumHeightRatio,
            float opacity,
            int sortingOrder,
            ref uint state)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            Color fill = sortingOrder < -93
                ? new Color(0.2f, 0.26f, 0.39f, opacity)
                : new Color(0.09f, 0.13f, 0.23f, opacity + 0.14f);
            Color outline = new Color(0.045f, 0.07f, 0.13f, Mathf.Clamp01(opacity + 0.23f));
            Color window = new Color(1f, 0.73f, 0.18f, sortingOrder < -93 ? 0.24f : 0.42f);
            float baseY = bounds.yMin + bounds.height * (sortingOrder < -93 ? 0.04f : 0.015f);
            float cursor = bounds.xMin - 0.8f;
            int buildingIndex = 0;
            while (cursor < bounds.xMax + 0.4f && buildingIndex < 96)
            {
                float width = CaveVisualRange(ref state, 2.1f, 5.2f);
                float minHeightRatio = sortingOrder < -93 ? 0.28f : 0.16f;
                float height = bounds.height * CaveVisualRange(ref state, minHeightRatio, maximumHeightRatio);
                float left = cursor;
                float right = Mathf.Min(bounds.xMax + 0.8f, cursor + width);
                float topLeft = baseY + height + CaveVisualRange(ref state, -0.22f, 0.22f);
                float topRight = baseY + height + CaveVisualRange(ref state, -0.22f, 0.22f);
                Vector2[] building =
                {
                    new Vector2(left, baseY),
                    new Vector2(right, baseY),
                    new Vector2(right, topRight),
                    new Vector2(left, topLeft)
                };
                AppendFilledCavePolygon(vertices, colors, triangles, building, fill);
                AppendCaveWobblyStroke(vertices, colors, triangles, building[3], building[2], 0.035f, outline, 0.035f, ref state);
                AppendCaveWobblyStroke(vertices, colors, triangles, building[0], building[3], 0.028f, outline, 0.025f, ref state);
                AppendCaveWobblyStroke(vertices, colors, triangles, building[1], building[2], 0.028f, outline, 0.025f, ref state);

                int litWindows = Mathf.Clamp(Mathf.RoundToInt(width * height * 0.045f), 2, 7);
                for (int lit = 0; lit < litWindows; lit++)
                {
                    float windowWidth = CaveVisualRange(ref state, 0.12f, 0.24f);
                    float windowHeight = CaveVisualRange(ref state, 0.17f, 0.34f);
                    float windowX = CaveVisualRange(ref state, left + 0.32f, right - 0.32f);
                    float windowY = CaveVisualRange(ref state, baseY + 0.45f, Mathf.Min(topLeft, topRight) - 0.45f);
                    Vector2[] pane =
                    {
                        new Vector2(windowX - windowWidth, windowY - windowHeight),
                        new Vector2(windowX + windowWidth, windowY - windowHeight),
                        new Vector2(windowX + windowWidth, windowY + windowHeight),
                        new Vector2(windowX - windowWidth, windowY + windowHeight)
                    };
                    AppendFilledCavePolygon(vertices, colors, triangles, pane, window);
                }

                if (buildingIndex % 5 == 1)
                {
                    float roofY = Mathf.Max(topLeft, topRight);
                    float antennaX = Mathf.Lerp(left, right, CaveVisualRange(ref state, 0.32f, 0.7f));
                    AppendPencilQuad(vertices, colors, triangles, new Vector2(antennaX, roofY), new Vector2(antennaX, roofY + CaveVisualRange(ref state, 0.5f, 1.5f)), 0.035f, outline);
                    AppendPencilQuad(vertices, colors, triangles, new Vector2(antennaX - 0.22f, roofY + 0.34f), new Vector2(antennaX + 0.22f, roofY + 0.34f), 0.025f, outline);
                }
                else if (buildingIndex % 7 == 3)
                {
                    float roofY = Mathf.Max(topLeft, topRight);
                    float tankCenter = (left + right) * 0.5f;
                    Vector2[] tank =
                    {
                        new Vector2(tankCenter - 0.42f, roofY + 0.28f),
                        new Vector2(tankCenter + 0.42f, roofY + 0.28f),
                        new Vector2(tankCenter + 0.34f, roofY + 0.88f),
                        new Vector2(tankCenter - 0.34f, roofY + 0.88f)
                    };
                    AppendFilledCavePolygon(vertices, colors, triangles, tank, fill * 1.15f);
                    AppendPencilQuad(vertices, colors, triangles, new Vector2(tankCenter - 0.3f, roofY), tank[0], 0.035f, outline);
                    AppendPencilQuad(vertices, colors, triangles, new Vector2(tankCenter + 0.3f, roofY), tank[1], 0.035f, outline);
                }

                cursor = right + CaveVisualRange(ref state, 0.18f, 0.7f);
                buildingIndex++;
            }

            CreatePencilMesh(parent, "Night City Skyline Layer " + sortingOrder, vertices, colors, triangles, sortingOrder);
        }

        private static void AddNightCityInfrastructure(
            Transform parent,
            Rect bounds,
            IList<StageObjectData> objects,
            int seed)
        {
            uint state = CreateCaveVisualState(seed);
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            float left = bounds.xMin + 1f;
            float right = bounds.xMax - 1f;
            float top = bounds.yMax - 1f;
            float bottom = bounds.yMin + 1f;
            TryGetNightCityBoundaryInterior(objects, ref left, ref right, ref top, ref bottom);

            Color pipeDark = new Color(0.055f, 0.075f, 0.12f, 0.9f);
            Color pipeFill = new Color(0.28f, 0.36f, 0.49f, 0.78f);
            float pipeY = top - 0.42f;
            AppendNightCityPipeRun(vertices, colors, triangles, new Vector2(left + 0.55f, pipeY), new Vector2(right - 0.55f, pipeY), pipeDark, pipeFill, ref state);
            AppendNightCityPipeRun(vertices, colors, triangles, new Vector2(left + 1.1f, pipeY - 0.42f), new Vector2(right - 1.4f, pipeY - 0.42f), pipeDark * 0.9f, pipeFill * 0.82f, ref state);

            int ventCount = Mathf.Clamp(Mathf.RoundToInt((right - left) / 34f), 1, 8);
            for (int vent = 0; vent < ventCount; vent++)
            {
                float t = (vent + 0.7f) / (ventCount + 0.4f);
                Vector2 center = new Vector2(
                    Mathf.Lerp(left + 1.5f, right - 1.5f, t),
                    top - CaveVisualRange(ref state, 0.8f, 1.2f));
                AppendNightCityVent(vertices, colors, triangles, center, CaveVisualRange(ref state, 0.72f, 1.15f), pipeDark, pipeFill, ref state);
            }

            int ceilingLampCount = Mathf.Clamp(Mathf.RoundToInt((right - left) / 20f), 2, 11);
            for (int lamp = 0; lamp < ceilingLampCount; lamp++)
            {
                float t = (lamp + 0.5f) / ceilingLampCount;
                Vector2 anchor = new Vector2(
                    Mathf.Lerp(left + 1.6f, right - 1.6f, t) + CaveVisualRange(ref state, -0.5f, 0.5f),
                    top - 0.08f);
                AppendNightCityHangingLamp(
                    vertices,
                    colors,
                    triangles,
                    anchor,
                    CaveVisualRange(ref state, 0.8f, 2.15f),
                    ref state);
            }

            int wallLampCount = Mathf.Clamp(Mathf.RoundToInt((top - bottom) / 10f), 1, 4);
            for (int lamp = 0; lamp < wallLampCount; lamp++)
            {
                float y = Mathf.Lerp(bottom + 2f, top - 2.5f, (lamp + 0.55f) / wallLampCount);
                AppendNightCityWallLamp(vertices, colors, triangles, new Vector2(left + 0.06f, y), 1f, ref state);
                AppendNightCityWallLamp(vertices, colors, triangles, new Vector2(right - 0.06f, y + CaveVisualRange(ref state, -0.7f, 0.7f)), -1f, ref state);
            }

            List<Rect> parts = new List<Rect>();
            int underFloorCount = 0;
            int platformLampCount = 0;
            if (objects != null)
            {
                for (int objectIndex = 0; objectIndex < objects.Count && underFloorCount < 30; objectIndex++)
                {
                    StageObjectData data = objects[objectIndex];
                    if (data == null || !IsNightCityInfrastructureTerrainType(data.type) || !IsAxisAligned(data.rotation))
                    {
                        continue;
                    }

                    parts.Clear();
                    AppendStageRects(data, parts);
                    for (int partIndex = 0; partIndex < parts.Count && underFloorCount < 30; partIndex++)
                    {
                        Rect rect = parts[partIndex];
                        if (rect.width < 4.5f || rect.height > 4.2f)
                        {
                            continue;
                        }

                        float inset = Mathf.Min(0.45f, rect.width * 0.08f);
                        float y = rect.yMin - CaveVisualRange(ref state, 0.18f, 0.34f);
                        AppendNightCityPipeRun(
                            vertices,
                            colors,
                            triangles,
                            new Vector2(rect.xMin + inset, y),
                            new Vector2(rect.xMax - inset, y),
                            pipeDark,
                            pipeFill,
                            ref state);
                        if (rect.width > 7f && (underFloorCount & 1) == 0)
                        {
                            AppendNightCityVent(
                                vertices,
                                colors,
                                triangles,
                                new Vector2(rect.center.x + CaveVisualRange(ref state, -rect.width * 0.22f, rect.width * 0.22f), y - 0.25f),
                                CaveVisualRange(ref state, 0.55f, 0.9f),
                                pipeDark,
                                pipeFill,
                                ref state);
                        }

                        if (platformLampCount < 12
                            && rect.width > 6.5f
                            && rect.yMin > bottom + 3f
                            && CaveVisual01(ref state) > 0.42f)
                        {
                            AppendNightCityHangingLamp(
                                vertices,
                                colors,
                                triangles,
                                new Vector2(
                                    rect.center.x + CaveVisualRange(ref state, -rect.width * 0.28f, rect.width * 0.28f),
                                    rect.yMin - 0.04f),
                                CaveVisualRange(ref state, 0.55f, 1.25f),
                                ref state);
                            platformLampCount++;
                        }
                        underFloorCount++;
                    }
                }
            }

            // Loose utility cables use a sagging pencil line, separate from the
            // rigid ducts, so the ceiling feels assembled rather than tiled.
            int cableSections = Mathf.Clamp(Mathf.CeilToInt((right - left) / 18f), 2, 14);
            for (int section = 0; section < cableSections; section++)
            {
                float x0 = Mathf.Lerp(left + 0.7f, right - 0.7f, section / (float)cableSections);
                float x1 = Mathf.Lerp(left + 0.7f, right - 0.7f, (section + 1f) / cableSections);
                Vector2 previous = new Vector2(x0, top - 0.16f);
                for (int sample = 1; sample <= 7; sample++)
                {
                    float t = sample / 7f;
                    float sag = Mathf.Sin(t * Mathf.PI) * CaveVisualRange(ref state, 0.22f, 0.62f);
                    Vector2 next = new Vector2(Mathf.Lerp(x0, x1, t), top - 0.16f - sag);
                    AppendPencilQuad(vertices, colors, triangles, previous, next, 0.035f, new Color(0.035f, 0.04f, 0.065f, 0.82f));
                    previous = next;
                }
            }

            CreatePencilMesh(parent, "Night City Lamps Ducts And Cables", vertices, colors, triangles, -24);
        }

        private static bool TryGetNightCityBoundaryInterior(
            IList<StageObjectData> objects,
            ref float left,
            ref float right,
            ref float top,
            ref float bottom)
        {
            if (objects == null)
            {
                return false;
            }

            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData data = objects[i];
                if (data == null || data.type != StageObjectType.StageBoundary)
                {
                    continue;
                }

                float inset = GetStageBoundaryInteriorThickness(data);
                left = data.position.x - data.size.x * 0.5f + inset;
                right = data.position.x + data.size.x * 0.5f - inset;
                top = data.position.y + data.size.y * 0.5f - inset;
                bottom = data.position.y - data.size.y * 0.5f + inset;
                return true;
            }
            return false;
        }

        private static bool IsNightCityInfrastructureTerrainType(StageObjectType type)
        {
            return type == StageObjectType.Platform
                || type == StageObjectType.Wall
                || type == StageObjectType.Ceiling
                || type == StageObjectType.HalfPlatform
                || type == StageObjectType.OneWayPlatform;
        }

        private static void AppendNightCityPipeRun(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 from,
            Vector2 to,
            Color dark,
            Color fill,
            ref uint state)
        {
            if ((to - from).sqrMagnitude < 0.04f)
            {
                return;
            }

            AppendCaveWobblyStroke(vertices, colors, triangles, from, to, 0.19f, dark, 0.025f, ref state);
            AppendCaveWobblyStroke(vertices, colors, triangles, from, to, 0.105f, fill, 0.017f, ref state);
            float length = Vector2.Distance(from, to);
            int clampCount = Mathf.Clamp(Mathf.FloorToInt(length / 4.5f), 1, 10);
            Vector2 direction = (to - from).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            for (int clamp = 1; clamp <= clampCount; clamp++)
            {
                Vector2 center = Vector2.Lerp(from, to, clamp / (clampCount + 1f));
                AppendPencilQuad(vertices, colors, triangles, center - normal * 0.16f, center + normal * 0.16f, 0.045f, dark);
            }
        }

        private static void AppendNightCityVent(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 center,
            float scale,
            Color dark,
            Color fill,
            ref uint state)
        {
            float halfWidth = 0.72f * scale;
            float halfHeight = 0.42f * scale;
            Vector2[] body =
            {
                center + new Vector2(-halfWidth, -halfHeight),
                center + new Vector2(halfWidth, -halfHeight + CaveVisualRange(ref state, -0.025f, 0.025f)),
                center + new Vector2(halfWidth - 0.02f, halfHeight),
                center + new Vector2(-halfWidth + 0.03f, halfHeight - 0.02f)
            };
            AppendFilledCavePolygon(vertices, colors, triangles, body, fill);
            for (int edge = 0; edge < body.Length; edge++)
            {
                AppendPencilQuad(vertices, colors, triangles, body[edge], body[(edge + 1) % body.Length], 0.045f, dark);
            }
            for (int louver = -2; louver <= 2; louver++)
            {
                float y = center.y + louver * halfHeight * 0.3f;
                AppendPencilQuad(
                    vertices,
                    colors,
                    triangles,
                    new Vector2(center.x - halfWidth * 0.66f, y + 0.025f),
                    new Vector2(center.x + halfWidth * 0.66f, y - 0.025f),
                    0.025f,
                    dark * 0.78f);
            }
        }

        private static void AppendNightCityHangingLamp(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 anchor,
            float cableLength,
            ref uint state)
        {
            Color graphite = new Color(0.045f, 0.05f, 0.075f, 0.94f);
            Color metal = new Color(0.24f, 0.28f, 0.36f, 0.92f);
            Color light = new Color(1f, 0.75f, 0.17f, 0.9f);
            Vector2 bulb = anchor + new Vector2(CaveVisualRange(ref state, -0.08f, 0.08f), -cableLength);
            AppendCaveWobblyStroke(vertices, colors, triangles, anchor, bulb + Vector2.up * 0.16f, 0.04f, graphite, 0.025f, ref state);

            Vector2[] shade =
            {
                bulb + new Vector2(-0.16f, 0.16f),
                bulb + new Vector2(0.16f, 0.16f),
                bulb + new Vector2(0.38f, -0.13f),
                bulb + new Vector2(-0.38f, -0.13f)
            };
            AppendFilledCavePolygon(vertices, colors, triangles, shade, metal);
            for (int edge = 0; edge < shade.Length; edge++)
            {
                AppendPencilQuad(vertices, colors, triangles, shade[edge], shade[(edge + 1) % shade.Length], 0.045f, graphite);
            }
            Vector2[] glow =
            {
                bulb + new Vector2(-0.24f, -0.11f),
                bulb + new Vector2(0.24f, -0.11f),
                bulb + new Vector2(1.25f, -2.8f),
                bulb + new Vector2(-1.25f, -2.8f)
            };
            AppendFilledCavePolygon(vertices, colors, triangles, glow, new Color(1f, 0.72f, 0.16f, 0.07f));
            AppendPencilQuad(vertices, colors, triangles, bulb + new Vector2(-0.13f, -0.13f), bulb + new Vector2(0.13f, -0.13f), 0.09f, light);
            AppendPencilQuad(vertices, colors, triangles, bulb + new Vector2(-0.36f, -0.28f), bulb + new Vector2(-0.55f, -0.56f), 0.025f, light * 0.65f);
            AppendPencilQuad(vertices, colors, triangles, bulb + new Vector2(0.36f, -0.28f), bulb + new Vector2(0.55f, -0.56f), 0.025f, light * 0.65f);
        }

        private static void AppendNightCityWallLamp(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 anchor,
            float direction,
            ref uint state)
        {
            Color graphite = new Color(0.045f, 0.05f, 0.075f, 0.94f);
            Color metal = new Color(0.25f, 0.3f, 0.4f, 0.9f);
            Color light = new Color(1f, 0.74f, 0.16f, 0.82f);
            Vector2 bracket = anchor + new Vector2(direction * 0.58f, 0.02f);
            Vector2 bulb = bracket + new Vector2(direction * 0.08f, -0.34f);
            AppendPencilQuad(vertices, colors, triangles, anchor, bracket, 0.07f, graphite);
            AppendPencilQuad(vertices, colors, triangles, bracket, bulb + Vector2.up * 0.12f, 0.05f, graphite);
            Vector2[] shade =
            {
                bulb + new Vector2(-0.22f, 0.13f),
                bulb + new Vector2(0.22f, 0.13f),
                bulb + new Vector2(0.32f, -0.1f),
                bulb + new Vector2(-0.32f, -0.1f)
            };
            AppendFilledCavePolygon(vertices, colors, triangles, shade, metal);
            for (int edge = 0; edge < shade.Length; edge++)
            {
                AppendPencilQuad(vertices, colors, triangles, shade[edge], shade[(edge + 1) % shade.Length], 0.038f, graphite);
            }
            Vector2[] glow =
            {
                bulb + new Vector2(direction * 0.1f, -0.06f),
                bulb + new Vector2(direction * 0.38f, -0.2f),
                bulb + new Vector2(direction * 2.1f, -1.65f),
                bulb + new Vector2(direction * 0.55f, -1.45f)
            };
            AppendFilledCavePolygon(vertices, colors, triangles, glow, new Color(1f, 0.72f, 0.16f, 0.065f));
            AppendPencilQuad(vertices, colors, triangles, bulb + new Vector2(-0.12f, -0.1f), bulb + new Vector2(0.12f, -0.1f), 0.075f, light);
        }

        private static Rect GetFactoryBackdropBounds(string stageId, IList<StageObjectData> objects)
        {
            // Several challenge stages construct their arena in Start(), after
            // the authored stage objects have been loaded. Give those stages a
            // stable visual canvas instead of relying on their nearly-empty JSON.
            switch (stageId)
            {
                case "6-2":
                    return Rect.MinMaxRect(-18f, -7f, 18f, 11f);
                case "8-1":
                    return Rect.MinMaxRect(-27f, -7f, 27f, 14f);
                case "10-3":
                    return Rect.MinMaxRect(-23f, -14f, 23f, 14f);
                case "11-2":
                case "11-3":
                    return Rect.MinMaxRect(-22f, -7f, 22f, 12f);
                case "13-3":
                    return Rect.MinMaxRect(-18f, -13f, 148f, 15f);
                case "14-3":
                    return Rect.MinMaxRect(-24f, -12f, 31f, 13f);
                default:
                    return GetCaveBackdropBounds(objects);
            }
        }

        private static bool HasAlternatePlayerLayoutDefinitions(
            string stageId,
            IList<StageObjectData> objects)
        {
            if (string.IsNullOrEmpty(stageId) || objects == null)
            {
                return false;
            }

            string layoutPrefix = stageId + "-layout-p";
            for (int objectIndex = 0; objectIndex < objects.Count; objectIndex++)
            {
                StageObjectData data = objects[objectIndex];
                if (data != null
                    && !string.IsNullOrEmpty(data.objectId)
                    && data.objectId.StartsWith(layoutPrefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddFactoryBackdrop(
            Transform parent,
            Rect bounds,
            int seed,
            bool includeInteriorMachinery)
        {
            Rect colorBounds = GetBackdropColorFillBounds(bounds);
            GameObject wash = new GameObject("Factory Graphite Paper Wash");
            wash.transform.SetParent(parent, false);
            wash.transform.localPosition = new Vector3(colorBounds.center.x, colorBounds.center.y, 0f);
            wash.transform.localScale = new Vector3(colorBounds.width, colorBounds.height, 1f);
            SpriteRenderer washRenderer = wash.AddComponent<SpriteRenderer>();
            washRenderer.sprite = GetSquareSprite();
            washRenderer.color = FactoryBackdropWashColor;
            washRenderer.sortingOrder = -99;

            uint state = CreateCaveVisualState(seed);
            List<Vector3> paperVertices = new List<Vector3>();
            List<Color> paperColors = new List<Color>();
            List<int> paperTriangles = new List<int>();
            int hatchCount = Mathf.Clamp(Mathf.RoundToInt(bounds.width * bounds.height / 24f), 72, 360);
            for (int hatch = 0; hatch < hatchCount; hatch++)
            {
                Vector2 from = new Vector2(
                    CaveVisualRange(ref state, bounds.xMin, bounds.xMax),
                    CaveVisualRange(ref state, bounds.yMin, bounds.yMax));
                float length = CaveVisualRange(ref state, 0.45f, 2.6f);
                Vector2 to = from + new Vector2(length, length * CaveVisualRange(ref state, 0.08f, 0.24f));
                to.x = Mathf.Min(to.x, bounds.xMax);
                to.y = Mathf.Min(to.y, bounds.yMax);
                AppendPencilQuad(
                    paperVertices,
                    paperColors,
                    paperTriangles,
                    from,
                    to,
                    CaveVisualRange(ref state, 0.01f, 0.023f),
                    new Color(0.2f, 0.25f, 0.29f, CaveVisualRange(ref state, 0.035f, 0.075f)));
            }
            CreatePencilMesh(parent, "Factory Background Graphite Hatching", paperVertices, paperColors, paperTriangles, -98);

            // Stages with player-count-specific layout definitions keep every
            // alternate wall in their JSON. Those definitions are selected later
            // by the stage controller and must not also become backdrop pipes and
            // gears. Keep only the quiet paper wash inside these runtime arenas.
            if (!includeInteriorMachinery)
            {
                return;
            }

            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            Color steel = new Color(0.2f, 0.25f, 0.31f, 0.3f);
            Color graphite = new Color(0.08f, 0.1f, 0.13f, 0.42f);
            Color rust = new Color(0.66f, 0.28f, 0.12f, 0.2f);

            float topY = bounds.yMax - Mathf.Clamp(bounds.height * 0.085f, 1.1f, 2.7f);
            float lowerY = bounds.yMin + Mathf.Clamp(bounds.height * 0.12f, 1.1f, 3.3f);
            AppendFactoryTruss(vertices, colors, triangles,
                new Vector2(bounds.xMin + 0.4f, topY), new Vector2(bounds.xMax - 0.4f, topY),
                0.72f, steel, graphite, ref state);
            AppendFactoryTruss(vertices, colors, triangles,
                new Vector2(bounds.xMin + 0.4f, lowerY), new Vector2(bounds.xMax - 0.4f, lowerY),
                0.55f, steel * 0.82f, graphite * 0.82f, ref state);

            int columnCount = Mathf.Clamp(Mathf.RoundToInt(bounds.width / 18f), 2, 13);
            for (int column = 0; column < columnCount; column++)
            {
                float t = (column + 0.5f) / columnCount;
                float x = Mathf.Lerp(bounds.xMin + 1.2f, bounds.xMax - 1.2f, t)
                    + CaveVisualRange(ref state, -0.7f, 0.7f);
                float columnBottom = bounds.yMin + CaveVisualRange(ref state, 0.2f, 1.6f);
                float columnTop = bounds.yMax - CaveVisualRange(ref state, 0.5f, 1.7f);
                AppendFactoryTruss(vertices, colors, triangles,
                    new Vector2(x, columnBottom), new Vector2(x, columnTop),
                    CaveVisualRange(ref state, 0.42f, 0.68f), steel, graphite, ref state);
            }

            int gearCount = Mathf.Clamp(Mathf.RoundToInt(bounds.width / 18f + bounds.height / 14f), 5, 18);
            for (int gear = 0; gear < gearCount; gear++)
            {
                Vector2 center = new Vector2(
                    CaveVisualRange(ref state, bounds.xMin + 1.2f, bounds.xMax - 1.2f),
                    CaveVisualRange(ref state, bounds.yMin + 1.3f, bounds.yMax - 1.3f));
                float radius = CaveVisualRange(ref state, 0.45f, 1.25f);
                AppendFactoryGear(
                    vertices,
                    colors,
                    triangles,
                    center,
                    radius,
                    8 + Mathf.FloorToInt(CaveVisual01(ref state) * 5f),
                    gear % 3 == 0 ? rust : steel,
                    graphite,
                    ref state);
            }

            int armCount = Mathf.Clamp(Mathf.RoundToInt(bounds.width / 45f), 1, 6);
            for (int arm = 0; arm < armCount; arm++)
            {
                Vector2 basePoint = new Vector2(
                    Mathf.Lerp(bounds.xMin + 2f, bounds.xMax - 2f, (arm + 0.5f) / armCount),
                    lowerY + 0.45f);
                Vector2 elbow = basePoint + new Vector2(
                    CaveVisualRange(ref state, -1.8f, 1.8f),
                    CaveVisualRange(ref state, 2.2f, 4.8f));
                Vector2 wrist = elbow + new Vector2(
                    CaveVisualRange(ref state, 1.2f, 3.4f) * (arm % 2 == 0 ? 1f : -1f),
                    CaveVisualRange(ref state, 0.5f, 2.2f));
                AppendPencilQuad(vertices, colors, triangles, basePoint, elbow, 0.28f, rust);
                AppendPencilQuad(vertices, colors, triangles, elbow, wrist, 0.24f, rust);
                AppendFactoryGear(vertices, colors, triangles, elbow, 0.34f, 8, rust, graphite, ref state);
                AppendFactoryGear(vertices, colors, triangles, basePoint, 0.42f, 9, rust, graphite, ref state);
            }

            // Large background pipe banks with visible elbows and valve wheels.
            Vector2[] upperPipe =
            {
                new Vector2(bounds.xMin + 0.7f, topY - 1.3f),
                new Vector2(bounds.xMin + bounds.width * 0.28f, topY - 1.3f),
                new Vector2(bounds.xMin + bounds.width * 0.28f, topY - 3.2f),
                new Vector2(bounds.xMin + bounds.width * 0.62f, topY - 3.2f),
                new Vector2(bounds.xMin + bounds.width * 0.62f, topY - 1.8f),
                new Vector2(bounds.xMax - 0.8f, topY - 1.8f)
            };
            AppendFactoryPipePolyline(vertices, colors, triangles, upperPipe, graphite, new Color(0.33f, 0.4f, 0.47f, 0.32f), ref state);
            AppendFactoryGear(vertices, colors, triangles, upperPipe[2], 0.48f, 8, rust, graphite, ref state);
            AppendFactoryGear(vertices, colors, triangles, upperPipe[4], 0.42f, 8, rust, graphite, ref state);

            CreatePencilMesh(parent, "Factory Background Ironwork And Machinery", vertices, colors, triangles, -78);
        }

        private static void AddFactoryInfrastructure(
            Transform parent,
            Rect bounds,
            IList<StageObjectData> objects,
            string stageId,
            int seed)
        {
            uint state = CreateCaveVisualState(seed);
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            Color dark = new Color(0.07f, 0.08f, 0.1f, 0.9f);
            Color steel = new Color(0.31f, 0.35f, 0.39f, 0.78f);
            float left = bounds.xMin + 1f;
            float right = bounds.xMax - 1f;
            float top = bounds.yMax - 1f;
            float bottom = bounds.yMin + 1f;
            // 14-3 stores an editor preview boundary, while its runtime arena is
            // rebuilt wider by StageLaserRelayController. Do not let that preview
            // crop the lamps and pipework from the playable right-hand section.
            if (stageId != "14-3")
            {
                TryGetNightCityBoundaryInterior(objects, ref left, ref right, ref top, ref bottom);
            }

            int lampCount = Mathf.Clamp(Mathf.RoundToInt((right - left) / 15f), 2, 13);
            for (int lamp = 0; lamp < lampCount; lamp++)
            {
                float t = (lamp + 0.5f) / lampCount;
                Vector2 anchor = new Vector2(
                    Mathf.Lerp(left + 1.3f, right - 1.3f, t) + CaveVisualRange(ref state, -0.35f, 0.35f),
                    top - 0.08f);
                AppendNightCityHangingLamp(
                    vertices,
                    colors,
                    triangles,
                    anchor,
                    CaveVisualRange(ref state, 0.65f, 1.75f),
                    ref state);
            }

            int wallLampCount = Mathf.Clamp(Mathf.RoundToInt((top - bottom) / 12f), 1, 8);
            for (int wallLamp = 0; wallLamp < wallLampCount; wallLamp++)
            {
                float t = (wallLamp + 0.55f) / wallLampCount;
                float y = Mathf.Lerp(bottom + 1.1f, top - 2.1f, t);
                bool useLeftWall = (wallLamp & 1) == 0;
                AppendNightCityWallLamp(
                    vertices,
                    colors,
                    triangles,
                    new Vector2(useLeftWall ? left + 0.08f : right - 0.08f, y),
                    useLeftWall ? 1f : -1f,
                    ref state);
            }

            Vector2[] ceilingPipe =
            {
                new Vector2(left + 0.35f, top - 0.45f),
                new Vector2(left + (right - left) * 0.35f, top - 0.45f),
                new Vector2(left + (right - left) * 0.35f, top - 1.05f),
                new Vector2(right - 0.35f, top - 1.05f)
            };
            AppendFactoryPipePolyline(vertices, colors, triangles, ceilingPipe, dark, steel, ref state);

            List<Rect> parts = new List<Rect>();
            int attachedCount = 0;
            if (objects != null)
            {
                for (int objectIndex = 0; objectIndex < objects.Count && attachedCount < 34; objectIndex++)
                {
                    StageObjectData data = objects[objectIndex];
                    if (data == null || !IsNightCityInfrastructureTerrainType(data.type) || !IsAxisAligned(data.rotation))
                    {
                        continue;
                    }

                    parts.Clear();
                    AppendStageRects(data, parts);
                    for (int partIndex = 0; partIndex < parts.Count && attachedCount < 34; partIndex++)
                    {
                        Rect rect = parts[partIndex];
                        if (rect.width >= 5f && rect.height <= 4f)
                        {
                            float y = rect.yMin - CaveVisualRange(ref state, 0.2f, 0.38f);
                            AppendNightCityPipeRun(
                                vertices,
                                colors,
                                triangles,
                                new Vector2(rect.xMin + 0.25f, y),
                                new Vector2(rect.xMax - 0.25f, y),
                                dark,
                                steel,
                                ref state);
                            if ((attachedCount & 2) == 0 && rect.width > 7f)
                            {
                                AppendFactoryGear(
                                    vertices,
                                    colors,
                                    triangles,
                                    new Vector2(rect.center.x + CaveVisualRange(ref state, -rect.width * 0.25f, rect.width * 0.25f), y - 0.42f),
                                    CaveVisualRange(ref state, 0.24f, 0.42f),
                                    8,
                                    new Color(0.52f, 0.27f, 0.1f, 0.65f),
                                    dark,
                                    ref state);
                            }
                            attachedCount++;
                        }
                        else if (rect.height >= 5f && rect.width <= 4f)
                        {
                            float x = rect.xMax + CaveVisualRange(ref state, 0.18f, 0.34f);
                            AppendNightCityPipeRun(
                                vertices,
                                colors,
                                triangles,
                                new Vector2(x, rect.yMin + 0.3f),
                                new Vector2(x, rect.yMax - 0.3f),
                                dark,
                                steel,
                                ref state);
                            attachedCount++;
                        }
                    }
                }
            }

            // Keep infrastructure behind monitors and their text (some challenge
            // monitors render around -32..-25) while remaining in front of the
            // distant factory silhouette.
            CreatePencilMesh(parent, "Factory Lamps Pipes And Gears", vertices, colors, triangles, -40);
        }

        private static void AppendFactoryTruss(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 from,
            Vector2 to,
            float width,
            Color fill,
            Color outline,
            ref uint state)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length <= 0.05f)
            {
                return;
            }

            Vector2 direction = delta / length;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            Vector2 railAFrom = from + normal * width * 0.5f;
            Vector2 railATo = to + normal * width * 0.5f;
            Vector2 railBFrom = from - normal * width * 0.5f;
            Vector2 railBTo = to - normal * width * 0.5f;
            AppendCaveWobblyStroke(vertices, colors, triangles, railAFrom, railATo, 0.1f, outline, 0.025f, ref state);
            AppendCaveWobblyStroke(vertices, colors, triangles, railBFrom, railBTo, 0.1f, outline, 0.025f, ref state);
            AppendPencilQuad(vertices, colors, triangles, railAFrom, railATo, 0.052f, fill);
            AppendPencilQuad(vertices, colors, triangles, railBFrom, railBTo, 0.052f, fill);

            int bays = Mathf.Clamp(Mathf.RoundToInt(length / 2.1f), 1, 48);
            for (int bay = 0; bay < bays; bay++)
            {
                float t0 = bay / (float)bays;
                float t1 = (bay + 1f) / bays;
                Vector2 a0 = Vector2.Lerp(railAFrom, railATo, t0);
                Vector2 a1 = Vector2.Lerp(railAFrom, railATo, t1);
                Vector2 b0 = Vector2.Lerp(railBFrom, railBTo, t0);
                Vector2 b1 = Vector2.Lerp(railBFrom, railBTo, t1);
                AppendPencilQuad(vertices, colors, triangles, a0, b1, 0.045f, outline * 0.88f);
                AppendPencilQuad(vertices, colors, triangles, b0, a1, 0.045f, outline * 0.88f);
                AppendPencilQuad(vertices, colors, triangles, a0, b0, 0.05f, outline);
            }
            AppendPencilQuad(vertices, colors, triangles, railATo, railBTo, 0.05f, outline);
        }

        private static void AppendFactoryGear(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 center,
            float radius,
            int teeth,
            Color fill,
            Color outline,
            ref uint state)
        {
            teeth = Mathf.Clamp(teeth, 7, 14);
            int pointCount = teeth * 4;
            Vector2[] edge = new Vector2[pointCount];
            float phase = CaveVisualRange(ref state, -Mathf.PI, Mathf.PI);
            for (int point = 0; point < pointCount; point++)
            {
                float angle = phase + point * Mathf.PI * 2f / pointCount;
                int toothPhase = point & 3;
                float tooth = toothPhase == 1 || toothPhase == 2 ? 1.16f : 0.92f;
                float hand = 1f + Mathf.Sin(point * 2.17f + phase) * 0.025f;
                edge[point] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * tooth * hand;
            }
            AppendFilledCavePolygon(vertices, colors, triangles, edge, fill);
            for (int point = 0; point < pointCount; point++)
            {
                AppendPencilQuad(vertices, colors, triangles, edge[point], edge[(point + 1) % pointCount], 0.035f, outline);
            }

            int spokes = Mathf.Clamp(teeth / 2, 4, 7);
            for (int spoke = 0; spoke < spokes; spoke++)
            {
                float angle = phase + spoke * Mathf.PI * 2f / spokes;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                AppendPencilQuad(vertices, colors, triangles, center + direction * radius * 0.2f, center + direction * radius * 0.76f, Mathf.Max(0.028f, radius * 0.07f), outline * 0.85f);
            }
            float hub = radius * 0.2f;
            AppendPencilQuad(vertices, colors, triangles, center - Vector2.right * hub, center + Vector2.right * hub, Mathf.Max(0.05f, hub * 0.7f), outline);
            AppendPencilQuad(vertices, colors, triangles, center - Vector2.up * hub, center + Vector2.up * hub, Mathf.Max(0.05f, hub * 0.7f), outline);
        }

        private static void AppendFactoryPipePolyline(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2[] points,
            Color dark,
            Color fill,
            ref uint state)
        {
            if (points == null || points.Length < 2)
            {
                return;
            }

            for (int point = 1; point < points.Length; point++)
            {
                AppendCaveWobblyStroke(vertices, colors, triangles, points[point - 1], points[point], 0.24f, dark, 0.022f, ref state);
                AppendCaveWobblyStroke(vertices, colors, triangles, points[point - 1], points[point], 0.135f, fill, 0.014f, ref state);
                if (point < points.Length - 1)
                {
                    AppendFactoryGear(vertices, colors, triangles, points[point], 0.21f, 8, fill, dark, ref state);
                }
            }
        }

        private static void AddUnderwaterWaterBackdrop(Transform parent, Rect bounds, int seed)
        {
            Rect colorBounds = GetBackdropColorFillBounds(bounds);
            GameObject wash = new GameObject("Underwater Crayon Wash");
            wash.transform.SetParent(parent, false);
            wash.transform.localPosition = new Vector3(colorBounds.center.x, colorBounds.center.y, 0f);
            wash.transform.localScale = new Vector3(colorBounds.width, colorBounds.height, 1f);
            SpriteRenderer washRenderer = wash.AddComponent<SpriteRenderer>();
            washRenderer.sprite = GetSquareSprite();
            washRenderer.color = new Color(0.22f, 0.68f, 0.82f, 0.22f);
            washRenderer.sortingOrder = -99;

            Color[] bands =
            {
                new Color(0.68f, 0.94f, 0.98f, 0.045f),
                new Color(0.4f, 0.82f, 0.92f, 0.065f),
                new Color(0.22f, 0.68f, 0.82f, 0.085f),
                new Color(0.09f, 0.47f, 0.65f, 0.11f)
            };
            float bandHeight = bounds.height / bands.Length;
            for (int band = 0; band < bands.Length; band++)
            {
                float bandTop = band == 0
                    ? colorBounds.yMax
                    : bounds.yMax - bandHeight * band;
                float bandBottom = band == bands.Length - 1
                    ? colorBounds.yMin
                    : bounds.yMax - bandHeight * (band + 1f);
                GameObject layer = new GameObject("Underwater Depth Band " + band);
                layer.transform.SetParent(parent, false);
                layer.transform.localPosition = new Vector3(
                    colorBounds.center.x,
                    (bandTop + bandBottom) * 0.5f,
                    0f);
                layer.transform.localScale = new Vector3(colorBounds.width, bandTop - bandBottom + 0.08f, 1f);
                SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
                renderer.sprite = GetSquareSprite();
                renderer.color = bands[band];
                renderer.sortingOrder = -98 + band;
            }

            uint state = CreateCaveVisualState(seed);
            AddUnderwaterLightShafts(parent, bounds, ref state);
            AddUnderwaterDistantScenery(parent, bounds, ref state);

            List<Vector3> currentVertices = new List<Vector3>();
            List<Color> currentColors = new List<Color>();
            List<int> currentTriangles = new List<int>();
            int waveRows = Mathf.Clamp(Mathf.CeilToInt(bounds.height / 5.2f), 4, 12);
            for (int row = 0; row < waveRows; row++)
            {
                float y = Mathf.Lerp(bounds.yMin + 0.8f, bounds.yMax - 0.8f, (row + 0.5f) / waveRows);
                float segmentWidth = Mathf.Clamp(bounds.width / 9f, 3.2f, 8.5f);
                for (float x = bounds.xMin + CaveVisualRange(ref state, 0.25f, 1.4f);
                    x < bounds.xMax - 0.25f;
                    x += segmentWidth + CaveVisualRange(ref state, 1.1f, 2.8f))
                {
                    Vector2 a = new Vector2(x, y + CaveVisualRange(ref state, -0.12f, 0.12f));
                    Vector2 b = new Vector2(
                        Mathf.Min(bounds.xMax - 0.2f, x + segmentWidth),
                        y + CaveVisualRange(ref state, -0.12f, 0.12f));
                    AppendCaveWobblyStroke(
                        currentVertices,
                        currentColors,
                        currentTriangles,
                        a,
                        b,
                        CaveVisualRange(ref state, 0.012f, 0.024f),
                        new Color(0.09f, 0.5f, 0.67f, CaveVisualRange(ref state, 0.09f, 0.16f)),
                        0.045f,
                        ref state);
                }
            }
            CreatePencilMesh(
                parent,
                "Underwater Broken Current Lines",
                currentVertices,
                currentColors,
                currentTriangles,
                -85);

            List<Vector3> causticVertices = new List<Vector3>();
            List<Color> causticColors = new List<Color>();
            List<int> causticTriangles = new List<int>();
            int causticCount = Mathf.Clamp(Mathf.RoundToInt(bounds.width * bounds.height / 180f), 8, 24);
            for (int caustic = 0; caustic < causticCount; caustic++)
            {
                Vector2 center = new Vector2(
                    CaveVisualRange(ref state, bounds.xMin + 0.8f, bounds.xMax - 0.8f),
                    CaveVisualRange(ref state, bounds.yMin + 0.8f, bounds.yMax - 0.8f));
                float span = CaveVisualRange(ref state, 0.45f, 1.35f);
                AppendCaveWobblyStroke(
                    causticVertices,
                    causticColors,
                    causticTriangles,
                    center - new Vector2(span, 0.02f),
                    center + new Vector2(0f, CaveVisualRange(ref state, 0.08f, 0.2f)),
                    0.018f,
                    new Color(0.78f, 0.98f, 1f, 0.2f),
                    0.025f,
                    ref state);
                AppendCaveWobblyStroke(
                    causticVertices,
                    causticColors,
                    causticTriangles,
                    center,
                    center + new Vector2(span, CaveVisualRange(ref state, -0.08f, 0.08f)),
                    0.014f,
                    new Color(0.76f, 0.98f, 1f, 0.15f),
                    0.025f,
                    ref state);
            }
            CreatePencilMesh(
                parent,
                "Underwater Broken Light Caustics",
                causticVertices,
                causticColors,
                causticTriangles,
                -83);

            AddUnderwaterBubbleColumns(parent, bounds, ref state);
        }

        private static void AddUnderwaterLightShafts(Transform parent, Rect bounds, ref uint state)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            int shaftCount = Mathf.Clamp(Mathf.CeilToInt(bounds.width / 24f), 2, 5);
            for (int shaft = 0; shaft < shaftCount; shaft++)
            {
                float slot = (shaft + 0.65f) / shaftCount;
                float topX = Mathf.Lerp(bounds.xMin, bounds.xMax, slot)
                    + CaveVisualRange(ref state, -1.2f, 1.2f);
                float topY = bounds.yMax - 0.25f;
                float bottomY = Mathf.Lerp(bounds.yMin, bounds.yMax, CaveVisualRange(ref state, 0.12f, 0.34f));
                float drift = CaveVisualRange(ref state, -2.2f, 2.2f);
                float topHalf = CaveVisualRange(ref state, 0.45f, 1.05f);
                float bottomHalf = CaveVisualRange(ref state, 1.4f, 3.1f);
                Vector2[] shaftEdge =
                {
                    new Vector2(topX - topHalf, topY),
                    new Vector2(topX + topHalf, topY - CaveVisualRange(ref state, 0.01f, 0.14f)),
                    new Vector2(topX + drift + bottomHalf, bottomY + CaveVisualRange(ref state, -0.18f, 0.18f)),
                    new Vector2(topX + drift - bottomHalf, bottomY)
                };
                Color fill = new Color(0.8f, 0.98f, 1f, CaveVisualRange(ref state, 0.035f, 0.06f));
                AppendFilledCavePolygon(vertices, colors, triangles, shaftEdge, fill);
                AppendPencilQuad(
                    vertices,
                    colors,
                    triangles,
                    shaftEdge[0],
                    shaftEdge[3],
                    0.018f,
                    new Color(0.79f, 0.97f, 1f, 0.08f));
                AppendPencilQuad(
                    vertices,
                    colors,
                    triangles,
                    shaftEdge[1],
                    shaftEdge[2],
                    0.014f,
                    new Color(0.79f, 0.97f, 1f, 0.065f));
            }
            CreatePencilMesh(parent, "Underwater Hand Drawn Light Shafts", vertices, colors, triangles, -94);
        }

        private static void AddUnderwaterDistantScenery(Transform parent, Rect bounds, ref uint state)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            float sceneryY = Mathf.Lerp(bounds.yMin, bounds.yMax, 0.18f);
            Color rockFill = new Color(0.08f, 0.34f, 0.43f, 0.105f);
            Color rockOutline = new Color(0.05f, 0.27f, 0.36f, 0.2f);
            for (int rock = 0; rock < 5; rock++)
            {
                float slot = rock / 4f;
                float x = Mathf.Lerp(bounds.xMin + 1.5f, bounds.xMax - 1.5f, slot)
                    + CaveVisualRange(ref state, -1.25f, 1.25f);
                float halfWidth = CaveVisualRange(ref state, 1.0f, 2.7f);
                float height = CaveVisualRange(ref state, 1.1f, 3.5f);
                Vector2[] rockEdge =
                {
                    new Vector2(x - halfWidth, sceneryY),
                    new Vector2(x - halfWidth * 0.72f, sceneryY + height * 0.42f),
                    new Vector2(x - halfWidth * 0.22f, sceneryY + height),
                    new Vector2(x + halfWidth * 0.2f, sceneryY + height * 0.83f),
                    new Vector2(x + halfWidth * 0.7f, sceneryY + height * 0.38f),
                    new Vector2(x + halfWidth, sceneryY)
                };
                AppendFilledCavePolygon(vertices, colors, triangles, rockEdge, rockFill);
                for (int edge = 0; edge < rockEdge.Length - 1; edge++)
                {
                    AppendPencilQuad(
                        vertices,
                        colors,
                        triangles,
                        rockEdge[edge],
                        rockEdge[edge + 1],
                        0.027f + (edge % 2) * 0.006f,
                        rockOutline);
                }
            }

            Color coral = new Color(0.08f, 0.4f, 0.45f, 0.18f);
            for (int coralIndex = 0; coralIndex < 5; coralIndex++)
            {
                bool leftSide = (coralIndex & 1) == 0;
                float x = leftSide
                    ? Mathf.Lerp(bounds.xMin + 1.1f, bounds.center.x - 2f, CaveVisual01(ref state) * 0.38f)
                    : Mathf.Lerp(bounds.center.x + 2f, bounds.xMax - 1.1f, 0.62f + CaveVisual01(ref state) * 0.38f);
                Vector2 root = new Vector2(x, sceneryY);
                float coralHeight = CaveVisualRange(ref state, 0.55f, 1.35f);
                Vector2 fork = root + new Vector2(CaveVisualRange(ref state, -0.12f, 0.12f), coralHeight * 0.54f);
                Vector2 tip = root + new Vector2(CaveVisualRange(ref state, -0.25f, 0.25f), coralHeight);
                AppendPencilQuad(vertices, colors, triangles, root, fork, 0.065f, coral);
                AppendPencilQuad(vertices, colors, triangles, fork, tip, 0.052f, coral);
                AppendPencilQuad(
                    vertices,
                    colors,
                    triangles,
                    fork,
                    fork + new Vector2(leftSide ? -0.34f : 0.34f, coralHeight * 0.24f),
                    0.045f,
                    coral * 0.86f);
            }

            Color fishFill = new Color(0.06f, 0.35f, 0.47f, 0.135f);
            Color fishOutline = new Color(0.04f, 0.28f, 0.39f, 0.18f);
            int schoolCount = Mathf.Clamp(Mathf.RoundToInt(bounds.width / 25f), 2, 4);
            for (int school = 0; school < schoolCount; school++)
            {
                Vector2 schoolCenter = new Vector2(
                    Mathf.Lerp(bounds.xMin + 3f, bounds.xMax - 3f, (school + 0.5f) / schoolCount),
                    Mathf.Lerp(bounds.yMin, bounds.yMax, CaveVisualRange(ref state, 0.38f, 0.7f)));
                int fishCount = 2 + Mathf.FloorToInt(CaveVisual01(ref state) * 3f);
                for (int fish = 0; fish < fishCount; fish++)
                {
                    float direction = ((school + fish) & 1) == 0 ? 1f : -1f;
                    Vector2 center = schoolCenter + new Vector2(
                        (fish - (fishCount - 1) * 0.5f) * CaveVisualRange(ref state, 0.65f, 1.05f),
                        Mathf.Sin((fish + 1) * 2.1f) * 0.32f);
                    float halfWidth = CaveVisualRange(ref state, 0.28f, 0.46f);
                    float halfHeight = halfWidth * CaveVisualRange(ref state, 0.38f, 0.54f);
                    Vector2[] body =
                    {
                        center + new Vector2(-halfWidth * direction, 0f),
                        center + new Vector2(-halfWidth * 0.25f * direction, halfHeight),
                        center + new Vector2(halfWidth * 0.65f * direction, halfHeight * 0.7f),
                        center + new Vector2(halfWidth * direction, 0f),
                        center + new Vector2(halfWidth * 0.65f * direction, -halfHeight * 0.7f),
                        center + new Vector2(-halfWidth * 0.25f * direction, -halfHeight)
                    };
                    Vector2 tailBase = center - Vector2.right * direction * halfWidth * 0.75f;
                    Vector2[] tail =
                    {
                        tailBase,
                        tailBase - Vector2.right * direction * halfWidth * 0.62f + Vector2.up * halfHeight,
                        tailBase - Vector2.right * direction * halfWidth * 0.62f - Vector2.up * halfHeight
                    };
                    AppendFilledCavePolygon(vertices, colors, triangles, body, fishFill);
                    AppendFilledCavePolygon(vertices, colors, triangles, tail, fishFill * 0.9f);
                    for (int edge = 0; edge < body.Length; edge++)
                    {
                        AppendPencilQuad(
                            vertices,
                            colors,
                            triangles,
                            body[edge],
                            body[(edge + 1) % body.Length],
                            0.015f,
                            fishOutline);
                    }
                }
            }
            CreatePencilMesh(parent, "Underwater Distant Reef And Fish Shadows", vertices, colors, triangles, -92);
        }

        private static void AddUnderwaterBubbleColumns(Transform parent, Rect bounds, ref uint state)
        {
            int columnCount = Mathf.Clamp(Mathf.RoundToInt(bounds.width / 12f), 4, 7);
            float verticalInset = Mathf.Clamp(bounds.height * 0.14f, 2.5f, 6f);
            float bottom = bounds.yMin + verticalInset;
            float top = bounds.yMax - verticalInset;
            float travelHeight = Mathf.Max(4f, top - bottom);
            int bubbleIndex = 0;
            for (int column = 0; column < columnCount; column++)
            {
                float columnX = Mathf.Lerp(
                    bounds.xMin + 1.2f,
                    bounds.xMax - 1.2f,
                    (column + 0.65f) / columnCount)
                    + CaveVisualRange(ref state, -0.65f, 0.65f);
                int bubblesInColumn = 5 + Mathf.FloorToInt(CaveVisual01(ref state) * 3f);
                float columnPhase = CaveVisualRange(ref state, 0f, travelHeight);
                for (int bubble = 0; bubble < bubblesInColumn; bubble++)
                {
                    float t = bubblesInColumn <= 1 ? 0f : bubble / (bubblesInColumn - 1f);
                    GameObject bubbleObject = new GameObject("Underwater Bubble Column " + column + " Bubble " + bubble);
                    bubbleObject.transform.SetParent(parent, false);
                    bubbleObject.transform.localPosition = new Vector3(columnX, Mathf.Lerp(bottom, top, t), 0f);
                    float size = CaveVisualRange(ref state, 0.11f, 0.24f) * Mathf.Lerp(0.84f, 1.22f, t);
                    bubbleObject.transform.localScale = Vector3.one * size;

                    SpriteRenderer wash = bubbleObject.AddComponent<SpriteRenderer>();
                    wash.sprite = GetCircleSprite();
                    wash.color = new Color(0.72f, 0.96f, 1f, 0.09f);
                    wash.sortingOrder = -67;

                    Vector3[] ring = new Vector3[15];
                    for (int point = 0; point < ring.Length; point++)
                    {
                        float angle = point / (ring.Length - 1f) * Mathf.PI * 2f;
                        float wobble = 0.47f + Mathf.Sin(point * 2.31f + column) * 0.025f;
                        ring[point] = new Vector3(Mathf.Cos(angle) * wobble, Mathf.Sin(angle) * wobble, -0.01f);
                    }
                    AddDoodleLine(
                        "Bubble Pencil Ring",
                        bubbleObject.transform,
                        ring,
                        new Color(0.52f, 0.9f, 0.98f, CaveVisualRange(ref state, 0.42f, 0.62f)),
                        0.075f,
                        -66);

                    GameObject shine = new GameObject("Bubble White Pencil Shine");
                    shine.transform.SetParent(bubbleObject.transform, false);
                    shine.transform.localPosition = new Vector3(-0.19f, 0.21f, -0.02f);
                    shine.transform.localScale = Vector3.one * 0.17f;
                    SpriteRenderer shineRenderer = shine.AddComponent<SpriteRenderer>();
                    shineRenderer.sprite = GetCircleSprite();
                    shineRenderer.color = new Color(0.98f, 1f, 1f, 0.78f);
                    shineRenderer.sortingOrder = -65;

                    AquariumBubbleMover mover = bubbleObject.AddComponent<AquariumBubbleMover>();
                    mover.Configure(
                        bottom,
                        top,
                        CaveVisualRange(ref state, 0.18f, 0.4f),
                        columnPhase + t * travelHeight + bubbleIndex * 0.17f);
                    bubbleIndex++;
                }
            }
        }

        private static void AddUnderwaterSeaweedField(
            Transform parent,
            IList<StageObjectData> objects,
            int seed)
        {
            if (objects == null)
            {
                return;
            }

            List<CaveBackdropSurface> floors = new List<CaveBackdropSurface>();
            List<Rect> avoidZones = new List<Rect>();
            for (int objectIndex = 0; objectIndex < objects.Count; objectIndex++)
            {
                StageObjectData data = objects[objectIndex];
                if (data == null)
                {
                    continue;
                }

                if (IsUnderwaterHorizontalPlatform(data))
                {
                    float halfWidth = data.size.x * 0.5f;
                    float top = data.position.y + data.size.y * 0.5f;
                    floors.Add(new CaveBackdropSurface
                    {
                        From = new Vector2(data.position.x - halfWidth + 0.3f, top),
                        To = new Vector2(data.position.x + halfWidth - 0.3f, top),
                        Outward = Vector2.up,
                        MaxHeight = IsUnderwaterMainSandFloor(data) ? 2.25f : 1.65f,
                        AllowsCrystal = false
                    });
                }

                if (data.type == StageObjectType.Spawn
                    || data.type == StageObjectType.Goal
                    || data.type == StageObjectType.Key
                    || data.type == StageObjectType.Keyhole
                    || data.type == StageObjectType.Button
                    || data.type == StageObjectType.ChallengeClock
                    || data.type == StageObjectType.ConveyorLeft
                    || data.type == StageObjectType.ConveyorRight)
                {
                    float horizontalPadding = data.type == StageObjectType.ChallengeClock ? 0.9f : 0.5f;
                    float verticalPadding = data.type == StageObjectType.ChallengeClock ? 0.45f : 0.3f;
                    float halfWidth = Mathf.Max(0.45f, Mathf.Abs(data.size.x) * 0.5f) + horizontalPadding;
                    float halfHeight = Mathf.Max(0.45f, Mathf.Abs(data.size.y) * 0.5f) + verticalPadding;
                    avoidZones.Add(Rect.MinMaxRect(
                        data.position.x - halfWidth,
                        data.position.y - halfHeight,
                        data.position.x + halfWidth,
                        data.position.y + halfHeight));
                }
            }

            uint state = CreateCaveVisualState(seed);
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            int clusterCount = 0;
            for (int floorIndex = 0; floorIndex < floors.Count && clusterCount < 20; floorIndex++)
            {
                CaveBackdropSurface floor = floors[floorIndex];
                float length = (floor.To - floor.From).magnitude;
                Vector2 tangent = (floor.To - floor.From).normalized;
                float cursor = CaveVisualRange(ref state, 0.7f, 2.2f);
                while (cursor < length - 0.55f && clusterCount < 20)
                {
                    Vector2 anchor = floor.From + tangent * cursor + Vector2.up * 0.035f;
                    float plantHeight = floor.MaxHeight * CaveVisualRange(ref state, 0.55f, 1f);
                    int plantVariant = Mathf.FloorToInt(CaveVisual01(ref state) * 4f) % 4;
                    Rect plantBounds = Rect.MinMaxRect(
                        anchor.x - 0.62f,
                        anchor.y - 0.08f,
                        anchor.x + 0.62f,
                        anchor.y + plantHeight + 0.18f);
                    bool blocked = false;
                    for (int avoidIndex = 0; avoidIndex < avoidZones.Count; avoidIndex++)
                    {
                        if (plantBounds.Overlaps(avoidZones[avoidIndex]))
                        {
                            blocked = true;
                            break;
                        }
                    }

                    if (!blocked && CaveVisual01(ref state) > 0.24f)
                    {
                        AppendUnderwaterPlantCluster(
                            vertices,
                            colors,
                            triangles,
                            anchor,
                            plantHeight,
                            plantVariant,
                            ref state);
                        clusterCount++;
                    }
                    cursor += CaveVisualRange(ref state, 2.1f, 5.8f);
                }
            }
            CreatePencilMesh(parent, "Underwater Seaweed And Coral", vertices, colors, triangles, -28);
        }

        private static void AppendUnderwaterPlantCluster(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 anchor,
            float height,
            int variant,
            ref uint state)
        {
            int normalizedVariant = ((variant % 4) + 4) % 4;
            Color[] outlines =
            {
                new Color(0.015f, 0.34f, 0.25f, 0.92f),
                new Color(0.01f, 0.40f, 0.32f, 0.9f),
                new Color(0.04f, 0.35f, 0.43f, 0.9f),
                new Color(0.06f, 0.43f, 0.29f, 0.92f)
            };
            Color[] fills =
            {
                new Color(0.20f, 0.68f, 0.43f, 0.52f),
                new Color(0.17f, 0.76f, 0.57f, 0.48f),
                new Color(0.18f, 0.68f, 0.72f, 0.48f),
                new Color(0.40f, 0.72f, 0.30f, 0.48f)
            };
            Color[] highlights =
            {
                new Color(0.58f, 0.91f, 0.61f, 0.48f),
                new Color(0.53f, 0.95f, 0.75f, 0.46f),
                new Color(0.61f, 0.93f, 0.91f, 0.46f),
                new Color(0.72f, 0.91f, 0.51f, 0.44f)
            };
            Color outline = outlines[normalizedVariant];
            Color fill = fills[normalizedVariant];
            Color highlight = highlights[normalizedVariant];

            if (normalizedVariant == 1 || normalizedVariant == 2)
            {
                int stemCount = normalizedVariant == 2
                    ? 4 + Mathf.FloorToInt(CaveVisual01(ref state) * 2f)
                    : 3 + Mathf.FloorToInt(CaveVisual01(ref state) * 2f);
                for (int stem = 0; stem < stemCount; stem++)
                {
                    float spread = normalizedVariant == 2 ? 0.24f : 0.2f;
                    float offset = (stem - (stemCount - 1) * 0.5f)
                        * CaveVisualRange(ref state, spread * 0.7f, spread * 1.2f);
                    AppendUnderwaterLeafyStem(
                        vertices,
                        colors,
                        triangles,
                        anchor + new Vector2(offset, 0f),
                        height * CaveVisualRange(ref state, normalizedVariant == 2 ? 0.48f : 0.6f, 1f),
                        normalizedVariant == 2 ? 0.82f : 1f,
                        outline,
                        fill,
                        highlight,
                        ref state);
                }
            }
            else
            {
                int bladeCount = normalizedVariant == 3
                    ? 4 + Mathf.FloorToInt(CaveVisual01(ref state) * 2f)
                    : 3 + Mathf.FloorToInt(CaveVisual01(ref state) * 3f);
                for (int blade = 0; blade < bladeCount; blade++)
                {
                    float centered = blade - (bladeCount - 1) * 0.5f;
                    float offset = centered * CaveVisualRange(ref state, 0.16f, 0.26f);
                    float bladeHeight = height * CaveVisualRange(
                        ref state,
                        normalizedVariant == 3 ? 0.42f : 0.58f,
                        normalizedVariant == 3 ? 0.78f : 1f);
                    float halfWidth = CaveVisualRange(
                        ref state,
                        normalizedVariant == 3 ? 0.12f : 0.09f,
                        normalizedVariant == 3 ? 0.21f : 0.17f);
                    AppendUnderwaterRibbonBlade(
                        vertices,
                        colors,
                        triangles,
                        anchor + new Vector2(offset, 0f),
                        bladeHeight,
                        halfWidth,
                        outline,
                        fill,
                        highlight,
                        ref state);
                }
            }

            AppendUnderwaterPlantBase(
                vertices,
                colors,
                triangles,
                anchor,
                outline,
                fill,
                ref state);
        }

        private static void AppendUnderwaterRibbonBlade(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 root,
            float height,
            float baseHalfWidth,
            Color outline,
            Color fill,
            Color highlight,
            ref uint state)
        {
            const int sampleCount = 13;
            Vector2[] centers = new Vector2[sampleCount];
            Vector2[] left = new Vector2[sampleCount];
            Vector2[] right = new Vector2[sampleCount];
            float phase = CaveVisualRange(ref state, -Mathf.PI, Mathf.PI);
            float cycles = CaveVisualRange(ref state, 1.15f, 1.85f);
            float amplitude = CaveVisualRange(ref state, baseHalfWidth * 0.85f, baseHalfWidth * 1.55f);
            float lean = CaveVisualRange(ref state, -0.16f, 0.16f) * Mathf.Min(1.8f, height);

            for (int sample = 0; sample < sampleCount; sample++)
            {
                float t = sample / (sampleCount - 1f);
                float wave = Mathf.Sin(phase + t * Mathf.PI * cycles) * amplitude * t;
                float pencilWobble = Mathf.Sin(phase * 0.61f + sample * 2.17f) * 0.012f * t;
                centers[sample] = root + new Vector2(lean * t + wave + pencilWobble, height * t);
            }

            for (int sample = 0; sample < sampleCount; sample++)
            {
                Vector2 before = centers[Mathf.Max(0, sample - 1)];
                Vector2 after = centers[Mathf.Min(sampleCount - 1, sample + 1)];
                Vector2 tangent = (after - before).normalized;
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                float t = sample / (sampleCount - 1f);
                float taper = Mathf.Lerp(1f, 0.055f, Mathf.Pow(t, 1.18f));
                float handCut = 0.91f + Mathf.Sin(sample * 2.03f + phase) * 0.09f;
                float halfWidth = baseHalfWidth * taper * handCut;
                left[sample] = centers[sample] + normal * halfWidth;
                right[sample] = centers[sample] - normal * halfWidth;
            }

            int first = vertices.Count;
            for (int sample = 0; sample < sampleCount; sample++)
            {
                float t = sample / (sampleCount - 1f);
                Color shaded = Color.Lerp(fill, outline, 0.08f + t * 0.08f);
                vertices.Add(left[sample]);
                vertices.Add(right[sample]);
                colors.Add(shaded);
                colors.Add(fill);
            }
            for (int sample = 0; sample < sampleCount - 1; sample++)
            {
                int index = first + sample * 2;
                triangles.Add(index);
                triangles.Add(index + 2);
                triangles.Add(index + 1);
                triangles.Add(index + 1);
                triangles.Add(index + 2);
                triangles.Add(index + 3);
            }

            for (int sample = 0; sample < sampleCount - 1; sample++)
            {
                float outlineWidth = 0.025f + (sample % 3) * 0.004f;
                AppendPencilQuad(vertices, colors, triangles, left[sample], left[sample + 1], outlineWidth, outline);
                AppendPencilQuad(vertices, colors, triangles, right[sample], right[sample + 1], outlineWidth, outline);
                AppendPencilQuad(
                    vertices,
                    colors,
                    triangles,
                    centers[sample] + (left[sample] - centers[sample]) * 0.14f,
                    centers[sample + 1] + (left[sample + 1] - centers[sample + 1]) * 0.14f,
                    0.014f,
                    highlight);
            }
            AppendPencilQuad(vertices, colors, triangles, left[0], right[0], 0.024f, outline);
            AppendPencilQuad(
                vertices,
                colors,
                triangles,
                left[sampleCount - 1],
                right[sampleCount - 1],
                0.022f,
                outline);

            for (int sample = 2; sample < sampleCount - 2; sample += 3)
            {
                Vector2 hatchFrom = Vector2.Lerp(left[sample], right[sample], 0.22f);
                Vector2 hatchTo = Vector2.Lerp(left[sample + 1], right[sample + 1], 0.72f);
                AppendPencilQuad(vertices, colors, triangles, hatchFrom, hatchTo, 0.011f, outline * 0.42f);
            }
        }

        private static void AppendUnderwaterLeafyStem(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 root,
            float height,
            float leafScale,
            Color outline,
            Color fill,
            Color highlight,
            ref uint state)
        {
            const int pointCount = 10;
            Vector2[] points = new Vector2[pointCount];
            float phase = CaveVisualRange(ref state, -Mathf.PI, Mathf.PI);
            float lean = CaveVisualRange(ref state, -0.16f, 0.16f);
            for (int point = 0; point < pointCount; point++)
            {
                float t = point / (pointCount - 1f);
                points[point] = root + new Vector2(
                    lean * t + Mathf.Sin(phase + t * Mathf.PI * 1.55f) * (0.04f + t * 0.09f),
                    height * t);
                if (point > 0)
                {
                    AppendPencilQuad(vertices, colors, triangles, points[point - 1], points[point], 0.052f, outline);
                    AppendPencilQuad(
                        vertices,
                        colors,
                        triangles,
                        points[point - 1] + Vector2.right * 0.014f,
                        points[point] + Vector2.right * 0.014f,
                        0.017f,
                        highlight);
                }
            }

            for (int point = 2; point < pointCount - 1; point++)
            {
                float side = (point & 1) == 0 ? -1f : 1f;
                float leafLength = CaveVisualRange(ref state, 0.19f, 0.34f) * leafScale;
                Vector2 leafBase = points[point];
                Vector2 leafTip = leafBase + new Vector2(
                    side * leafLength,
                    CaveVisualRange(ref state, 0.08f, 0.2f) * leafScale);
                AppendVineLeaf(
                    vertices,
                    colors,
                    triangles,
                    leafBase,
                    leafTip,
                    CaveVisualRange(ref state, 0.055f, 0.095f) * leafScale,
                    fill,
                    outline);
                if (point == 4 || point == 7)
                {
                    Vector2 oppositeTip = leafBase + new Vector2(
                        -side * leafLength * 0.72f,
                        CaveVisualRange(ref state, 0.06f, 0.14f));
                    AppendVineLeaf(
                        vertices,
                        colors,
                        triangles,
                        leafBase,
                        oppositeTip,
                        CaveVisualRange(ref state, 0.045f, 0.075f) * leafScale,
                        fill * 0.9f,
                        outline);
                }
            }

            Vector2 terminalDirection = (points[pointCount - 1] - points[pointCount - 2]).normalized;
            Vector2 terminalTip = points[pointCount - 1]
                + terminalDirection * CaveVisualRange(ref state, 0.17f, 0.27f) * leafScale;
            AppendVineLeaf(
                vertices,
                colors,
                triangles,
                points[pointCount - 1],
                terminalTip,
                CaveVisualRange(ref state, 0.06f, 0.1f) * leafScale,
                fill,
                outline);
        }

        private static void AppendUnderwaterPlantBase(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 anchor,
            Color outline,
            Color fill,
            ref uint state)
        {
            float halfWidth = CaveVisualRange(ref state, 0.2f, 0.34f);
            float moundHeight = CaveVisualRange(ref state, 0.08f, 0.14f);
            Vector2[] mound =
            {
                anchor + new Vector2(-halfWidth, -0.015f),
                anchor + new Vector2(-halfWidth * 0.68f, moundHeight * 0.6f),
                anchor + new Vector2(-halfWidth * 0.2f, moundHeight),
                anchor + new Vector2(halfWidth * 0.28f, moundHeight * 0.82f),
                anchor + new Vector2(halfWidth * 0.74f, moundHeight * 0.52f),
                anchor + new Vector2(halfWidth, -0.015f)
            };
            AppendFilledCavePolygon(vertices, colors, triangles, mound, fill * 0.82f);
            for (int edge = 0; edge < mound.Length - 1; edge++)
            {
                AppendPencilQuad(vertices, colors, triangles, mound[edge], mound[edge + 1], 0.025f, outline);
            }
            AppendPencilQuad(vertices, colors, triangles, mound[mound.Length - 1], mound[0], 0.02f, outline * 0.75f);

            for (int tuft = 0; tuft < 5; tuft++)
            {
                float t = tuft / 4f;
                Vector2 tuftRoot = Vector2.Lerp(mound[0], mound[mound.Length - 1], t);
                Vector2 tuftTip = tuftRoot + new Vector2(
                    CaveVisualRange(ref state, -0.08f, 0.08f),
                    CaveVisualRange(ref state, 0.11f, 0.24f));
                AppendPencilQuad(vertices, colors, triangles, tuftRoot, tuftTip, 0.019f, outline * 0.78f);
            }
        }

        private static Rect GetCaveBackdropBounds(IList<StageObjectData> objects)
        {
            bool found = false;
            float minX = 0f;
            float maxX = 0f;
            float minY = 0f;
            float maxY = 0f;
            List<Rect> rects = new List<Rect>();
            if (objects != null)
            {
                for (int objectIndex = 0; objectIndex < objects.Count; objectIndex++)
                {
                    StageObjectData data = objects[objectIndex];
                    if (data == null
                        || (!IsCaveTerrainType(data.type)
                            && data.type != StageObjectType.StageBoundary
                            && data.type != StageObjectType.Elevator))
                    {
                        continue;
                    }

                    rects.Clear();
                    AppendStageRects(data, rects);
                    for (int rectIndex = 0; rectIndex < rects.Count; rectIndex++)
                    {
                        Rect rect = rects[rectIndex];
                        if (!found)
                        {
                            minX = rect.xMin;
                            maxX = rect.xMax;
                            minY = rect.yMin;
                            maxY = rect.yMax;
                            found = true;
                        }
                        else
                        {
                            minX = Mathf.Min(minX, rect.xMin);
                            maxX = Mathf.Max(maxX, rect.xMax);
                            minY = Mathf.Min(minY, rect.yMin);
                            maxY = Mathf.Max(maxY, rect.yMax);
                        }
                    }
                }
            }

            if (!found)
            {
                minX = -30f;
                maxX = 30f;
                minY = -18f;
                maxY = 18f;
            }

            float width = Mathf.Max(32f, maxX - minX);
            float height = Mathf.Max(22f, maxY - minY);
            float marginX = Mathf.Max(10f, width * 0.08f);
            float marginY = Mathf.Max(8f, height * 0.1f);
            return Rect.MinMaxRect(
                minX - marginX,
                minY - marginY,
                maxX + marginX,
                maxY + marginY);
        }

        private static void AddCaveBackdropPaper(Transform parent, Rect bounds)
        {
            Rect colorBounds = GetBackdropColorFillBounds(bounds);
            GameObject paper = new GameObject("Cave Blue Gray Paper Wash");
            paper.transform.SetParent(parent, false);
            paper.transform.localPosition = new Vector3(colorBounds.center.x, colorBounds.center.y, 0f);
            paper.transform.localScale = new Vector3(colorBounds.width, colorBounds.height, 1f);
            SpriteRenderer renderer = paper.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = CaveBackdropPaperColor;
            renderer.sortingOrder = -99;
        }

        private struct CaveBackdropSurface
        {
            public Vector2 From;
            public Vector2 To;
            public Vector2 Outward;
            public float MaxHeight;
            public bool AllowsCrystal;
        }

        private static void AddCaveBackdropDrawing(
            Transform parent,
            Rect bounds,
            IList<StageObjectData> objects,
            int seed)
        {
            uint state = CreateCaveVisualState(seed);
            List<Vector3> hatchVertices = new List<Vector3>();
            List<Color> hatchColors = new List<Color>();
            List<int> hatchTriangles = new List<int>();
            List<Vector3> rockVertices = new List<Vector3>();
            List<Color> rockColors = new List<Color>();
            List<int> rockTriangles = new List<int>();
            List<Vector3> pencilVertices = new List<Vector3>();
            List<Color> pencilColors = new List<Color>();
            List<int> pencilTriangles = new List<int>();

            int hatchCount = Mathf.Clamp(Mathf.RoundToInt(bounds.width * bounds.height / 22f), 64, 320);
            for (int hatchIndex = 0; hatchIndex < hatchCount; hatchIndex++)
            {
                Vector2 start = new Vector2(
                    CaveVisualRange(ref state, bounds.xMin, bounds.xMax),
                    CaveVisualRange(ref state, bounds.yMin, bounds.yMax));
                float length = CaveVisualRange(ref state, 0.45f, 2.35f);
                Vector2 end = start + new Vector2(length, length * CaveVisualRange(ref state, 0.13f, 0.3f));
                end.x = Mathf.Min(end.x, bounds.xMax);
                end.y = Mathf.Min(end.y, bounds.yMax);
                Color hatch = new Color(0.24f, 0.34f, 0.4f, CaveVisualRange(ref state, 0.028f, 0.058f));
                AppendPencilQuad(
                    hatchVertices,
                    hatchColors,
                    hatchTriangles,
                    start,
                    end,
                    CaveVisualRange(ref state, 0.012f, 0.024f),
                    hatch);
            }

            List<Rect> solidRects = new List<Rect>();
            List<CaveBackdropSurface> surfaces = new List<CaveBackdropSurface>();
            CollectCaveBackdropSurfaces(objects, solidRects, surfaces);
            for (int surfaceIndex = 0; surfaceIndex < surfaces.Count; surfaceIndex++)
            {
                CaveBackdropSurface surface = surfaces[surfaceIndex];
                Vector2 midpoint = (surface.From + surface.To) * 0.5f;
                if (!IsCaveBackdropSurfaceExposed(midpoint, surface.Outward, solidRects))
                {
                    continue;
                }

                AppendCaveBackdropClustersOnSurface(
                    rockVertices,
                    rockColors,
                    rockTriangles,
                    pencilVertices,
                    pencilColors,
                    pencilTriangles,
                    surface,
                    ref state);

                if (surface.AllowsCrystal
                    && surface.Outward.y > 0.7f
                    && (surface.To - surface.From).magnitude > 4f
                    && CaveVisual01(ref state) > 0.83f)
                {
                    Vector2 anchor = Vector2.Lerp(
                        surface.From,
                        surface.To,
                        CaveVisualRange(ref state, 0.22f, 0.78f));
                    AppendCaveBackdropCrystal(
                        rockVertices,
                        rockColors,
                        rockTriangles,
                        pencilVertices,
                        pencilColors,
                        pencilTriangles,
                        anchor + surface.Outward * 0.04f,
                        CaveVisualRange(ref state, 0.52f, 0.95f),
                        ref state);
                }
            }

            CreatePencilMesh(parent, "Cave Background Paper Hatching", hatchVertices, hatchColors, hatchTriangles, -98);
            CreatePencilMesh(parent, "Cave Background Rock Silhouettes", rockVertices, rockColors, rockTriangles, -92);
            CreatePencilMesh(parent, "Cave Background Graphite Drawing", pencilVertices, pencilColors, pencilTriangles, -91);
        }

        private static void CollectCaveBackdropSurfaces(
            IList<StageObjectData> objects,
            List<Rect> solidRects,
            List<CaveBackdropSurface> surfaces)
        {
            if (objects == null)
            {
                return;
            }

            List<Rect> parts = new List<Rect>();
            for (int objectIndex = 0; objectIndex < objects.Count; objectIndex++)
            {
                StageObjectData data = objects[objectIndex];
                if (data == null)
                {
                    continue;
                }

                if (data.type == StageObjectType.StageBoundary)
                {
                    float inset = GetStageBoundaryInteriorThickness(data);
                    float left = data.position.x - data.size.x * 0.5f + inset;
                    float right = data.position.x + data.size.x * 0.5f - inset;
                    float top = data.position.y + data.size.y * 0.5f - inset;
                    surfaces.Add(new CaveBackdropSurface
                    {
                        From = new Vector2(left + 0.2f, top),
                        To = new Vector2(right - 0.2f, top),
                        Outward = Vector2.down,
                        MaxHeight = 2.65f,
                        AllowsCrystal = false
                    });
                    continue;
                }

                if (!IsStaticCaveBackdropAnchor(data.type)
                    || (data.pathPoints != null && data.pathPoints.Length >= 2))
                {
                    continue;
                }

                parts.Clear();
                AppendStageRects(data, parts);
                for (int partIndex = 0; partIndex < parts.Count; partIndex++)
                {
                    Rect rect = parts[partIndex];
                    solidRects.Add(rect);
                    if (rect.width < 1.65f || rect.width < rect.height * 1.22f)
                    {
                        continue;
                    }

                    float inset = Mathf.Min(0.16f, rect.width * 0.06f);
                    surfaces.Add(new CaveBackdropSurface
                    {
                        From = new Vector2(rect.xMin + inset, rect.yMin),
                        To = new Vector2(rect.xMax - inset, rect.yMin),
                        Outward = Vector2.down,
                        MaxHeight = Mathf.Clamp(rect.width * 0.12f, 0.7f, 2.15f),
                        AllowsCrystal = false
                    });

                    if (data.type == StageObjectType.Platform && rect.width >= 3.25f)
                    {
                        surfaces.Add(new CaveBackdropSurface
                        {
                            From = new Vector2(rect.xMin + inset, rect.yMax),
                            To = new Vector2(rect.xMax - inset, rect.yMax),
                            Outward = Vector2.up,
                            MaxHeight = Mathf.Clamp(rect.width * 0.075f, 0.42f, 1.25f),
                            AllowsCrystal = true
                        });
                    }
                }
            }
        }

        private static bool IsStaticCaveBackdropAnchor(StageObjectType type)
        {
            return type == StageObjectType.Platform
                || type == StageObjectType.Wall
                || type == StageObjectType.Ceiling
                || type == StageObjectType.HalfPlatform;
        }

        private static bool IsCaveBackdropSurfaceExposed(
            Vector2 midpoint,
            Vector2 outward,
            List<Rect> solidRects)
        {
            Vector2 sample = midpoint + outward.normalized * 0.12f;
            for (int i = 0; i < solidRects.Count; i++)
            {
                Rect rect = solidRects[i];
                if (sample.x > rect.xMin + 0.015f
                    && sample.x < rect.xMax - 0.015f
                    && sample.y > rect.yMin + 0.015f
                    && sample.y < rect.yMax - 0.015f)
                {
                    return false;
                }
            }
            return true;
        }

        private static void AppendCaveBackdropClustersOnSurface(
            List<Vector3> fillVertices,
            List<Color> fillColors,
            List<int> fillTriangles,
            List<Vector3> lineVertices,
            List<Color> lineColors,
            List<int> lineTriangles,
            CaveBackdropSurface surface,
            ref uint state)
        {
            Vector2 edge = surface.To - surface.From;
            float length = edge.magnitude;
            if (length < 0.7f)
            {
                return;
            }

            Vector2 tangent = edge / length;
            Vector2 outward = surface.Outward.normalized;
            float cursor = CaveVisualRange(ref state, 0.08f, 0.55f);
            int guard = 0;
            while (cursor < length - 0.35f && guard++ < 80)
            {
                cursor += CaveVisualRange(ref state, 0.12f, 0.72f);
                float clusterLength = Mathf.Min(
                    CaveVisualRange(ref state, 0.72f, 2.8f),
                    length - cursor);
                if (clusterLength < 0.42f)
                {
                    break;
                }

                if (CaveVisual01(ref state) > 0.12f)
                {
                    AppendOrganicCaveBackdropCluster(
                        fillVertices,
                        fillColors,
                        fillTriangles,
                        lineVertices,
                        lineColors,
                        lineTriangles,
                        surface.From + tangent * cursor,
                        tangent,
                        outward,
                        clusterLength,
                        surface.MaxHeight,
                        ref state);
                }
                cursor += clusterLength + CaveVisualRange(ref state, 0.22f, 1.05f);
            }
        }

        private static void AppendOrganicCaveBackdropCluster(
            List<Vector3> fillVertices,
            List<Color> fillColors,
            List<int> fillTriangles,
            List<Vector3> lineVertices,
            List<Color> lineColors,
            List<int> lineTriangles,
            Vector2 start,
            Vector2 tangent,
            Vector2 outward,
            float clusterLength,
            float maxHeight,
            ref uint state)
        {
            int toothCount = Mathf.Clamp(
                Mathf.RoundToInt(clusterLength / CaveVisualRange(ref state, 0.48f, 0.82f)),
                1,
                6);
            float slotWidth = clusterLength / toothCount;
            Color outline = new Color(
                CaveBackdropGraphiteColor.r,
                CaveBackdropGraphiteColor.g,
                CaveBackdropGraphiteColor.b,
                CaveVisualRange(ref state, 0.25f, 0.39f));
            Color hatch = new Color(0.22f, 0.28f, 0.33f, 0.12f);

            Vector2 rootPrevious = start + outward * CaveVisualRange(ref state, -0.025f, 0.04f);
            for (int toothIndex = 0; toothIndex < toothCount; toothIndex++)
            {
                float centerDistance = (toothIndex + 0.5f) * slotWidth
                    + CaveVisualRange(ref state, -0.12f, 0.12f) * slotWidth;
                float width = slotWidth * CaveVisualRange(ref state, 0.62f, 1.04f);
                float height = maxHeight * CaveVisualRange(ref state, 0.34f, 1f);
                Vector2 center = start + tangent * centerDistance;
                Vector2 baseLeft = center - tangent * (width * 0.5f);
                Vector2 baseRight = center + tangent * (width * 0.5f);
                Vector2 shoulderLeft = center - tangent * (width * 0.29f) + outward * (height * 0.2f);
                Vector2 shoulderRight = center + tangent * (width * 0.33f) + outward * (height * 0.24f);
                Vector2 tip = center
                    + outward * height
                    + tangent * CaveVisualRange(ref state, -0.14f, 0.14f) * width;
                Vector2[] polygon = { baseLeft, shoulderLeft, tip, shoulderRight, baseRight };
                Color fill = new Color(
                    CaveBackdropRockColor.r,
                    CaveBackdropRockColor.g,
                    CaveBackdropRockColor.b,
                    CaveVisualRange(ref state, 0.09f, 0.155f));
                AppendFilledCavePolygon(fillVertices, fillColors, fillTriangles, polygon, fill);

                for (int edgeIndex = 0; edgeIndex < polygon.Length - 1; edgeIndex++)
                {
                    AppendCaveWobblyStroke(
                        lineVertices,
                        lineColors,
                        lineTriangles,
                        polygon[edgeIndex],
                        polygon[edgeIndex + 1],
                        CaveVisualRange(ref state, 0.021f, 0.039f),
                        outline,
                        0.027f,
                        ref state);
                }

                int hatchCount = 2 + Mathf.FloorToInt(CaveVisual01(ref state) * 4f);
                for (int hatchIndex = 0; hatchIndex < hatchCount; hatchIndex++)
                {
                    float baseT = (hatchIndex + CaveVisualRange(ref state, 0.35f, 0.72f)) / (hatchCount + 0.8f);
                    Vector2 hatchFrom = Vector2.Lerp(baseLeft, baseRight, Mathf.Clamp01(baseT));
                    Vector2 hatchTo = Vector2.Lerp(
                        hatchFrom,
                        tip + tangent * CaveVisualRange(ref state, -0.08f, 0.08f) * width,
                        CaveVisualRange(ref state, 0.42f, 0.82f));
                    AppendCaveWobblyStroke(
                        lineVertices, lineColors, lineTriangles,
                        hatchFrom, hatchTo, 0.012f, hatch, 0.02f, ref state);
                }

                Vector2 rootNext = baseRight + outward * CaveVisualRange(ref state, -0.02f, 0.055f);
                AppendCaveWobblyStroke(
                    lineVertices,
                    lineColors,
                    lineTriangles,
                    rootPrevious,
                    rootNext,
                    CaveVisualRange(ref state, 0.025f, 0.045f),
                    outline,
                    0.022f,
                    ref state);
                rootPrevious = rootNext;
            }
        }

        private static void AppendCaveWobblyStroke(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 from,
            Vector2 to,
            float width,
            Color color,
            float wobble,
            ref uint state)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length <= 0.001f)
            {
                return;
            }

            Vector2 direction = delta / length;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            int segmentCount = Mathf.Clamp(Mathf.CeilToInt(length / 0.2f), 2, 28);
            float phase = CaveVisualRange(ref state, -Mathf.PI, Mathf.PI);
            Vector2 previous = from;
            for (int segment = 1; segment <= segmentCount; segment++)
            {
                float t = segment / (float)segmentCount;
                float envelope = Mathf.Sin(t * Mathf.PI);
                float displacement = (
                    Mathf.Sin(phase + t * Mathf.PI * CaveVisualRange(ref state, 1.35f, 2.4f)) * wobble
                    + CaveVisualRange(ref state, -wobble * 0.32f, wobble * 0.32f)) * envelope;
                Vector2 next = Vector2.Lerp(from, to, t) + normal * displacement;
                if (segment == 1
                    || segment == segmentCount
                    || CaveVisual01(ref state) > 0.1f)
                {
                    AppendPencilQuad(
                        vertices,
                        colors,
                        triangles,
                        previous,
                        next,
                        width * CaveVisualRange(ref state, 0.82f, 1.18f),
                        color);
                }
                previous = next;
            }
        }

        private static void AppendCaveBackdropRockTooth(
            List<Vector3> rockVertices,
            List<Color> rockColors,
            List<int> rockTriangles,
            List<Vector3> pencilVertices,
            List<Color> pencilColors,
            List<int> pencilTriangles,
            Vector2 anchor,
            Vector2 tangent,
            Vector2 direction,
            float width,
            float height,
            ref uint state)
        {
            tangent.Normalize();
            direction.Normalize();
            float lean = CaveVisualRange(ref state, -0.16f, 0.16f) * width;
            Vector2[] edge =
            {
                anchor - tangent * (width * 0.5f),
                anchor - tangent * (width * 0.29f) + direction * (height * 0.24f),
                anchor + direction * height + tangent * lean,
                anchor + tangent * (width * 0.31f) + direction * (height * 0.2f),
                anchor + tangent * (width * 0.5f)
            };
            AppendFilledCavePolygon(
                rockVertices,
                rockColors,
                rockTriangles,
                edge,
                new Color(
                    CaveBackdropRockColor.r,
                    CaveBackdropRockColor.g,
                    CaveBackdropRockColor.b,
                    CaveVisualRange(ref state, 0.11f, 0.2f)));

            Color outline = new Color(
                CaveBackdropGraphiteColor.r,
                CaveBackdropGraphiteColor.g,
                CaveBackdropGraphiteColor.b,
                CaveVisualRange(ref state, 0.2f, 0.34f));
            for (int i = 0; i < edge.Length - 1; i++)
            {
                AppendPencilQuad(
                    pencilVertices,
                    pencilColors,
                    pencilTriangles,
                    edge[i],
                    edge[i + 1],
                    CaveVisualRange(ref state, 0.025f, 0.052f),
                    outline);
            }
            AppendPencilQuad(pencilVertices, pencilColors, pencilTriangles, edge[4], edge[0], 0.03f, outline);

            Color hatch = new Color(0.28f, 0.33f, 0.38f, 0.12f);
            for (int hatchIndex = 0; hatchIndex < 3; hatchIndex++)
            {
                float baseT = (hatchIndex + 1f) / 4f;
                Vector2 from = Vector2.Lerp(edge[0], edge[4], baseT);
                Vector2 to = Vector2.Lerp(from, edge[2], CaveVisualRange(ref state, 0.4f, 0.72f));
                AppendPencilQuad(pencilVertices, pencilColors, pencilTriangles, from, to, 0.018f, hatch);
            }
        }

        private static void AppendCaveBackdropBrokenContour(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            float left,
            float right,
            float y,
            int row,
            ref uint state)
        {
            int segmentCount = Mathf.Clamp(Mathf.CeilToInt((right - left) / 5.5f), 5, 32);
            Vector2 previous = new Vector2(left, y + CaveVisualRange(ref state, -0.16f, 0.16f));
            Color contour = new Color(0.22f, 0.27f, 0.31f, 0.16f);
            for (int segment = 1; segment <= segmentCount; segment++)
            {
                Vector2 next = new Vector2(
                    Mathf.Lerp(left, right, segment / (float)segmentCount),
                    y + Mathf.Sin(row * 1.71f + segment * 1.29f) * 0.22f
                        + CaveVisualRange(ref state, -0.09f, 0.09f));
                if (CaveVisual01(ref state) > 0.22f)
                {
                    AppendPencilQuad(vertices, colors, triangles, previous, next, 0.024f, contour);
                    AppendPencilQuad(
                        vertices,
                        colors,
                        triangles,
                        previous + new Vector2(0f, -0.035f),
                        next + new Vector2(0f, -0.035f),
                        0.011f,
                        new Color(0.54f, 0.59f, 0.62f, 0.1f));
                }
                previous = next;
            }
        }

        private static void AppendCaveBackdropCrystal(
            List<Vector3> rockVertices,
            List<Color> rockColors,
            List<int> rockTriangles,
            List<Vector3> pencilVertices,
            List<Color> pencilColors,
            List<int> pencilTriangles,
            Vector2 anchor,
            float scale,
            ref uint state)
        {
            Color[] fills =
            {
                new Color(0.35f, 0.58f, 0.82f, 0.13f),
                new Color(0.55f, 0.4f, 0.78f, 0.12f),
                new Color(0.33f, 0.72f, 0.76f, 0.11f)
            };
            for (int crystal = 0; crystal < 3; crystal++)
            {
                float offset = (crystal - 1) * scale * 0.34f;
                float height = scale * (crystal == 1 ? 1.25f : CaveVisualRange(ref state, 0.65f, 0.92f));
                float width = scale * (crystal == 1 ? 0.44f : 0.34f);
                Vector2 baseLeft = anchor + new Vector2(offset - width * 0.5f, 0f);
                Vector2 baseRight = anchor + new Vector2(offset + width * 0.5f, 0f);
                Vector2 tip = anchor + new Vector2(offset + CaveVisualRange(ref state, -0.08f, 0.08f), height);
                Vector2[] polygon = { baseLeft, tip, baseRight };
                AppendFilledCavePolygon(rockVertices, rockColors, rockTriangles, polygon, fills[crystal]);
                Color outline = new Color(fills[crystal].r, fills[crystal].g, fills[crystal].b, 0.28f);
                AppendPencilQuad(pencilVertices, pencilColors, pencilTriangles, baseLeft, tip, 0.025f, outline);
                AppendPencilQuad(pencilVertices, pencilColors, pencilTriangles, tip, baseRight, 0.025f, outline);
            }
        }

        private static void AppendCaveBackdropCrack(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 root,
            float length,
            ref uint state)
        {
            float angle = CaveVisualRange(ref state, -2.7f, -0.44f);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 tangent = new Vector2(-direction.y, direction.x);
            Vector2 previous = root;
            Color crack = new Color(0.17f, 0.2f, 0.24f, 0.2f);
            for (int segment = 1; segment <= 4; segment++)
            {
                Vector2 next = root
                    + direction * (length * segment / 4f)
                    + tangent * CaveVisualRange(ref state, -0.16f, 0.16f);
                AppendPencilQuad(vertices, colors, triangles, previous, next, 0.026f - segment * 0.003f, crack);
                if (segment == 2)
                {
                    Vector2 branch = next
                        + (direction * 0.42f + tangent * (CaveVisual01(ref state) > 0.5f ? 0.7f : -0.7f)).normalized
                            * (length * 0.35f);
                    AppendPencilQuad(vertices, colors, triangles, next, branch, 0.017f, crack);
                }
                previous = next;
            }
        }

        private static void AppendFilledCavePolygon(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2[] edge,
            Color color)
        {
            if (edge == null || edge.Length < 3)
            {
                return;
            }

            Vector2 center = Vector2.zero;
            for (int i = 0; i < edge.Length; i++) center += edge[i];
            center /= edge.Length;
            int first = vertices.Count;
            vertices.Add(center);
            colors.Add(color);
            for (int i = 0; i < edge.Length; i++)
            {
                vertices.Add(edge[i]);
                colors.Add(color);
            }
            for (int i = 0; i < edge.Length; i++)
            {
                triangles.Add(first);
                triangles.Add(first + 1 + i);
                triangles.Add(first + 1 + ((i + 1) % edge.Length));
            }
        }

        private static void AddUnderwaterSandFill(Transform parent, Vector2 size, int seed)
        {
            AddSolidPencilFill(parent, size, UnderwaterSandStrokeColor, 4, 0.72f);
            uint state = CreateCaveVisualState(seed);
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            float left = -size.x * 0.5f + 0.08f;
            float right = size.x * 0.5f - 0.08f;
            float bottom = -size.y * 0.5f + 0.06f;
            float top = size.y * 0.5f - 0.06f;
            int grainCount = Mathf.Clamp(Mathf.RoundToInt(size.x * Mathf.Max(1f, size.y) * 4.2f), 18, 260);
            Color grain = new Color(
                UnderwaterSandStrokeColor.r,
                UnderwaterSandStrokeColor.g,
                UnderwaterSandStrokeColor.b,
                0.3f);
            for (int grainIndex = 0; grainIndex < grainCount; grainIndex++)
            {
                Vector2 center = new Vector2(
                    CaveVisualRange(ref state, left, right),
                    CaveVisualRange(ref state, bottom, top));
                float length = CaveVisualRange(ref state, 0.035f, 0.13f);
                float angle = CaveVisualRange(ref state, -0.45f, 0.45f);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                AppendPencilQuad(
                    vertices,
                    colors,
                    triangles,
                    center - direction * (length * 0.5f),
                    center + direction * (length * 0.5f),
                    CaveVisualRange(ref state, 0.008f, 0.016f),
                    grain);
            }

            Color wave = new Color(0.72f, 0.51f, 0.23f, 0.46f);
            for (int waveIndex = 0; waveIndex < 2; waveIndex++)
            {
                AppendCaveWobblyStroke(
                    vertices,
                    colors,
                    triangles,
                    new Vector2(left, top - waveIndex * 0.08f),
                    new Vector2(right, top - waveIndex * 0.08f),
                    waveIndex == 0 ? 0.035f : 0.019f,
                    wave,
                    0.035f,
                    ref state);
            }
            CreatePencilMesh(parent, "Underwater Sand Grains", vertices, colors, triangles, 7);
        }

        private static void AddUnderwaterRockFill(Transform parent, Vector2 size, int seed)
        {
            AddSolidPencilFill(parent, size, UnderwaterRockStrokeColor, 4, 1.16f);
            uint state = CreateCaveVisualState(seed);
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            float left = -size.x * 0.5f + 0.08f;
            float right = size.x * 0.5f - 0.08f;
            float bottom = -size.y * 0.5f + 0.08f;
            float top = size.y * 0.5f - 0.08f;
            int crackCount = Mathf.Clamp(Mathf.RoundToInt((size.x + size.y) / 3.1f), 1, 24);
            Color crack = new Color(0.08f, 0.2f, 0.24f, 0.48f);
            for (int crackIndex = 0; crackIndex < crackCount; crackIndex++)
            {
                Vector2 root = new Vector2(
                    CaveVisualRange(ref state, left, right),
                    CaveVisualRange(ref state, bottom, top));
                Vector2 previous = root;
                float angle = CaveVisualRange(ref state, -2.75f, -0.38f);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 normal = new Vector2(-direction.y, direction.x);
                float length = CaveVisualRange(ref state, 0.32f, 0.9f);
                for (int segment = 1; segment <= 3; segment++)
                {
                    Vector2 next = root
                        + direction * (length * segment / 3f)
                        + normal * CaveVisualRange(ref state, -0.08f, 0.08f);
                    next.x = Mathf.Clamp(next.x, left, right);
                    next.y = Mathf.Clamp(next.y, bottom, top);
                    AppendPencilQuad(
                        vertices,
                        colors,
                        triangles,
                        previous,
                        next,
                        0.019f - segment * 0.002f,
                        crack);
                    previous = next;
                }
            }

            int shadeCount = Mathf.Clamp(Mathf.RoundToInt(size.x * size.y / 1.8f), 4, 180);
            Color shade = new Color(
                UnderwaterRockAccentColor.r,
                UnderwaterRockAccentColor.g,
                UnderwaterRockAccentColor.b,
                0.2f);
            for (int shadeIndex = 0; shadeIndex < shadeCount; shadeIndex++)
            {
                Vector2 start = new Vector2(
                    CaveVisualRange(ref state, left, right),
                    CaveVisualRange(ref state, bottom, top));
                Vector2 end = start + new Vector2(
                    CaveVisualRange(ref state, 0.18f, 0.62f),
                    CaveVisualRange(ref state, 0.08f, 0.32f));
                end.x = Mathf.Clamp(end.x, left, right);
                end.y = Mathf.Clamp(end.y, bottom, top);
                AppendPencilQuad(vertices, colors, triangles, start, end, 0.012f, shade);
            }
            CreatePencilMesh(parent, "Underwater Rock Crayon Texture", vertices, colors, triangles, 7);
        }

        private static void AddUnderwaterSandCap(Transform parent, Vector2 size, int seed)
        {
            float capHeight = Mathf.Min(0.34f, Mathf.Max(0.18f, size.y * 0.22f));
            GameObject cap = new GameObject("Underwater Sand Cap");
            cap.transform.SetParent(parent, false);
            cap.transform.localPosition = new Vector3(0f, size.y * 0.5f - capHeight * 0.5f, -0.01f);
            cap.transform.localScale = new Vector3(size.x - 0.08f, capHeight, 1f);
            SpriteRenderer renderer = cap.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = UnderwaterSandPaperColor;
            renderer.sortingOrder = 8;

            uint state = CreateCaveVisualState(seed);
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            float y = size.y * 0.5f - 0.015f;
            AppendCaveWobblyStroke(
                vertices,
                colors,
                triangles,
                new Vector2(-size.x * 0.5f + 0.04f, y),
                new Vector2(size.x * 0.5f - 0.04f, y),
                0.043f,
                new Color(UnderwaterSandStrokeColor.r, UnderwaterSandStrokeColor.g, UnderwaterSandStrokeColor.b, 0.88f),
                0.04f,
                ref state);
            int grains = Mathf.Clamp(Mathf.RoundToInt(size.x * 2.4f), 5, 90);
            for (int grainIndex = 0; grainIndex < grains; grainIndex++)
            {
                Vector2 grain = new Vector2(
                    CaveVisualRange(ref state, -size.x * 0.5f + 0.08f, size.x * 0.5f - 0.08f),
                    y - CaveVisualRange(ref state, 0.07f, capHeight - 0.035f));
                AppendPencilQuad(
                    vertices,
                    colors,
                    triangles,
                    grain,
                    grain + new Vector2(CaveVisualRange(ref state, 0.035f, 0.1f), 0.015f),
                    0.012f,
                    new Color(0.55f, 0.37f, 0.16f, 0.34f));
            }
            CreatePencilMesh(parent, "Underwater Sand Cap Pencil", vertices, colors, triangles, 10);
        }

        private static void AddUnderwaterTerrainBoxOutline(Transform parent, Vector2 size, Color color)
        {
            AddSolidSketchBoxOutline(parent, size, color, 0.068f, 13);
            bool sand = Mathf.Abs(color.r - UnderwaterSandStrokeColor.r) < 0.06f
                && Mathf.Abs(color.g - UnderwaterSandStrokeColor.g) < 0.06f;
            Color accent = sand
                ? new Color(0.79f, 0.61f, 0.3f, 0.42f)
                : new Color(
                    UnderwaterRockAccentColor.r,
                    UnderwaterRockAccentColor.g,
                    UnderwaterRockAccentColor.b,
                    0.42f);
            AddSolidSketchBoxOutline(
                parent,
                size,
                accent,
                0.026f,
                14,
                new Vector3(0.025f, -0.018f, 0f));
        }

        private static void AddUnderwaterRockUnderside(Transform parent, Vector2 size, int seed)
        {
            // Broad, irregular rock teeth sit behind the real collider. They are
            // decorative only and preserve the authored rectangular physics.
            AddCaveStalactitesOnWorldBottomEdge(parent, size, seed);
        }

        private static void AddCaveTerrainFill(
            Transform parent,
            Vector2 size,
            Color color,
            int seed)
        {
            AddSolidPencilFill(parent, size, color, 4, 1.45f);
            AddCaveRockTexture(parent, size, seed);
            AddCaveTerrainEdgeGradient(parent, size, color);
            AddCaveCracks(parent, size, seed + 389);
        }

        private static void AddCaveTerrainBoxOutline(Transform parent, Vector2 size)
        {
            Vector2[] corners =
            {
                new Vector2(-size.x * 0.5f, -size.y * 0.5f),
                new Vector2(size.x * 0.5f, -size.y * 0.5f),
                new Vector2(size.x * 0.5f, size.y * 0.5f),
                new Vector2(-size.x * 0.5f, size.y * 0.5f)
            };
            List<Vector3> main = new List<Vector3>();
            List<Vector3> echo = new List<Vector3>();
            List<Vector3> dryPencil = new List<Vector3>();
            for (int edgeIndex = 0; edgeIndex < corners.Length; edgeIndex++)
            {
                Vector2 from = corners[edgeIndex];
                Vector2 to = corners[(edgeIndex + 1) % corners.Length];
                Vector2 direction = to - from;
                Vector2 inward = new Vector2(-direction.y, direction.x).normalized;
                int segmentCount = Mathf.Clamp(Mathf.CeilToInt(direction.magnitude / 0.7f), 6, 28);
                for (int pointIndex = 0; pointIndex <= segmentCount; pointIndex++)
                {
                    if (edgeIndex > 0 && pointIndex == 0)
                    {
                        continue;
                    }

                    float t = pointIndex / (float)segmentCount;
                    float envelope = Mathf.Sin(t * Mathf.PI);
                    float wobble = (
                        Mathf.Sin(edgeIndex * 4.17f + pointIndex * 1.91f) * 0.026f
                        + Mathf.Sin(edgeIndex * 1.73f + pointIndex * 4.07f) * 0.012f) * envelope;
                    Vector2 point = Vector2.Lerp(from, to, t);
                    main.Add(point + inward * wobble);
                    echo.Add(point + inward * (0.038f + wobble * 0.68f));
                    dryPencil.Add(point + inward * (-0.026f - wobble * 0.44f));
                }
            }

            main.Add(main[0]);
            echo.Add(echo[0]);
            dryPencil.Add(dryPencil[0]);
            AddDoodleLine(
                "Cave Rough Pencil Outline",
                parent,
                main.ToArray(),
                CaveTerrainStrokeColor,
                0.096f,
                13);
            AddDoodleLine(
                "Cave Graphite Echo",
                parent,
                echo.ToArray(),
                CaveTerrainAccentColor,
                0.038f,
                12);
            AddDoodleLine(
                "Cave Dry Pencil Edge",
                parent,
                dryPencil.ToArray(),
                new Color(CaveTerrainStrokeColor.r, CaveTerrainStrokeColor.g, CaveTerrainStrokeColor.b, 0.58f),
                0.028f,
                14);
        }

        private static void AddCaveRockTexture(Transform parent, Vector2 size, int seed)
        {
            if (parent == null || size.x < 0.18f || size.y < 0.18f)
            {
                return;
            }

            float left = -size.x * 0.5f + 0.055f;
            float right = size.x * 0.5f - 0.055f;
            float bottom = -size.y * 0.5f + 0.055f;
            float top = size.y * 0.5f - 0.055f;
            if (right <= left || top <= bottom)
            {
                return;
            }

            uint state = CreateCaveVisualState(seed);
            int strokeCount = Mathf.Clamp(
                Mathf.RoundToInt(size.x * size.y * 1.15f + (size.x + size.y) * 1.8f),
                8,
                86);
            List<Vector3> vertices = new List<Vector3>(strokeCount * 12);
            List<Color> colors = new List<Color>(strokeCount * 12);
            List<int> triangles = new List<int>(strokeCount * 18);
            for (int strokeIndex = 0; strokeIndex < strokeCount; strokeIndex++)
            {
                float x = CaveVisualRange(ref state, left, right);
                float y = CaveVisualRange(ref state, bottom, top);
                float maxLength = Mathf.Min(1.35f, Mathf.Max(0.16f, right - x));
                float length = CaveVisualRange(ref state, 0.18f, Mathf.Max(0.19f, maxLength));
                float rise = CaveVisualRange(ref state, -0.17f, 0.28f);
                if ((strokeIndex & 3) == 0)
                {
                    rise *= -0.55f;
                }

                Vector2 start = new Vector2(x, y);
                Vector2 end = new Vector2(Mathf.Min(right, x + length), Mathf.Clamp(y + rise, bottom, top));
                Vector2 bend = Vector2.Lerp(start, end, CaveVisualRange(ref state, 0.38f, 0.62f));
                bend.y = Mathf.Clamp(bend.y + CaveVisualRange(ref state, -0.055f, 0.055f), bottom, top);
                Color graphite = new Color(
                    CaveTerrainStrokeColor.r,
                    CaveTerrainStrokeColor.g,
                    CaveTerrainStrokeColor.b,
                    CaveVisualRange(ref state, 0.11f, 0.25f));
                float width = CaveVisualRange(ref state, 0.009f, 0.018f);
                AppendPencilQuad(vertices, colors, triangles, start, bend, width, graphite);
                AppendPencilQuad(vertices, colors, triangles, bend, end, width * 0.9f, graphite);
            }

            int facetCount = Mathf.Clamp(Mathf.RoundToInt(size.x * size.y / 4.5f), 1, 12);
            for (int facetIndex = 0; facetIndex < facetCount; facetIndex++)
            {
                Vector2 center = new Vector2(
                    CaveVisualRange(ref state, left, right),
                    CaveVisualRange(ref state, bottom, top));
                float radiusX = CaveVisualRange(ref state, 0.12f, Mathf.Min(0.48f, size.x * 0.18f));
                float radiusY = CaveVisualRange(ref state, 0.07f, Mathf.Min(0.3f, size.y * 0.2f));
                Color facet = new Color(
                    CaveTerrainAccentColor.r,
                    CaveTerrainAccentColor.g,
                    CaveTerrainAccentColor.b,
                    CaveVisualRange(ref state, 0.1f, 0.19f));
                Vector2 a = new Vector2(Mathf.Clamp(center.x - radiusX, left, right), center.y);
                Vector2 b = new Vector2(center.x, Mathf.Clamp(center.y + radiusY, bottom, top));
                Vector2 c = new Vector2(Mathf.Clamp(center.x + radiusX, left, right), center.y - radiusY * 0.2f);
                AppendPencilQuad(vertices, colors, triangles, a, b, 0.012f, facet);
                AppendPencilQuad(vertices, colors, triangles, b, c, 0.012f, facet);
            }

            CreatePencilMesh(parent, "Cave Layered Graphite Texture", vertices, colors, triangles, 5);
        }

        private static void AddCaveCracks(Transform parent, Vector2 size, int seed)
        {
            if (parent == null || size.x < 0.32f || size.y < 0.24f)
            {
                return;
            }

            uint state = CreateCaveVisualState(seed);
            bool horizontalSlab = size.x > size.y * 1.28f;
            bool verticalSlab = size.y > size.x * 1.28f;
            float longSide = Mathf.Max(size.x, size.y);
            int crackCount = Mathf.Clamp(Mathf.FloorToInt(longSide / 5.2f) + 1, 1, 7);
            List<Vector3> vertices = new List<Vector3>(crackCount * 44);
            List<Color> colors = new List<Color>(crackCount * 44);
            List<int> triangles = new List<int>(crackCount * 66);
            Color crack = new Color(0.12f, 0.13f, 0.15f, 0.72f);
            Color dry = new Color(0.38f, 0.4f, 0.43f, 0.36f);

            for (int crackIndex = 0; crackIndex < crackCount; crackIndex++)
            {
                Vector2 root;
                Vector2 inward;
                float depth;
                if (horizontalSlab)
                {
                    bool fromTop = CaveVisual01(ref state) > 0.36f;
                    root = new Vector2(
                        CaveVisualRange(ref state, -size.x * 0.4f, size.x * 0.4f),
                        (fromTop ? 1f : -1f) * (size.y * 0.5f - 0.025f));
                    inward = fromTop ? Vector2.down : Vector2.up;
                    depth = Mathf.Clamp(size.y * CaveVisualRange(ref state, 0.34f, 0.72f), 0.12f, 0.72f);
                }
                else if (verticalSlab)
                {
                    bool fromRight = CaveVisual01(ref state) > 0.5f;
                    root = new Vector2(
                        (fromRight ? 1f : -1f) * (size.x * 0.5f - 0.025f),
                        CaveVisualRange(ref state, -size.y * 0.4f, size.y * 0.4f));
                    inward = fromRight ? Vector2.left : Vector2.right;
                    depth = Mathf.Clamp(size.x * CaveVisualRange(ref state, 0.34f, 0.72f), 0.12f, 0.72f);
                }
                else
                {
                    float angle = CaveVisualRange(ref state, 0f, Mathf.PI * 2f);
                    inward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    root = new Vector2(
                        CaveVisualRange(ref state, -size.x * 0.28f, size.x * 0.28f),
                        CaveVisualRange(ref state, -size.y * 0.28f, size.y * 0.28f));
                    depth = Mathf.Clamp(Mathf.Min(size.x, size.y) * CaveVisualRange(ref state, 0.24f, 0.46f), 0.13f, 0.68f);
                }

                Vector2 tangent = new Vector2(-inward.y, inward.x);
                Vector2 p1 = root + inward * (depth * 0.34f) + tangent * CaveVisualRange(ref state, -0.1f, 0.1f);
                Vector2 p2 = root + inward * (depth * 0.68f) + tangent * CaveVisualRange(ref state, -0.13f, 0.13f);
                Vector2 tip = root + inward * depth + tangent * CaveVisualRange(ref state, -0.11f, 0.11f);
                AppendCaveCrackStroke(vertices, colors, triangles, root, p1, crack, dry);
                AppendCaveCrackStroke(vertices, colors, triangles, p1, p2, crack, dry);
                AppendCaveCrackStroke(vertices, colors, triangles, p2, tip, crack, dry);

                float branchLength = depth * CaveVisualRange(ref state, 0.28f, 0.48f);
                Vector2 branchDirection = (inward * 0.52f + tangent * (CaveVisual01(ref state) > 0.5f ? 0.85f : -0.85f)).normalized;
                Vector2 branchBend = p1 + branchDirection * (branchLength * 0.55f) + tangent * CaveVisualRange(ref state, -0.035f, 0.035f);
                Vector2 branchTip = p1 + branchDirection * branchLength;
                AppendCaveCrackStroke(vertices, colors, triangles, p1, branchBend, crack, dry);
                AppendCaveCrackStroke(vertices, colors, triangles, branchBend, branchTip, crack, dry);

                if ((crackIndex + seed & 1) == 0)
                {
                    Vector2 secondDirection = (inward * 0.38f - branchDirection * 0.72f).normalized;
                    AppendCaveCrackStroke(
                        vertices,
                        colors,
                        triangles,
                        p2,
                        p2 + secondDirection * (branchLength * 0.62f),
                        crack,
                        dry);
                }
            }

            CreatePencilMesh(parent, "Cave Hand Drawn Cracks", vertices, colors, triangles, 10);
        }

        private static void AppendCaveCrackStroke(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 from,
            Vector2 to,
            Color crack,
            Color dry)
        {
            AppendPencilQuad(vertices, colors, triangles, from, to, 0.028f, crack);
            Vector2 offset = new Vector2(0.012f, -0.008f);
            AppendPencilQuad(vertices, colors, triangles, from + offset, to + offset, 0.011f, dry);
        }

        private static void AddCaveTerrainEdgeGradient(Transform parent, Vector2 size, Color color)
        {
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            float horizontalDepth = Mathf.Min(halfWidth, Mathf.Clamp(size.x * 0.18f, 0.1f, 0.78f));
            float verticalDepth = Mathf.Min(halfHeight, Mathf.Clamp(size.y * 0.27f, 0.1f, 0.72f));
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            Color edge = new Color(color.r, color.g, color.b, 0.22f);
            Color clear = new Color(color.r, color.g, color.b, 0.015f);

            AppendGradientQuad(
                vertices, colors, triangles,
                new Vector2(-halfWidth, -halfHeight), new Vector2(halfWidth, -halfHeight),
                new Vector2(-halfWidth, -halfHeight + verticalDepth), new Vector2(halfWidth, -halfHeight + verticalDepth),
                edge, clear);
            AppendGradientQuad(
                vertices, colors, triangles,
                new Vector2(halfWidth, -halfHeight), new Vector2(halfWidth, halfHeight),
                new Vector2(halfWidth - horizontalDepth, -halfHeight), new Vector2(halfWidth - horizontalDepth, halfHeight),
                edge, clear);
            AppendGradientQuad(
                vertices, colors, triangles,
                new Vector2(halfWidth, halfHeight), new Vector2(-halfWidth, halfHeight),
                new Vector2(halfWidth, halfHeight - verticalDepth), new Vector2(-halfWidth, halfHeight - verticalDepth),
                edge, clear);
            AppendGradientQuad(
                vertices, colors, triangles,
                new Vector2(-halfWidth, halfHeight), new Vector2(-halfWidth, -halfHeight),
                new Vector2(-halfWidth + horizontalDepth, halfHeight), new Vector2(-halfWidth + horizontalDepth, -halfHeight),
                edge, clear);

            AppendCaveEdgeScribbles(
                vertices, colors, triangles,
                new Vector2(-halfWidth, -halfHeight), new Vector2(halfWidth, -halfHeight),
                Vector2.up * verticalDepth, 11);
            AppendCaveEdgeScribbles(
                vertices, colors, triangles,
                new Vector2(halfWidth, -halfHeight), new Vector2(halfWidth, halfHeight),
                Vector2.left * horizontalDepth, 23);
            AppendCaveEdgeScribbles(
                vertices, colors, triangles,
                new Vector2(halfWidth, halfHeight), new Vector2(-halfWidth, halfHeight),
                Vector2.down * verticalDepth, 37);
            AppendCaveEdgeScribbles(
                vertices, colors, triangles,
                new Vector2(-halfWidth, halfHeight), new Vector2(-halfWidth, -halfHeight),
                Vector2.right * horizontalDepth, 53);

            CreatePencilMesh(parent, "Cave Dark Edge Shading", vertices, colors, triangles, 6);
        }

        private static void AddCaveConnectedEdgeGradient(
            Transform parent,
            Vector2 from,
            Vector2 to,
            float depth)
        {
            Vector2 edge = to - from;
            if (parent == null || edge.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector2 inward = new Vector2(-edge.y, edge.x).normalized * depth;
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            Color dark = new Color(CaveTerrainStrokeColor.r, CaveTerrainStrokeColor.g, CaveTerrainStrokeColor.b, 0.22f);
            Color clear = new Color(CaveTerrainStrokeColor.r, CaveTerrainStrokeColor.g, CaveTerrainStrokeColor.b, 0.015f);
            AppendGradientQuad(vertices, colors, triangles, from, to, from + inward, to + inward, dark, clear);
            AppendCaveEdgeScribbles(vertices, colors, triangles, from, to, inward, 73);
            CreatePencilMesh(parent, "Cave Connected Edge Shading", vertices, colors, triangles, 6);
        }

        private static void AppendCaveEdgeScribbles(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 from,
            Vector2 to,
            Vector2 inward,
            int seed)
        {
            Vector2 edge = to - from;
            float length = edge.magnitude;
            if (length <= 0.05f || inward.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector2 direction = edge / length;
            Vector2 inwardDirection = inward.normalized;
            int segmentCount = Mathf.Clamp(Mathf.CeilToInt(length / 0.78f), 2, 34);
            for (int band = 0; band < 4; band++)
            {
                float bandT = (band + 0.4f) / 4f;
                float distance = inward.magnitude * bandT;
                Color graphite = new Color(
                    CaveTerrainStrokeColor.r,
                    CaveTerrainStrokeColor.g,
                    CaveTerrainStrokeColor.b,
                    Mathf.Lerp(0.22f, 0.06f, bandT));
                for (int segment = 0; segment < segmentCount; segment++)
                {
                    float phase = seed * 0.19f + band * 2.31f + segment * 1.73f;
                    float startT = (segment + 0.05f + Mathf.Abs(Mathf.Sin(phase)) * 0.1f) / segmentCount;
                    float endT = (segment + 0.62f + Mathf.Abs(Mathf.Cos(phase * 1.27f)) * 0.25f) / segmentCount;
                    Vector2 offset = inwardDirection * (distance + Mathf.Sin(phase * 1.61f) * 0.015f);
                    Vector2 a = from + direction * (length * Mathf.Clamp01(startT)) + offset;
                    Vector2 b = from + direction * (length * Mathf.Clamp01(endT)) + offset
                        + inwardDirection * (Mathf.Cos(phase) * 0.02f);
                    AppendPencilQuad(vertices, colors, triangles, a, b, 0.012f, graphite);
                }
            }
        }

        private static void AddCaveStalactitesOnWorldBottomEdge(
            Transform parent,
            Vector2 size,
            int seed)
        {
            if (parent == null)
            {
                return;
            }

            Vector3 localDown3 = parent.InverseTransformDirection(Vector3.down);
            Vector2 localDown = new Vector2(localDown3.x, localDown3.y);
            if (localDown.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector2 outward;
            float halfLength;
            float halfDepth;
            if (Mathf.Abs(localDown.x) > Mathf.Abs(localDown.y))
            {
                outward = localDown.x >= 0f ? Vector2.right : Vector2.left;
                halfLength = size.y * 0.5f;
                halfDepth = size.x * 0.5f;
            }
            else
            {
                outward = localDown.y >= 0f ? Vector2.up : Vector2.down;
                halfLength = size.x * 0.5f;
                halfDepth = size.y * 0.5f;
            }

            float length = halfLength * 2f;
            float depth = halfDepth * 2f;
            if (length < Mathf.Max(1.35f, depth * 1.15f))
            {
                return;
            }

            Vector2 tangent = new Vector2(-outward.y, outward.x);
            Vector2 edgeCenter = outward * halfDepth;
            Vector2 from = edgeCenter - tangent * halfLength;
            Vector2 to = edgeCenter + tangent * halfLength;
            AddCaveStalactitesAlongEdge(parent, from, to, outward, seed);
        }

        private static void AddCaveStalactitesAlongExposedEdge(
            Transform parent,
            Vector2 from,
            Vector2 to,
            float terrainDepth,
            int seed)
        {
            Vector2 edge = to - from;
            float length = edge.magnitude;
            if (parent == null || length < Mathf.Max(1.35f, terrainDepth * 1.15f))
            {
                return;
            }

            Vector2 inward = new Vector2(-edge.y, edge.x).normalized;
            Vector2 outward = -inward;
            Vector3 worldOutward3 = parent.TransformDirection(new Vector3(outward.x, outward.y, 0f));
            Vector2 worldOutward = new Vector2(worldOutward3.x, worldOutward3.y).normalized;
            if (Vector2.Dot(worldOutward, Vector2.down) < 0.76f)
            {
                return;
            }

            AddCaveStalactitesAlongEdge(parent, from, to, outward, seed);
        }

        private static void AddCaveStalactitesAlongEdge(
            Transform parent,
            Vector2 from,
            Vector2 to,
            Vector2 outward,
            int seed)
        {
            Vector2 edge = to - from;
            float length = edge.magnitude;
            if (length < 1.35f || outward.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector2 tangent = edge / length;
            outward.Normalize();
            uint state = CreateCaveVisualState(seed);
            int estimatedClusters = Mathf.Clamp(Mathf.RoundToInt(length / 1.15f), 1, 96);
            List<Vector3> fillVertices = new List<Vector3>(estimatedClusters * 24);
            List<Color> fillColors = new List<Color>(estimatedClusters * 24);
            List<int> fillTriangles = new List<int>(estimatedClusters * 30);
            List<Vector3> lineVertices = new List<Vector3>(estimatedClusters * 150);
            List<Color> lineColors = new List<Color>(estimatedClusters * 150);
            List<int> lineTriangles = new List<int>(estimatedClusters * 210);
            float cursor = CaveVisualRange(ref state, 0.04f, 0.28f);
            int guard = 0;
            while (cursor < length - 0.22f && guard++ < 128)
            {
                cursor += CaveVisualRange(ref state, 0.06f, 0.38f);
                float clusterSpan = Mathf.Min(
                    CaveVisualRange(ref state, 0.45f, 1.55f),
                    length - cursor);
                if (clusterSpan < 0.28f)
                {
                    break;
                }

                float centerDistance = cursor + clusterSpan * CaveVisualRange(ref state, 0.42f, 0.58f);
                Vector2 anchor = from + tangent * centerDistance + outward * 0.018f;
                int variant = Mathf.FloorToInt(CaveVisual01(ref state) * 4f) % 4;
                float width = Mathf.Clamp(clusterSpan * CaveVisualRange(ref state, 0.5f, 0.83f), 0.31f, 1.18f);
                float height = CaveVisualRange(ref state, 0.5f, 1.62f);
                AppendCaveStalactiteCluster(
                    fillVertices,
                    fillColors,
                    fillTriangles,
                    lineVertices,
                    lineColors,
                    lineTriangles,
                    anchor,
                    tangent,
                    outward,
                    width,
                    height,
                    variant,
                    ref state);
                cursor += clusterSpan + CaveVisualRange(ref state, 0.1f, 0.72f);
            }

            CreatePencilMesh(parent, "Cave Stalactite Fill", fillVertices, fillColors, fillTriangles, 8);
            CreatePencilMesh(parent, "Cave Stalactite Pencil", lineVertices, lineColors, lineTriangles, 14);
        }

        private static void AppendCaveStalactiteCluster(
            List<Vector3> fillVertices,
            List<Color> fillColors,
            List<int> fillTriangles,
            List<Vector3> lineVertices,
            List<Color> lineColors,
            List<int> lineTriangles,
            Vector2 anchor,
            Vector2 tangent,
            Vector2 outward,
            float width,
            float height,
            int variant,
            ref uint state)
        {
            switch (variant)
            {
                case 0:
                    AppendCaveStalactiteTooth(
                        fillVertices, fillColors, fillTriangles, lineVertices, lineColors, lineTriangles,
                        anchor, tangent, outward, 0f, width, height * 1.12f,
                        CaveVisualRange(ref state, -0.12f, 0.12f), ref state);
                    break;
                case 1:
                    AppendCaveStalactiteTooth(
                        fillVertices, fillColors, fillTriangles, lineVertices, lineColors, lineTriangles,
                        anchor, tangent, outward, -width * 0.21f, width * 0.58f, height * 0.72f, -0.05f, ref state);
                    AppendCaveStalactiteTooth(
                        fillVertices, fillColors, fillTriangles, lineVertices, lineColors, lineTriangles,
                        anchor, tangent, outward, width * 0.2f, width * 0.52f, height * 1.05f, 0.06f, ref state);
                    break;
                case 2:
                    AppendCaveStalactiteTooth(
                        fillVertices, fillColors, fillTriangles, lineVertices, lineColors, lineTriangles,
                        anchor, tangent, outward, -width * 0.29f, width * 0.38f, height * 0.58f, -0.025f, ref state);
                    AppendCaveStalactiteTooth(
                        fillVertices, fillColors, fillTriangles, lineVertices, lineColors, lineTriangles,
                        anchor, tangent, outward, 0f, width * 0.54f, height * 1.18f, 0.035f, ref state);
                    AppendCaveStalactiteTooth(
                        fillVertices, fillColors, fillTriangles, lineVertices, lineColors, lineTriangles,
                        anchor, tangent, outward, width * 0.31f, width * 0.34f, height * 0.48f, 0.015f, ref state);
                    break;
                default:
                    AppendCaveStalactiteTooth(
                        fillVertices, fillColors, fillTriangles, lineVertices, lineColors, lineTriangles,
                        anchor, tangent, outward, -width * 0.08f, width * 0.76f, height * 0.82f, -0.08f, ref state);
                    AppendCaveStalactiteTooth(
                        fillVertices, fillColors, fillTriangles, lineVertices, lineColors, lineTriangles,
                        anchor, tangent, outward, width * 0.34f, width * 0.3f, height * 1.3f, 0.045f, ref state);
                    break;
            }

            Color shelfInk = new Color(0.13f, 0.145f, 0.17f, 0.82f);
            Color shelfDry = new Color(0.62f, 0.65f, 0.68f, 0.32f);
            Vector2 shelfPrevious = anchor - tangent * (width * 0.58f);
            for (int shelfSegment = 1; shelfSegment <= 5; shelfSegment++)
            {
                float t = shelfSegment / 5f;
                Vector2 shelfNext = anchor
                    + tangent * Mathf.Lerp(-width * 0.58f, width * 0.58f, t)
                    + outward * CaveVisualRange(ref state, -0.025f, 0.075f);
                AppendPencilQuad(
                    lineVertices, lineColors, lineTriangles,
                    shelfPrevious, shelfNext,
                    CaveVisualRange(ref state, 0.043f, 0.066f),
                    shelfInk);
                AppendPencilQuad(
                    lineVertices, lineColors, lineTriangles,
                    shelfPrevious - outward * 0.024f + tangent * 0.009f,
                    shelfNext - outward * 0.024f + tangent * 0.009f,
                    0.016f,
                    shelfDry);
                shelfPrevious = shelfNext;
            }
        }

        private static void AppendCaveStalactiteTooth(
            List<Vector3> fillVertices,
            List<Color> fillColors,
            List<int> fillTriangles,
            List<Vector3> lineVertices,
            List<Color> lineColors,
            List<int> lineTriangles,
            Vector2 anchor,
            Vector2 tangent,
            Vector2 outward,
            float offset,
            float width,
            float height,
            float lean,
            ref uint state)
        {
            Vector2 center = anchor + tangent * offset;
            Vector2 baseLeft = center
                - tangent * (width * 0.5f)
                + outward * CaveVisualRange(ref state, -0.035f, 0.035f);
            Vector2 baseRight = center
                + tangent * (width * 0.5f)
                + outward * CaveVisualRange(ref state, -0.035f, 0.035f);
            Vector2 lowerLeft = center
                - tangent * (width * CaveVisualRange(ref state, 0.35f, 0.44f))
                + outward * (height * CaveVisualRange(ref state, 0.12f, 0.22f));
            Vector2 upperLeft = center
                - tangent * (width * CaveVisualRange(ref state, 0.16f, 0.28f))
                + outward * (height * CaveVisualRange(ref state, 0.43f, 0.61f));
            Vector2 tip = center
                + outward * height
                + tangent * ((lean + CaveVisualRange(ref state, -0.045f, 0.045f)) * width);
            Vector2 upperRight = center
                + tangent * (width * CaveVisualRange(ref state, 0.14f, 0.27f))
                + outward * (height * CaveVisualRange(ref state, 0.48f, 0.66f));
            Vector2 lowerRight = center
                + tangent * (width * CaveVisualRange(ref state, 0.34f, 0.43f))
                + outward * (height * CaveVisualRange(ref state, 0.14f, 0.28f));
            Vector2[] edge =
            {
                baseLeft,
                lowerLeft,
                upperLeft,
                tip,
                upperRight,
                lowerRight,
                baseRight
            };
            AppendFilledCavePolygon(
                fillVertices,
                fillColors,
                fillTriangles,
                edge,
                new Color(0.31f, 0.335f, 0.37f, 0.38f));
            Vector2 washOffset = tangent * CaveVisualRange(ref state, -0.025f, 0.025f)
                - outward * CaveVisualRange(ref state, 0.006f, 0.028f);
            Vector2[] washEdge = new Vector2[edge.Length];
            for (int pointIndex = 0; pointIndex < edge.Length; pointIndex++)
            {
                washEdge[pointIndex] = edge[pointIndex] + washOffset;
            }
            AppendFilledCavePolygon(
                fillVertices,
                fillColors,
                fillTriangles,
                washEdge,
                new Color(0.52f, 0.55f, 0.58f, 0.1f));

            Color outline = new Color(0.13f, 0.145f, 0.17f, 0.86f);
            Color dry = new Color(0.56f, 0.59f, 0.62f, 0.29f);
            for (int edgeIndex = 0; edgeIndex < edge.Length; edgeIndex++)
            {
                Vector2 from = edge[edgeIndex];
                Vector2 to = edge[(edgeIndex + 1) % edge.Length];
                float widthVariation = CaveVisualRange(ref state, 0.043f, 0.069f);
                AppendCaveWobblyStroke(
                    lineVertices,
                    lineColors,
                    lineTriangles,
                    from,
                    to,
                    widthVariation,
                    outline,
                    CaveVisualRange(ref state, 0.024f, 0.047f),
                    ref state);
                Vector2 echoOffset = -outward * CaveVisualRange(ref state, 0.012f, 0.035f)
                    + tangent * CaveVisualRange(ref state, -0.015f, 0.015f);
                AppendCaveWobblyStroke(
                    lineVertices,
                    lineColors,
                    lineTriangles,
                    from + echoOffset,
                    to + echoOffset,
                    CaveVisualRange(ref state, 0.011f, 0.02f),
                    dry,
                    0.022f,
                    ref state);
            }

            Color hatch = new Color(0.14f, 0.16f, 0.19f, 0.32f);
            int hatchCount = 4 + Mathf.FloorToInt(CaveVisual01(ref state) * 6f);
            for (int hatchIndex = 0; hatchIndex < hatchCount; hatchIndex++)
            {
                if (CaveVisual01(ref state) < 0.13f)
                {
                    continue;
                }
                float baseT = (hatchIndex + CaveVisualRange(ref state, 0.3f, 0.78f)) / (hatchCount + 0.8f);
                Vector2 hatchFrom = Vector2.Lerp(baseLeft, baseRight, baseT)
                    + outward * (height * CaveVisualRange(ref state, 0.035f, 0.13f));
                Vector2 hatchTo = Vector2.Lerp(
                    hatchFrom,
                    tip + tangent * Mathf.Sin(hatchIndex * 2.13f + lean) * (width * 0.08f),
                    CaveVisualRange(ref state, 0.42f, 0.88f));
                AppendCaveWobblyStroke(
                    lineVertices,
                    lineColors,
                    lineTriangles,
                    hatchFrom,
                    hatchTo,
                    CaveVisualRange(ref state, 0.012f, 0.022f),
                    hatch,
                    0.019f,
                    ref state);
            }

            int crossHatchCount = 1 + Mathf.FloorToInt(CaveVisual01(ref state) * 3f);
            for (int crossIndex = 0; crossIndex < crossHatchCount; crossIndex++)
            {
                float t = CaveVisualRange(ref state, 0.22f, 0.68f);
                Vector2 centerLine = Vector2.Lerp(center, tip, t);
                float half = width * Mathf.Lerp(0.27f, 0.08f, t);
                AppendCaveWobblyStroke(
                    lineVertices,
                    lineColors,
                    lineTriangles,
                    centerLine - tangent * half,
                    centerLine + tangent * half,
                    0.012f,
                    new Color(hatch.r, hatch.g, hatch.b, 0.2f),
                    0.014f,
                    ref state);
            }
        }

        private static uint CreateCaveVisualState(int seed)
        {
            uint state = unchecked((uint)seed) ^ 0x9E3779B9u;
            return state == 0u ? 0xA341316Cu : state;
        }

        private static uint NextCaveVisualValue(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        private static float CaveVisual01(ref uint state)
        {
            return (NextCaveVisualValue(ref state) & 0x00FFFFFFu) / 16777215f;
        }

        private static float CaveVisualRange(ref uint state, float min, float max)
        {
            if (max <= min)
            {
                return min;
            }

            return Mathf.Lerp(min, max, CaveVisual01(ref state));
        }

        private static void AddNatureTerrainFill(
            Transform parent,
            Vector2 size,
            Color color,
            bool addInteriorPlants,
            int seed)
        {
            AddNatureTerrainCoreTexture(parent, size, color);
            AddNatureTerrainEdgeGradient(parent, size, color);
            if (addInteriorPlants)
            {
                AddNatureTerrainInteriorPlants(parent, size, seed);
            }
        }

        private static void AddNatureTerrainCoreTexture(Transform parent, Vector2 size, Color color)
        {
            if (!IsVerticalNatureSurface(parent, size, out bool runAlongLocalX))
            {
                AddNatureDiagonalPencilTexture(parent, size, color);
                return;
            }

            float left = -size.x * 0.5f;
            float right = size.x * 0.5f;
            float bottom = -size.y * 0.5f;
            float top = size.y * 0.5f;
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            int strokeIndex = 0;

            for (int layer = 0; layer < 4; layer++)
            {
                float spacing = 0.14f + layer * 0.027f;
                float acrossStart = runAlongLocalX ? bottom : left;
                float acrossEnd = runAlongLocalX ? top : right;
                float alongStart = runAlongLocalX ? left : bottom;
                float alongEnd = runAlongLocalX ? right : top;
                float across = acrossStart + 0.045f + layer * 0.035f;
                while (across < acrossEnd - 0.035f)
                {
                    Vector2 previous = runAlongLocalX
                        ? new Vector2(alongStart + 0.035f, across)
                        : new Vector2(across, alongStart + 0.035f);
                    Color stroke = new Color(
                        color.r,
                        color.g,
                        color.b,
                        0.105f + layer * 0.03f + Mathf.Abs(Mathf.Sin(strokeIndex * 1.73f)) * 0.05f);
                    for (int segment = 1; segment <= 7; segment++)
                    {
                        float t = segment / 7f;
                        float along = Mathf.Lerp(alongStart + 0.035f, alongEnd - 0.035f, t);
                        float wobble = Mathf.Sin(strokeIndex * 1.91f + segment * 2.17f) * 0.022f;
                        Vector2 next = runAlongLocalX
                            ? new Vector2(along, across + wobble)
                            : new Vector2(across + wobble, along);
                        AppendPencilQuad(
                            vertices,
                            colors,
                            triangles,
                            previous,
                            next,
                            0.009f + layer * 0.0025f,
                            stroke);
                        previous = next;
                    }

                    across += spacing + Mathf.Sin(strokeIndex * 2.41f) * 0.018f;
                    strokeIndex++;
                }
            }

            CreatePencilMesh(parent, "Nature Vertical Pencil Texture", vertices, colors, triangles, 4);
        }

        private static void AddNatureDiagonalPencilTexture(Transform parent, Vector2 size, Color color)
        {
            float left = -size.x * 0.5f;
            float right = size.x * 0.5f;
            float bottom = -size.y * 0.5f;
            float top = size.y * 0.5f;
            const float slope = 0.27f;
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            int strokeIndex = 0;

            for (int layer = 0; layer < 4; layer++)
            {
                float spacing = 0.14f + layer * 0.025f;
                float baseline = bottom - slope * size.x + layer * 0.032f;
                while (baseline < top)
                {
                    float startX = left;
                    float startY = baseline;
                    float endX = right;
                    float endY = baseline + slope * size.x;
                    if (startY < bottom)
                    {
                        startX += (bottom - startY) / slope;
                        startY = bottom;
                    }
                    if (endY > top)
                    {
                        endX -= (endY - top) / slope;
                        endY = top;
                    }

                    if (endX - startX > 0.04f)
                    {
                        Vector2 previous = new Vector2(startX, startY);
                        Color stroke = new Color(
                            color.r,
                            color.g,
                            color.b,
                            0.11f + layer * 0.03f + Mathf.Abs(Mathf.Sin(strokeIndex * 1.67f)) * 0.05f);
                        int segmentCount = Mathf.Clamp(Mathf.CeilToInt((endX - startX) / 1.15f), 2, 14);
                        for (int segment = 1; segment <= segmentCount; segment++)
                        {
                            float t = segment / (float)segmentCount;
                            Vector2 next = Vector2.Lerp(new Vector2(startX, startY), new Vector2(endX, endY), t);
                            Vector2 normal = new Vector2(-slope, 1f).normalized;
                            next += normal * (Mathf.Sin(strokeIndex * 1.93f + segment * 2.11f) * 0.018f);
                            AppendPencilQuad(
                                vertices,
                                colors,
                                triangles,
                                previous,
                                next,
                                0.009f + layer * 0.0025f,
                                stroke);
                            previous = next;
                        }
                    }

                    baseline += spacing + Mathf.Sin(strokeIndex * 2.37f) * 0.014f;
                    strokeIndex++;
                }
            }

            // A few faint reverse strokes keep the fill from looking like a digital hatch pattern.
            int crossCount = Mathf.Clamp(Mathf.CeilToInt(size.x / 1.7f), 2, 18);
            for (int cross = 0; cross < crossCount; cross++)
            {
                float x = Mathf.Lerp(left + 0.08f, right - 0.08f, (cross + 0.5f) / crossCount);
                float y = Mathf.Lerp(bottom + 0.08f, top - 0.08f, 0.25f + Mathf.Abs(Mathf.Sin(cross * 1.71f)) * 0.5f);
                AppendPencilQuad(
                    vertices,
                    colors,
                    triangles,
                    new Vector2(x - 0.24f, y + 0.07f),
                    new Vector2(x + 0.24f, y - 0.07f),
                    0.007f,
                    new Color(color.r, color.g, color.b, 0.12f));
            }

            CreatePencilMesh(parent, "Nature Diagonal Pencil Texture", vertices, colors, triangles, 4);
        }

        private static bool IsVerticalNatureSurface(Transform parent, Vector2 size, out bool runAlongLocalX)
        {
            Vector3 localXInWorld = parent.TransformVector(new Vector3(size.x, 0f, 0f));
            Vector3 localYInWorld = parent.TransformVector(new Vector3(0f, size.y, 0f));
            float worldWidth = Mathf.Abs(localXInWorld.x) + Mathf.Abs(localYInWorld.x);
            float worldHeight = Mathf.Abs(localXInWorld.y) + Mathf.Abs(localYInWorld.y);
            Vector3 localXAxis = parent.TransformDirection(Vector3.right);
            Vector3 localYAxis = parent.TransformDirection(Vector3.up);
            runAlongLocalX = Mathf.Abs(localXAxis.y) > Mathf.Abs(localYAxis.y);
            return worldHeight > worldWidth * 1.08f;
        }

        private static void AddNatureTerrainInteriorPlants(Transform parent, Vector2 size, int seed)
        {
            if (parent == null || size.x < 0.45f || size.y < 0.35f)
            {
                return;
            }

            Vector3 localUp3 = parent.InverseTransformDirection(Vector3.up);
            Vector2 localUp = new Vector2(localUp3.x, localUp3.y);
            if (localUp.sqrMagnitude <= 0.000001f)
            {
                localUp = Vector2.up;
            }
            localUp.Normalize();

            Vector2 inward;
            float halfLength;
            float availableDepth;
            if (Mathf.Abs(localUp.x) > Mathf.Abs(localUp.y))
            {
                inward = localUp.x >= 0f ? Vector2.right : Vector2.left;
                halfLength = size.y * 0.5f;
                availableDepth = size.x;
            }
            else
            {
                inward = localUp.y >= 0f ? Vector2.up : Vector2.down;
                halfLength = size.x * 0.5f;
                availableDepth = size.y;
            }

            Vector2 tangent = new Vector2(-inward.y, inward.x);
            float halfDepth = availableDepth * 0.5f;
            Vector2 edgeCenter = -inward * halfDepth;
            Vector2 from = edgeCenter - tangent * halfLength;
            Vector2 to = edgeCenter + tangent * halfLength;
            float plantDepth = Mathf.Clamp(availableDepth * 0.34f, 0.18f, 0.48f);
            AddNatureInteriorPlantsAlongEdge(parent, from, to, inward * plantDepth, seed);
        }

        private static void AddNaturePlantsOnWorldBottomEdge(
            Transform parent,
            Vector2 from,
            Vector2 to,
            float depth,
            int seed)
        {
            Vector2 edge = to - from;
            if (parent == null || edge.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector2 inward = new Vector2(-edge.y, edge.x).normalized;
            Vector3 worldInward = parent.TransformDirection(new Vector3(inward.x, inward.y, 0f));
            if (Vector2.Dot(new Vector2(worldInward.x, worldInward.y).normalized, Vector2.up) < 0.72f)
            {
                return;
            }

            float plantDepth = Mathf.Clamp(depth * 1.45f, 0.17f, 0.46f);
            AddNatureInteriorPlantsAlongEdge(parent, from, to, inward * plantDepth, seed);
        }

        private static void AddNatureInteriorPlantsAlongEdge(
            Transform parent,
            Vector2 from,
            Vector2 to,
            Vector2 inward,
            int seed)
        {
            Vector2 edge = to - from;
            float length = edge.magnitude;
            if (length < 0.58f || inward.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector2 inwardDirection = inward.normalized;
            float maxHeight = inward.magnitude;
            int clusterCount = Mathf.Clamp(Mathf.FloorToInt(length / 3.15f) + 1, 1, 8);
            List<float> usedPositions = new List<float>(clusterCount);
            for (int cluster = 0; cluster < clusterCount; cluster++)
            {
                float height = Mathf.Min(
                    maxHeight,
                    maxHeight * Mathf.Lerp(0.5f, 1f, GetNatureNoise01(seed, cluster * 17 + 3)));
                float width = height * Mathf.Lerp(1.75f, 3.5f, GetNatureNoise01(seed, cluster * 17 + 5));
                float margin = Mathf.Clamp(width * 0.52f / length + 0.015f, 0.035f, 0.42f);
                float t = margin;
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    float noise = GetNatureNoise01(seed, cluster * 29 + attempt * 7 + 11);
                    t = Mathf.Lerp(margin, 1f - margin, noise);
                    bool overlaps = false;
                    float minimumGap = Mathf.Clamp(width * 0.25f / length, 0.022f, 0.11f);
                    for (int usedIndex = 0; usedIndex < usedPositions.Count; usedIndex++)
                    {
                        if (Mathf.Abs(usedPositions[usedIndex] - t) < minimumGap)
                        {
                            overlaps = true;
                            break;
                        }
                    }
                    if (!overlaps)
                    {
                        break;
                    }
                }
                usedPositions.Add(t);
                Vector2 root = Vector2.Lerp(from, to, t) + inwardDirection * 0.012f;
                AddNatureGrassSpriteCluster(
                    parent,
                    root,
                    inwardDirection,
                    width,
                    height,
                    seed + cluster * 13);
            }
        }

        private static void AddNatureGrassSpriteCluster(
            Transform parent,
            Vector2 root,
            Vector2 inward,
            float width,
            float height,
            int seed)
        {
            if (natureGrassSprite == null)
            {
                natureGrassSprite = Resources.Load<Sprite>("StageDecorations/grass-doodle");
            }
            if (natureBushSprite == null)
            {
                natureBushSprite = Resources.Load<Sprite>("StageDecorations/bush-doodle");
            }
            if (natureFlowerSprite == null)
            {
                natureFlowerSprite = Resources.Load<Sprite>("StageDecorations/flower-doodle");
            }

            int variant = (seed & 0x7fffffff) % 10;
            if (variant < 6 || natureBushSprite == null)
            {
                float squat = Mathf.Lerp(0.72f, 1f, GetNatureNoise01(seed, 71));
                AddNaturePlantSprite(
                    parent,
                    "Nature Uneven Grass Patch",
                    natureGrassSprite,
                    root,
                    inward,
                    width,
                    height * squat,
                    seed,
                    new Color(0.9f, 1f, 0.88f, 0.97f),
                    9);
                return;
            }

            if (variant < 8 || natureFlowerSprite == null)
            {
                AddNaturePlantSprite(
                    parent,
                    "Nature Low Bush Patch",
                    natureBushSprite,
                    root,
                    inward,
                    Mathf.Min(width, height * 2.75f),
                    height * Mathf.Lerp(0.62f, 0.82f, GetNatureNoise01(seed, 79)),
                    seed,
                    new Color(0.93f, 1f, 0.9f, 0.94f),
                    9);
                return;
            }

            AddNaturePlantSprite(
                parent,
                "Nature Flower Base Grass",
                natureGrassSprite,
                root,
                inward,
                width * 0.72f,
                height * 0.48f,
                seed + 1,
                new Color(0.9f, 1f, 0.88f, 0.96f),
                9);
            Vector2 tangent = new Vector2(inward.y, -inward.x).normalized;
            AddNaturePlantSprite(
                parent,
                "Nature Small Wildflower",
                natureFlowerSprite,
                root + tangent * Mathf.Lerp(-0.08f, 0.08f, GetNatureNoise01(seed, 83)),
                inward,
                height * 0.58f,
                height * Mathf.Lerp(0.76f, 1f, GetNatureNoise01(seed, 89)),
                seed + 2,
                new Color(1f, 1f, 1f, 0.94f),
                10);
        }

        private static void AddNaturePlantSprite(
            Transform parent,
            string objectName,
            Sprite sprite,
            Vector2 root,
            Vector2 inward,
            float width,
            float height,
            int seed,
            Color tint,
            int sortingOrder)
        {
            if (sprite == null || sprite.bounds.size.x <= 0f || sprite.bounds.size.y <= 0f)
            {
                return;
            }

            float scaleX = Mathf.Max(0.08f, width) / sprite.bounds.size.x;
            float scaleY = Mathf.Max(0.06f, height) / sprite.bounds.size.y;
            float angle = Mathf.Atan2(inward.y, inward.x) * Mathf.Rad2Deg - 90f;
            Vector2[] spriteVertices = sprite.vertices;
            float lowestVisibleY = spriteVertices.Length > 0 ? spriteVertices[0].y : sprite.bounds.min.y;
            for (int i = 1; i < spriteVertices.Length; i++)
            {
                lowestVisibleY = Mathf.Min(lowestVisibleY, spriteVertices[i].y);
            }

            GameObject plant = new GameObject(objectName);
            plant.transform.SetParent(parent, false);
            plant.transform.localPosition = root + inward * (-lowestVisibleY * scaleY);
            plant.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            plant.transform.localScale = new Vector3(((seed & 1) == 0 ? 1f : -1f) * scaleX, scaleY, 1f);
            SpriteRenderer renderer = plant.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = tint;
            renderer.sortingOrder = sortingOrder;
        }

        private static float GetNatureNoise01(int seed, int salt)
        {
            unchecked
            {
                uint value = (uint)(seed + salt * 374761393);
                value = (value ^ (value >> 13)) * 1274126177u;
                value ^= value >> 16;
                return (value & 0x00ffffffu) / 16777215f;
            }
        }

        private static void AppendTerrainGrassTuft(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 root,
            Vector2 tangent,
            Vector2 inward,
            float height,
            int seed)
        {
            Color dark = new Color(0.12f, 0.49f, 0.17f, 0.72f);
            Color light = new Color(0.39f, 0.72f, 0.3f, 0.48f);
            const int bladeCount = 7;
            for (int blade = 0; blade < bladeCount; blade++)
            {
                float centered = (blade - (bladeCount - 1) * 0.5f) / (bladeCount - 1f);
                float bladeHeight = height * (0.48f + Mathf.Abs(Mathf.Sin(seed * 0.23f + blade * 1.49f)) * 0.52f);
                Vector2 bladeRoot = root + tangent * centered * height * 0.75f;
                Vector2 bladeTip = bladeRoot
                    + inward * bladeHeight
                    + tangent * (centered * height * 0.42f + Mathf.Sin(seed + blade * 2.1f) * 0.035f);
                AppendPencilQuad(vertices, colors, triangles, bladeRoot, bladeTip, 0.018f, dark);
                if ((blade & 1) == 0)
                {
                    AppendPencilQuad(
                        vertices,
                        colors,
                        triangles,
                        bladeRoot + tangent * 0.009f,
                        bladeTip + tangent * 0.009f,
                        0.009f,
                        light);
                }
            }

            AppendPencilQuad(
                vertices,
                colors,
                triangles,
                root - tangent * height * 0.43f,
                root + tangent * height * 0.43f,
                0.022f,
                dark * 0.72f);
        }

        private static void AppendTerrainLeafySprig(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 root,
            Vector2 tangent,
            Vector2 inward,
            float height,
            int seed)
        {
            Color stem = new Color(0.1f, 0.45f, 0.16f, 0.78f);
            Color fill = new Color(0.39f, 0.74f, 0.31f, 0.2f);
            Color outline = new Color(0.13f, 0.52f, 0.18f, 0.76f);
            Vector2 tip = root + inward * height + tangent * Mathf.Sin(seed * 0.41f) * 0.06f;
            AppendPencilQuad(vertices, colors, triangles, root, tip, 0.021f, stem);
            for (int leafIndex = 1; leafIndex <= 3; leafIndex++)
            {
                float t = 0.22f + leafIndex * 0.19f;
                float side = ((leafIndex + seed) & 1) == 0 ? 1f : -1f;
                Vector2 leafBase = Vector2.Lerp(root, tip, t);
                Vector2 leafTip = leafBase
                    + tangent * side * (0.1f + leafIndex * 0.012f)
                    + inward * 0.065f;
                AppendVineLeaf(vertices, colors, triangles, leafBase, leafTip, 0.043f, fill, outline);
            }
        }

        private static void AppendTerrainTinyFlower(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 root,
            Vector2 tangent,
            Vector2 inward,
            float height)
        {
            Color stem = new Color(0.13f, 0.48f, 0.18f, 0.7f);
            Color petal = new Color(0.96f, 0.72f, 0.12f, 0.72f);
            Vector2 center = root + inward * height;
            AppendPencilQuad(vertices, colors, triangles, root, center, 0.017f, stem);
            AppendPencilQuad(vertices, colors, triangles, center - tangent * 0.055f, center + tangent * 0.055f, 0.025f, petal);
            AppendPencilQuad(vertices, colors, triangles, center - inward * 0.055f, center + inward * 0.055f, 0.025f, petal);
            AppendPencilQuad(
                vertices,
                colors,
                triangles,
                center - (tangent + inward).normalized * 0.045f,
                center + (tangent + inward).normalized * 0.045f,
                0.018f,
                petal * 0.82f);
        }

        private static int GetStableNatureVisualSeed(string value)
        {
            unchecked
            {
                int hash = 17;
                if (!string.IsNullOrEmpty(value))
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        hash = hash * 31 + value[i];
                    }
                }
                return hash & 0x7fffffff;
            }
        }

        private static void AddNatureTerrainEdgeGradient(Transform parent, Vector2 size, Color color)
        {
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            float horizontalDepth = Mathf.Min(halfWidth, Mathf.Clamp(size.x * 0.16f, 0.08f, 0.7f));
            float verticalDepth = Mathf.Min(halfHeight, Mathf.Clamp(size.y * 0.24f, 0.08f, 0.65f));
            Color edge = new Color(color.r, color.g, color.b, 0.1f);
            Color clear = new Color(color.r, color.g, color.b, 0f);
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();

            AppendGradientQuad(
                vertices, colors, triangles,
                new Vector2(-halfWidth, -halfHeight), new Vector2(-halfWidth, halfHeight),
                new Vector2(-halfWidth + horizontalDepth, -halfHeight), new Vector2(-halfWidth + horizontalDepth, halfHeight),
                edge, clear);
            AppendGradientQuad(
                vertices, colors, triangles,
                new Vector2(halfWidth, halfHeight), new Vector2(halfWidth, -halfHeight),
                new Vector2(halfWidth - horizontalDepth, halfHeight), new Vector2(halfWidth - horizontalDepth, -halfHeight),
                edge, clear);
            AppendGradientQuad(
                vertices, colors, triangles,
                new Vector2(-halfWidth, -halfHeight), new Vector2(halfWidth, -halfHeight),
                new Vector2(-halfWidth, -halfHeight + verticalDepth), new Vector2(halfWidth, -halfHeight + verticalDepth),
                edge, clear);
            AppendGradientQuad(
                vertices, colors, triangles,
                new Vector2(halfWidth, halfHeight), new Vector2(-halfWidth, halfHeight),
                new Vector2(halfWidth, halfHeight - verticalDepth), new Vector2(-halfWidth, halfHeight - verticalDepth),
                edge, clear);

            AppendNatureEdgeScribbles(
                vertices, colors, triangles,
                new Vector2(-halfWidth, -halfHeight), new Vector2(halfWidth, -halfHeight),
                Vector2.up * verticalDepth, 11);
            AppendNatureEdgeScribbles(
                vertices, colors, triangles,
                new Vector2(halfWidth, halfHeight), new Vector2(-halfWidth, halfHeight),
                Vector2.down * verticalDepth, 23);
            AppendNatureEdgeScribbles(
                vertices, colors, triangles,
                new Vector2(-halfWidth, halfHeight), new Vector2(-halfWidth, -halfHeight),
                Vector2.right * horizontalDepth, 37);
            AppendNatureEdgeScribbles(
                vertices, colors, triangles,
                new Vector2(halfWidth, -halfHeight), new Vector2(halfWidth, halfHeight),
                Vector2.left * horizontalDepth, 53);

            CreatePencilMesh(parent, "Nature Terrain Edge Shading", vertices, colors, triangles, 5);
        }

        private static void AddNatureConnectedEdgeGradient(
            Transform parent,
            Vector2 from,
            Vector2 to,
            float depth)
        {
            Vector2 direction = to - from;
            if (direction.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector2 inward = new Vector2(-direction.y, direction.x).normalized * depth;
            Color edge = new Color(TitleTerrainStrokeColor.r, TitleTerrainStrokeColor.g, TitleTerrainStrokeColor.b, 0.1f);
            Color clear = new Color(edge.r, edge.g, edge.b, 0f);
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            AppendGradientQuad(vertices, colors, triangles, from, to, from + inward, to + inward, edge, clear);
            int seed = Mathf.RoundToInt(
                Mathf.Abs(from.x * 13f + from.y * 17f + to.x * 19f + to.y * 23f));
            AppendNatureEdgeScribbles(vertices, colors, triangles, from, to, inward, seed);
            CreatePencilMesh(parent, "Nature Connected Edge Shading", vertices, colors, triangles, 5);
        }

        private static void AppendNatureEdgeScribbles(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 edgeFrom,
            Vector2 edgeTo,
            Vector2 inward,
            int seed)
        {
            Vector2 edge = edgeTo - edgeFrom;
            float length = edge.magnitude;
            if (length <= 0.001f || inward.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector2 direction = edge / length;
            Vector2 inwardDirection = inward.normalized;
            int segmentCount = Mathf.Max(1, Mathf.CeilToInt(length / 0.68f));
            const int bandCount = 9;
            for (int band = 0; band < bandCount; band++)
            {
                float bandT = band / (bandCount - 1f);
                float distance = inward.magnitude * Mathf.Pow(bandT, 1.45f);
                float alpha = Mathf.Lerp(0.32f, 0.028f, bandT);
                Color stroke = new Color(
                    TitleTerrainStrokeColor.r,
                    TitleTerrainStrokeColor.g,
                    TitleTerrainStrokeColor.b,
                    alpha);
                for (int segment = 0; segment < segmentCount; segment++)
                {
                    float phase = seed * 0.37f + band * 1.91f + segment * 2.53f;
                    float startDistance = length * (segment + 0.06f + Mathf.Abs(Mathf.Sin(phase)) * 0.07f) / segmentCount;
                    float endDistance = length * (segment + 0.7f + Mathf.Abs(Mathf.Cos(phase * 1.27f)) * 0.16f) / segmentCount;
                    startDistance = Mathf.Clamp(startDistance, 0f, length);
                    endDistance = Mathf.Clamp(endDistance, startDistance, length);
                    float inwardJitter = Mathf.Sin(phase * 1.73f) * 0.014f;
                    Vector2 offset = inwardDirection * Mathf.Max(0.006f, distance + inwardJitter);
                    Vector2 start = edgeFrom + direction * startDistance + offset;
                    Vector2 end = edgeFrom + direction * endDistance + offset
                        + inwardDirection * (Mathf.Cos(phase * 1.41f) * 0.018f);
                    AppendPencilQuad(
                        vertices,
                        colors,
                        triangles,
                        start,
                        end,
                        0.009f + (1f - bandT) * 0.006f,
                        stroke);
                }
            }
        }

        private static void AppendGradientQuad(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector2 outerFrom,
            Vector2 outerTo,
            Vector2 innerFrom,
            Vector2 innerTo,
            Color outerColor,
            Color innerColor)
        {
            int first = vertices.Count;
            vertices.Add(outerFrom);
            vertices.Add(outerTo);
            vertices.Add(innerFrom);
            vertices.Add(innerTo);
            colors.Add(outerColor);
            colors.Add(outerColor);
            colors.Add(innerColor);
            colors.Add(innerColor);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 1);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
        }

        private static void AddDoorDoodle(Transform parent)
        {
            Vector3[] door =
            {
                new Vector3(-0.24f, -0.5f, -0.01f),
                new Vector3(-0.24f, 0.3f, -0.01f),
                new Vector3(0.24f, 0.3f, -0.01f),
                new Vector3(0.24f, -0.5f, -0.01f)
            };
            AddDoodleLine("Door", parent, door, Color.black, 0.045f, 15);
        }

        private static void AddPencilFillLocal(Transform parent, Vector2 size, Color color)
        {
            Color pencil = new Color(color.r, color.g, color.b, 0.22f);
            int index = 0;
            float inverseScale = 1f / Mathf.Max(Mathf.Max(size.x, size.y), 0.1f);
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();

            for (int layer = 0; layer < 3; layer++)
            {
                float y = -0.42f + layer * 0.08f;
                while (y < 0.44f)
                {
                    float x = -0.52f + layer * 0.06f + Mathf.Sin(index * 1.3f) * 0.03f;
                    while (x < 0.5f)
                    {
                        Vector3 start = new Vector3(Mathf.Clamp(x, -0.5f, 0.5f), Mathf.Clamp(y + Mathf.Sin(index) * 0.03f, -0.48f, 0.48f), -0.02f);
                        Vector3 end = new Vector3(Mathf.Clamp(start.x + 0.22f + Mathf.Abs(Mathf.Sin(index * 0.7f)) * 0.18f, -0.5f, 0.5f), Mathf.Clamp(start.y + 0.25f, -0.48f, 0.48f), -0.02f);
                        AppendPencilQuad(vertices, colors, triangles, start, end, 0.012f * inverseScale, pencil);
                        x += 0.16f + Mathf.Abs(Mathf.Sin(index * 1.9f)) * 0.07f;
                        index++;
                    }

                    y += 0.17f;
                }
            }

            CreatePencilMesh(parent, "Pencil Fill Mesh", vertices, colors, triangles, 4);
        }

        private static void AppendPencilQuad(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector3 from,
            Vector3 to,
            float width,
            Color color)
        {
            Vector2 delta = (Vector2)(to - from);
            if (delta.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * 0.5f);
            int first = vertices.Count;
            vertices.Add(from + (Vector3)normal);
            vertices.Add(from - (Vector3)normal);
            vertices.Add(to + (Vector3)normal);
            vertices.Add(to - (Vector3)normal);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
            triangles.Add(first + 1);
        }

        private static void CreatePencilMesh(
            Transform parent,
            string name,
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            int sortingOrder)
        {
            if (parent == null || vertices.Count == 0)
            {
                return;
            }

            GameObject visual = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(parent, false);
            Mesh mesh = new Mesh { name = name };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            visual.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetLineMaterial();
            renderer.sortingOrder = sortingOrder;
        }

        private static void AddDoodleCircle(Transform parent, float radius, Color color, float width)
        {
            AddDoodleCircleAt(parent, Vector2.zero, radius, color, width, 20);
        }

        private static void AddDoodleCircleAt(
            Transform parent,
            Vector2 center,
            float radius,
            Color color,
            float width,
            int sortingOrder)
        {
            Vector3[] points = new Vector3[22];
            for (int i = 0; i < points.Length; i++)
            {
                float t = i / (float)(points.Length - 1);
                float angle = t * Mathf.PI * 2f;
                float wobble = 1f + Mathf.Sin(i * 1.7f) * 0.04f;
                points[i] = new Vector3(
                    center.x + Mathf.Cos(angle) * radius * wobble,
                    center.y + Mathf.Sin(angle) * radius * wobble,
                    0f);
            }

            AddDoodleLine("Circle", parent, points, color, width, sortingOrder);
        }

        private static void AddFilledKeyholeSilhouette(Transform parent)
        {
            const int circleSegments = 32;
            const float centerY = 0.22f;
            const float radius = 0.21f;

            Vector3[] vertices = new Vector3[circleSegments + 6];
            int[] triangles = new int[circleSegments * 3 + 6];
            Color[] colors = new Color[vertices.Length];
            Color black = new Color(0.025f, 0.022f, 0.018f, 1f);

            vertices[0] = new Vector3(0f, centerY, -0.02f);
            for (int i = 0; i <= circleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / circleSegments;
                vertices[i + 1] = new Vector3(
                    Mathf.Cos(angle) * radius,
                    centerY + Mathf.Sin(angle) * radius,
                    -0.02f);
            }

            for (int i = 0; i < circleSegments; i++)
            {
                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 1;
                triangles[triangleIndex + 2] = i + 2;
            }

            int stemStart = circleSegments + 2;
            vertices[stemStart] = new Vector3(-0.085f, 0.09f, -0.02f);
            vertices[stemStart + 1] = new Vector3(0.085f, 0.09f, -0.02f);
            vertices[stemStart + 2] = new Vector3(0.19f, -0.4f, -0.02f);
            vertices[stemStart + 3] = new Vector3(-0.19f, -0.4f, -0.02f);

            int stemTriangleStart = circleSegments * 3;
            triangles[stemTriangleStart] = stemStart;
            triangles[stemTriangleStart + 1] = stemStart + 1;
            triangles[stemTriangleStart + 2] = stemStart + 2;
            triangles[stemTriangleStart + 3] = stemStart;
            triangles[stemTriangleStart + 4] = stemStart + 2;
            triangles[stemTriangleStart + 5] = stemStart + 3;

            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = black;
            }

            Mesh mesh = new Mesh
            {
                name = "Filled Keyhole Silhouette Mesh",
                vertices = vertices,
                triangles = triangles,
                colors = colors
            };
            mesh.RecalculateBounds();

            GameObject visual = new GameObject("Filled Keyhole Silhouette");
            visual.transform.SetParent(parent, false);
            MeshFilter filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetLineMaterial();
            renderer.sortingOrder = 22;
        }

        private static void AddDoodleLine(string name, Transform parent, Vector3[] points, Color color, float width, int sortingOrder)
        {
            GameObject lineObject = new GameObject(name);
            lineObject.transform.SetParent(parent, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 6;
            line.numCornerVertices = 4;
            line.material = GetLineMaterial();
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = sortingOrder;
        }

        private static Material GetLineMaterial()
        {
            if (lineMaterial != null)
            {
                return lineMaterial;
            }

            lineMaterial = DoodleRuntimeAssets.LineMaterial;
            return lineMaterial;
        }

        private static Sprite GetSquareSprite()
        {
            return DoodleRuntimeAssets.SquareSprite;
        }

        private static Sprite GetCircleSprite()
        {
            if (circleSprite != null)
            {
                return circleSprite;
            }

            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime Movable Circle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            float radius = center - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = 1f - Mathf.Clamp01(distance - radius + 1f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            circleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return circleSprite;
        }

        private static Sprite GetScaleBodySprite()
        {
            if (scaleBodySprite != null)
            {
                return scaleBodySprite;
            }

            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime Ink Scale Body",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float vertical = y / (float)(size - 1);
                float halfWidth = Mathf.Lerp(0.49f, 0.4f, vertical);
                for (int x = 0; x < size; x++)
                {
                    float horizontal = x / (float)(size - 1) - 0.5f;
                    pixels[y * size + x] = Mathf.Abs(horizontal) <= halfWidth
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            scaleBodySprite = Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                size);
            return scaleBodySprite;
        }

        private static Font FindHandwrittenFont()
        {
            return DoodleRuntimeAssets.HandwrittenFont;
        }
    }
}
