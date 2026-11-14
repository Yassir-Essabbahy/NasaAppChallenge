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
            Cards.Add(new IntroCard
            {
                id = "card1_sun",
                type = IntroCardType.BigWord,
                textLineKeys = new[] { "intro.card1.line1" },
                accentWordIndex = -1,
                holdTime = 3.5f,
                fadeInTime = 1.1f,
                fadeOutTime = 0.15f,
                hardCutExit = false,
                sfxSlot = IntroSfxSlot.None,
                uiShakeIntensity = 0.4f
            });
            Cards.Add(new IntroCard
            {
                id = "card2_stamp",
                type = IntroCardType.Stamp,
                textLineKeys = new[] { "intro.card2.line1", "intro.card2.line2", "intro.card2.line3" },
                accentWordIndex = -1,
                holdTime = 2.0f,
                fadeInTime = 0.15f,
                fadeOutTime = 0.15f,
                hardCutExit = false,
                sfxSlot = IntroSfxSlot.TypeClick,
                uiShakeIntensity = 0f
            });
            Cards.Add(new IntroCard
            {
                id = "card3_survivors",
                type = IntroCardType.BigWord,
                textLineKeys = new[] { "intro.card3.line1" },
                accentWordIndex = -1,
                holdTime = 1.6f,
                fadeInTime = 0.12f,
                fadeOutTime = 0f,
                hardCutExit = true,
                sfxSlot = IntroSfxSlot.BassHit,
                uiShakeIntensity = 3.5f
            });
            Cards.Add(new IntroCard
            {
                id = "card4_sols",
                type = IntroCardType.BigWord,
                textLineKeys = new[] { "intro.card4.line1" },
                accentWordIndex = -1,
                holdTime = 1.6f,
                fadeInTime = 0.12f,
                fadeOutTime = 0f,
                hardCutExit = true,
                sfxSlot = IntroSfxSlot.BassHit,
                uiShakeIntensity = 3.5f
            });
            Cards.Add(new IntroCard
            {
                id = "card5_commander",
                type = IntroCardType.BigWord,
                textLineKeys = new[] { "intro.card5.line1" },
                accentWordIndex = 1,
                holdTime = 1.6f,
                fadeInTime = 0.12f,
                fadeOutTime = 0f,
                hardCutExit = true,
                sfxSlot = IntroSfxSlot.BassHit,
                uiShakeIntensity = 4.5f
            });
            Cards.Add(new IntroCard
            {
                id = "card6_transfer",
                type = IntroCardType.SmallLines,
                textLineKeys = new[] { "intro.card6.line1", "intro.card6.line2" },
                accentWordIndex = -1,
                holdTime = 2.2f,
                fadeInTime = 0.15f,
                fadeOutTime = 0.15f,
                hardCutExit = false,
                sfxSlot = IntroSfxSlot.TypeClick,
                uiShakeIntensity = 0.4f
            });
            Cards.Add(new IntroCard
            {
                id = "card7_radio",
                type = IntroCardType.RadioLine,
                textLineKeys = new[] { "intro.card7.line1" },
                accentWordIndex = -1,
                holdTime = 2.6f,
                fadeInTime = 0.20f,
                fadeOutTime = 0.15f,
                hardCutExit = true,
                sfxSlot = IntroSfxSlot.RadioCrackle,
                uiShakeIntensity = 1.0f
            });
            Cards.Add(new IntroCard
            {
                id = "card8_titleslam",
                type = IntroCardType.TitleSlam,
                textLineKeys = new[] { "intro.card8.line1", "intro.card8.line2" },
                accentWordIndex = -1,
                holdTime = 2.4f,
                fadeInTime = 0.18f,
                fadeOutTime = 0f,
                hardCutExit = true,
                sfxSlot = IntroSfxSlot.BassHit,
                uiShakeIntensity = 6.0f
            });
        }
    }
}
