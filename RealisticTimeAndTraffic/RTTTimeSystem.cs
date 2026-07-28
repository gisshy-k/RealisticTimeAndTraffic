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
    public partial class RTTTimeSystem : GameSystemBase
    {
        private EntityQuery m_TimeDataQuery;
        private EntityQuery m_TimeSettingsQuery;
        private SimulationSystem m_SimulationSystem;

        private uint m_LastFrameIndex;
        private float m_SubFrameAccumulator;
        private bool m_Initialized;

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

            // ==================================================
            // 1. Slower Time (Safely decelerates future simulation speed only)
            // ==================================================
            if (!m_TimeDataQuery.IsEmptyIgnoreFilter)
            {
                var timeData = m_TimeDataQuery.GetSingleton<TimeData>();

                if (!m_Initialized)
                {
                    m_LastFrameIndex = currentFrame;
                    m_Initialized = true;
                }

                // [BUG FIX] Safety lock to prevent time travel upon loading a save or exiting the main menu.
                // Using 'long' ensures safe detection of time rewinds (e.g., loading an older save).
                long frameDifference = (long)currentFrame - (long)m_LastFrameIndex;

                // A standard simulation runs at 60 ticks per second.
                // If the difference is negative or unusually large (e.g., > 256 frames during a load jump),
                // we reset the tracking to prevent anomalous time offsets.
                if (frameDifference < 0 || frameDifference > 256)
                {
                    m_LastFrameIndex = currentFrame;
                    frameDifference = 0; // Skip offsetting for this jumped frame
                }

                uint deltaFrames = (uint)frameDifference;

                if (isCustomTimeEnabled && deltaFrames > 0)
                {
                    float timeFactor = Math.Max(setting.SlowerTimeFactor, 1f);
                    if (timeFactor > 1.001f)
                    {
                        // Calculate the number of frames to offset based on the elapsed difference
                        float framesToCancel = deltaFrames * (1f - (1f / timeFactor));
                        m_SubFrameAccumulator += framesToCancel;

                        uint shift = (uint)Mathf.FloorToInt(m_SubFrameAccumulator);
                        if (shift > 0)
                        {
                            // Offset elapsed time without shifting the absolute reference point (Safest approach)
                            timeData.m_FirstFrame += shift;
                            m_SubFrameAccumulator -= shift;

                            m_TimeDataQuery.SetSingleton(timeData);
                        }
                    }
                }
                m_LastFrameIndex = currentFrame;
            }

            // ==================================================
            // 2. Days Per Month (Maintains absolute time while modifying calendar definitions)
            // ==================================================
            if (!m_TimeSettingsQuery.IsEmptyIgnoreFilter)
            {
                var timeSettings = m_TimeSettingsQuery.GetSingleton<TimeSettingsData>();

                // [BUG FIX] Hardcoded vanilla days per year (12) instead of reading from save data.
                // Since C:S2 saves modified TimeSettingsData directly into the save file,
                // reading it dynamically would cause an infinite multiplier loop upon repeated loads.
                // Forcing this absolute value ensures self-healing of corrupted save files.
                int vanillaDaysPerYear = 12;

                int daysFactor = isCustomTimeEnabled ? Math.Max(setting.DaysPerMonth, 1) : 1;
                int targetDaysPerYear = Math.Max(vanillaDaysPerYear * daysFactor, 1);

                // Overrides the length of a year. 
                // This updates the UI year while perfectly preserving "absolute time" (e.g., citizen age, statistics).
                if (timeSettings.m_DaysPerYear != targetDaysPerYear)
                {
                    timeSettings.m_DaysPerYear = targetDaysPerYear;
                    m_TimeSettingsQuery.SetSingleton(timeSettings);
                }
            }
        }
    }
}