using System;
using System.Text;

namespace DrawBody.Prototype
{
    public readonly struct SfxDefinition
    {
        public readonly string ResourcePath;
        public readonly float Volume;
        public readonly float PitchMin;
        public readonly float PitchMax;
        public readonly float Cooldown;

        public SfxDefinition(string resourcePath, float volume, float pitchMin, float pitchMax, float cooldown)
        {
            ResourcePath = resourcePath;
            Volume = volume;
            PitchMin = pitchMin;
            PitchMax = pitchMax;
            Cooldown = cooldown;
        }
    }

    public static class SfxCatalog
    {
        public static SfxDefinition Get(SfxId id)
        {
            float volume = 0.65f;
            float pitchMin = 0.97f;
            float pitchMax = 1.03f;
            float cooldown = 0.025f;

            switch (id)
            {
                case SfxId.UiButtonHover:
                case SfxId.UiCursorMove:
                    volume = 0.32f;
                    cooldown = 0.07f;
                    break;
                case SfxId.UiSliderTick:
                    volume = 0.42f;
                    cooldown = 0.08f;
                    break;
                case SfxId.DrawPenLoop:
                case SfxId.DrawEraserLoop:
                    volume = 0.28f;
                    pitchMin = 0.9f;
                    pitchMax = 1.1f;
                    cooldown = 0.055f;
                    break;
                case SfxId.EditorObjectMove:
                case SfxId.EditorObjectResize:
                case SfxId.EditorObjectRotate:
                    volume = 0.48f;
                    cooldown = 0.06f;
                    break;
                case SfxId.PlayerFootstepPaper:
                case SfxId.CatRunLoop:
                    volume = 0.42f;
                    pitchMin = 0.9f;
                    pitchMax = 1.1f;
                    cooldown = 0.08f;
                    break;
                case SfxId.BirdGlideLoop:
                    volume = 0.34f;
                    cooldown = 0.18f;
                    break;
                case SfxId.PlayerDeath:
                case SfxId.DrawInkOver:
                case SfxId.BombExplosion:
                    volume = 0.85f;
                    cooldown = 0.2f;
                    break;
                case SfxId.BombTick:
                    volume = 0.62f;
                    pitchMin = 0.98f;
                    pitchMax = 1.04f;
                    cooldown = 0.08f;
                    break;
                case SfxId.BombFuseStart:
                case SfxId.BombWallBreak:
                    volume = 0.72f;
                    cooldown = 0.12f;
                    break;
                case SfxId.DynamiteExplosion:
                    volume = 1f;
                    pitchMin = 0.82f;
                    pitchMax = 0.9f;
                    cooldown = 0.35f;
                    break;
                case SfxId.DynamiteTick:
                    volume = 0.72f;
                    cooldown = 0.08f;
                    break;
                case SfxId.DynamiteFuseStart:
                case SfxId.EnemyCharge:
                case SfxId.EnemyDefeat:
                case SfxId.EnemyShellBounce:
                case SfxId.BeamFire:
                case SfxId.CannonFire:
                    volume = 0.78f;
                    cooldown = 0.12f;
                    break;
                case SfxId.EnemyShoot:
                case SfxId.EnemyJump:
                    volume = 0.62f;
                    cooldown = 0.1f;
                    break;
                case SfxId.EmotePop:
                    volume = 0.48f;
                    pitchMin = 0.98f;
                    pitchMax = 1.06f;
                    cooldown = 0.06f;
                    break;
                case SfxId.CrumblingFloorWarning:
                    volume = 0.48f;
                    cooldown = 0.3f;
                    break;
                case SfxId.CrumblingFloorCollapse:
                    volume = 0.82f;
                    cooldown = 0.2f;
                    break;
                case SfxId.StageCountdownTick:
                    volume = 0.62f;
                    cooldown = 0.35f;
                    break;
                case SfxId.StageCountdownGo:
                case SfxId.CoinCollect:
                case SfxId.CollectibleCollect:
                case SfxId.SwitchPress:
                case SfxId.CircuitConnect:
                    volume = 0.68f;
                    cooldown = 0.06f;
                    break;
                case SfxId.StageClear:
                case SfxId.GoalReached:
                case SfxId.CircuitComplete:
                    volume = 0.86f;
                    cooldown = 0.5f;
                    break;
                case SfxId.StageFailed:
                    volume = 0.82f;
                    cooldown = 0.6f;
                    break;
                case SfxId.DoorOpen:
                case SfxId.DoorClose:
                case SfxId.JumpPadLaunch:
                case SfxId.SpeedBoost:
                case SfxId.MissileLaunch:
                case SfxId.MissileImpact:
                case SfxId.CrateBreak:
                case SfxId.PillarImpact:
                case SfxId.KeyUnlock:
                case SfxId.GunShot:
                case SfxId.Ricochet:
                case SfxId.BossCharge:
                case SfxId.BossDash:
                case SfxId.BossBeamCharge:
                case SfxId.BossSuction:
                    volume = 0.78f;
                    cooldown = 0.1f;
                    break;
                case SfxId.TurtleShellEnter:
                    volume = 0.3f;
                    cooldown = 0.12f;
                    break;
                case SfxId.TurtleShellExit:
                    volume = 0.24f;
                    cooldown = 0.12f;
                    break;
                case SfxId.BossAttackWarning:
                    volume = 0.78f;
                    cooldown = 0.45f;
                    break;
                case SfxId.CrateImpact:
                    volume = 0.55f;
                    cooldown = 0.08f;
                    break;
                case SfxId.PillarWarning:
                    volume = 0.7f;
                    cooldown = 0.3f;
                    break;
            }

            return new SfxDefinition(BuildResourcePath(id), volume, pitchMin, pitchMax, cooldown);
        }

