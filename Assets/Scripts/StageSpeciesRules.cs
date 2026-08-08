using System;
using System.Collections.Generic;

namespace DrawBody.Prototype
{
    [Flags]
    public enum StageSpeciesMask
    {
        None = 0,
        Human = 1 << 0,
        Cat = 1 << 1,
        Bird = 1 << 2,
        Turtle = 1 << 3,
        Slime = 1 << 4,
        All = Human | Cat | Bird | Turtle | Slime
    }

    public static class StageSpeciesRules
    {
        private static readonly DrawManager.Species[] OrderedSpecies =
        {
            DrawManager.Species.Human,
            DrawManager.Species.Cat,
            DrawManager.Species.Bird,
            DrawManager.Species.Turtle,
            DrawManager.Species.Slime
        };

        public static StageSpeciesMask GetAllowedForStage(string stageId)
        {
            return GetAllowedForWorld(GetWorldNumber(stageId));
        }

        public static StageSpeciesMask GetAllowedForWorld(int world)
        {
            switch (world)
            {
                case 1: return StageSpeciesMask.Human;
                case 2: return StageSpeciesMask.Human | StageSpeciesMask.Cat;
                case 3: return StageSpeciesMask.Human | StageSpeciesMask.Bird;
                case 4: return StageSpeciesMask.Human | StageSpeciesMask.Turtle;
                case 5: return StageSpeciesMask.Human | StageSpeciesMask.Slime;
                case 6: return StageSpeciesMask.Cat | StageSpeciesMask.Bird;
                case 7: return StageSpeciesMask.Cat | StageSpeciesMask.Turtle;
                case 8: return StageSpeciesMask.Bird | StageSpeciesMask.Slime;
                case 9: return StageSpeciesMask.Turtle | StageSpeciesMask.Slime;
                case 10: return StageSpeciesMask.Human | StageSpeciesMask.Cat | StageSpeciesMask.Bird;
                case 11: return StageSpeciesMask.Human | StageSpeciesMask.Turtle | StageSpeciesMask.Slime;
                case 12: return StageSpeciesMask.Cat | StageSpeciesMask.Bird | StageSpeciesMask.Turtle;
                case 13: return StageSpeciesMask.Human | StageSpeciesMask.Cat | StageSpeciesMask.Slime;
                case 14: return StageSpeciesMask.Human | StageSpeciesMask.Cat | StageSpeciesMask.Bird | StageSpeciesMask.Turtle;
                case 15: return StageSpeciesMask.All;
                default: return StageSpeciesMask.All;
            }
        }

        public static int GetWorldNumber(string stageId)
        {
            if (string.IsNullOrWhiteSpace(stageId))
            {
                return 0;
            }

            int separator = stageId.IndexOf('-');
            string worldPart = separator >= 0 ? stageId.Substring(0, separator) : stageId;
            return int.TryParse(worldPart, out int world) ? world : 0;
        }

        public static bool IsAllowed(StageSpeciesMask mask, DrawManager.Species species)
        {
            return (mask & ToMask(species)) != 0;
        }

        public static DrawManager.Species GetFirstAllowed(StageSpeciesMask mask)
        {
            for (int i = 0; i < OrderedSpecies.Length; i++)
            {
                if (IsAllowed(mask, OrderedSpecies[i]))
                {
                    return OrderedSpecies[i];
                }
            }
            return DrawManager.Species.Human;
        }

        public static IReadOnlyList<DrawManager.Species> GetOrderedSpecies()
        {
            return OrderedSpecies;
        }

        public static string GetSpeciesLocalizationKey(DrawManager.Species species)
        {
            switch (species)
            {
                case DrawManager.Species.Cat: return "species_cat";
                case DrawManager.Species.Bird: return "species_bird";
                case DrawManager.Species.Turtle: return "species_turtle";
                case DrawManager.Species.Slime: return "species_slime";
                default: return "species_human";
            }
        }

        private static StageSpeciesMask ToMask(DrawManager.Species species)
        {
            switch (species)
            {
                case DrawManager.Species.Cat: return StageSpeciesMask.Cat;
                case DrawManager.Species.Bird: return StageSpeciesMask.Bird;
                case DrawManager.Species.Turtle: return StageSpeciesMask.Turtle;
                case DrawManager.Species.Slime: return StageSpeciesMask.Slime;
                default: return StageSpeciesMask.Human;
            }
        }
    }
}
