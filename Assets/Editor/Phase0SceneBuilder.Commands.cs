using DrawBody.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.EditorTools
{
    public static partial class Phase0SceneBuilder
    {
        private static void AddDrawCommand(GameObject buttonObject, DrawManager drawManager, DrawButtonCommand.Command command, int intValue = 0)
        {
            DrawButtonCommand buttonCommand = buttonObject.AddComponent<DrawButtonCommand>();
            AssignObject(buttonCommand, "drawManager", drawManager);
            AssignEnum(buttonCommand, "command", (int)command);
            AssignInt(buttonCommand, "intValue", intValue);
        }

        private static void AddBrushSizeSliderCommand(GameObject sliderObject, DrawManager drawManager, Text valueText)
        {
            BrushSizeSliderCommand sliderCommand = sliderObject.AddComponent<BrushSizeSliderCommand>();
            AssignObject(sliderCommand, "drawManager", drawManager);
            AssignObject(sliderCommand, "valueText", valueText);
        }

        private static void AddStageSelectCommand(GameObject buttonObject, StageManager stageManager, string stageId)
        {
            StageSelectButtonCommand command = buttonObject.AddComponent<StageSelectButtonCommand>();
            AssignObject(command, "stageManager", stageManager);
            AssignString(command, "stageId", stageId);
        }

        private static void AddStageEditCommand(GameObject buttonObject, StageManager stageManager, string stageId)
        {
            StageEditButtonCommand command = buttonObject.AddComponent<StageEditButtonCommand>();
            AssignObject(command, "stageManager", stageManager);
            AssignString(command, "stageId", stageId);
        }

        private static void AddGameplayCommand(GameObject buttonObject, StageManager stageManager, GameplayButtonCommand.Command command)
        {
            GameplayButtonCommand buttonCommand = buttonObject.AddComponent<GameplayButtonCommand>();
            AssignObject(buttonCommand, "stageManager", stageManager);
            AssignEnum(buttonCommand, "command", (int)command);
        }

        private static void AddTitleCommand(GameObject buttonObject, StageManager stageManager, TitleButtonCommand.Command command, Text statusText = null)
        {
            TitleButtonCommand buttonCommand = buttonObject.AddComponent<TitleButtonCommand>();
            AssignObject(buttonCommand, "stageManager", stageManager);
            if (statusText != null)
            {
                AssignObject(buttonCommand, "statusText", statusText);
            }

            AssignEnum(buttonCommand, "command", (int)command);
        }

        private static void AddMultiCommand(GameObject buttonObject, MultiMenuButtonCommand.Command command)
        {
            MultiMenuButtonCommand buttonCommand = buttonObject.AddComponent<MultiMenuButtonCommand>();
            AssignEnum(buttonCommand, "command", (int)command);
        }

        private static void AddRuntimeStageEditorCommand(GameObject buttonObject, StageManager stageManager, RuntimeStageEditorButtonCommand.Command command)
        {
            RuntimeStageEditorButtonCommand buttonCommand = buttonObject.AddComponent<RuntimeStageEditorButtonCommand>();
            AssignObject(buttonCommand, "stageManager", stageManager);
            AssignEnum(buttonCommand, "command", (int)command);
        }

        private static string GetStageObjectLocalizationKey(StageObjectType type)
        {
            switch (type)
            {
                case StageObjectType.Wall:
                    return "stage_object_wall";
                case StageObjectType.Spawn:
                    return "stage_object_spawn";
                case StageObjectType.Goal:
                    return "stage_object_goal";
                case StageObjectType.BalanceScale:
                    return "stage_object_balance";
                case StageObjectType.Weight:
                    return "stage_object_weight";
                default:
                    return "stage_object_platform";
            }
        }

        private static void AddPartCommands(Button[] partButtons, DrawManager drawManager)
        {
            DrawManager.BodyPart[] parts =
            {
                DrawManager.BodyPart.Head,
                DrawManager.BodyPart.Torso,
                DrawManager.BodyPart.LeftArm,
                DrawManager.BodyPart.RightArm,
                DrawManager.BodyPart.LeftLeg,
                DrawManager.BodyPart.RightLeg,
                DrawManager.BodyPart.LeftFrontLeg,
                DrawManager.BodyPart.RightFrontLeg,
                DrawManager.BodyPart.LeftBackLeg,
                DrawManager.BodyPart.RightBackLeg,
                DrawManager.BodyPart.Tail,
                DrawManager.BodyPart.LeftWing,
                DrawManager.BodyPart.RightWing,
                DrawManager.BodyPart.TailFeather,
                DrawManager.BodyPart.SlimeBody
            };

            for (int i = 0; i < partButtons.Length && i < parts.Length; i++)
            {
                PartButtonCommand command = partButtons[i].gameObject.AddComponent<PartButtonCommand>();
                AssignObject(command, "drawManager", drawManager);
                AssignEnum(command, "bodyPart", (int)parts[i]);
                AssignColor(command, "selectedColor", new Color(0.52f, 0.76f, 1f, 0.95f));
                AssignColor(command, "normalColor", new Color(0.98f, 0.94f, 0.82f, 0.95f));
            }
        }
    }
}
