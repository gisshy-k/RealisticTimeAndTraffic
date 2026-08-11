using Colossal.Collections;
using Game;
using Game.Prefabs;
using Game.Simulation;
using Game.Citizens;
using Game.Common;
using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace RealisticTimeAndTraffic
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(BirthSystem))]
    public partial class DemographicsSyncSystem : GameSystemBase
    {
        private PrefabSystem m_PrefabSystem;
        private SimulationSystem m_SimulationSystem;
        private TimeSystem m_TimeSystem;

        private EntityQuery m_CitizenParamQuery;
        private EntityQuery m_TimeDataQuery;
        private EntityQuery m_AllCitizensQuery;
        private EntityQuery m_DeadCitizensQuery;

        private float m_LastTargetDivisor = -1f;

        // Tracker variables
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

            // DeathRate (HealthcareParameterData) is completely removed for stability.
            m_CitizenParamQuery = GetEntityQuery(ComponentType.ReadWrite<CitizenParametersData>(), ComponentType.ReadOnly<PrefabData>());
            m_TimeDataQuery = GetEntityQuery(ComponentType.ReadOnly<TimeData>());
            m_AllCitizensQuery = GetEntityQuery(ComponentType.ReadOnly<Citizen>());
            m_DeadCitizensQuery = GetEntityQuery(ComponentType.ReadOnly<Citizen>(), ComponentType.ReadOnly<HealthProblem>());

            RequireForUpdate(m_CitizenParamQuery);

            Mod.log.Info("[DemographicsSync] System initialized. Scaling BirthRate ONLY based on Days.");
        }

        protected override void OnUpdate()
        {
            if (Mod.m_Setting == null) return;

            RunQuarterlyTracker();

            // ====================================================================
            // ★ Days-Only Logic with Pure Vanilla Fallback
            // If the toggle is OFF, targetDivisor becomes 1.0f (Vanilla).
            // Slower setting is intentionally ignored for demographic scaling in this release.
            // ====================================================================
            bool isSyncEnabled = Mod.m_Setting.CustomTimeFlow && Mod.m_Setting.SyncCitizenAging;
            float targetDivisor = 1f;

            if (isSyncEnabled)
            {
                int daysPerMonth = Mathf.Max(Mod.m_Setting.DaysPerMonth, 1);
                targetDivisor = (float)daysPerMonth;
            }

            if (!m_OriginalsCached)
            {
                if (!CacheOriginals()) return;
            }

            EnforceScaledRates(targetDivisor, isSyncEnabled);
        }

        private void RunQuarterlyTracker()
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

            if (currentSegment != m_LastReportSegment || currentDay != m_LastReportDay)
            {
                string timeLabel = currentSegment switch
                {
                    1 => "06:00",
                    2 => "12:00",
                    3 => "18:00",
                    _ => "00:00"
                };

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

                string toggleState = (Mod.m_Setting != null && Mod.m_Setting.CustomTimeFlow && Mod.m_Setting.SyncCitizenAging) ? "ON" : "OFF";
                Mod.log.Info($"[DemographicsTracker] --- DAY {m_LastReportDay} | TIME: {timeLabel} | AgingSync: {toggleState} ---");
                Mod.log.Info($"[DemographicsTracker] Babies born TODAY: {bornToday} | Bodies: {deadCount}");
                Mod.log.Info($"[DemographicsTracker] ---------------------------------------");

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
            Mod.log.Info($"[DemographicsSync] Originals cached. Vanilla BaseBirth: {m_OriginalBaseBirthRate}");
            return true;
        }

        private void EnforceScaledRates(float targetDivisor, bool isSyncEnabled)
        {
            // Update only if divisor has changed
            if (Mathf.Abs(m_LastTargetDivisor - targetDivisor) > 0.001f)
            {
                // =====================================================================================
                // 1. BIRTH RATE SCALING ONLY: Uses ONLY Days setting
                // =====================================================================================
                var citizenData = m_CitizenParamQuery.GetSingleton<CitizenParametersData>();
                float expectedBirthRate = 1f - Mathf.Pow(1f - m_OriginalBaseBirthRate, 1f / targetDivisor);

                citizenData.m_BaseBirthRate = expectedBirthRate;
                citizenData.m_AdultFemaleBirthRateBonus = 1f - Mathf.Pow(1f - m_OriginalAdultFemaleBonus, 1f / targetDivisor);
                m_CitizenParamQuery.SetSingleton(citizenData);

                string stateLabel = isSyncEnabled ? "ON" : "OFF (Vanilla Mode)";
                Mod.log.Info($"[DemographicsSync] Rates FORCED! Toggle: {stateLabel} | Birth Divisor (Days Only): {targetDivisor}");

                m_LastTargetDivisor = targetDivisor;
            }
        }
    }
}