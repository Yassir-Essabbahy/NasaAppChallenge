using UnityEngine;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// Placeholder-friendly ScriptableObject storing one educational NASA fact linked to an event and constant.
    /// Text should stay at or below 25 words for quick student readability in the Debrief screen.
    /// </summary>
    [CreateAssetMenu(fileName = "FactCard", menuName = "Outpost/Fact Card", order = 3)]
    public class FactCard : ScriptableObject
    {
        public string id;
        public string title;
        [TextArea(2, 4)]
        public string text;
        public string sourceId;
        public string relatedEventId;
        public string relatedConstantId;
    }
}