        private static string BuildResourcePath(SfxId id)
        {
            switch (id)
            {
                case SfxId.BombFuseStart:
                    return "Audio/SFX/Gimmick/bomb_fuse_start";
                case SfxId.BombTick:
                    return "Audio/SFX/Gimmick/bomb_tick";
                case SfxId.BombExplosion:
                    return "Audio/SFX/Gimmick/bomb_explosion";
                case SfxId.BombWallBreak:
                    return "Audio/SFX/Gimmick/bomb_wall_break";
                case SfxId.CatClawAttach:
                    return "Audio/SFX/Species/cat_claw_attach";
                case SfxId.CatClawRelease:
                    return "Audio/SFX/Species/cat_claw_release";
                case SfxId.DynamiteFuseStart:
                    return "Audio/SFX/Gimmick/dynamite_fuse_start";
                case SfxId.DynamiteTick:
                    return "Audio/SFX/Gimmick/dynamite_tick";
                case SfxId.DynamiteExplosion:
                    return "Audio/SFX/Gimmick/dynamite_explosion";
                case SfxId.EnemyCharge:
                    return "Audio/SFX/Enemy/enemy_charge";
                case SfxId.EnemyShoot:
                    return "Audio/SFX/Enemy/enemy_shoot";
                case SfxId.EnemyJump:
                    return "Audio/SFX/Enemy/enemy_jump";
                case SfxId.EnemyDefeat:
                    return "Audio/SFX/Enemy/enemy_defeat";
                case SfxId.EnemyShellBounce:
                    return "Audio/SFX/Enemy/enemy_shell_bounce";
                case SfxId.BeamFire:
                    return "Audio/SFX/Gimmick/beam_fire";
                case SfxId.CannonFire:
                    return "Audio/SFX/Gimmick/cannon_fire";
                case SfxId.EmotePop:
                    return "Audio/SFX/UI/emote_pop";
                case SfxId.CrumblingFloorWarning:
                    return "Audio/SFX/Gimmick/crumbling_floor_warning";
                case SfxId.CrumblingFloorCollapse:
                    return "Audio/SFX/Gimmick/crumbling_floor_collapse";
                case SfxId.StageCountdownTick:
                    return "Audio/SFX/Gameplay/stage_countdown_tick";
                case SfxId.StageCountdownGo:
                    return "Audio/SFX/Gameplay/stage_countdown_go";
                case SfxId.StageClear:
                    return "Audio/SFX/Gameplay/stage_clear";
                case SfxId.StageFailed:
                    return "Audio/SFX/Gameplay/stage_failed";
                case SfxId.CoinCollect:
                    return "Audio/SFX/Gameplay/coin_collect";
                case SfxId.CollectibleCollect:
                    return "Audio/SFX/Gameplay/collectible_collect";
                case SfxId.GoalReached:
                    return "Audio/SFX/Gameplay/goal_reached";
                case SfxId.SwitchPress:
                    return "Audio/SFX/Gimmick/switch_press";
                case SfxId.DoorOpen:
                    return "Audio/SFX/Gimmick/door_open";
                case SfxId.DoorClose:
                    return "Audio/SFX/Gimmick/door_close";
                case SfxId.JumpPadLaunch:
                    return "Audio/SFX/Gimmick/jump_pad_launch";
                case SfxId.SpeedBoost:
                    return "Audio/SFX/Gimmick/speed_boost";
                case SfxId.MissileLaunch:
                    return "Audio/SFX/Gimmick/missile_launch";
                case SfxId.MissileImpact:
                    return "Audio/SFX/Gimmick/missile_impact";
                case SfxId.CircuitConnect:
                    return "Audio/SFX/Gimmick/circuit_connect";
                case SfxId.CircuitComplete:
                    return "Audio/SFX/Gimmick/circuit_complete";
                case SfxId.CrateImpact:
                    return "Audio/SFX/Gimmick/crate_impact";
                case SfxId.CrateBreak:
                    return "Audio/SFX/Gimmick/crate_break";
                case SfxId.PillarWarning:
                    return "Audio/SFX/Gimmick/pillar_warning";
                case SfxId.PillarImpact:
                    return "Audio/SFX/Gimmick/pillar_impact";
                case SfxId.KeyUnlock:
                    return "Audio/SFX/Gimmick/key_unlock";
                case SfxId.GunShot:
                    return "Audio/SFX/Combat/gun_shot";
                case SfxId.Ricochet:
                    return "Audio/SFX/Combat/ricochet";
                case SfxId.BossCharge:
                    return "Audio/SFX/Combat/boss_charge";
                case SfxId.TurtleShellEnter:
                    return "Audio/SFX/Species/turtle_shell_enter";
                case SfxId.TurtleShellExit:
                    return "Audio/SFX/Species/turtle_shell_exit";
                case SfxId.BossAttackWarning:
                    return "Audio/SFX/Combat/boss_attack_warning";
                case SfxId.BossDash:
                    return "Audio/SFX/Combat/boss_dash";
                case SfxId.BossBeamCharge:
                    return "Audio/SFX/Combat/boss_beam_charge";
                case SfxId.BossSuction:
                    return "Audio/SFX/Combat/boss_suction";
            }

            string name = id.ToString();
            string category;
            string fileName;

            if (name.StartsWith("Ui", StringComparison.Ordinal))
            {
                category = "UI";
                fileName = "ui" + name.Substring(2);
            }
            else if (name.StartsWith("Draw", StringComparison.Ordinal))
            {
                category = "Draw";
                fileName = name;
            }
            else if (name.StartsWith("Editor", StringComparison.Ordinal))
            {
                category = "Editor";
                fileName = name;
            }
            else if (name.StartsWith("Player", StringComparison.Ordinal))
            {
                category = "Player";
                fileName = name;
            }
            else
            {
                category = "Species";
                fileName = name;
            }

            return "Audio/SFX/" + category + "/" + ToSnakeCase(fileName);
        }

        private static string ToSnakeCase(string value)
        {
            StringBuilder result = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (i > 0 && char.IsUpper(current))
                {
                    result.Append('_');
                }
                result.Append(char.ToLowerInvariant(current));
            }
            return result.ToString();
        }
    }
}
