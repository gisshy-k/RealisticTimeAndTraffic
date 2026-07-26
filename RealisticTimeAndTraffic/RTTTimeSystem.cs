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

        private int m_VanillaDaysPerYear = -1;

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

                uint deltaFrames = currentFrame - m_LastFrameIndex;

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

                if (m_VanillaDaysPerYear < 0)
                    m_VanillaDaysPerYear = timeSettings.m_DaysPerYear;

                int daysFactor = isCustomTimeEnabled ? Math.Max(setting.DaysPerMonth, 1) : 1;
                int targetDaysPerYear = Math.Max(m_VanillaDaysPerYear * daysFactor, 1);

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