using System;
using System.Collections.Generic;
using UnityEngine;

namespace Outpost.Runtime.Terminal
{
    public enum IntroCardType
    {
        BigWord = 0,
        Stamp = 1,
        SmallLines = 2,
        RadioLine = 3,
        TitleSlam = 4,
        BlackHold = 5
    }

    public enum IntroSfxSlot
    {
        None = 0,
        BassHit = 1,
        TypeClick = 2,
        RadioCrackle = 3
    }

    [Serializable]
    public class IntroCard
    {
        public string id;
        public IntroCardType type;
        public string[] textLineKeys;
        public int accentWordIndex = -1;
        public float holdTime = 2.0f;
        public float fadeInTime = 0.25f;
        public float fadeOutTime = 0.15f;
        public bool hardCutExit;
        public IntroSfxSlot sfxSlot = IntroSfxSlot.None;
        public AudioClip customSfx;
        public float uiShakeIntensity;
    }

    /// <summary>
    /// Data-driven card sequence for the opening cinematic intro.
    /// Edit card order, timings, accent words, SFX slots, and shake intensity in Assets/Resources/IntroSequenceData.asset.
    /// </summary>
    [CreateAssetMenu(fileName = "IntroSequenceData", menuName = "Outpost/Intro Sequence Data", order = 6)]
    public class IntroSequenceData : ScriptableObject
    {
        public List<IntroCard> Cards = new List<IntroCard>();

        public void PopulateDefaultCards()
        {
            Cards.Clear();
            // Single opening title card: "THE SUN IS FAR AWAY."
            Cards.Add(new IntroCard
            {
                id = "card1_sun",
                type = IntroCardType.BigWord,
                textLineKeys = new[] { "intro.card1.line1" },
                accentWordIndex = -1,
                holdTime = 3.5f,
                fadeInTime = 1.1f,
                fadeOutTime = 0.25f,
                hardCutExit = false,
                sfxSlot = IntroSfxSlot.None,
                uiShakeIntensity = 0.4f
            });
        }
    }
}
