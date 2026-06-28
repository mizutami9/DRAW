using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class StageManager : MonoBehaviour
    {
        [SerializeField] private PlayerController2D player;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private DrawManager drawManager;
        [SerializeField] private StageLoader stageLoader;
        [SerializeField] private RuntimeStageEditor stageEditor;
        [SerializeField] private CameraFollow2D cameraFollow;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float fallResetY = -6f;
        [SerializeField] private LayerMask groundLayer = 1 << 6;
        [SerializeField] private float groundSeparation = 0.06f;

        private PlayerController2D primaryPlayer;
        private PlayerController2D secondaryPlayer;
        private bool drawing;
        private bool cleared;
        private bool stageStarted;
        private bool stageEditing;
        private bool titleMode;
        private string currentStageId = "1-0";
        private readonly Dictionary<PlayerController2D, DrawManager.DrawingState> drawingStates =
            new Dictionary<PlayerController2D, DrawManager.DrawingState>();

        private void Awake()
        {
            if (player == null)
            {
                player = FindObjectOfType<PlayerController2D>();
            }

            primaryPlayer = player;

            if (uiManager == null)
            {
                uiManager = FindObjectOfType<UIManager>();
            }

            if (drawManager == null)
            {
                drawManager = FindObjectOfType<DrawManager>();
            }

            if (stageLoader == null)
            {
                stageLoader = FindObjectOfType<StageLoader>();
            }

            if (stageEditor == null)
            {
                stageEditor = FindObjectOfType<RuntimeStageEditor>();
            }

            if (cameraFollow == null)
            {
                cameraFollow = FindObjectOfType<CameraFollow2D>();
            }

            ConfigureActivePlayerTargets();
        }

        private void Start()
        {
            EnterTitle();
        }

        public void EnterTitle()
        {
            currentStageId = "title";
            titleMode = true;
            stageStarted = true;
            stageEditing = false;
            drawing = false;
            cleared = false;
            Time.timeScale = 1f;
            stageLoader?.ShowFallbackStage();
            SetCameraFollowEnabled(true);
            uiManager?.SetDrawing(false);
            uiManager?.SetCleared(false);
            uiManager?.SetStageSelect(false);
            uiManager?.SetStageEditor(false);
            uiManager?.SetTitle(true);
            drawManager?.SetActive(false);
            RespawnPlayers();
            SetActivePlayer(player != null ? player : primaryPlayer, true);
        }

        public void OpenSingleMenu()
        {
            titleMode = false;
            OpenStageSelect();
        }

        public void OpenMultiMenu()
        {
            uiManager?.SetMulti(true);
        }

        public void OpenOptionMenu()
        {
            uiManager?.SetOption(true);
        }

        public void CloseTitleSubmenu()
        {
            uiManager?.SetMulti(false);
            uiManager?.SetOption(false);
        }

        public void ExitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void Update()
        {
            if (!stageStarted)
            {
                return;
            }

            if (stageEditing)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (drawing)
                {
                    CancelDrawingMode();
                }
                else
                {
                    uiManager?.HideMenu();
                }
            }

            if (Input.GetKeyDown(KeyCode.Tab) && !cleared)
            {
                if (drawing)
                {
                    CancelDrawingMode();
                }
                else
                {
                    EnterDrawingMode();
                }
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                Retry();
            }

            if (!drawing && !cleared)
            {
                RespawnFallenPlayers();
            }
        }

        public void ClearStage()
        {
            if (cleared)
            {
                return;
            }

            cleared = true;
            ExitDrawingMode();
            SetAllPlayerControls(false);
            player?.ResetMotion();
            secondaryPlayer?.ResetMotion();
            uiManager?.SetCleared(true);
        }

        public void Retry()
        {
            if (!stageStarted)
            {
                return;
            }

            cleared = false;
            CancelDrawingMode();

            RespawnPlayers();

            uiManager?.SetCleared(false);
        }

        public void EnterDrawingMode()
        {
            if (!stageStarted)
            {
                return;
            }

            drawing = true;
            if (player != null)
            {
                player.ResetMotion();
            }

            Time.timeScale = titleMode ? 1f : 0f;
            player?.SetControlsEnabled(false);
            uiManager?.SetDrawing(true);
            drawManager?.SetActive(true);
        }

        public void ExitDrawingMode()
        {
            if (!drawing)
            {
                return;
            }

            drawing = false;
            RestoreRedrawPose();
            Time.timeScale = 1f;
            uiManager?.SetDrawing(false);
            drawManager?.SetActive(false);
            SaveDrawingState(player);
            player?.SetControlsEnabled(!cleared);
        }

        public void CancelDrawingMode()
        {
            if (!drawing)
            {
                return;
            }

            drawManager?.CancelEditing();
            ExitDrawingMode();
        }

        public void SelectStage(string stageId)
        {
            currentStageId = string.IsNullOrEmpty(stageId) ? "1-0" : stageId;
            titleMode = false;
            if (stageLoader != null)
            {
                if (currentStageId == "1-0")
                {
                    stageLoader.ShowFallbackStage();
                }
                else
                {
                    stageLoader.LoadStage(currentStageId);
                }
            }

            stageStarted = true;
            drawing = false;
            cleared = false;
            Time.timeScale = 1f;
            SetCameraFollowEnabled(true);
            uiManager?.SetTitle(false);
            uiManager?.SetStageSelect(false);
            uiManager?.SetStageEditor(false);
            uiManager?.SetDrawing(false);
            uiManager?.SetCleared(false);
            RespawnPlayers();
            SetActivePlayer(player != null ? player : primaryPlayer, true);
        }

        public void OpenStageEditor(string stageId)
        {
            currentStageId = string.IsNullOrEmpty(stageId) ? "1-1" : stageId;
            CancelDrawingMode();
            stageStarted = false;
            stageEditing = true;
            titleMode = false;
            cleared = false;
            Time.timeScale = 0f;
            SetCameraFollowEnabled(false);
            player?.ResetMotion();
            player?.SetControlsEnabled(false);
            stageLoader?.HideStages();
            uiManager?.HideMenu();
            uiManager?.SetStageSelect(false);
            uiManager?.SetDrawing(false);
            uiManager?.SetCleared(false);
            uiManager?.SetStageEditor(true);
            stageEditor?.Open(currentStageId);
        }

        public void CloseStageEditor()
        {
            stageEditor?.Close();
            stageEditing = false;
            stageStarted = false;
            Time.timeScale = 0f;
            SetCameraFollowEnabled(true);
            player?.ResetMotion();
            player?.SetControlsEnabled(false);
            uiManager?.SetStageEditor(false);
            uiManager?.SetStageSelect(true);
        }

        public void TestEditedStage()
        {
            if (stageEditor == null)
            {
                return;
            }

            stageEditor.TestPlay();
            stageEditing = false;
            stageStarted = true;
            drawing = false;
            cleared = false;
            Time.timeScale = 1f;
            SetCameraFollowEnabled(true);
            uiManager?.SetStageEditor(false);
            uiManager?.SetStageSelect(false);
            uiManager?.SetDrawing(false);
            uiManager?.SetCleared(false);
            RespawnPlayers();
            SetActivePlayer(player != null ? player : primaryPlayer, true);
        }

        public void OpenStageSelect()
        {
            CancelDrawingMode();
            stageEditor?.Close();
            stageEditing = false;
            stageStarted = false;
            titleMode = false;
            Time.timeScale = 0f;
            SetCameraFollowEnabled(true);
            player?.ResetMotion();
            player?.SetControlsEnabled(false);
            uiManager?.HideMenu();
            uiManager?.SetTitle(false);
            uiManager?.SetStageEditor(false);
            uiManager?.SetStageSelect(true);
        }

        public Transform ActivePlayerTransform => player != null ? player.transform : null;
        public Transform RemotePlayerTransform => secondaryPlayer != null ? secondaryPlayer.transform : null;

        public Rigidbody2D ActivePlayerBody
        {
            get
            {
                return player != null ? player.GetComponent<Rigidbody2D>() : null;
            }
        }

        public void EnsureOnlineRemotePlayer()
        {
            if (secondaryPlayer == null)
            {
                AddCharacter();
            }

            if (secondaryPlayer != null)
            {
                secondaryPlayer.SetControlsEnabled(false);
            }
        }

        public void ApplyOnlineRemoteState(Vector2 position, Vector2 velocity, float rotation)
        {
            EnsureOnlineRemotePlayer();
            if (secondaryPlayer == null)
            {
                return;
            }

            Rigidbody2D remoteBody = secondaryPlayer.GetComponent<Rigidbody2D>();
            if (remoteBody != null)
            {
                remoteBody.position = position;
                remoteBody.linearVelocity = velocity;
                remoteBody.rotation = rotation;
            }
            else
            {
                secondaryPlayer.transform.position = position;
                secondaryPlayer.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            }

            secondaryPlayer.SetControlsEnabled(false);
        }

        public void ApplyOnlineRemoteBodyData(OnlineBodyData bodyData)
        {
            if (bodyData == null || drawManager == null || string.IsNullOrEmpty(bodyData.Json))
            {
                return;
            }

            EnsureOnlineRemotePlayer();
            if (secondaryPlayer == null)
            {
                return;
            }

            DrawManager.DrawingState remoteState = drawManager.CreateStateFromBodyJson(bodyData.Json);
            if (remoteState == null)
            {
                return;
            }

            SaveDrawingState(player);
            BodyBuilder remoteBuilder = secondaryPlayer.GetComponent<BodyBuilder>();
            PlayerAbilityController remoteAbilities = secondaryPlayer.GetComponent<PlayerAbilityController>();
            drawManager.SetBuildTarget(remoteBuilder, remoteAbilities);
            drawManager.LoadState(remoteState, true);
            drawingStates[secondaryPlayer] = CloneDrawingState(remoteState);
            ConfigureActivePlayerTargets();
            LoadDrawingState(player);
            secondaryPlayer.SetControlsEnabled(false);
            LiftPlayerOutOfGround(secondaryPlayer);
        }

        public void AddCharacter()
        {
            if (secondaryPlayer != null || primaryPlayer == null)
            {
                return;
            }

            SaveDrawingState(primaryPlayer);
            Vector3 offset = new Vector3(Mathf.Sign(primaryPlayer.FacingDirection) * 1.25f, 0.35f, 0f);
            GameObject clone = Instantiate(primaryPlayer.gameObject, primaryPlayer.transform.position + offset, primaryPlayer.transform.rotation, primaryPlayer.transform.parent);
            clone.name = "Player 2";
            secondaryPlayer = clone.GetComponent<PlayerController2D>();
            if (secondaryPlayer == null)
            {
                Destroy(clone);
                return;
            }

            secondaryPlayer.ResetMotion();
            secondaryPlayer.SetControlsEnabled(false);
            if (drawingStates.TryGetValue(primaryPlayer, out DrawManager.DrawingState state))
            {
                drawingStates[secondaryPlayer] = CloneDrawingState(state);
            }

            BodyBuilder bodyBuilder = secondaryPlayer.GetComponent<BodyBuilder>();
            if (bodyBuilder != null)
            {
                bodyBuilder.SetFacingDirection(primaryPlayer.FacingDirection);
            }

            LiftPlayerOutOfGround(secondaryPlayer);
        }

        public void DeleteAddedCharacter()
        {
            if (secondaryPlayer == null)
            {
                return;
            }

            if (player == secondaryPlayer)
            {
                SetActivePlayer(primaryPlayer, true);
            }

            primaryPlayer?.GetComponent<PlayerCarryController>()?.ForceDrop();
            secondaryPlayer.GetComponent<PlayerCarryController>()?.ForceDrop();

            GameObject target = secondaryPlayer.gameObject;
            drawingStates.Remove(secondaryPlayer);
            secondaryPlayer = null;
            Destroy(target);
        }

        public void SwitchCharacter()
        {
            if (secondaryPlayer == null || primaryPlayer == null)
            {
                return;
            }

            SetActivePlayer(player == secondaryPlayer ? primaryPlayer : secondaryPlayer, true);
        }

        private void RestoreRedrawPose()
        {
            if (player == null)
            {
                return;
            }

            RespawnPlayer(player, GetRespawnOffset(player), false);
        }

        private void LiftPlayerOutOfGround(PlayerController2D targetPlayer)
        {
            if (targetPlayer == null)
            {
                return;
            }

            Collider2D[] colliders = targetPlayer.GetComponentsInChildren<Collider2D>();
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(groundLayer);
            filter.useTriggers = false;
            Collider2D[] hits = new Collider2D[12];

            for (int iteration = 0; iteration < 24; iteration++)
            {
                bool overlapped = false;
                float highestGroundTop = float.NegativeInfinity;
                float lowestPlayerBottom = float.PositiveInfinity;

                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider2D playerCollider = colliders[i];
                    if (playerCollider == null || !playerCollider.enabled || playerCollider.isTrigger)
                    {
                        continue;
                    }

                    lowestPlayerBottom = Mathf.Min(lowestPlayerBottom, playerCollider.bounds.min.y);
                    int hitCount = playerCollider.Overlap(filter, hits);
                    for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                    {
                        Collider2D hit = hits[hitIndex];
                        if (hit == null || hit.isTrigger)
                        {
                            continue;
                        }

                        overlapped = true;
                        highestGroundTop = Mathf.Max(highestGroundTop, hit.bounds.max.y);
                    }
                }

                if (!overlapped)
                {
                    return;
                }

                float lift = groundSeparation;
                if (!float.IsNegativeInfinity(highestGroundTop) && !float.IsPositiveInfinity(lowestPlayerBottom))
                {
                    lift = Mathf.Max(lift, highestGroundTop - lowestPlayerBottom + groundSeparation);
                }

                targetPlayer.transform.position += Vector3.up * lift;
            }
        }

        private void RespawnPlayers()
        {
            RespawnPlayer(primaryPlayer, GetRespawnOffset(primaryPlayer), primaryPlayer == player);
            RespawnPlayer(secondaryPlayer, GetRespawnOffset(secondaryPlayer), secondaryPlayer == player);
        }

        private void RespawnFallenPlayers()
        {
            RespawnIfFallen(primaryPlayer);
            RespawnIfFallen(secondaryPlayer);
        }

        private void RespawnIfFallen(PlayerController2D targetPlayer)
        {
            if (targetPlayer == null || targetPlayer.transform.position.y >= fallResetY)
            {
                return;
            }

            primaryPlayer?.GetComponent<PlayerCarryController>()?.ForceDrop();
            secondaryPlayer?.GetComponent<PlayerCarryController>()?.ForceDrop();
            RespawnPlayer(targetPlayer, GetRespawnOffset(targetPlayer), targetPlayer == player);
        }

        private void RespawnPlayer(PlayerController2D targetPlayer, Vector3 offset, bool enableControls)
        {
            if (targetPlayer == null || spawnPoint == null)
            {
                return;
            }

            targetPlayer.transform.position = spawnPoint.position + offset;
            targetPlayer.ResetMotion();
            targetPlayer.SetControlsEnabled(enableControls && stageStarted && !drawing && !cleared && !stageEditing);
            LiftPlayerOutOfGround(targetPlayer);
        }

        private Vector3 GetRespawnOffset(PlayerController2D targetPlayer)
        {
            return targetPlayer != null && targetPlayer == secondaryPlayer ? new Vector3(1.25f, 0f, 0f) : Vector3.zero;
        }

        private void SetActivePlayer(PlayerController2D nextPlayer, bool enableControls)
        {
            if (nextPlayer == null)
            {
                return;
            }

            if (player != null && player != nextPlayer)
            {
                SaveDrawingState(player);
                player.GetComponent<PlayerCarryController>()?.ForceDrop();
                player.SetControlsEnabled(false);
            }

            player = nextPlayer;
            ConfigureActivePlayerTargets();
            LoadDrawingState(player);
            player.SetControlsEnabled(enableControls && stageStarted && !drawing && !cleared && !stageEditing);
        }

        private void SetAllPlayerControls(bool enabled)
        {
            primaryPlayer?.SetControlsEnabled(enabled);
            secondaryPlayer?.SetControlsEnabled(enabled);
        }

        private void ConfigureActivePlayerTargets()
        {
            if (player == null)
            {
                return;
            }

            cameraFollow?.SetTarget(player.transform);
            drawManager?.SetBuildTarget(player.GetComponent<BodyBuilder>(), player.GetComponent<PlayerAbilityController>());
        }

        private void SaveDrawingState(PlayerController2D targetPlayer)
        {
            if (targetPlayer == null || drawManager == null)
            {
                return;
            }

            drawingStates[targetPlayer] = drawManager.CreateState();
        }

        private void LoadDrawingState(PlayerController2D targetPlayer)
        {
            if (targetPlayer == null || drawManager == null)
            {
                return;
            }

            if (!drawingStates.TryGetValue(targetPlayer, out DrawManager.DrawingState state))
            {
                drawingStates[targetPlayer] = drawManager.CreateState();
                return;
            }

            drawManager.LoadState(state, true);
        }

        private static DrawManager.DrawingState CloneDrawingState(DrawManager.DrawingState source)
        {
            DrawManager.DrawingState clone = new DrawManager.DrawingState
            {
                Species = source.Species,
                Part = source.Part
            };

            foreach (KeyValuePair<DrawManager.Species, Dictionary<DrawManager.BodyPart, List<Vector2>>> speciesPair in source.Points)
            {
                Dictionary<DrawManager.BodyPart, List<Vector2>> parts = new Dictionary<DrawManager.BodyPart, List<Vector2>>();
                foreach (KeyValuePair<DrawManager.BodyPart, List<Vector2>> partPair in speciesPair.Value)
                {
                    parts[partPair.Key] = new List<Vector2>(partPair.Value);
                }

                clone.Points[speciesPair.Key] = parts;
            }

            return clone;
        }

        private void SetCameraFollowEnabled(bool enabled)
        {
            if (cameraFollow != null)
            {
                cameraFollow.enabled = enabled;
            }
        }

    }
}
