using Outpost.Sim;
using UnityEngine;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// Persists the player's chosen UI language in PlayerPrefs and restores it before any scene initializes.
    /// </summary>
    public static class LanguageBootLoader
    {
        public const string PrefLanguage = "Outpost_Language";
        public const string PrefLanguageSelected = "Outpost_LanguageSelected";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeSavedLanguage()
        {
            LoadSavedLanguage();
        }

        public static void LoadSavedLanguage()
        {
            int saved = PlayerPrefs.GetInt(PrefLanguage, 0);
            Localization.CurrentLanguage = saved == 1 ? Language.Arabic : Language.English;
        }

        public static void SetAndSaveLanguage(Language lang, bool markSelected = false)
        {
            int code = lang == Language.Arabic ? 1 : 0;
            PlayerPrefs.SetInt(PrefLanguage, code);
            if (markSelected)
                PlayerPrefs.SetInt(PrefLanguageSelected, 1);
            PlayerPrefs.Save();
            Localization.SetLanguage(lang);
        }

        public static bool HasSelectedLanguageBefore()
        {
            return PlayerPrefs.GetInt(PrefLanguageSelected, 0) == 1;
        }
    }
}
