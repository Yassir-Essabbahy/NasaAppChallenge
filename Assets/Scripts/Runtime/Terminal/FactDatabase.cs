using System.Collections.Generic;
using UnityEngine;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// Holds the collection of FactCard ScriptableObjects rendered in the "What NASA Knows" debrief section.
    /// Swap individual FactCard assets in the Inspector or Assets/Resources/Facts/ to replace placeholder copy.
    /// </summary>
    [CreateAssetMenu(fileName = "FactDatabase", menuName = "Outpost/Fact Database", order = 4)]
    public class FactDatabase : ScriptableObject
    {
        public List<FactCard> Cards = new List<FactCard>();

        public FactCard GetByEventId(string eventId)
        {
            if (string.IsNullOrEmpty(eventId) || Cards == null)
                return null;

            for (int i = 0; i < Cards.Count; i++)
            {
                if (Cards[i] != null && Cards[i].relatedEventId == eventId)
                    return Cards[i];
            }
            return null;
        }
    }
}
