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
                    volume = 0.85f;
                    cooldown = 0.2f;
                    break;
            }

            return new SfxDefinition(BuildResourcePath(id), volume, pitchMin, pitchMax, cooldown);
        }

        private static string BuildResourcePath(SfxId id)
        {
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
