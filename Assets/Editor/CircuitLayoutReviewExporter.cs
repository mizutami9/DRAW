using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using DrawBody.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DrawBody.EditorTools
{
    // Run in an isolated project with -executeMethod ...CircuitLayoutReviewExporter.Run
    // and -circuit-review-output <directory>, without -quit.
    [InitializeOnLoad]
    public static class CircuitLayoutReviewExporter
    {
        private const int Rounds = StageHumanCircuitController.RoundCount;
        private const string Pending = "PICO.CircuitLayoutReview.Pending";
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static CircuitLayoutReviewExporter() => EditorApplication.playModeStateChanged += OnPlayMode;
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }
        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            try { Export(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        private static void Export()
        {
            string directory = Path.GetFullPath("Artifacts/13-3-three-rounds-review");
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-circuit-review-output") directory = Path.GetFullPath(args[i + 1]);
            Directory.CreateDirectory(directory);
            StageData data = JsonUtility.FromJson<StageData>(Resources.Load<TextAsset>("Stages/13-3").text);
            Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/Yomogi-Regular.ttf");
            var overview = new Texture2D(3200, Rounds * 180, TextureFormat.RGB24, false);
            StringBuilder report = new StringBuilder("13-3 current generated layout. Coordinates include current motion at t=0.\n");
            for (int count = 1; count <= 4; count++)
            {
                GameObject root = new GameObject("Circuit Layout Review " + count);
                StageObjectFactory factory = root.AddComponent<StageObjectFactory>();
                factory.ConfigureStageVisualTheme("13-3", root.transform);
                foreach (StageObjectData item in data.objects) factory.Create(item, root.transform);
                StageHumanCircuitController circuit = root.AddComponent<StageHumanCircuitController>();
                Set(circuit, "playerCount", count);
                Call(circuit, "BuildStageCircuits");
                Call(circuit, "PrepareSequentialStage");
                Call(circuit, "BuildBoxStations");
                factory.CreateFactoryStageBackdrop("13-3", data.objects, root.transform);
                // Capture after the first visual update: freshly created accent
                // sprites otherwise still have their default full-size circles.
                foreach (ThemedBackdropAmbientAnimator animator in root.GetComponentsInChildren<ThemedBackdropAmbientAnimator>())
                    Call(animator, "Update");
                GameObject cameraObject = new GameObject("Review Camera");
                cameraObject.transform.SetParent(root.transform);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.94f, 0.96f, 0.92f, 1f);
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                GameObject headingObject = new GameObject("Review Label (not gameplay UI)");
                headingObject.transform.SetParent(root.transform);
                TextMesh heading = headingObject.AddComponent<TextMesh>();
                heading.font = font;
                heading.fontSize = 64;
                heading.characterSize = 0.12f;
                heading.anchor = TextAnchor.UpperLeft;
                heading.color = new Color(0.04f, 0.09f, 0.16f);
                heading.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                heading.GetComponent<MeshRenderer>().sortingOrder = 1000;
                var playerSheet = new Texture2D(1600, Rounds * 360, TextureFormat.RGB24, false);
                IList rooms = (IList)Get(circuit, "rooms");
                for (int round = 0; round < Rounds; round++)
                {
                    Call(circuit, "ActivateRound", round, false);
                    Call(circuit, "UpdateMovingGaps");
                    Call(circuit, "RefreshAllVisuals");
                    heading.text = count + " PLAYER" + (count == 1 ? "" : "S") + "  /  STAGE " + (round + 1);
                    foreach (TextMesh text in root.GetComponentsInChildren<TextMesh>(true))
                    {
                        text.font = font;
                        text.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                        font.RequestCharactersInTexture(text.text, text.fontSize);
                    }
                    camera.transform.position = new Vector3(0f, 0.55f, -10f);
                    camera.orthographicSize = 9.6f;
                    heading.characterSize = 0.075f;
                    heading.transform.position = new Vector3(-20.3f, 9.9f, -1f);
                    Texture2D full = Capture(camera, 1600, 720);
                    Save(full, Path.Combine(directory, $"p{count}-stage{round + 1}-full.png"));
                    Object.DestroyImmediate(full);
                    camera.transform.position = new Vector3(0f, -4.5f, -10f);
                    camera.orthographicSize = 4.8f;
                    heading.characterSize = 0.12f;
                    heading.transform.position = new Vector3(-20.3f, 0.05f, -1f);
                    Texture2D detail = Capture(camera, 1600, 360);
                    Save(detail, Path.Combine(directory, $"p{count}-stage{round + 1}.png"));
                    playerSheet.SetPixels(0, (Rounds - 1 - round) * 360, 1600, 360, detail.GetPixels());
                    Object.DestroyImmediate(detail);
                    Texture2D thumb = Capture(camera, 800, 180);
                    overview.SetPixels((count - 1) * 800, (Rounds - 1 - round) * 180, 800, 180, thumb.GetPixels());
                    Object.DestroyImmediate(thumb);
                    report.AppendLine($"P{count} stage {round + 1}");
                    foreach (object path in (IList)Get(rooms[round], "Paths"))
                        foreach (object gap in (IList)Get(path, "Gaps"))
                            report.AppendLine($"  gap: {Get(Get(gap, "Left"), "Position")} -> {Get(Get(gap, "Right"), "Position")}");
                }
                playerSheet.Apply();
                Save(playerSheet, Path.Combine(directory, $"{count}-players-all-stages.png"));
                Object.DestroyImmediate(playerSheet);
                Object.DestroyImmediate(root);
            }
            overview.Apply();
            Save(overview, Path.Combine(directory, "overview-layouts.png"));
            Object.DestroyImmediate(overview);
            File.WriteAllText(Path.Combine(directory, "layout-coordinates.txt"), report.ToString(), Encoding.UTF8);
            WriteHtml(directory);
            Debug.Log("[CIRCUIT REVIEW] Exported all layouts to " + directory);
        }
        private static Texture2D Capture(Camera camera, int width, int height)
        {
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = rt;
            camera.aspect = (float)width / height;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return image;
        }
        private static void Save(Texture2D texture, string path) => File.WriteAllBytes(path, texture.EncodeToPNG());
        private static void WriteHtml(string directory)
        {
            var html = new StringBuilder("<!doctype html><html lang='ja'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>13-3 配置レビュー</title><style>body{font:16px system-ui;background:#eef2e9;color:#182839;margin:24px}h1{margin-bottom:8px}p{line-height:1.7}nav{position:sticky;top:0;background:#eef2e9;padding:12px;z-index:1}button,a{margin-right:12px;color:#174e80}button{padding:8px 16px}section{margin:28px 0}article{background:white;padding:12px;margin:16px 0;border-radius:8px}img{display:block;width:100%;height:auto}summary{cursor:pointer;padding:10px}small{color:#405366}</style><h1>13-3 ／ 1〜4人 × 全3ステージ</h1><p>現行コードからUnityで生成した配置。キャラ・HUDは省略。上段の人数・ステージ名はレビュー用の注記です。ステージ3の可動端子は時刻0の位置。クリックで原寸表示できます。</p><nav><button onclick='show(0)'>すべて</button>");
            for (int p = 1; p <= 4; p++) html.Append($"<button onclick='show({p})'>{p}人</button>");
            html.Append("<a href='overview-layouts.png'>12構成の一覧画像</a></nav>");
            for (int p = 1; p <= 4; p++)
            {
                html.Append($"<section data-p='{p}'><h2>{p}人構成</h2><a href='{p}-players-all-stages.png'>この人数の3ステージを1枚で開く</a>");
                for (int r = 1; r <= Rounds; r++)
                    html.Append($"<article><h3>{p}人 ／ ステージ{r}</h3><a href='p{p}-stage{r}.png'><img loading='lazy' src='p{p}-stage{r}.png'></a><details><summary>部屋全体を見る（箱装置・床・モニターを含む）</summary><a href='p{p}-stage{r}-full.png'><img loading='lazy' src='p{p}-stage{r}-full.png'></a></details></article>");
                html.Append("</section>");
            }
            html.Append("<script>function show(p){document.querySelectorAll('section').forEach(s=>s.hidden=p!==0&&Number(s.dataset.p)!==p)}</script></html>");
            File.WriteAllText(Path.Combine(directory, "index.html"), html.ToString(), Encoding.UTF8);
        }
        private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    }
}
