using System.Collections.Generic;

namespace DrawBody.Prototype
{
    public sealed partial class RuntimeStageEditor
    {
        public void Undo()
        {
            if (undoStack.Count == 0)
            {
                SetStatus(LocalizationManager.T("stage_editor_status_nothing_undo"));
                return;
            }

            redoStack.Push(CreateSnapshot());
            RestoreSnapshot(undoStack.Pop());
            GameSfx.Play(SfxId.EditorUndo);
            SetStatus(LocalizationManager.T("stage_editor_status_undo_done"));
        }

        public void Redo()
        {
            if (redoStack.Count == 0)
            {
                SetStatus(LocalizationManager.T("stage_editor_status_nothing_redo"));
                return;
            }

            undoStack.Push(CreateSnapshot());
            RestoreSnapshot(redoStack.Pop());
            GameSfx.Play(SfxId.EditorRedo);
            SetStatus(LocalizationManager.T("stage_editor_status_redo_done"));
        }

        private void PushUndo()
        {
            undoStack.Push(CreateSnapshot());
            redoStack.Clear();
        }

        private EditorSnapshot CreateSnapshot()
        {
            List<StageObjectData> snapshotObjects = new List<StageObjectData>(objects.Count);
            for (int i = 0; i < objects.Count; i++)
            {
                snapshotObjects.Add(CloneData(objects[i]));
            }

            return new EditorSnapshot
            {
                Objects = snapshotObjects,
                RuleMode = stageRuleMode,
                TimeLimitSeconds = stageTimeLimitSeconds,
                CollectionTarget = stageCollectionTarget,
                RequiredCollectionCount = stageRequiredCollectionCount
            };
        }

        private void RestoreSnapshot(EditorSnapshot snapshot)
        {
            ClearRangeSelection();
            objects.Clear();
            for (int i = 0; i < snapshot.Objects.Count; i++)
            {
                objects.Add(CloneData(snapshot.Objects[i]));
            }
            stageRuleMode = snapshot.RuleMode;
            stageTimeLimitSeconds = snapshot.TimeLimitSeconds;
            stageCollectionTarget = snapshot.CollectionTarget;
            stageRequiredCollectionCount = snapshot.RequiredCollectionCount;

            selectedData = null;
            selectedObject = null;
            linkSourceData = null;
            BuildEditorObjects();
            RefreshText();
            RefreshListPanel();
        }
    }
}
