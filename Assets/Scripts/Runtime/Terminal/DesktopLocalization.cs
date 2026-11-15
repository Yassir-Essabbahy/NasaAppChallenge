using System.Collections.Generic;
using Outpost.Sim;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// Desktop OS localization helper. Checks Outpost.Sim.Localization first so all existing
    /// keys are reused verbatim, and provides Desktop-skin English &amp; Arabic fallback keys.
    /// </summary>
    public static class DesktopLocalization
    {
        public const TerminalAppId MissionControlAppId = (TerminalAppId)8;

        private static readonly Dictionary<string, string> DesktopStrings = new Dictionary<string, string>
        {
            { "desktop.mode_to_classic", "Classic View" },
            { "desktop.mode_to_desktop", "Desktop OS" },
            { "desktop.os_brand", "OUTPOST OS" },
            { "desktop.start", "OUTPOST" },
            { "desktop.start_header", "OUTPOST OS // APPS" },
            { "desktop.start_classic", "Classic Dashboard" },
            { "desktop.start_help", "Help & Glossary" },
            { "desktop.start_leave", "Leave Terminal [ESC]" },
            { "app.mission_control", "Mission Control" },
            { "dock.mission_control", "Control" },
            { "desktop.app.air_water", "Air & Water" },
            { "desktop.app.food", "Food" },
            { "desktop.app.shield", "Shield" },
            { "desktop.app.comms", "Communications" },
            { "desktop.win.minimize", "_" },
            { "desktop.win.close", "X" },
            { "desktop.rescue_in", "RESCUE IN {0} SOLS" },
            { "desktop.rescue_one", "RESCUE IN 1 SOL" },
            { "desktop.rescue_tonight", "RESCUE TONIGHT" },
            { "desktop.ap_chip", "AP {0}/{1}" },
            { "desktop.bell_tooltip_title", "Outpost Alerts" },
            { "desktop.bell_tooltip_body", "Click to inspect pending alerts or system warnings." },
            { "desktop.bell_nominal", "All alerts clear — systems nominal." },
            { "desktop.toast_alert", "INCOMING ALERT: {0}" },
            { "desktop.forecast_tip_title", "Sol Forecast" },
            { "desktop.forecast_label", "Forecast:" },
            { "desktop.first_shift_short", "FIRST SHIFT" },
            { "desktop.mission_goal_short", "MISSION GOAL" },
            { "desktop.goal_survive", "Survive until rescue." },
            { "desktop.status_nominal", "SYSTEMS NOMINAL" },
            { "desktop.status_alert", "OUTPOST ALERT" },
            { "desktop.status_critical", "CRITICAL ALERT" },
            { "desktop.view_alert", "VIEW ALERT" },
            { "desktop.eva_locked", "[EVA LOCKED]" },
            { "desktop.helios_summary", "SYSTEMS SUMMARY" },
            { "desktop.leave_short", "LEAVE" },
            { "desktop.end_sol_short", "END SOL" },
            { "desktop.resolve_event_short", "EVENT" },
            { "desktop.tasks_short", "TASKS {0}/3" },
            { "duty.title_sol", "SOL {0} SHIFT ({1}/3)" },
            { "duty.title_ready", "SOL {0} SHIFT READY" },
            { "duty.step_event_sol1", "1. Handle Dust Alert" },
            { "duty.step_event", "1. Resolve Today's Event" },
            { "duty.step_ops_sol1", "2. Tune Power or Act" },
            { "duty.step_ops", "2. Run 1 Command Action (AP)" },
            { "duty.step_term_audit", "3. Check {0}" },
            { "duty.room.sol1", "3. Medbay: Check Cmdr. Vale" },
            { "duty.room.sol2", "3. Storage: Inspect O2 Tank" },
            { "duty.room.sol3_eva", "3. Surface: Radiation Post" },
            { "duty.room.sol3_indoor", "3. Storage: Water Tank Check" },
            { "duty.room.sol4", "3. Storage: Inspect Water Tank" },
            { "duty.room.sol5_eva", "3. Surface: Check Antenna" },
            { "duty.room.sol5_indoor", "3. Desk Radio: Check Signal" },
            { "duty.room.sol6", "3. Greenhouse: Check Crops" },
            { "duty.room.sol7_eva", "3. Surface: Solar / Rover Check" },
            { "duty.room.sol7_indoor", "3. Medbay: Final Crew Check" },
            { "duty.hud_pending", "SOL {0} DUTY: {1} [Press E at station]" },
            { "duty.hud_done", "SHIFT DUTY COMPLETE — Return to Command Terminal [E]" },
            { "duty.room_done_toast", "Shift round complete! Return to Command Terminal [E] to End Sol." },
            { "duty.leave_room_toast", "Press LEAVE or [Esc] to walk out and complete: {0}" },
            { "duty.blocked_toast", "Complete today's duties ({0}/3) first: {1}" }
        };

        private static readonly Dictionary<string, string> DesktopArabic = new Dictionary<string, string>
        {
            { "desktop.mode_to_classic", "العرض الكلاسيكي" },
            { "desktop.mode_to_desktop", "سطح المكتب" },
            { "desktop.os_brand", "نظام القاعدة OS" },
            { "desktop.start", "القاعدة" },
            { "desktop.start_header", "نظام القاعدة // التطبيقات" },
            { "desktop.start_classic", "اللوحة الكلاسيكية" },
            { "desktop.start_help", "المساعدة والمعجم" },
            { "desktop.start_leave", "مغادرة المحطة [ESC]" },
            { "app.mission_control", "مركز التحكم" },
            { "dock.mission_control", "التحكم" },
            { "desktop.app.air_water", "الهواء والماء" },
            { "desktop.app.food", "الغذاء" },
            { "desktop.app.shield", "التدريع" },
            { "desktop.app.comms", "الاتصالات" },
            { "desktop.win.minimize", "_" },
            { "desktop.win.close", "X" },
            { "desktop.rescue_in", "الإنقاذ خلال {0} أيام" },
            { "desktop.rescue_one", "الإنقاذ خلال يوم واحد" },
            { "desktop.rescue_tonight", "الإنقاذ الليلة" },
            { "desktop.ap_chip", "نقاط {0}/{1}" },
            { "desktop.bell_tooltip_title", "تنبيهات القاعدة" },
            { "desktop.bell_tooltip_body", "انقر لفحص التنبيهات العاجلة أو تحذيرات الأنظمة." },
            { "desktop.bell_nominal", "لا توجد تنبيهات — الأنظمة مستقرة." },
            { "desktop.toast_alert", "تنبيه وارد: {0}" },
            { "desktop.forecast_tip_title", "توقعات اليوم" },
            { "desktop.forecast_label", "التوقع:" },
            { "desktop.first_shift_short", "المناوبة الأولى" },
            { "desktop.mission_goal_short", "هدف المهمة" },
            { "desktop.goal_survive", "اصمد حتى الإنقاذ." },
            { "desktop.status_nominal", "الأنظمة مستقرة" },
            { "desktop.status_alert", "تنبيه القاعدة" },
            { "desktop.status_critical", "تنبيه حرج" },
            { "desktop.view_alert", "عرض التنبيه" },
            { "desktop.eva_locked", "[البوابة مغلقة]" },
            { "desktop.helios_summary", "ملخص الأنظمة" },
            { "desktop.leave_short", "مغادرة" },
            { "desktop.end_sol_short", "إنهاء اليوم" },
            { "desktop.resolve_event_short", "حدث" },
            { "desktop.tasks_short", "المهام {0}/3" },
            { "duty.title_sol", "مناوبة اليوم {0} ({1}/3)" },
            { "duty.title_ready", "مناوبة اليوم {0} جاهزة" },
            { "duty.step_event_sol1", "1. تعامل مع تنبيه الغبار" },
            { "duty.step_event", "1. احسم حدث اليوم" },
            { "duty.step_ops_sol1", "2. عدّل الطاقة أو نفذ مهمة" },
            { "duty.step_ops", "2. نفذ إجراءً واحداً (نقطة عمل)" },
            { "duty.step_term_audit", "3. افحص {0}" },
            { "duty.room.sol1", "3. العيادة: اطمئن على القائد فيل" },
            { "duty.room.sol2", "3. المخزن: افحص خزان الأكسجين" },
            { "duty.room.sol3_eva", "3. السطح: محطة الإشعاع" },
            { "duty.room.sol3_indoor", "3. المخزن: افحص خزان الماء" },
            { "duty.room.sol4", "3. المخزن: فحص خزان الماء" },
            { "duty.room.sol5_eva", "3. السطح: افحص الهوائي" },
            { "duty.room.sol5_indoor", "3. راديو المكتب: افحص الإشارة" },
            { "duty.room.sol6", "3. الدفيئة: افحص المحاصيل" },
            { "duty.room.sol7_eva", "3. السطح: فحص الألواح والعربة" },
            { "duty.room.sol7_indoor", "3. العيادة: فحص الطاقم الأخير" },
            { "duty.hud_pending", "مهمة اليوم {0}: {1} [اضغط E عند المحطة]" },
            { "duty.hud_done", "اكتملت مهام المناوبة — عد لمحطة القيادة [E]" },
            { "duty.room_done_toast", "اكتملت جولة المناوبة! عد لمحطة القيادة [E] لإنهاء اليوم." },
            { "duty.leave_room_toast", "اضغط مغادرة أو [Esc] للخروج وإتمام: {0}" },
            { "duty.blocked_toast", "أكمل مهام اليوم ({0}/3) أولاً: {1}" }
        };

        /// <summary>Returns the raw unshaped string for desktop or sim keys in the active language.</summary>
        public static string GetRaw(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            string simRaw = Localization.GetRaw(key);
            if (simRaw != key)
                return simRaw;

            if (Localization.CurrentLanguage == Language.Arabic && DesktopArabic.TryGetValue(key, out string ar))
                return ar;
            if (DesktopStrings.TryGetValue(key, out string en))
                return en;

            return key;
        }

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            return Localization.ShapeDisplay(GetRaw(key));
        }

        public static string Format(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            return Localization.ShapeDisplay(string.Format(GetRaw(key), args));
        }

        public static IEnumerable<string> GetAllDesktopKeys() => DesktopStrings.Keys;
        public static bool HasArabicTranslation(string key) => !string.IsNullOrEmpty(key) && DesktopArabic.ContainsKey(key);

        public static string GetAppTitle(TerminalAppId appId)
        {
            switch (appId)
            {
                case MissionControlAppId: return Get("app.mission_control");
                case TerminalAppId.PowerGrid: return Get("app.power");
                case TerminalAppId.LifeSupport: return Get("desktop.app.air_water");
                case TerminalAppId.Greenhouse: return Get("desktop.app.food");
                case TerminalAppId.Radiation: return Get("desktop.app.shield");
                case TerminalAppId.Crew: return Get("app.crew");
                case TerminalAppId.Comms: return Get("desktop.app.comms");
                case TerminalAppId.CommandersLog: return Get("dock.log");
                case TerminalAppId.Sources: return Get("app.sources");
                default: return appId.ToString();
            }
        }

        public static string GetAppShortLabel(TerminalAppId appId)
        {
            switch (appId)
            {
                case MissionControlAppId: return Get("dock.mission_control");
                case TerminalAppId.PowerGrid: return Get("dock.power");
                case TerminalAppId.LifeSupport: return Get("dock.lifesupport");
                case TerminalAppId.Greenhouse: return Get("dock.greenhouse");
                case TerminalAppId.Radiation: return Get("dock.radiation");
                case TerminalAppId.Crew: return Get("dock.crew");
                case TerminalAppId.Comms: return Get("dock.comms");
                case TerminalAppId.CommandersLog: return Get("dock.log");
                case TerminalAppId.Sources: return Get("dock.sources");
                default: return appId.ToString();
            }
        }
    }
}
