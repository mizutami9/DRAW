using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using DrawBody.Prototype;
using UnityEditor;
using UnityEngine;

namespace DrawBody.EditorTools
{
    public sealed class SfxBrowserWindow : EditorWindow
    {
        private sealed class Entry
        {
            public string Category;
            public string Action;
            public SfxId Id;
            public string ResourcePath;
            public AudioClip Clip;
        }

        private static readonly Regex TableRowPattern = new Regex(
            @"^\|\s*(.*?)\s*\|\s*`([A-Za-z0-9]+)`\s*\|",
            RegexOptions.Compiled);

        private readonly List<Entry> entries = new List<Entry>();
        private Vector2 scroll;
        private string search = string.Empty;
        private GUIStyle actionStyle;
        private GUIStyle idStyle;
        private string loadError;

        [MenuItem("Tools/NicoDraw/SE Browser")]
        private static void Open()
        {
            SfxBrowserWindow window = GetWindow<SfxBrowserWindow>();
            window.titleContent = new GUIContent(LocalizationManager.T("sfx_browser_title"));
            window.minSize = new Vector2(720f, 440f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadEntries();
            LocalizationManager.LanguageChanged += HandleLanguageChanged;
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= HandleLanguageChanged;
            StopPreview();
            ReleaseGeneratedClips();
        }

        private void HandleLanguageChanged()
        {
            titleContent = new GUIContent(LocalizationManager.T("sfx_browser_title"));
            Repaint();
        }

        private void OnGUI()
        {
            EnsureStyles();
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(LocalizationManager.T("sfx_browser_title"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(LocalizationManager.T("sfx_browser_help"), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                search = GUILayout.TextField(search, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.MinWidth(220f));
                if (GUILayout.Button(LocalizationManager.T("sfx_browser_stop"), EditorStyles.toolbarButton, GUILayout.Width(72f)))
                {
                    StopPreview();
                }
                if (GUILayout.Button(LocalizationManager.T("sfx_browser_reload"), EditorStyles.toolbarButton, GUILayout.Width(72f)))
                {
                    LoadEntries();
                }
            }

            if (!string.IsNullOrEmpty(loadError))
            {
                EditorGUILayout.HelpBox(loadError, MessageType.Error);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            string shownCategory = null;
            int visibleCount = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (!MatchesSearch(entry)) continue;
                visibleCount++;

                if (!string.Equals(shownCategory, entry.Category, StringComparison.Ordinal))
                {
                    shownCategory = entry.Category;
                    EditorGUILayout.Space(8f);
                    EditorGUILayout.LabelField(shownCategory, EditorStyles.boldLabel);
                }

                DrawEntry(entry);
            }

            if (visibleCount == 0)
            {
                EditorGUILayout.HelpBox(LocalizationManager.T("sfx_browser_no_results"), MessageType.Info);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawEntry(Entry entry)
        {
            AudioClip clip = entry.Clip;
            bool muted = GameSfx.IsMuted(entry.Id);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUIContent action = new GUIContent(entry.Action, entry.ResourcePath);
                EditorGUILayout.LabelField(action, actionStyle, GUILayout.MinWidth(310f), GUILayout.ExpandWidth(true));
                EditorGUILayout.SelectableLabel(entry.Id.ToString(), idStyle, GUILayout.Width(190f), GUILayout.Height(21f));
                using (new EditorGUI.DisabledScope(clip == null))
                {
                    string buttonLabel = muted
                        ? LocalizationManager.T("sfx_browser_muted")
                        : LocalizationManager.T("sfx_browser_play");
                    if (GUILayout.Button(buttonLabel, GUILayout.Width(72f), GUILayout.Height(24f)))
                    {
                        PlayPreview(clip);
                    }
                }
            }
        }

        private bool MatchesSearch(Entry entry)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            return entry.Action.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || entry.Id.ToString().IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || entry.Category.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || entry.ResourcePath.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void LoadEntries()
        {
            ReleaseGeneratedClips();
            entries.Clear();
            loadError = null;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "SFX_USAGE.md"));
            if (!File.Exists(path))
            {
                loadError = LocalizationManager.T("sfx_browser_file_missing");
                return;
            }

            string category = LocalizationManager.T("sfx_browser_uncategorized");
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    category = line.Substring(3).Trim();
                    continue;
                }

                Match match = TableRowPattern.Match(line);
                if (!match.Success || !Enum.TryParse(match.Groups[2].Value, out SfxId id)) continue;
                AudioClip previewClip = GameSfx.CreatePreviewClip(id);
                if (previewClip != null && GameSfx.UsesGeneratedClip(id))
                {
                    AudioClip generatedClip = previewClip;
                    previewClip = CreateAssetBackedPreview(id, generatedClip);
                    DestroyImmediate(generatedClip);
                }

                entries.Add(new Entry
                {
                    Category = category,
                    Action = StripMarkdown(match.Groups[1].Value),
                    Id = id,
                    ResourcePath = GameSfx.IsMuted(id)
                        ? LocalizationManager.T("sfx_browser_muted")
                        : GameSfx.UsesGeneratedClip(id)
                            ? LocalizationManager.T("sfx_browser_generated")
                            : SfxCatalog.Get(id).ResourcePath,
                    Clip = previewClip
                });
            }

            Repaint();
        }

