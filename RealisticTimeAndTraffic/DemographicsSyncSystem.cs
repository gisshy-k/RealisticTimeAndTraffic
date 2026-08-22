using Colossal.Collections;
using Game;
using Game.Citizens;
using Game.Common;
using Game.Prefabs;
using Game.Simulation;
using RealisticTimeAndTraffic.Systems;
using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace RealisticTimeAndTraffic
{
    // Executes before the vanilla BirthSystem to ensure birth rates are scaled.
    // VOLATILE: Depends on Game.Prefabs.CitizenParametersData components.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(BirthSystem))]
    public partial class DemographicsSyncSystem : GameSystemBase, IModCleanup
    {
        private PrefabSystem m_PrefabSystem;
        private SimulationSystem m_SimulationSystem;
        private TimeSystem m_TimeSystem;

        private EntityQuery m_CitizenParamQuery;
        private EntityQuery m_TimeDataQuery;
        private EntityQuery m_AllCitizensQuery;
        private EntityQuery m_DeadCitizensQuery;

        private float m_LastTargetDivisor = -1f;

        private int m_LastReportDay = -1;
        private int m_LastReportSegment = -1;

        private bool m_OriginalsCached = false;
        private float m_OriginalBaseBirthRate;
        private float m_OriginalAdultFemaleBonus;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_TimeSystem = World.GetOrCreateSystemManaged<TimeSystem>();

            m_CitizenParamQuery = GetEntityQuery(ComponentType.ReadWrite<CitizenParametersData>(), ComponentType.ReadOnly<PrefabData>());
            m_TimeDataQuery = GetEntityQuery(ComponentType.ReadOnly<TimeData>());
            m_AllCitizensQuery = GetEntityQuery(ComponentType.ReadOnly<Citizen>());
            m_DeadCitizensQuery = GetEntityQuery(ComponentType.ReadOnly<Citizen>(), ComponentType.ReadOnly<HealthProblem>());

            RequireForUpdate(m_CitizenParamQuery);
        }

        private void LogDebug(string message)
        {
            if (Mod.m_Setting != null && Mod.m_Setting.DebugLogging)
            {
                Mod.log.Info($"[Demographics] {message}");
            }
        }

        protected override void OnUpdate()
        {
            if (Mod.m_Setting == null) return;

            bool isSyncEnabled = Mod.m_Setting.CustomTimeFlow && Mod.m_Setting.SyncCitizenAging;
            float targetDivisor = 1f;

            if (isSyncEnabled)
            {
                targetDivisor = (float)Mathf.Max(Mod.m_Setting.DaysPerMonth, 1);
            }

            if (!m_OriginalsCached)
            {
                if (!CacheOriginals()) return;
            }

            EnforceScaledRates(targetDivisor, isSyncEnabled);

            if (Mod.m_Setting.DebugLogging)
            {
                RunQuarterlyTracker(targetDivisor);
            }
        }

        private void RunQuarterlyTracker(float currentDivisor)
        {
            uint currentFrame = m_SimulationSystem.frameIndex;
            TimeData timeData = m_TimeDataQuery.GetSingleton<TimeData>();
            int currentDay = TimeSystem.GetDay(currentFrame, timeData);

            float timeOfDay = m_TimeSystem.normalizedTime;
            int currentSegment = Mathf.FloorToInt(timeOfDay * 4f);

            if (m_LastReportSegment == -1 || m_LastReportDay == -1)
            {
                m_LastReportDay = currentDay;
                m_LastReportSegment = currentSegment;
                return;
            }

            // --- SEGMENT CHANGE DETECTED (Edge Trigger) ---
            if (currentSegment != m_LastReportSegment || currentDay != m_LastReportDay)
            {
                NativeArray<Citizen> citizens = m_AllCitizensQuery.ToComponentDataArray<Citizen>(Allocator.TempJob);
                int bornToday = 0;
                for (int i = 0; i < citizens.Length; i++)
                {
                    if (citizens[i].m_BirthDay == m_LastReportDay) bornToday++;
                }
                citizens.Dispose();

                NativeArray<HealthProblem> healths = m_DeadCitizensQuery.ToComponentDataArray<HealthProblem>(Allocator.TempJob);
                int deadCount = 0;
                for (int i = 0; i < healths.Length; i++)
                {
                    if ((healths[i].m_Flags & HealthProblemFlags.Dead) != 0) deadCount++;
                }
                healths.Dispose();

                string timeLabel = currentSegment switch { 1 => "06:00", 2 => "12:00", 3 => "18:00", _ => "00:00" };

                LogDebug($"Day {m_LastReportDay} ({timeLabel}) | Divisor: {currentDivisor} | Born Today: {bornToday} | Bodies: {deadCount}");

                m_LastReportSegment = currentSegment;
                m_LastReportDay = currentDay;
            }
        }

        private bool CacheOriginals()
        {
            Entity citizenEntity = m_CitizenParamQuery.GetSingletonEntity();
            var citizenPrefab = m_PrefabSystem.GetPrefab<CitizenParametersPrefab>(citizenEntity);

            if (citizenPrefab == null) return false;

            m_OriginalBaseBirthRate = citizenPrefab.m_BaseBirthRate;
            m_OriginalAdultFemaleBonus = citizenPrefab.m_AdultFemaleBirthRateBonus;
            m_OriginalsCached = true;

            LogDebug($"Original Vanilla Rates Cached. BaseBirth: {m_OriginalBaseBirthRate:F6}");
            return true;
        }

        private void EnforceScaledRates(float targetDivisor, bool isSyncEnabled)
        {
            // --- TARGET DIVISOR CHANGE DETECTED (Edge Trigger) ---
            if (Mathf.Abs(m_LastTargetDivisor - targetDivisor) > 0.001f)
            {
                var citizenData = m_CitizenParamQuery.GetSingleton<CitizenParametersData>();

                citizenData.m_BaseBirthRate = 1f - Mathf.Pow(1f - m_OriginalBaseBirthRate, 1f / targetDivisor);
                citizenData.m_AdultFemaleBirthRateBonus = 1f - Mathf.Pow(1f - m_OriginalAdultFemaleBonus, 1f / targetDivisor);

                m_CitizenParamQuery.SetSingleton(citizenData);
                m_LastTargetDivisor = targetDivisor;

                string status = isSyncEnabled ? "SCALED" : "VANILLA FALLBACK";
                LogDebug($"Status: {status} | Target Divisor: {targetDivisor} | New BaseBirthRate: {citizenData.m_BaseBirthRate:F6} (From {m_OriginalBaseBirthRate:F6})");
            }
        }

        protected override void OnDestroy()
        {
            Cleanup();
            base.OnDestroy();
        }

        public void Cleanup()
        {
            if (m_OriginalsCached && !m_CitizenParamQuery.IsEmptyIgnoreFilter)
            {
                var citizenData = m_CitizenParamQuery.GetSingleton<CitizenParametersData>();
                citizenData.m_BaseBirthRate = m_OriginalBaseBirthRate;
                citizenData.m_AdultFemaleBirthRateBonus = m_OriginalAdultFemaleBonus;
                m_CitizenParamQuery.SetSingleton(citizenData);
                LogDebug("Cleanup executed. Vanilla rates restored.");
            }
        }
    }
}