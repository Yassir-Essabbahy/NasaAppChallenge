using System;
using System.Collections.Generic;
using UnityEngine;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// Holds the collection of FactCard ScriptableObjects rendered in the "What NASA Knows" debrief section.
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
                if (Cards[i] != null && string.Equals(Cards[i].relatedEventId, eventId, StringComparison.Ordinal))
                    return Cards[i];
            }
            return null;
        }

        public FactCard GetById(string cardId)
        {
            if (string.IsNullOrEmpty(cardId) || Cards == null)
                return null;

            for (int i = 0; i < Cards.Count; i++)
            {
                if (Cards[i] != null && string.Equals(Cards[i].id, cardId, StringComparison.Ordinal))
                    return Cards[i];
            }
            return null;
        }
    }
}
