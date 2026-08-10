using Colossal;
using System.Collections.Generic;

namespace RealisticTimeAndTraffic
{
    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;

        public LocaleEN(Setting setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                // MOD Name
                { m_Setting.GetSettingsLocaleID(), "Realistic Time and Traffic" },
                
                // Group Names
                { m_Setting.GetOptionGroupLocaleID(Setting.kTimeGroup), "Time Settings" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kTrafficGroup), "Traffic Settings" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kDebugGroup), "Debug Mode" },

                // Time Settings
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.CustomTimeFlow)), "Custom Time Flow" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.CustomTimeFlow)), "Enable to customize time speed and days per month." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DaysPerMonth)), "Days Per Month" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.DaysPerMonth)), "Set the number of days in a month." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DateFormat)), "Date Format" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.DateFormat)), "Select the format for the custom date display in the UI." },
                { m_Setting.GetEnumValueLocaleID(Setting.DateFormatEnum.DDMMYYYY), "DD/MM/YYYY" },
                { m_Setting.GetEnumValueLocaleID(Setting.DateFormatEnum.MMDDYYYY), "MM/DD/YYYY" },
                { m_Setting.GetEnumValueLocaleID(Setting.DateFormatEnum.YYYYMMDD), "YYYY/MM/DD" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SlowerTimeFactor)), "Slower Time Factor" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.SlowerTimeFactor)), "Slows down the simulation time (e.g., 2 means half speed)." },

                // Sync Citizen Aging
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SyncCitizenAging)), "Sync Aging & Demographics" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.SyncCitizenAging)), "Enable to synchronize citizen aging and population dynamics with your custom time flow. It automatically adjusts aging per month and scales daily birth and death rates based on both the 'Days Per Month' and 'Slower Time Factor' settings to prevent population explosions or mass die-offs." },

                // Traffic Settings
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.TrafficReduction)), "Traffic Reduction" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.TrafficReduction)), "Enable to adjust the frequency of citizens leaving home for work, school, shopping, and leisure." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.TrafficReductionLevel)), "Traffic Reduction Level" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.TrafficReductionLevel)), "15 = Extreme reduction (Ghost Town). 10 = Vanilla behavior. Lower values increase trips. 5 = Crowded (⅓–½ of canceled trips allowed). 0 = Unleashed (no reduction)." },

                // Debug Mode
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DebugLogging)), "Enable Debug Logging" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.DebugLogging)), "Outputs detailed tracking logs to help troubleshoot mod behavior. Keep disabled during normal gameplay to save performance." },
            };
        }

        public void Unload() { }
    }
}