using UnityEngine;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(PlayerController2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerAbilityController : MonoBehaviour
    {
        public enum JumpTier
        {
            Normal,
            Double,
            Triple
        }

        public enum ArmTier
        {
            NormalReach,
            LongReach,
            FastSwing
        }

        public enum TorsoTier
        {
            Normal,
            HeavySwitch,
            Heavy
        }

        public struct AbilityProfile
        {
            public float LegInk;
            public float ArmInk;
            public float TorsoInk;
            public float TotalInk;
            public DrawManager.Species Species;
            public JumpTier Jump;
            public ArmTier Arm;
            public TorsoTier Torso;
        }

        [SerializeField] private PlayerController2D playerController;
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private float normalMass = 0.05f;
        [SerializeField] private float heavySwitchMass = 0.2f;
        [SerializeField] private float heavyMass = 0.35f;
        [SerializeField] private float inkMassScale = 0.001f;

        public AbilityProfile CurrentProfile { get; private set; }

        private void Awake()
        {
            if (playerController == null)
            {
                playerController = GetComponent<PlayerController2D>();
            }

            if (rb == null)
            {
                rb = GetComponent<Rigidbody2D>();
            }

            ApplyProfile(CreateDefaultProfile());
        }

        public AbilityProfile ApplyFromDrawing(DrawManager drawManager)
        {
            AbilityProfile profile = CalculateProfile(drawManager);
            ApplyProfile(profile);
            return profile;
        }

        public static AbilityProfile CalculateProfile(DrawManager drawManager)
        {
            DrawManager.Species species = drawManager.CurrentSpecies;
            float legInk = GetInkIfActive(drawManager, DrawManager.BodyPart.LeftLeg)
                + GetInkIfActive(drawManager, DrawManager.BodyPart.RightLeg)
                + GetInkIfActive(drawManager, DrawManager.BodyPart.LeftFrontLeg)
                + GetInkIfActive(drawManager, DrawManager.BodyPart.RightFrontLeg)
                + GetInkIfActive(drawManager, DrawManager.BodyPart.LeftBackLeg)
                + GetInkIfActive(drawManager, DrawManager.BodyPart.RightBackLeg);
            float armInk = GetInkIfActive(drawManager, DrawManager.BodyPart.LeftArm)
                + GetInkIfActive(drawManager, DrawManager.BodyPart.RightArm)
                + GetInkIfActive(drawManager, DrawManager.BodyPart.LeftWing)
                + GetInkIfActive(drawManager, DrawManager.BodyPart.RightWing)
                + GetInkIfActive(drawManager, DrawManager.BodyPart.Tail);
            float torsoInk = GetInkIfActive(drawManager, DrawManager.BodyPart.Torso)
                + GetInkIfActive(drawManager, DrawManager.BodyPart.SlimeBody);
            float totalInk = CalculateTotalInk(drawManager);

            return new AbilityProfile
            {
                Species = species,
                LegInk = legInk,
                ArmInk = armInk,
                TorsoInk = torsoInk,
                TotalInk = totalInk,
                Jump = legInk >= 80f ? JumpTier.Triple : legInk >= 50f ? JumpTier.Double : JumpTier.Normal,
                Arm = armInk >= 80f ? ArmTier.FastSwing : armInk >= 50f ? ArmTier.LongReach : ArmTier.NormalReach,
                Torso = torsoInk >= 80f ? TorsoTier.Heavy : torsoInk >= 50f ? TorsoTier.HeavySwitch : TorsoTier.Normal
            };
        }

        private static float GetInkIfActive(DrawManager drawManager, DrawManager.BodyPart part)
        {
            return drawManager.IsPartActive(part) ? drawManager.GetInk(part) : 0f;
        }

        private static float CalculateTotalInk(DrawManager drawManager)
        {
            float total = 0f;
            foreach (DrawManager.BodyPart part in System.Enum.GetValues(typeof(DrawManager.BodyPart)))
            {
                if (drawManager.IsPartActive(part))
                {
                    total += drawManager.GetInk(part);
                }
            }

            return total;
        }

        public static string GetProfileSummary(AbilityProfile profile)
        {
            string baseSummary = LocalizationManager.Format(
                "ability_summary",
                profile.LegInk,
                GetJumpLabel(profile.Jump),
                profile.ArmInk,
                GetArmLabel(profile.Arm),
                profile.TorsoInk,
                GetTorsoLabel(profile.Torso));
            return $"{profile.Species}   {baseSummary}";
        }

        private void ApplyProfile(AbilityProfile profile)
        {
            CurrentProfile = profile;
            playerController.SetJumpMultiplier(GetJumpMultiplier(profile.Jump));
            playerController.ApplySpeciesMovement(profile.Species);

            if (rb != null)
            {
                rb.mass = Mathf.Max(GetMass(profile.Torso), Mathf.Max(normalMass, profile.TotalInk * inkMassScale));
            }
        }

        private AbilityProfile CreateDefaultProfile()
        {
            return new AbilityProfile
            {
                TotalInk = normalMass / Mathf.Max(inkMassScale, 0.001f),
                Jump = JumpTier.Normal,
                Arm = ArmTier.NormalReach,
                Torso = TorsoTier.Normal
            };
        }

        private static float GetJumpMultiplier(JumpTier tier)
        {
            switch (tier)
            {
                case JumpTier.Double:
                    return 1.5f;
                case JumpTier.Triple:
                    return 2f;
                default:
                    return 1f;
            }
        }

        private float GetMass(TorsoTier tier)
        {
            switch (tier)
            {
                case TorsoTier.HeavySwitch:
                    return heavySwitchMass;
                case TorsoTier.Heavy:
                    return heavyMass;
                default:
                    return normalMass;
            }
        }

        private static string GetJumpLabel(JumpTier tier)
        {
            switch (tier)
            {
                case JumpTier.Double:
                    return LocalizationManager.T("jump_double");
                case JumpTier.Triple:
                    return LocalizationManager.T("jump_triple");
                default:
                    return LocalizationManager.T("jump_normal");
            }
        }

        private static string GetArmLabel(ArmTier tier)
        {
            switch (tier)
            {
                case ArmTier.LongReach:
                    return LocalizationManager.T("arm_long");
                case ArmTier.FastSwing:
                    return LocalizationManager.T("arm_fast");
                default:
                    return LocalizationManager.T("arm_normal");
            }
        }

        private static string GetTorsoLabel(TorsoTier tier)
        {
            switch (tier)
            {
                case TorsoTier.HeavySwitch:
                    return LocalizationManager.T("torso_switch");
                case TorsoTier.Heavy:
                    return LocalizationManager.T("torso_heavy");
                default:
                    return LocalizationManager.T("torso_normal");
            }
        }
    }
}
