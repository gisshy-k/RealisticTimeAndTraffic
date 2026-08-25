using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;

namespace RealisticTimeAndTraffic
{
    [FileLocation(nameof(RealisticTimeAndTraffic))]
    [SettingsUIGroupOrder(kTimeGroup, kTrafficGroup, kDebugGroup)]
    [SettingsUIShowGroupName(kTimeGroup, kTrafficGroup, kDebugGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";
        public const string kTimeGroup = "Time Settings";
        public const string kTrafficGroup = "Traffic Settings";
        public const string kDebugGroup = "Debug Mode";

        public Setting(IMod mod) : base(mod)
        {
            SetDefaults();
        }

        // ==========================================
        // Time Settings
        // ==========================================
        [SettingsUISection(kSection, kTimeGroup)]
        public bool CustomTimeFlow { get; set; }

        [SettingsUISection(kSection, kTimeGroup)]
        [SettingsUISlider(min = 1, max = 30, step = 1, scalarMultiplier = 1)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsCustomTimeDisabled))]
        public int DaysPerMonth { get; set; }

        public enum DateFormatEnum
        {
            DDMMYYYY,
            MMDDYYYY,
            YYYYMMDD
        }

        [SettingsUISection(kSection, kTimeGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsDateFormatDisabled))]
        public DateFormatEnum DateFormat { get; set; }

        [SettingsUISection(kSection, kTimeGroup)]
        [SettingsUISlider(min = 1f, max = 10f, step = 0.5f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsCustomTimeDisabled))]
        public float SlowerTimeFactor { get; set; }

        [SettingsUISection(kSection, kTimeGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsCustomTimeDisabled))]
        public bool SyncCitizenAging { get; set; }

        // ==========================================
        // Traffic Settings
        // ==========================================
        [SettingsUISection(kSection, kTrafficGroup)]
        public bool TrafficReduction { get; set; }

        [SettingsUISection(kSection, kTrafficGroup)]
        [SettingsUISlider(min = 0, max = 15, step = 1, scalarMultiplier = 1)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsTrafficReductionDisabled))]
        public int TrafficReductionLevel { get; set; }

        // ==========================================
        // Debug Mode
        // ==========================================
        [SettingsUISection(kSection, kDebugGroup)]
        public bool DebugLogging { get; set; }

        // ==========================================
        // UI Disabler Conditions
        // ==========================================
        public bool IsCustomTimeDisabled() => !CustomTimeFlow;

        public bool IsDateFormatDisabled() => !CustomTimeFlow || DaysPerMonth < 2;

        public bool IsTrafficReductionDisabled() => !TrafficReduction;

        // ==========================================
        // Defaults
        // ==========================================
        public override void SetDefaults()
        {
            CustomTimeFlow = false;
            DaysPerMonth = 1;
            SyncCitizenAging = false;
            DateFormat = DateFormatEnum.DDMMYYYY;
            SlowerTimeFactor = 1f;

            TrafficReduction = false;
            TrafficReductionLevel = 10;
            DebugLogging = false;
        }
    }
}