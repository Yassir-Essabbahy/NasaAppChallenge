using Outpost.Sim;
using UnityEngine;

namespace Outpost.Runtime.Room
{
    public enum InteractablePropType
    {
        Monitor,
        Radio,
        MedbayDoor,
        Window,
        Door,
        ReadOnlyTablet,
        AirlockCycle,
        GreenhousePlanter,
        StorageWaterTank,
        StorageOxygenTank,
        MedbayPatient,
        SurfaceSolarArray,
        SurfaceRover,
        SurfaceAntenna,
        SurfaceRadiationPost,
        SurfaceIceMarker,
        CustomFact,
        SuitRack
    }

    /// <summary>
    /// Marks a collider-bearing room/surface prop for center-screen raycast prompts and interaction.
    /// </summary>
    public class InteractableProp : MonoBehaviour
    {
        [SerializeField] private InteractablePropType propType;
        [SerializeField] private string promptLocalizationKey;
        [SerializeField] private string messageLocalizationKey;
        [SerializeField] private DoorInteractable doorTarget;
        [SerializeField] private AirlockController airlockTarget;
        [SerializeField] private string customPromptText;
        [SerializeField] private string customMessageText;
        [SerializeField] private string sourceId;

        public InteractablePropType PropType => propType;
        public DoorInteractable DoorTarget => doorTarget;
        public AirlockController AirlockTarget => airlockTarget;
        public string CustomMessageText => customMessageText;
        public string SourceId => sourceId;
        public string PromptText => !string.IsNullOrEmpty(customPromptText) ? customPromptText : Localization.Get(promptLocalizationKey);
        public string MessageText => !string.IsNullOrEmpty(customMessageText) ? customMessageText : Localization.Get(messageLocalizationKey);

        public void ConfigureCustomFact(string prompt, string message, string srcId = "", InteractablePropType type = InteractablePropType.CustomFact)
        {
            propType = type;
            customPromptText = prompt;
            customMessageText = message;
            sourceId = srcId;
        }

        public void Configure(InteractablePropType type, string promptKey, string messageKey = "")
        {
            propType = type;
            promptLocalizationKey = promptKey;
            messageLocalizationKey = messageKey;
        }

        public void ConfigureDoor(DoorInteractable door)
        {
            propType = InteractablePropType.Door;
            doorTarget = door;
        }

        public void ConfigureAirlock(AirlockController airlock)
        {
            propType = InteractablePropType.AirlockCycle;
            airlockTarget = airlock;
        }

        public string GetDynamicPrompt(GameSession session)
        {
            switch (propType)
            {
                case InteractablePropType.Door:
                    return doorTarget != null ? doorTarget.GetPromptText() : PromptText;

                case InteractablePropType.AirlockCycle:
                    return airlockTarget != null
                        ? airlockTarget.GetCyclePrompt(session?.CurrentSnapshot)
                        : PromptText;

                case InteractablePropType.SurfaceSolarArray:
                {
                    var snap = session?.CurrentSnapshot;
                    bool canClean = snap != null &&
                                    !snap.SolarFlareActive &&
                                    snap.DustStormSolsRemaining == 0 &&
                                    snap.ActionPoints > 0 &&
                                    snap.PanelDustPct > 5f;
                    return Localization.Get(canClean ? "poi.solar.clean_prompt" : "poi.solar.inspect_prompt");
                }

                case InteractablePropType.SuitRack:
                    return SuitController.Instance != null
                        ? SuitController.Instance.GetRackPromptText()
                        : Localization.Get("suit_put_on");

                default:
                    return PromptText;
            }
        }
    }
}
