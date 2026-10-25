using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DrawBody.Prototype
{
    public static class CharacterDrawingPresetStore
    {
        public const int SlotCount = 10;
        public static event Action PresetsChanged;
        private const string AutoSaveFileName = "character_autosave.json";

        [Serializable]
        private sealed class PresetFile
        {
            public int coordinateVersion;
            public int selectedSpecies;
            public int selectedPart;
            public SpeciesEntry[] species;
        }

        [Serializable]
        private sealed class SpeciesEntry
        {
            public int species;
            public PartEntry[] parts;
        }

        [Serializable]
        private sealed class PartEntry
        {
            public int part;
            public Vector2[] points;
            public float[] strokeWidths;
        }

        public static bool Exists(int slot)
        {
            return File.Exists(GetPath(slot));
        }

        public static bool Exists(DrawManager.Species species, int slot)
        {
            if (File.Exists(GetSpeciesPath(species, slot))) return true;
            return TryExtractSpeciesState(LoadFromPath(GetPath(slot)), species, out _);
        }

        public static bool Save(int slot, DrawManager.DrawingState state)
        {
            bool saved = SaveToPath(GetPath(slot), state);
            if (saved) PresetsChanged?.Invoke();
            return saved;
        }

        public static bool SaveAuto(DrawManager.DrawingState state)
        {
            return SaveToPath(Path.Combine(Application.persistentDataPath, AutoSaveFileName), state);
        }

        public static bool Save(DrawManager.Species species, int slot, DrawManager.DrawingState state)
        {
            if (state == null
                || !state.Points.TryGetValue(
                    species,
                    out Dictionary<DrawManager.BodyPart, List<Vector2>> sourceParts))
            {
                return false;
            }

            DrawManager.DrawingState speciesState = new DrawManager.DrawingState
            {
                Species = species,
                Part = state.Species == species ? state.Part : DrawManager.GetPartsForSpecies(species)[0]
            };
            Dictionary<DrawManager.BodyPart, List<Vector2>> copiedParts =
                new Dictionary<DrawManager.BodyPart, List<Vector2>>();
            foreach (KeyValuePair<DrawManager.BodyPart, List<Vector2>> part in sourceParts)
            {
                copiedParts[part.Key] = part.Value != null
                    ? new List<Vector2>(part.Value)
                    : new List<Vector2>();
            }

            speciesState.Points[species] = copiedParts;
            if (state.StrokeWidths.TryGetValue(
                    species,
                    out Dictionary<DrawManager.BodyPart, List<float>> sourceWidths))
            {
                Dictionary<DrawManager.BodyPart, List<float>> copiedWidths =
                    new Dictionary<DrawManager.BodyPart, List<float>>();
                foreach (KeyValuePair<DrawManager.BodyPart, List<float>> part in sourceWidths)
                {
                    copiedWidths[part.Key] = part.Value != null
                        ? new List<float>(part.Value)
                        : new List<float>();
                }
                speciesState.StrokeWidths[species] = copiedWidths;
            }
            bool saved = SaveToPath(GetSpeciesPath(species, slot), speciesState);
            if (saved) PresetsChanged?.Invoke();
            return saved;
        }

        private static bool SaveToPath(string path, DrawManager.DrawingState state)
        {
            if (state == null) return false;
            try
            {
                List<SpeciesEntry> speciesEntries = new List<SpeciesEntry>();
                foreach (KeyValuePair<DrawManager.Species, Dictionary<DrawManager.BodyPart, List<Vector2>>> speciesPair in state.Points)
                {
                    List<PartEntry> partEntries = new List<PartEntry>();
                    foreach (KeyValuePair<DrawManager.BodyPart, List<Vector2>> partPair in speciesPair.Value)
                    {
                        partEntries.Add(new PartEntry
                        {
                            part = (int)partPair.Key,
                            points = partPair.Value != null ? partPair.Value.ToArray() : Array.Empty<Vector2>(),
                            strokeWidths = state.StrokeWidths.TryGetValue(
                                    speciesPair.Key,
                                    out Dictionary<DrawManager.BodyPart, List<float>> speciesWidths)
                                && speciesWidths.TryGetValue(partPair.Key, out List<float> widths)
                                && widths != null
                                    ? widths.ToArray()
                                    : Array.Empty<float>()
                        });
                    }
                    speciesEntries.Add(new SpeciesEntry
                    {
                        species = (int)speciesPair.Key,
                        parts = partEntries.ToArray()
                    });
                }
                PresetFile file = new PresetFile
                {
                    coordinateVersion = DrawManager.BodyCoordinateVersion,
                    selectedSpecies = (int)state.Species,
                    selectedPart = (int)state.Part,
                    species = speciesEntries.ToArray()
                };
                File.WriteAllText(path, JsonUtility.ToJson(file));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not save character preset: " + exception.Message);
                return false;
            }
        }

        public static DrawManager.DrawingState Load(int slot)
        {
            return LoadFromPath(GetPath(slot));
        }

        public static DrawManager.DrawingState LoadAuto()
        {
            return LoadFromPath(Path.Combine(Application.persistentDataPath, AutoSaveFileName));
        }

        public static DrawManager.DrawingState Load(DrawManager.Species species, int slot)
        {
            DrawManager.DrawingState state = LoadFromPath(GetSpeciesPath(species, slot));
            bool loadedLegacy = state == null;
            if (loadedLegacy)
            {
                state = LoadFromPath(GetPath(slot));
            }

            if (!TryExtractSpeciesState(state, species, out DrawManager.DrawingState speciesState))
                return null;

            // Older builds stored all species in character_preset_N.json. Keep
            // that file intact and lazily copy the requested species into the
            // current per-species format so existing presets do not disappear.
            if (loadedLegacy) SaveToPath(GetSpeciesPath(species, slot), speciesState);
            return speciesState;
        }

        private static bool TryExtractSpeciesState(
            DrawManager.DrawingState state,
            DrawManager.Species species,
            out DrawManager.DrawingState speciesState)
        {
            speciesState = null;
            if (state == null
                || !state.Points.TryGetValue(
                    species,
                    out Dictionary<DrawManager.BodyPart, List<Vector2>> sourceParts))
            {
                return false;
            }

            speciesState = new DrawManager.DrawingState
            {
                Species = species,
                Part = state.Species == species
                    ? state.Part
                    : DrawManager.GetPartsForSpecies(species)[0]
            };
            speciesState.Points[species] = sourceParts;
            if (state.StrokeWidths.TryGetValue(
                    species,
                    out Dictionary<DrawManager.BodyPart, List<float>> sourceWidths))
            {
                speciesState.StrokeWidths[species] = sourceWidths;
            }
            return true;
        }

        private static DrawManager.DrawingState LoadFromPath(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                PresetFile file = JsonUtility.FromJson<PresetFile>(File.ReadAllText(path));
                if (file?.species == null) return null;
                DrawManager.DrawingState state = new DrawManager.DrawingState
                {
                    Species = (DrawManager.Species)file.selectedSpecies,
                    Part = (DrawManager.BodyPart)file.selectedPart
                };
                foreach (DrawManager.Species species in Enum.GetValues(typeof(DrawManager.Species)))
                {
                    Dictionary<DrawManager.BodyPart, List<Vector2>> parts = new Dictionary<DrawManager.BodyPart, List<Vector2>>();
                    Dictionary<DrawManager.BodyPart, List<float>> widths = new Dictionary<DrawManager.BodyPart, List<float>>();
                    foreach (DrawManager.BodyPart part in DrawManager.GetAllParts()) parts[part] = new List<Vector2>();
                    foreach (DrawManager.BodyPart part in DrawManager.GetAllParts()) widths[part] = new List<float>();
                    state.Points[species] = parts;
                    state.StrokeWidths[species] = widths;
                }
                for (int i = 0; i < file.species.Length; i++)
                {
                    SpeciesEntry speciesEntry = file.species[i];
                    DrawManager.Species species = (DrawManager.Species)speciesEntry.species;
                    if (!state.Points.TryGetValue(species, out Dictionary<DrawManager.BodyPart, List<Vector2>> parts)
                        || speciesEntry.parts == null) continue;
                    for (int p = 0; p < speciesEntry.parts.Length; p++)
                    {
                        PartEntry partEntry = speciesEntry.parts[p];
                        List<Vector2> points = partEntry.points != null
                            ? new List<Vector2>(partEntry.points)
                            : new List<Vector2>();
                        if (species == DrawManager.Species.Slime && file.coordinateVersion < 3)
                        {
                            for (int point = 0; point < points.Count; point++)
                            {
                                if (!float.IsNaN(points[point].x) && !float.IsNaN(points[point].y))
                                {
                                    points[point] *= 0.2f;
                                }
                            }
                        }
                        parts[(DrawManager.BodyPart)partEntry.part] = points;
                        state.StrokeWidths[species][(DrawManager.BodyPart)partEntry.part] =
                            partEntry.strokeWidths != null
                                ? new List<float>(partEntry.strokeWidths)
                                : new List<float>();
                    }
                }
                return state;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not load character preset: " + exception.Message);
                return null;
            }
        }

        private static string GetPath(int slot)
        {
            int safeSlot = Mathf.Clamp(slot, 0, SlotCount - 1) + 1;
            return Path.Combine(Application.persistentDataPath, "character_preset_" + safeSlot + ".json");
        }

        private static string GetSpeciesPath(DrawManager.Species species, int slot)
        {
            int safeSlot = Mathf.Clamp(slot, 0, SlotCount - 1) + 1;
            string speciesName = species.ToString().ToLowerInvariant();
            return Path.Combine(
                Application.persistentDataPath,
                "draw_preset_" + speciesName + "_" + safeSlot + ".json");
        }
    }
}
