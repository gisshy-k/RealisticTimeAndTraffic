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
    // Executes before the vanilla AgingSystem to ensure birthdays are shifted 
    // before citizens' ages are updated in the current frame.
    // VOLATILE: Depends on Game.Citizens.Citizen.m_BirthDay field.
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
        private int m_GraceFrames = 0;

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
        }

        // Wrapper to strictly respect the master debug toggle.
        private void LogDebug(string message)
        {
            if (Mod.m_Setting != null && Mod.m_Setting.DebugLogging)
            {
                Mod.log.Info($"[AgingSync] {message}");
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
            bool isActive = Mod.m_Setting != null &&
                            Mod.m_Setting.SyncCitizenAging &&
                            Mod.m_Setting.DaysPerMonth > 1;

            if (!isActive)
            {
                if (m_WasActive || m_IsFirstRun)
                {
                    LogDebug("Feature disabled. Citizens will age at the vanilla rate.");
                    m_WasActive = false;
                    m_IsFirstRun = false;
                }
                return;
            }

            if (!m_WasActive || m_IsFirstRun)
            {
                LogDebug("Feature enabled. Tracking and synchronizing birthdays.");
                m_WasActive = true;
                m_IsFirstRun = false;
            }

            uint currentFrame = m_SimulationSystem.frameIndex;
            TimeData timeData = m_TimeDataQuery.GetSingleton<TimeData>();
            int currentDay = TimeSystem.GetDay(currentFrame, timeData);

            float yearProgress = m_TimeSystem.normalizedDate;
            double monthExact = Math.Round((double)yearProgress * 12.0, 4);
            int currentUIMonth = ((((int)Math.Floor(monthExact)) % 12) + 12) % 12 + 1;

            if (m_LastProcessedDay == -1)
            {
                m_LastProcessedDay = currentDay;
                m_LastProcessedMonth = currentUIMonth;
                m_LastProcessedFrame = currentFrame;
                m_GraceFrames = 60;
                LogDebug($"Initialization detected. Grace period started (Target Day: {currentDay}).");
                return;
            }

            int rawDayDiff = currentDay - m_LastProcessedDay;
            long frameDelta = (long)currentFrame - (long)m_LastProcessedFrame;

            if (rawDayDiff < 0 || rawDayDiff > 1 || frameDelta < 0 || frameDelta > 1000)
            {
                m_LastProcessedDay = currentDay;
                m_LastProcessedMonth = currentUIMonth;
                m_LastProcessedFrame = currentFrame;
                m_GraceFrames = 60;
                LogDebug($"Save Load or Time Jump detected (DayDelta: {rawDayDiff}, FrameDelta: {frameDelta}). Grace period restarted.");
                return;
            }

            if (m_GraceFrames > 0)
            {
                m_GraceFrames--;
                m_LastProcessedDay = currentDay;
                m_LastProcessedMonth = currentUIMonth;
                m_LastProcessedFrame = currentFrame;

                if (m_GraceFrames == 0)
                {
                    LogDebug($"Grace period ended. Tracker locked at Day {currentDay} (UI Month {currentUIMonth}).");
                }
                return;
            }

            if (rawDayDiff == 0)
            {
                m_LastProcessedFrame = currentFrame;
                return;
            }

            // --- DAY CHANGE DETECTED (Edge Trigger) ---
            int daysElapsed = rawDayDiff;
            int shiftAmount = 0;
            bool monthChanged = (currentUIMonth == (m_LastProcessedMonth % 12) + 1);

            if (monthChanged)
            {
                shiftAmount = Math.Max(0, daysElapsed - 1);
                LogDebug($"Day {m_LastProcessedDay} -> {currentDay} | UI Month {m_LastProcessedMonth} -> {currentUIMonth} | Action: AGING ALLOWED (Shift +{shiftAmount})");
            }
            else
            {
                shiftAmount = daysElapsed;
                LogDebug($"Day {m_LastProcessedDay} -> {currentDay} | UI Month {currentUIMonth} (Unchanged) | Action: AGING BLOCKED (Shift +{shiftAmount})");
            }

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