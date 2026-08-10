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

        // ★ Added: Grace period counter for initialization
        private int m_GraceFrames = 0;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_TimeSystem = World.GetOrCreateSystemManaged<TimeSystem>();

            m_TimeDataQuery = GetEntityQuery(ComponentType.ReadOnly<TimeData>());
            m_CitizenQuery = GetEntityQuery(ComponentType.ReadWrite<Citizen>());

            RequireForUpdate(m_CitizenQuery);
            RequireForUpdate(m_TimeDataQuery);

            Mod.log.Info("[AgingSync] CitizenAgingSyncSystem initialized. Equipped with Grace Period logic.");
        }

        private void LogDebug(string message)
        {
            if (Mod.m_Setting != null && Mod.m_Setting.DebugLogging)
            {
                Mod.log.Info(message);
            }
        }

        [BurstCompile]
        private partial struct BirthdayShiftJob : IJobEntity
        {
            public short ShiftAmount;

            public void Execute(ref Citizen citizen)
            {
                citizen.m_BirthDay = (short)(citizen.m_BirthDay + ShiftAmount);
            }
        }

        protected override void OnUpdate()
        {
            if (Mod.m_Setting == null || !Mod.m_Setting.SyncCitizenAging) return;

            int daysPerMonth = Mathf.Max(Mod.m_Setting.DaysPerMonth, 1);
            if (daysPerMonth == 1) return;

            uint currentFrame = m_SimulationSystem.frameIndex;
            TimeData timeData = m_TimeDataQuery.GetSingleton<TimeData>();
            int currentDay = TimeSystem.GetDay(currentFrame, timeData);

            float yearProgress = m_TimeSystem.normalizedDate;

            double monthExact = Math.Round((double)yearProgress * 12.0, 4);
            int currentMonth = Mathf.Clamp((int)Math.Floor(monthExact) + 1, 1, 12);

            // 1. Initial Load
            if (m_LastProcessedDay == -1)
            {
                m_LastProcessedDay = currentDay;
                m_LastProcessedMonth = currentMonth;
                m_LastProcessedFrame = currentFrame;
                m_GraceFrames = 60; // Start 60-frame grace period
                LogDebug($"[AgingSync] Initialization detected. Starting grace period...");
                return;
            }

            int daysElapsed = currentDay - m_LastProcessedDay;
            long frameDelta = (long)currentFrame - (long)m_LastProcessedFrame;

            // 2. Save Load Detection
            if (daysElapsed < 0 || daysElapsed > 1 || frameDelta < 0 || frameDelta > 1000)
            {
                m_LastProcessedDay = currentDay;
                m_LastProcessedMonth = currentMonth;
                m_LastProcessedFrame = currentFrame;
                m_GraceFrames = 60; // Start 60-frame grace period
                LogDebug($"[AgingSync] Save Load detected (DayDelta: {daysElapsed}, FrameDelta: {frameDelta}). Starting grace period...");
                return;
            }

            // ====================================================================
            // 3. GRACE PERIOD (approx. 1 second after load)
            // Allows time for vanilla systems and custom time mods to apply their 
            // offsets, preventing the system from locking in a temporary vanilla month.
            // ====================================================================
            if (m_GraceFrames > 0)
            {
                m_GraceFrames--;
                m_LastProcessedDay = currentDay;
                m_LastProcessedMonth = currentMonth; // Continuously update to catch the settled month
                m_LastProcessedFrame = currentFrame;

                if (m_GraceFrames == 0)
                {
                    LogDebug($"[AgingSync] Grace period ended. Tracker locked at Day {currentDay} (UI Month {currentMonth}).");
                }
                return;
            }

            // 4. Same Day Synchronization
            if (daysElapsed == 0)
            {
                m_LastProcessedFrame = currentFrame;
                return;
            }

            // 5. TRUE MIDNIGHT TRANSITION (daysElapsed == 1)
            int shiftAmount = 0;
            bool monthChanged = (currentMonth == (m_LastProcessedMonth % 12) + 1);

            if (monthChanged)
            {
                shiftAmount = Math.Max(0, daysElapsed - 1);
                LogDebug($"[AgingSync] Month transition ({m_LastProcessedMonth} -> {currentMonth}) at Day {currentDay}. Action: AGING ALLOWED.");
            }
            else
            {
                shiftAmount = daysElapsed;
                LogDebug($"[AgingSync] Same UI Month ({currentMonth}) at Day {currentDay}. Action: AGING BLOCKED (Shift +{shiftAmount}).");
            }

            // Memorize the latest state
            m_LastProcessedDay = currentDay;
            m_LastProcessedMonth = currentMonth;
            m_LastProcessedFrame = currentFrame;

            if (shiftAmount > 0)
            {
                BirthdayShiftJob shiftJob = new BirthdayShiftJob
                {
                    ShiftAmount = (short)shiftAmount
                };

                shiftJob.Run(m_CitizenQuery);
            }
        }
    }
}