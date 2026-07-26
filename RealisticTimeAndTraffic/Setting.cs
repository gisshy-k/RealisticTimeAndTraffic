using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;

namespace RealisticTimeAndTraffic
{
    [FileLocation(nameof(RealisticTimeAndTraffic))]
    [SettingsUISection("Time Settings", "Traffic Settings")]
    public class Setting : ModSetting
    {
        public Setting(IMod mod) : base(mod)
        {
            SetDefaults();
        }

        // ==========================================
        // Time Settings
        // ==========================================
        [SettingsUISection("Time Settings")]
        public bool CustomTimeFlow { get; set; }

        [SettingsUISection("Time Settings")]
        [SettingsUISlider(min = 1, max = 30, step = 1, scalarMultiplier = 1)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsCustomTimeDisabled))]
        public int DaysPerMonth { get; set; }

        public enum DateFormatEnum
        {
            DDMMYYYY,
            MMDDYYYY,
            YYYYMMDD
        }

        [SettingsUISection("Time Settings")]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsDateFormatDisabled))]
        public DateFormatEnum DateFormat { get; set; }

        [SettingsUISection("Time Settings")]
        [SettingsUISlider(min = 1f, max = 10f, step = 0.5f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsCustomTimeDisabled))]
        public float SlowerTimeFactor { get; set; }

        // ==========================================
        // Traffic Settings
        // ==========================================
        [SettingsUISection("Traffic Settings")]
        public bool TrafficReduction { get; set; }

        [SettingsUISection("Traffic Settings")]
        [SettingsUISlider(min = 0, max = 15, step = 1, scalarMultiplier = 1)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsTrafficReductionDisabled))]
        public int TrafficReductionLevel { get; set; }

        // ==========================================
        // UI Disabler Conditions
        // ==========================================

        // Disables the entire Time Settings section if CustomTimeFlow is unchecked.
        public bool IsCustomTimeDisabled() => !CustomTimeFlow;

        // Disables the Date Format dropdown if Time Settings are off OR if DaysPerMonth is less than 2.
        // This ensures UI consistency because the backend system falls back to vanilla behavior at 1 day/month.
        public bool IsDateFormatDisabled() => !CustomTimeFlow || DaysPerMonth < 2;

        // Disables the entire Traffic Settings section if TrafficReduction is unchecked.
        public bool IsTrafficReductionDisabled() => !TrafficReduction;

        // ==========================================
        // Defaults
        // ==========================================
        public override void SetDefaults()
        {
            CustomTimeFlow = false;
            DaysPerMonth = 1;
            DateFormat = DateFormatEnum.DDMMYYYY;
            SlowerTimeFactor = 1f;

            TrafficReduction = false;
            TrafficReductionLevel = 10;
        }
    }
}