using Game;
using Unity.Entities;
using Game.Simulation;
using Game.Common;
using Game.Prefabs;
using UnityEngine;
using System;
using UnityEngine.Scripting;

namespace RealisticTimeAndTraffic.Systems
{
    // Updates simulation time data to control the flow of time and the calendar year length.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TimeSystem))]
    public partial class RTTTimeSystem : GameSystemBase, IModCleanup
    {
        private EntityQuery m_TimeDataQuery;
        private EntityQuery m_TimeSettingsQuery;
        private SimulationSystem m_SimulationSystem;

        private uint m_LastFrameIndex;
        private float m_SubFrameAccumulator;
        private bool m_Initialized;

        // Hardcoded vanilla default. Reading dynamically from the save file causes recursive multiplication issues.
        private const int VANILLA_DAYS_PER_YEAR = 12;

        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            m_TimeDataQuery = GetEntityQuery(ComponentType.ReadWrite<TimeData>());
            m_TimeSettingsQuery = GetEntityQuery(ComponentType.ReadWrite<TimeSettingsData>());
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
        }

        [Preserve]
        protected override void OnUpdate()
        {
            var setting = Mod.m_Setting;
            if (setting == null) return;

            bool isCustomTimeEnabled = setting.CustomTimeFlow;
            uint currentFrame = m_SimulationSystem.frameIndex;

            // 1. Decelerate Simulation Time (Slower Time Factor)
            if (!m_TimeDataQuery.IsEmptyIgnoreFilter)
            {
                var timeData = m_TimeDataQuery.GetSingleton<TimeData>();

                if (!m_Initialized)
                {
                    m_LastFrameIndex = currentFrame;
                    m_Initialized = true;
                }

                // Detect frame jumps (e.g., loading a save) to prevent erroneous time offsets.
                long frameDifference = (long)currentFrame - (long)m_LastFrameIndex;
                if (frameDifference < 0 || frameDifference > 256)
                {
                    m_LastFrameIndex = currentFrame;
                    frameDifference = 0;
                }

                uint deltaFrames = (uint)frameDifference;

                if (isCustomTimeEnabled && deltaFrames > 0)
                {
                    float timeFactor = Math.Max(setting.SlowerTimeFactor, 1f);
                    if (timeFactor > 1.001f)
                    {
                        // Accumulate fractional frames to cancel out elapsed time, slowing down the clock.
                        float framesToCancel = deltaFrames * (1f - (1f / timeFactor));
                        m_SubFrameAccumulator += framesToCancel;

                        uint shift = (uint)Mathf.FloorToInt(m_SubFrameAccumulator);
                        if (shift > 0)
                        {
                            timeData.m_FirstFrame += shift;
                            m_SubFrameAccumulator -= shift;
                            m_TimeDataQuery.SetSingleton(timeData);
                        }
                    }
                }
                m_LastFrameIndex = currentFrame;
            }

            // 2. Modify Calendar Length (Days Per Month)
            if (!m_TimeSettingsQuery.IsEmptyIgnoreFilter)
            {
                var timeSettings = m_TimeSettingsQuery.GetSingleton<TimeSettingsData>();

                int daysFactor = isCustomTimeEnabled ? Math.Max(setting.DaysPerMonth, 1) : 1;
                int targetDaysPerYear = Math.Max(VANILLA_DAYS_PER_YEAR * daysFactor, 1);

                // Update the length of a year only if the target value differs.
                if (timeSettings.m_DaysPerYear != targetDaysPerYear)
                {
                    timeSettings.m_DaysPerYear = targetDaysPerYear;
                    m_TimeSettingsQuery.SetSingleton(timeSettings);
                }
            }
        }

        protected override void OnDestroy()
        {
            Cleanup();
            base.OnDestroy();
        }

        public void Cleanup()
        {
            if (!m_TimeSettingsQuery.IsEmptyIgnoreFilter)
            {
                var timeSettings = m_TimeSettingsQuery.GetSingleton<TimeSettingsData>();
                if (timeSettings.m_DaysPerYear != VANILLA_DAYS_PER_YEAR)
                {
                    timeSettings.m_DaysPerYear = VANILLA_DAYS_PER_YEAR;
                    m_TimeSettingsQuery.SetSingleton(timeSettings);
                }
            }
        }
    }
}