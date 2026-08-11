using Game;
using Game.Citizens;
using Game.Simulation;
using Game.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using System;

namespace RealisticTimeAndTraffic
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(AgingSystem))]
    public partial class CitizenAgingSyncSystem : GameSystemBase
    {
        private SimulationSystem m_SimulationSystem;
        private TimeSystem m_TimeSystem;
        private EntityQuery m_TimeDataQuery;
        private EntityQuery m_CitizenQuery;

        private int m_LastProcessedDay = -1;
        private int m_LastProcessedMonth = -1;
        private uint m_LastProcessedFrame = 0;

        // Grace period counter for initialization and save loading
        private int m_GraceFrames = 0;

        // Added state tracking flags to log toggle changes clearly
        private bool m_WasActive = true;
        private bool m_IsFirstRun = true;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_TimeSystem = World.GetOrCreateSystemManaged<TimeSystem>();

            m_TimeDataQuery = GetEntityQuery(ComponentType.ReadOnly<TimeData>());
            m_CitizenQuery = GetEntityQuery(ComponentType.ReadWrite<Citizen>());

            RequireForUpdate(m_CitizenQuery);
            RequireForUpdate(m_TimeDataQuery);

            Mod.log.Info("[AgingSync] CitizenAgingSyncSystem initialized. Equipped with Vanilla Float Sync logic.");
        }

        private void LogDebug(string message)
        {
            if (Mod.m_Setting != null && Mod.m_Setting.DebugLogging)
            {
                Mod.log.Info(message);
            }
        }

        [BurstCompile]
        internal partial struct BirthdayShiftJob : IJobEntity
        {
            public short ShiftAmount;

            public void Execute(ref Citizen citizen)
            {
                citizen.m_BirthDay = (short)(citizen.m_BirthDay + ShiftAmount);
            }
        }

        protected override void OnUpdate()
        {
            // Determine if the feature is currently active based on settings
            bool isActive = Mod.m_Setting != null &&
                            Mod.m_Setting.SyncCitizenAging &&
                            Mod.m_Setting.DaysPerMonth > 1;

            // Handle the case where the feature is turned OFF
            if (!isActive)
            {
                // Log only once when the state changes to OFF or on the first run
                if (m_WasActive || m_IsFirstRun)
                {
                    Mod.log.Info("[AgingSync] Feature is OFF (Vanilla Mode). Citizens will age normally.");
                    m_WasActive = false;
                    m_IsFirstRun = false;
                }
                return; // Stop processing and allow normal aging
            }

            // Handle the case where the feature is turned ON
            if (!m_WasActive || m_IsFirstRun)
            {
                // Log only once when the state changes to ON
                Mod.log.Info("[AgingSync] Feature is ON. Tracking and shifting birthdays.");
                m_WasActive = true;
                m_IsFirstRun = false;
            }

            uint currentFrame = m_SimulationSystem.frameIndex;
            TimeData timeData = m_TimeDataQuery.GetSingleton<TimeData>();
            int currentDay = TimeSystem.GetDay(currentFrame, timeData);

            // ====================================================================
            // ★ CRITICAL FIX: The Vanilla Float Sync
            // We directly read the vanilla normalizedDate (0.0 to 1.0) because it 
            // perfectly accounts for any mid-game setting changes (phase shifts).
            // By using Modulo (% 12) instead of Clamp, we elegantly handle the 
            // "Year Boundary" (1.0) wrapping perfectly to Month 1 without errors.
            // ====================================================================
            float yearProgress = m_TimeSystem.normalizedDate;
            double monthExact = Math.Round((double)yearProgress * 12.0, 4);

            // Safe modulo operation to ensure 12.0 wraps to 0, plus 1 to make it Month 1.
            int currentUIMonth = ((((int)Math.Floor(monthExact)) % 12) + 12) % 12 + 1;

            // 1. Initial Load
            if (m_LastProcessedDay == -1)
            {
                m_LastProcessedDay = currentDay;
                m_LastProcessedMonth = currentUIMonth;
                m_LastProcessedFrame = currentFrame;
                m_GraceFrames = 60; // Start 60-frame grace period
                LogDebug($"[AgingSync] Initialization detected. Starting grace period...");
                return;
            }

            int rawDayDiff = currentDay - m_LastProcessedDay;
            long frameDelta = (long)currentFrame - (long)m_LastProcessedFrame;

            // 2. Save Load Detection
            if (rawDayDiff < 0 || rawDayDiff > 1 || frameDelta < 0 || frameDelta > 1000)
            {
                m_LastProcessedDay = currentDay;
                m_LastProcessedMonth = currentUIMonth;
                m_LastProcessedFrame = currentFrame;
                m_GraceFrames = 60;
                LogDebug($"[AgingSync] Save Load detected (DayDelta: {rawDayDiff}, FrameDelta: {frameDelta}). Starting grace period...");
                return;
            }

            // 3. GRACE PERIOD (Wait for vanilla float to settle upon load)
            if (m_GraceFrames > 0)
            {
                m_GraceFrames--;
                m_LastProcessedDay = currentDay;
                m_LastProcessedMonth = currentUIMonth;
                m_LastProcessedFrame = currentFrame;

                if (m_GraceFrames == 0)
                {
                    LogDebug($"[AgingSync] Grace period ended. Tracker locked at Day {currentDay} (UI Month {currentUIMonth}).");
                }
                return;
            }

            // 4. DAY CHANGE DETECTION
            if (rawDayDiff == 0)
            {
                m_LastProcessedFrame = currentFrame;
                return;
            }

            int daysElapsed = rawDayDiff;
            int shiftAmount = 0;
            bool monthChanged = (currentUIMonth == (m_LastProcessedMonth % 12) + 1);

            if (monthChanged)
            {
                shiftAmount = Math.Max(0, daysElapsed - 1);
                LogDebug($"[AgingSync] Month transition ({m_LastProcessedMonth} -> {currentUIMonth}) at Day {currentDay}. Action: AGING ALLOWED.");
            }
            else
            {
                shiftAmount = daysElapsed;
                LogDebug($"[AgingSync] Same UI Month ({currentUIMonth}) at Day {currentDay}. Action: AGING BLOCKED (Shift +{shiftAmount}).");
            }

            // Memorize the latest state
            m_LastProcessedDay = currentDay;
            m_LastProcessedMonth = currentUIMonth;
            m_LastProcessedFrame = currentFrame;

            if (shiftAmount > 0)
            {
                new BirthdayShiftJob
                {
                    ShiftAmount = (short)shiftAmount
                }.Run(m_CitizenQuery);
            }
        }
    }
}