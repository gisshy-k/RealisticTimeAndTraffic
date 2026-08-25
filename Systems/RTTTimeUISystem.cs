using Game.UI;
using Game.Simulation;
using Game.SceneFlow;
using System;
using Unity.Entities;
using Game.Prefabs;
using Game.Common;
using System.Reflection;

namespace RealisticTimeAndTraffic.Systems
{
    // Injects custom JavaScript to display the modified date format in the game's UI.
    // VOLATILE: Depends on Cohtml UI view and vanilla DOM class names (e.g., 'date-time-container').
    public partial class RTTTimeUISystem : UISystemBase
    {
        private EntityQuery m_TimeSettingsQuery;
        private EntityQuery m_TimeDataQuery;
        private SimulationSystem m_SimulationSystem;
        private string m_LastDate = null;
        private MethodInfo m_ExecuteScriptMethod;
        private object m_CohtmlView;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_TimeSettingsQuery = GetEntityQuery(ComponentType.ReadOnly<TimeSettingsData>());
            m_TimeDataQuery = GetEntityQuery(ComponentType.ReadOnly<TimeData>());
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
        }

        // Uses reflection to obtain access to the Cohtml UI view.
        // VOLATILE: GameManager.instance.userInterface.view.View
        private void EnsureCohtmlView()
        {
            if (m_CohtmlView == null || m_ExecuteScriptMethod == null)
            {
                var uiView = GameManager.instance.userInterface?.view?.View;
                if (uiView != null)
                {
                    m_CohtmlView = uiView;
                    m_ExecuteScriptMethod = uiView.GetType().GetMethod("ExecuteScript", new Type[] { typeof(string) });
                }
            }
        }

        // Executes a JavaScript string in the UI engine context.
        private void ExecuteJS(string script)
        {
            EnsureCohtmlView();
            if (m_CohtmlView != null && m_ExecuteScriptMethod != null)
            {
                try
                {
                    m_ExecuteScriptMethod.Invoke(m_CohtmlView, new object[] { script });
                }
                catch (Exception) { /* Ignore exceptions to prevent simulation crashes */ }
            }
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (m_SimulationSystem == null || Mod.m_Setting == null) return;
            if (m_TimeDataQuery.IsEmptyIgnoreFilter || m_TimeSettingsQuery.IsEmptyIgnoreFilter) return;

            var setting = Mod.m_Setting;
            string newDateOutput = "";

            if (setting.CustomTimeFlow && setting.DaysPerMonth > 1)
            {
                var timeSettings = m_TimeSettingsQuery.GetSingleton<TimeSettingsData>();
                var data = m_TimeDataQuery.GetSingleton<TimeData>();

                int daysPerYear = Math.Max(timeSettings.m_DaysPerYear, 1);
                int daysPerMonth = Math.Max(setting.DaysPerMonth, 1);
                long kVanillaTicksPerDay = 262144;

                double offsetTicks = (double)data.TimeOffset * kVanillaTicksPerDay + (double)data.GetDateOffset(daysPerYear) * kVanillaTicksPerDay * daysPerYear;
                long currentTicks = (long)(m_SimulationSystem.frameIndex - data.m_FirstFrame) + (long)offsetTicks;
                long totalDays = currentTicks / kVanillaTicksPerDay;

                int year = data.m_StartingYear + (int)(totalDays / daysPerYear);
                int dayOfYear = (int)(totalDays % daysPerYear);
                if (dayOfYear < 0) dayOfYear += daysPerYear;

                int month = (dayOfYear / daysPerMonth) + 1;
                int day = (dayOfYear % daysPerMonth) + 1;
                if (month > 12) month = 12;

                switch (setting.DateFormat)
                {
                    case Setting.DateFormatEnum.DDMMYYYY: newDateOutput = $"{day:D2}/{month:D2}/{year}"; break;
                    case Setting.DateFormatEnum.MMDDYYYY: newDateOutput = $"{month:D2}/{day:D2}/{year}"; break;
                    case Setting.DateFormatEnum.YYYYMMDD: newDateOutput = $"{year}/{month:D2}/{day:D2}"; break;
                    default: newDateOutput = $"{month:D2}/{day:D2}/{year}"; break;
                }
            }

            // Inject the UI modification script only when the date string changes.
            if (newDateOutput != m_LastDate)
            {
                // VOLATILE: The document.querySelectorAll selector targets vanilla CS2 UI classes. 
                // If Colossal Order updates the UI structure, these selectors must be updated.
                string jsPayload = $@"
                    (function() {{
                        window.RTTDate = '{newDateOutput}'; 

                        if (!window.RTTEnforce) {{
                            let s = document.createElement('style');
                            s.id = 'rtt-date-style';
                            s.innerHTML = `
                                .rtt-custom-date {{
                                    width: auto !important;
                                    min-width: 120px !important;
                                    white-space: nowrap !important;
                                    flex-shrink: 0 !important;
                                    overflow: visible !important;
                                }}
                            `;
                            document.head.appendChild(s);

                            // Replaces the vanilla date display with the custom format safely.
                            window.RTTEnforce = function() {{
                                let dateStr = window.RTTDate || '';
                                let vanillaDates = document.querySelectorAll('div[class*=\'date-time-container\'] div[class*=\'date_\'], div[class*=\'time-controls_\'] div[class*=\'date_\']');
                                
                                vanillaDates.forEach(v => {{
                                    if (v.classList.contains('rtt-custom-date')) return;
                                    
                                    let p = v.parentElement;
                                    if (!p) return;
                                    
                                    let custom = p.querySelector('.rtt-custom-date');
                                    
                                    if (dateStr !== '') {{
                                        v.style.display = 'none'; 
                                        if (!custom) {{
                                            custom = document.createElement('div');
                                            custom.className = v.className + ' rtt-custom-date';
                                            v.insertAdjacentElement('afterend', custom);
                                        }}
                                        if (custom.textContent !== dateStr) {{
                                            custom.textContent = dateStr;
                                        }}
                                        custom.style.display = '';
                                    }} else {{
                                        v.style.display = ''; 
                                        if (custom) custom.style.display = 'none';
                                    }}
                                }});
                            }};

                            let observer = new MutationObserver(() => {{ window.RTTEnforce(); }});
                            observer.observe(document.body, {{ childList: true, subtree: true }});
                        }}
                        
                        window.RTTEnforce();
                    }})();
                ";

                ExecuteJS(jsPayload);
                m_LastDate = newDateOutput;
            }
        }
    }
}