using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DrawBody.Prototype
{
    public static class CharacterDrawingPresetStore
    {
        public const int SlotCount = 3;

        [Serializable]
        private sealed class PresetFile
        {
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
        }

        public static bool Exists(int slot)
        {
            return File.Exists(GetPath(slot));
        }

        public static bool Save(int slot, DrawManager.DrawingState state)
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
                            points = partPair.Value != null ? partPair.Value.ToArray() : Array.Empty<Vector2>()
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
                    selectedSpecies = (int)state.Species,
                    selectedPart = (int)state.Part,
                    species = speciesEntries.ToArray()
                };
                File.WriteAllText(GetPath(slot), JsonUtility.ToJson(file));
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
            try
            {
                string path = GetPath(slot);
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
                    foreach (DrawManager.BodyPart part in DrawManager.GetAllParts()) parts[part] = new List<Vector2>();
                    state.Points[species] = parts;
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
                        parts[(DrawManager.BodyPart)partEntry.part] = partEntry.points != null
                            ? new List<Vector2>(partEntry.points)
                            : new List<Vector2>();
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
    }
}
