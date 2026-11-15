using System;
using System.Collections.Generic;
using NUnit.Framework;
using Outpost.Runtime.Audio;
using Outpost.Runtime.Terminal;
using Outpost.Sim;
using UnityEngine;

namespace Outpost.Tests.EditMode
{
    public class FactAndSourceIntegrityTests
    {
        private readonly List<UnityEngine.Object> _cleanup = new List<UnityEngine.Object>();

        private static readonly string[] ExpectedSourceIds =
        {
            "nasa-jpl-opportunity-recovery",
            "nasa-jpl-opportunity-hunkers",
            "nasa-ntrs-mer-dust",
            "nasa-artemis2-sun",
            "nasa-svs-radiation",
            "hassler-2014-rad",
            "iss-water-98",
            "nasa-veggie-lettuce",
            "npr-veggie-mood",
            "esa-mars-facts",
            "esa-meet-mars",
            "nasa-mars-fact-sheet",
            "space-mars-atmosphere",
            "esa-light-time",
            "sis-habitat",
            "abc-chapea",
            "usgs-jezero-dem",
            "nasa-3d-mars"
        };

        private static readonly string[] AmbientAnnouncerIds =
        {
            "f_sol",
            "f_gravity",
            "f_moons",
            "f_cold",
            "f_signal"
        };

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _cleanup.Count; i++)
            {
                if (_cleanup[i] != null)
                    UnityEngine.Object.DestroyImmediate(_cleanup[i]);
            }
            _cleanup.Clear();
        }

        private static int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            return text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        [Test]
        public void NoFactCardOrSourceUsesPlaceholderPrefix()
        {
            FactDatabase factDb = Resources.Load<FactDatabase>("FactDatabase");
            SourcesDatabase sourcesDb = Resources.Load<SourcesDatabase>("SourcesDatabase");

            Assert.IsNotNull(factDb, "Resources/FactDatabase.asset is missing.");
            Assert.IsNotNull(sourcesDb, "Resources/SourcesDatabase.asset is missing.");
            Assert.AreEqual(11, factDb.Cards.Count, "Expected all 11 FactCards in FactDatabase.");

            for (int i = 0; i < factDb.Cards.Count; i++)
            {
                FactCard card = factDb.Cards[i];
                Assert.IsNotNull(card, $"FactCard[{i}] is null.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(card.id), $"FactCard[{i}] missing id.");

                string resolvedTitle = card.ResolveTitle();
                string resolvedText = card.ResolveText();

                Assert.IsFalse(string.IsNullOrWhiteSpace(card.title), $"{card.id} raw title empty.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(card.text), $"{card.id} raw text empty.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(resolvedTitle), $"{card.id} resolved title empty.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(resolvedText), $"{card.id} resolved text empty.");

                Assert.IsFalse(card.title.StartsWith("[PLACEHOLDER]", StringComparison.OrdinalIgnoreCase), $"{card.id} title starts with [PLACEHOLDER]");
                Assert.IsFalse(card.text.StartsWith("[PLACEHOLDER]", StringComparison.OrdinalIgnoreCase), $"{card.id} text starts with [PLACEHOLDER]");
                Assert.IsFalse(resolvedTitle.StartsWith("[PLACEHOLDER]", StringComparison.OrdinalIgnoreCase), $"{card.id} resolvedTitle starts with [PLACEHOLDER]");
                Assert.IsFalse(resolvedText.StartsWith("[PLACEHOLDER]", StringComparison.OrdinalIgnoreCase), $"{card.id} resolvedText starts with [PLACEHOLDER]");

                int rawWords = CountWords(card.text);
                int locWords = CountWords(resolvedText);
                Assert.LessOrEqual(rawWords, 25, $"{card.id} raw text exceeds 25 words ({rawWords}): {card.text}");
                Assert.LessOrEqual(locWords, 25, $"{card.id} resolved text exceeds 25 words ({locWords}): {resolvedText}");
            }

            foreach (var rec in sourcesDb.Entries)
            {
                Assert.IsNotNull(rec);
                string title = rec.ResolveTitle();
                string org = rec.ResolveOrganization();
                string supports = rec.ResolveSupports();
                Assert.IsFalse(title.StartsWith("[PLACEHOLDER]", StringComparison.OrdinalIgnoreCase), $"Source {rec.Id} title has [PLACEHOLDER]");
                Assert.IsFalse(org.StartsWith("[PLACEHOLDER]", StringComparison.OrdinalIgnoreCase), $"Source {rec.Id} org has [PLACEHOLDER]");
                Assert.IsFalse(supports.StartsWith("[PLACEHOLDER]", StringComparison.OrdinalIgnoreCase), $"Source {rec.Id} supports has [PLACEHOLDER]");
            }
        }

        [Test]
        public void AllFactCardsSourcesAndAmbientAnnouncerLinksResolve()
        {
            FactDatabase factDb = Resources.Load<FactDatabase>("FactDatabase");
            SourcesDatabase sourcesDb = Resources.Load<SourcesDatabase>("SourcesDatabase");
            AnnouncerDatabase announcerDb = Resources.Load<AnnouncerDatabase>("AnnouncerDatabase");

            Assert.IsNotNull(factDb);
            Assert.IsNotNull(sourcesDb);
            Assert.IsNotNull(announcerDb);
            Assert.AreEqual(ExpectedSourceIds.Length, sourcesDb.Entries.Count, "SourcesDatabase entry count mismatch.");

            for (int i = 0; i < ExpectedSourceIds.Length; i++)
            {
                string id = ExpectedSourceIds[i];
                SourceRecord rec = sourcesDb.FindById(id);
                Assert.IsNotNull(rec, $"Missing SourceRecord '{id}' in SourcesDatabase.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(rec.UrlText), $"Source '{id}' has empty UrlText.");
                Assert.AreNotEqual(rec.TitleKey, rec.ResolveTitle(), $"Localization missing for {rec.TitleKey}");
                Assert.AreNotEqual(rec.OrganizationKey, rec.ResolveOrganization(), $"Localization missing for {rec.OrganizationKey}");
                Assert.AreNotEqual(rec.SupportsKey, rec.ResolveSupports(), $"Localization missing for {rec.SupportsKey}");
            }

            // Every EventCatalog event maps to a real FactCard, and baseline radiation card exists too.
            foreach (string evtId in EventCatalog.EventsById.Keys)
            {
                FactCard card = factDb.GetByEventId(evtId);
                Assert.IsNotNull(card, $"Event '{evtId}' has no matching FactCard in FactDatabase.");
            }
            Assert.IsNotNull(factDb.GetById("fact_radiation_baseline"), "Missing fact_radiation_baseline in FactDatabase.");

            // Every FactCard links to valid SourceRecords.
            foreach (FactCard card in factDb.Cards)
            {
                IReadOnlyList<string> ids = card.GetSourceIds();
                Assert.Greater(ids.Count, 0, $"FactCard '{card.id}' has no source IDs.");
                for (int i = 0; i < ids.Count; i++)
                {
                    Assert.IsNotNull(sourcesDb.FindById(ids[i]), $"FactCard '{card.id}' references unknown sourceId '{ids[i]}'.");
                }
            }

            // All 5 ambient announcer facts have subtitles and valid SourcesDatabase links.
            foreach (string lineId in AmbientAnnouncerIds)
            {
                AnnouncerLine line = announcerDb.FindLine(lineId);
                Assert.IsNotNull(line, $"AnnouncerDatabase missing line '{lineId}'.");
                string subtitle = AnnouncerLocalization.GetSubtitle(line.id, line.subtitleKey);
                Assert.IsFalse(string.IsNullOrWhiteSpace(subtitle), $"Missing subtitle for '{lineId}'.");
                Assert.AreNotEqual(line.subtitleKey, subtitle, $"Localization fell back to key for '{lineId}'.");

                IReadOnlyList<string> lineSources = line.GetSourceIds();
                Assert.Greater(lineSources.Count, 0, $"Ambient announcer line '{lineId}' missing sourceIds.");
                for (int i = 0; i < lineSources.Count; i++)
                {
                    Assert.IsNotNull(sourcesDb.FindById(lineSources[i]), $"Announcer line '{lineId}' references unknown sourceId '{lineSources[i]}'.");
                }
            }
        }

        [Test]
        public void TwentyHeadlessMissionsAndAnnouncerSchedulerRunWithZeroExceptions()
        {
            var cfg = new SimConfig();

            // 1. 20 seeded headless missions mixing actions, choices, and allocations
            for (int seed = 1001; seed <= 1020; seed++)
            {
                var sim = new OutpostSimulation(cfg);
                sim.StartNewMission(seed);

                while (sim.Status == MissionStatus.InProgress)
                {
                    float ls = (seed % 3 == 0) ? 0.55f : 0.90f;
                    float gh = (seed % 4 == 0) ? 0.45f : 0.75f;
                    float ht = (seed % 5 == 0) ? 0.40f : 0.65f;
                    sim.SetAllocations(ls, gh, ht, 0.20f);

                    if (sim.CurrentEvent != null && !sim.CurrentEventResolved)
                    {
                        int chosen = 0;
                        for (int c = 0; c < sim.CurrentEvent.Choices.Count; c++)
                        {
                            if (sim.CanChooseEventChoice(c))
                            {
                                chosen = c;
                                if ((seed + sim.CurrentSol) % 2 == 1 && c + 1 < sim.CurrentEvent.Choices.Count && sim.CanChooseEventChoice(c + 1))
                                    chosen = c + 1;
                                break;
                            }
                        }
                        sim.ResolveEventChoice(chosen);
                    }

                    if (sim.CanPerformAction(CrewActionType.CleanPanels))
                        sim.PerformAction(CrewActionType.CleanPanels);
                    if (sim.CanPerformAction(CrewActionType.ReinforceShielding))
                        sim.PerformAction(CrewActionType.ReinforceShielding);
                    if (sim.CanPerformAction(CrewActionType.RestMeal))
                        sim.PerformAction(CrewActionType.RestMeal);

                    sim.EndSol();
                }

                Assert.IsTrue(sim.Status == MissionStatus.Won || sim.Status == MissionStatus.Lost);
                Assert.Greater(sim.SolLog.Count, 0);
            }

            // 2. BalanceRunner check & diagnostic report
            BalanceReport careless = BalanceRunner.RunStrategy(BotStrategy.Careless, 1000, cfg);
            BalanceReport average = BalanceRunner.RunStrategy(BotStrategy.Average, 1000, cfg);
            BalanceReport optimal = BalanceRunner.RunStrategy(BotStrategy.Optimal, 1000, cfg);
            Debug.Log(BalanceRunner.RunFullReport(1000, cfg));
            Assert.Less(careless.WinRate, 0.30f);
            Assert.That(average.WinRate, Is.InRange(0.30f, 0.75f));
            Assert.Greater(optimal.WinRate, 0.80f);

            // 3. AnnouncerDatabase + AnnouncerSystem scheduler smoke test
            AnnouncerDatabase announcerDb = Resources.Load<AnnouncerDatabase>("AnnouncerDatabase");
            Assert.IsNotNull(announcerDb);

            var go = new GameObject("Test_AnnouncerSystem");
            _cleanup.Add(go);
            var sys = go.AddComponent<AnnouncerSystem>();
            sys.Initialize(announcerDb, null, () => true, () => false, () => false);

            var pickedHistory = new List<string>();
            for (int i = 0; i < 6; i++)
            {
                bool started = sys.PlayRandomNow(bypassSuppression: true);
                Assert.IsTrue(started, $"PlayRandomNow failed on iteration {i}.");
                Assert.IsFalse(string.IsNullOrEmpty(sys.LastPlayedId));
                pickedHistory.Add(sys.LastPlayedId);
                sys.StopCurrent();
            }

            // First 5 picks must avoid immediate duplicates within RecentHistoryLimit (4)
            for (int i = 1; i < Math.Min(pickedHistory.Count, 5); i++)
            {
                for (int prev = Math.Max(0, i - 4); prev < i; prev++)
                    Assert.AreNotEqual(pickedHistory[prev], pickedHistory[i], "Announcer repeated a line within recent history window.");
            }
        }

        [Test]
        public void WebGLPlatformHelper_TouchNoticeAndLocalizationKeysResolveCleanly()
        {
            Assert.IsFalse(WebGLPlatformHelper.ShouldShowTouchDeviceNotice(isMobilePlatform: false, hasTouch: false, hasKeyboard: true, hasMouse: true));
            Assert.IsFalse(WebGLPlatformHelper.ShouldShowTouchDeviceNotice(isMobilePlatform: false, hasTouch: true, hasKeyboard: true, hasMouse: true));
            Assert.IsTrue(WebGLPlatformHelper.ShouldShowTouchDeviceNotice(isMobilePlatform: false, hasTouch: true, hasKeyboard: false, hasMouse: false));
            Assert.IsTrue(WebGLPlatformHelper.ShouldShowTouchDeviceNotice(isMobilePlatform: true, hasTouch: false, hasKeyboard: true, hasMouse: true));

            string[] webglKeys =
            {
                "title.click_start",
                "title.mobile_notice",
                "pause.fullscreen",
                "pause.click_to_resume",
                "pause.pointer_lost_hint"
            };

            for (int i = 0; i < webglKeys.Length; i++)
            {
                string localized = Localization.Get(webglKeys[i]);
                Assert.IsFalse(string.IsNullOrWhiteSpace(localized));
                Assert.AreNotEqual(webglKeys[i], localized);
            }
        }

        [Test]
        public void ArabicShaper_ShapesAndPreservesTagsAndNumbers()
        {
            string input = "<b>الطاقة 42%</b>";
            string shaped = ArabicShaper.Shape(input);
            Assert.IsTrue(shaped.Contains("<b>") && shaped.Contains("</b>"), $"Rich-text tags broken: {shaped}");
            Assert.IsTrue(shaped.Contains("42%"), $"LTR numbers/percent reversed or dropped: {shaped}");
            Assert.IsTrue(ArabicShaper.ContainsArabic(shaped), "Shaped output lost Arabic glyphs.");
        }

        [Test]
        public void LocalizationAndDesktopLocalization_AllKeysHaveArabicTranslations()
        {
            foreach (string key in Localization.GetAllEnglishKeys())
            {
                Assert.IsTrue(Localization.HasArabicTranslation(key), $"Missing Arabic translation in Localization for key: {key}");
                string raw = Localization.GetRaw(key);
                Assert.IsNotNull(raw, $"Null raw translation for key: {key}");
                if (key != "sol.summary_bullets_prefix")
                    Assert.IsFalse(string.IsNullOrEmpty(raw), $"Empty raw translation for key: {key}");
            }

            foreach (string key in DesktopLocalization.GetAllDesktopKeys())
            {
                Assert.IsTrue(DesktopLocalization.HasArabicTranslation(key), $"Missing Arabic translation in DesktopLocalization for key: {key}");
            }

            Language prev = Localization.CurrentLanguage;
            try
            {
                Localization.CurrentLanguage = Language.Arabic;
                Assert.IsTrue(ArabicShaper.ContainsArabic(Localization.Get("title.language")));
                Assert.IsTrue(ArabicShaper.ContainsArabic(Localization.Get("intro.card1.line1")));
                Assert.IsTrue(ArabicShaper.ContainsArabic(DesktopLocalization.Get("desktop.leave_short")));
            }
            finally
            {
                Localization.CurrentLanguage = prev;
            }
        }

        [Test]
        public void PoiAndAnnouncer_ContainNoSourceCitationTags()
        {
            string[] poiKeys =
            {
                "poi.solar.fact", "poi.rad.fact", "poi.o2.fact", "poi.h2o.fact",
                "poi.gh.fact", "poi.airlock.fact", "poi.crew.fact", "poi.suit.fact"
            };
            Language prev = Localization.CurrentLanguage;
            try
            {
                foreach (Language lang in new[] { Language.English, Language.Arabic })
                {
                    Localization.CurrentLanguage = lang;
                    foreach (string k in poiKeys)
                    {
                        string raw = Localization.GetRaw(k);
                        Assert.IsFalse(raw.Contains("[SRC-", StringComparison.OrdinalIgnoreCase), $"{lang} POI {k} still contains citation tag: {raw}");
                    }
                }
            }
            finally
            {
                Localization.CurrentLanguage = prev;
            }
        }

        [Test]
        public void PlayerGate_MouseSensitivity_ClampsAndPersists()
        {
            bool hadPrev = PlayerPrefs.HasKey(Outpost.Runtime.Room.PlayerGate.PrefMouseSensitivity);
            float prevVal = PlayerPrefs.GetFloat(Outpost.Runtime.Room.PlayerGate.PrefMouseSensitivity, Outpost.Runtime.Room.PlayerGate.DefaultMouseSensitivity);

            var go = new GameObject("Test_PlayerGateSens");
            _cleanup.Add(go);
            var gate = go.AddComponent<Outpost.Runtime.Room.PlayerGate>();

            try
            {
                gate.SetMouseSensitivity(99f);
                Assert.AreEqual(Outpost.Runtime.Room.PlayerGate.MaxMouseSensitivity, gate.GetMouseSensitivity(), 0.001f);

                gate.SetMouseSensitivity(-5f);
                Assert.AreEqual(Outpost.Runtime.Room.PlayerGate.MinMouseSensitivity, gate.GetMouseSensitivity(), 0.001f);

                gate.SetMouseSensitivity(1.45f);
                Assert.AreEqual(1.45f, Outpost.Runtime.Room.PlayerGate.LoadSavedMouseSensitivity(), 0.001f);
            }
            finally
            {
                if (hadPrev)
                    PlayerPrefs.SetFloat(Outpost.Runtime.Room.PlayerGate.PrefMouseSensitivity, prevVal);
                else
                    PlayerPrefs.DeleteKey(Outpost.Runtime.Room.PlayerGate.PrefMouseSensitivity);
                PlayerPrefs.Save();
            }
        }
    }
}