        private void ReleaseGeneratedClips()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry.Clip != null
                    && GameSfx.UsesGeneratedClip(entry.Id)
                    && !AssetDatabase.Contains(entry.Clip))
                {
                    DestroyImmediate(entry.Clip);
                    entry.Clip = null;
                }
            }
        }

        private static AudioClip CreateAssetBackedPreview(SfxId id, AudioClip source)
        {
            const string cacheFolder = "Assets/Generated/SfxPreviewCache";
            if (!AssetDatabase.IsValidFolder(cacheFolder))
            {
                AssetDatabase.CreateFolder("Assets/Generated", "SfxPreviewCache");
            }
            string assetPath = cacheFolder + "/" + id + ".wav";
            WriteWaveFile(assetPath, source);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer != null)
            {
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                bool changed = settings.loadType != AudioClipLoadType.DecompressOnLoad
                    || settings.compressionFormat != AudioCompressionFormat.PCM
                    || !settings.preloadAudioData;
                if (changed)
                {
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    settings.compressionFormat = AudioCompressionFormat.PCM;
                    settings.preloadAudioData = true;
                    importer.defaultSampleSettings = settings;
                    importer.SaveAndReimport();
                }
            }

            return AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        }

        private static void WriteWaveFile(string path, AudioClip clip)
        {
            int channels = Mathf.Max(1, clip.channels);
            int sampleRate = Mathf.Max(8000, clip.frequency);
            float[] samples = new float[clip.samples * channels];
            clip.GetData(samples, 0);
            int dataLength = samples.Length * sizeof(short);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataLength);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });
                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * sizeof(short));
                writer.Write((short)(channels * sizeof(short)));
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataLength);
                for (int i = 0; i < samples.Length; i++)
                {
                    writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue));
                }
            }
        }

        private static string StripMarkdown(string value)
        {
            return value.Replace("**", string.Empty).Replace("`", string.Empty).Trim();
        }

        private void EnsureStyles()
        {
            if (actionStyle == null)
            {
                actionStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, fontSize = 13 };
                idStyle = new GUIStyle(EditorStyles.textField) { alignment = TextAnchor.MiddleLeft, fontSize = 11 };
            }
        }

        private static void PlayPreview(AudioClip clip)
        {
            if (clip == null) return;
            StopPreview();
            InvokeAudioUtil(new[] { "PlayPreviewClip", "PlayClip" }, clip);
        }

        private static void StopPreview()
        {
            InvokeAudioUtil(new[] { "StopAllPreviewClips", "StopAllClips" }, null);
        }

        private static void InvokeAudioUtil(string[] methodNames, AudioClip clip)
        {
            Type audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (audioUtil == null) return;
            MethodInfo[] methods = audioUtil.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (int nameIndex = 0; nameIndex < methodNames.Length; nameIndex++)
            {
                for (int methodIndex = 0; methodIndex < methods.Length; methodIndex++)
                {
                    MethodInfo method = methods[methodIndex];
                    if (!string.Equals(method.Name, methodNames[nameIndex], StringComparison.Ordinal)) continue;
                    ParameterInfo[] parameters = method.GetParameters();
                    if (clip != null && (parameters.Length == 0 || parameters[0].ParameterType != typeof(AudioClip))) continue;
                    if (clip == null && parameters.Length != 0) continue;

                    object[] arguments = new object[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        Type type = parameters[i].ParameterType;
                        arguments[i] = type == typeof(AudioClip) ? clip
                            : type == typeof(int) ? 0
                            : type == typeof(bool) ? false
                            : type.IsValueType ? Activator.CreateInstance(type)
                            : null;
                    }
                    method.Invoke(null, arguments);
                    return;
                }
            }
        }
    }
}
