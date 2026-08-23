namespace DrawBody.Prototype
{
    public sealed partial class RuntimeStageEditor
    {
        public void MarkSelectedAsLinkSource()
        {
            if (selectedData == null)
            {
                SetStatus(LocalizationManager.T("stage_editor_status_select_link_source"));
                return;
            }

            if (selectedData.type == StageObjectType.BackgroundKey)
            {
                PushUndo();
                selectedData.type = StageObjectType.Key;
                selectedData.size = StageObjectFactory.CreateDefaultData(
                    StageObjectType.Key,
                    selectedData.position).size;
                RebuildSelectedObject();
            }

            if (!CanBeLinkSource(selectedData.type))
            {
                SetStatus(LocalizationManager.T("stage_editor_status_invalid_link_source"));
                return;
            }

            linkSourceData = selectedData;
            SetStatus(LocalizationManager.Format("stage_editor_status_link_source_set", GetObjectLabel(selectedData.type)));
            RefreshText();
            RefreshListPanel();
        }

        public void LinkSelectedAsTarget()
        {
            if (linkSourceData == null)
            {
                SetStatus(LocalizationManager.T("stage_editor_status_link_source_first"));
                return;
            }

            if (selectedData == null)
            {
                SetStatus(LocalizationManager.T("stage_editor_status_select_link_target"));
                return;
            }

            if (selectedData == linkSourceData)
            {
                SetStatus(LocalizationManager.T("stage_editor_status_link_different"));
                return;
            }

            if ((linkSourceData.type == StageObjectType.Key && selectedData.type != StageObjectType.Keyhole)
                || (linkSourceData.type == StageObjectType.PoseCharacterKey
                    && selectedData.type != StageObjectType.PoseCharacterKeyhole))
            {
                SetStatus(LocalizationManager.T("stage_editor_status_key_requires_keyhole"));
                return;
            }

            PushUndo();
            linkSourceData.linkTargetId = selectedData.objectId;
            linkSourceData.linkAction = linkSourceData.type == StageObjectType.Key
                || linkSourceData.type == StageObjectType.PoseCharacterKey
                ? "Unlock"
                : GetDefaultLinkAction(selectedData.type);
            if (linkSourceData.linkAction == "RevealGrowRightToLeft")
            {
                objectFactory?.FitSeparateBridges(objects);
                RebuildSelectedObject();
                RefreshBridgeConnectionVisuals();
            }
            SetStatus(linkSourceData.linkAction == "RevealGrowRightToLeft"
                ? LocalizationManager.T("stage_editor_status_linked_bridge_right")
                : LocalizationManager.Format("stage_editor_status_linked", GetObjectLabel(linkSourceData.type), GetObjectLabel(selectedData.type)));
            RefreshText();
            RefreshListPanel();
        }

        public void ToggleSelectedLinkAction()
        {
            if (selectedData == null || string.IsNullOrEmpty(selectedData.linkTargetId))
            {
                SetStatus(LocalizationManager.T("stage_editor_status_select_linked"));
                return;
            }

            if (selectedData.type == StageObjectType.Key
                || selectedData.type == StageObjectType.PoseCharacterKey
                || selectedData.linkAction == "Unlock")
            {
                SetStatus(LocalizationManager.T("stage_editor_status_unlock_action_fixed"));
                return;
            }

            if (selectedData.linkAction == "Activate")
            {
                SetStatus(LocalizationManager.T("stage_editor_status_activate_action_fixed"));
                return;
            }

            StageObjectType targetType = FindLinkedTargetType(selectedData.linkTargetId);
            PushUndo();
            if (targetType == StageObjectType.MovingPlatform || targetType == StageObjectType.MovingOneWayPlatform)
            {
                selectedData.linkAction = GetNextMovingPlatformAction(selectedData.linkAction);
            }
            else
            {
                selectedData.linkAction = selectedData.linkAction == "Hide"
                    ? GetDefaultLinkAction(targetType)
                    : "Hide";
            }
            SetStatus(LocalizationManager.Format(
                "stage_editor_status_link_action_changed",
                GetLinkActionLabel(selectedData.linkAction)));
            RefreshText();
            RefreshListPanel();
        }

        public string SelectedLinkActionLabel
        {
            get
            {
                if (selectedData == null || string.IsNullOrEmpty(selectedData.linkTargetId))
                {
                    return LocalizationManager.T("stage_editor_link_action");
                }

                return GetLinkActionLabel(selectedData.linkAction);
            }
        }

        public void ClearSelectedLink()
        {
            if (selectedData == null)
            {
                SetStatus(LocalizationManager.T("stage_editor_status_select_linked"));
                return;
            }

            PushUndo();
            selectedData.linkTargetId = string.Empty;
            selectedData.linkAction = string.Empty;
            if (linkSourceData == selectedData)
            {
                linkSourceData = null;
            }

            SetStatus(LocalizationManager.T("stage_editor_status_link_cleared"));
            RefreshText();
            RefreshListPanel();
        }

        private static bool CanBeLinkSource(StageObjectType type)
        {
            return type == StageObjectType.Button
                || type == StageObjectType.EscortFriendButton
                || type == StageObjectType.WeightButton
                || type == StageObjectType.SimultaneousButton
                || type == StageObjectType.HoldButton
                || type == StageObjectType.PressurePlate
                || type == StageObjectType.Lever
                || type == StageObjectType.ToggleSwitch
                || type == StageObjectType.TimerSwitch
                || type == StageObjectType.RedSwitch
                || type == StageObjectType.BlueSwitch
                || type == StageObjectType.GreenSwitch
                || type == StageObjectType.YellowSwitch
                || type == StageObjectType.InkScale
                || type == StageObjectType.Key
                || type == StageObjectType.Keyhole
                || type == StageObjectType.PoseCharacterKey
                || type == StageObjectType.PoseCharacterKeyhole;
        }

        private static string GetDefaultLinkAction(StageObjectType targetType)
        {
            if (targetType == StageObjectType.Dynamite
                || targetType == StageObjectType.BombDropper
                || targetType == StageObjectType.EnemyDropper
                || targetType == StageObjectType.BeamEmitter
                || targetType == StageObjectType.MissileLauncher)
            {
                return "Activate";
            }

            if (targetType == StageObjectType.MovingPlatform || targetType == StageObjectType.MovingOneWayPlatform)
            {
                return "MoveRight";
            }

            if (targetType == StageObjectType.Platform
                || targetType == StageObjectType.HalfPlatform
                || targetType == StageObjectType.Wall
                || targetType == StageObjectType.Door
                || targetType == StageObjectType.Shutter)
            {
                return "RevealGrowRightToLeft";
            }

            return "Reveal";
        }

        private StageObjectType FindLinkedTargetType(string objectId)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null && objects[i].objectId == objectId)
                {
                    return objects[i].type;
                }
            }

            return StageObjectType.Platform;
        }

        private static string GetLinkActionLabel(string action)
        {
            if (action == "Hide")
            {
                return LocalizationManager.T("stage_editor_link_mode_hide");
            }

            if (action == "Unlock")
            {
                return LocalizationManager.T("stage_editor_link_mode_unlock");
            }

            if (action == "Activate")
            {
                return LocalizationManager.T("stage_editor_link_mode_activate");
            }

            if (action == "Move" || action == "MoveRight")
            {
                return LocalizationManager.T("stage_editor_link_mode_move_right");
            }

            if (action == "MoveUp")
            {
                return LocalizationManager.T("stage_editor_link_mode_move_up");
            }

            if (action == "MoveLeft")
            {
                return LocalizationManager.T("stage_editor_link_mode_move_left");
            }

            if (action == "MoveDown")
            {
                return LocalizationManager.T("stage_editor_link_mode_move_down");
            }

            return LocalizationManager.T("stage_editor_link_mode_reveal");
        }

        private static string GetNextMovingPlatformAction(string action)
        {
            switch (action)
            {
                case "Move":
                case "MoveRight":
                    return "MoveUp";
                case "MoveUp":
                    return "MoveLeft";
                case "MoveLeft":
                    return "MoveDown";
                default:
                    return "MoveRight";
            }
        }
    }
}
