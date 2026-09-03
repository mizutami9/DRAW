using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    public enum StageValidationSeverity
    {
        Warning,
        Error
    }

    public readonly struct StageValidationIssue
    {
        public readonly StageValidationSeverity Severity;
        public readonly string ObjectId;
        public readonly string Message;

        public StageValidationIssue(StageValidationSeverity severity, string objectId, string message)
        {
            Severity = severity;
            ObjectId = objectId;
            Message = message;
        }

        public override string ToString()
        {
            string owner = string.IsNullOrEmpty(ObjectId) ? "stage" : ObjectId;
            return $"[{Severity}] {owner}: {Message}";
        }
    }

    /// <summary>Pure validation shared by the editor menu and future automated tests.</summary>
    public static class StageDataValidator
    {
        public static List<StageValidationIssue> Validate(StageData stage, string expectedId = null)
        {
            List<StageValidationIssue> issues = new List<StageValidationIssue>();
            if (stage == null)
            {
                issues.Add(new StageValidationIssue(StageValidationSeverity.Error, null, "Stage JSON could not be parsed."));
                return issues;
            }

            if (string.IsNullOrWhiteSpace(stage.id))
                issues.Add(new StageValidationIssue(StageValidationSeverity.Error, null, "Stage ID is empty."));
            else if (!string.IsNullOrEmpty(expectedId) && !string.Equals(stage.id, expectedId, StringComparison.Ordinal))
                issues.Add(new StageValidationIssue(StageValidationSeverity.Error, null, $"Stage ID '{stage.id}' does not match file name '{expectedId}'."));

            if (stage.objects == null)
            {
                issues.Add(new StageValidationIssue(StageValidationSeverity.Error, null, "Object array is null."));
                return issues;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> linkedTargets = new HashSet<string>(StringComparer.Ordinal);
            int spawnCount = 0;
            for (int i = 0; i < stage.objects.Length; i++)
            {
                StageObjectData obj = stage.objects[i];
                if (obj == null)
                {
                    issues.Add(new StageValidationIssue(StageValidationSeverity.Error, null, $"Object at index {i} is null."));
                    continue;
                }

                string id = obj.objectId;
                if (string.IsNullOrWhiteSpace(id))
                    issues.Add(new StageValidationIssue(StageValidationSeverity.Error, null, $"Object at index {i} has no objectId."));
                else if (!ids.Add(id))
                    issues.Add(new StageValidationIssue(StageValidationSeverity.Error, id, "Duplicate objectId."));

                if (!IsFinite(obj.position.x) || !IsFinite(obj.position.y))
                    issues.Add(new StageValidationIssue(StageValidationSeverity.Error, id, "Position is not finite."));
                if (!IsFinite(obj.size.x) || !IsFinite(obj.size.y) || obj.size.x <= 0f || obj.size.y <= 0f)
                    issues.Add(new StageValidationIssue(StageValidationSeverity.Error, id, "Size must be finite and greater than zero."));
                if (!IsFinite(obj.rotation) || !IsFinite(obj.actionStrength) || !IsFinite(obj.movementSpeed))
                    issues.Add(new StageValidationIssue(StageValidationSeverity.Error, id, "A numeric setting is not finite."));

                if (obj.type == StageObjectType.Spawn) spawnCount++;
                if (!string.IsNullOrWhiteSpace(obj.linkTargetId)) linkedTargets.Add(obj.linkTargetId);
                ValidateRectParts(obj, issues);
            }

            foreach (string target in linkedTargets)
            {
                if (!ids.Contains(target))
                    issues.Add(new StageValidationIssue(StageValidationSeverity.Error, target, "Link target does not exist."));
            }

            if (spawnCount == 0 && stage.id != "1-0")
                issues.Add(new StageValidationIssue(StageValidationSeverity.Warning, null, "No Spawn object is defined."));
            if (stage.ruleMode != StageRuleMode.Normal && stage.timeLimitSeconds <= 0f)
                issues.Add(new StageValidationIssue(StageValidationSeverity.Error, null, "Timed stage must have a positive time limit."));
            if (stage.ruleMode == StageRuleMode.TimedCollection && stage.requiredCollectionCount < 0)
                issues.Add(new StageValidationIssue(StageValidationSeverity.Error, null, "Collection target must be greater than zero."));

            return issues;
        }

        private static void ValidateRectParts(StageObjectData obj, List<StageValidationIssue> issues)
        {
            if (obj.connectedRects == null) return;
            for (int i = 0; i < obj.connectedRects.Length; i++)
            {
                StageRectPartData part = obj.connectedRects[i];
                if (part == null)
                {
                    issues.Add(new StageValidationIssue(StageValidationSeverity.Error, obj.objectId, $"Connected rect {i} is null."));
                    continue;
                }
                if (!IsFinite(part.position.x) || !IsFinite(part.position.y)
                    || !IsFinite(part.size.x) || !IsFinite(part.size.y)
                    || part.size.x <= 0f || part.size.y <= 0f)
                {
                    issues.Add(new StageValidationIssue(StageValidationSeverity.Error, obj.objectId, $"Connected rect {i} has invalid geometry."));
                }
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
