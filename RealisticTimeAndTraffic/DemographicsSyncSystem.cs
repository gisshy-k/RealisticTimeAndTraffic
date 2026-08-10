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
    [UpdateBefore(typeof(DeathCheckSystem))]
    public partial class DemographicsSyncSystem : GameSystemBase
    {
        private PrefabSystem m_PrefabSystem;
        private SimulationSystem m_SimulationSystem;
        private TimeSystem m_TimeSystem; // ★ Added: Used for 6-hour interval tracking

        private EntityQuery m_CitizenParamQuery;
        private EntityQuery m_HealthcareParamQuery;
        private EntityQuery m_TimeDataQuery;
        private EntityQuery m_AllCitizensQuery;
        private EntityQuery m_DeadCitizensQuery;

        private float m_LastDivisor = -1f;

        // Tracker variables
        private int m_LastReportDay = -1;
        private int m_LastReportSegment = -1; // ★ Added: Divides the day into 4 segments (0-3)

        private bool m_OriginalsCached = false;
        private float m_OriginalBaseBirthRate;
        private float m_OriginalAdultFemaleBonus;
        private AnimationCurve m_OriginalDeathRateCurve;
        private AnimationCurve m_OriginalLegacyDeathRateCurve;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_TimeSystem = World.GetOrCreateSystemManaged<TimeSystem>(); // ★ Added

            m_CitizenParamQuery = GetEntityQuery(ComponentType.ReadWrite<CitizenParametersData>(), ComponentType.ReadOnly<PrefabData>());
            m_HealthcareParamQuery = GetEntityQuery(ComponentType.ReadWrite<HealthcareParameterData>(), ComponentType.ReadOnly<PrefabData>());

            m_TimeDataQuery = GetEntityQuery(ComponentType.ReadOnly<TimeData>());
            m_AllCitizensQuery = GetEntityQuery(ComponentType.ReadOnly<Citizen>());
            m_DeadCitizensQuery = GetEntityQuery(ComponentType.ReadOnly<Citizen>(), ComponentType.ReadOnly<HealthProblem>());

            RequireForUpdate(m_CitizenParamQuery);
            RequireForUpdate(m_HealthcareParamQuery);

            Mod.log.Info("[DemographicsSync] System initialized. 6-Hour Tracker is ACTIVE.");
        }

        protected override void OnUpdate()
        {
            if (Mod.m_Setting == null) return;

            // 1. Execute the 6-hour report (Tracker)
            RunQuarterlyTracker();

            // 2. Calculate the divisor
            float targetDivisor = 1f;
            if (Mod.m_Setting.CustomTimeFlow)
            {
                float timeFactor = Mathf.Max(Mod.m_Setting.SlowerTimeFactor, 1f);
                int daysPerMonth = Mod.m_Setting.SyncCitizenAging ? Mathf.Max(Mod.m_Setting.DaysPerMonth, 1) : 1;
                targetDivisor = timeFactor * (float)daysPerMonth;
            }

            // 3. Cache original data
            if (!m_OriginalsCached)
            {
                if (!CacheOriginals()) return;
            }

            // 4. Enforce monitoring and overwriting
            EnforceScaledRates(targetDivisor);
        }

        private void RunQuarterlyTracker()
        {
            uint currentFrame = m_SimulationSystem.frameIndex;
            TimeData timeData = m_TimeDataQuery.GetSingleton<TimeData>();
            int currentDay = TimeSystem.GetDay(currentFrame, timeData);

            // ★ Get the time of day (0.0 - 1.0) and multiply by 4 to get segments 0, 1, 2, 3
            float timeOfDay = m_TimeSystem.normalizedTime;
            int currentSegment = Mathf.FloorToInt(timeOfDay * 4f);

            if (m_LastReportSegment == -1 || m_LastReportDay == -1)
            {
                m_LastReportDay = currentDay;
                m_LastReportSegment = currentSegment;
                return;
            }

            // When the segment (6 hours) changes or a new day starts
            if (currentSegment != m_LastReportSegment || currentDay != m_LastReportDay)
            {
                string timeLabel = currentSegment switch
                {
                    1 => "06:00",
                    2 => "12:00",
                    3 => "18:00",
                    _ => "00:00"
                };

                // Count births (Cumulative, so the value at 24:00 represents the daily total)
                NativeArray<Citizen> citizens = m_AllCitizensQuery.ToComponentDataArray<Citizen>(Allocator.TempJob);
                int bornToday = 0;
                for (int i = 0; i < citizens.Length; i++)
                {
                    if (citizens[i].m_BirthDay == m_LastReportDay)
                    {
                        bornToday++;
                    }
                }
                citizens.Dispose();

                // Count dead bodies (Real-time snapshot of uncollected bodies)
                NativeArray<HealthProblem> healths = m_DeadCitizensQuery.ToComponentDataArray<HealthProblem>(Allocator.TempJob);
                int deadCount = 0;
                for (int i = 0; i < healths.Length; i++)
                {
                    if ((healths[i].m_Flags & HealthProblemFlags.Dead) != 0)
                    {
                        deadCount++;
                    }
                }
                healths.Dispose();

                // Log output
                Mod.log.Info($"[DemographicsTracker] --- DAY {m_LastReportDay} | TIME: {timeLabel} ---");
                Mod.log.Info($"[DemographicsTracker] Babies born TODAY (Cumulative): {bornToday}");
                Mod.log.Info($"[DemographicsTracker] Uncollected bodies (Snapshot): {deadCount}");
                Mod.log.Info($"[DemographicsTracker] ---------------------------------------");

                m_LastReportSegment = currentSegment;
                m_LastReportDay = currentDay;
            }
        }

        private bool CacheOriginals()
        {
            Entity citizenEntity = m_CitizenParamQuery.GetSingletonEntity();
            var citizenPrefab = m_PrefabSystem.GetPrefab<CitizenParametersPrefab>(citizenEntity);
            Entity healthcareEntity = m_HealthcareParamQuery.GetSingletonEntity();
            var healthcarePrefab = m_PrefabSystem.GetPrefab<HealthcarePrefab>(healthcareEntity);

            if (citizenPrefab == null || healthcarePrefab == null) return false;

            m_OriginalBaseBirthRate = citizenPrefab.m_BaseBirthRate;
            m_OriginalAdultFemaleBonus = citizenPrefab.m_AdultFemaleBirthRateBonus;
            m_OriginalDeathRateCurve = CopyCurve(healthcarePrefab.m_DeathRate);
            m_OriginalLegacyDeathRateCurve = CopyCurve(healthcarePrefab.m_LegacyDeathRate);

            m_OriginalsCached = true;
            Mod.log.Info($"[DemographicsSync] Originals cached. Vanilla BaseBirth: {m_OriginalBaseBirthRate}");
            return true;
        }

        private AnimationCurve CopyCurve(AnimationCurve source)
        {
            if (source == null) return new AnimationCurve();
            AnimationCurve copy = new AnimationCurve();
            foreach (var key in source.keys) copy.AddKey(key);
            return copy;
        }

        private void EnforceScaledRates(float targetDivisor)
        {
            var citizenData = m_CitizenParamQuery.GetSingleton<CitizenParametersData>();
            float expectedBirthRate = m_OriginalBaseBirthRate / targetDivisor;

            // Check if the vanilla system (or another mod) has reverted the values
            if (Mathf.Abs(citizenData.m_BaseBirthRate - expectedBirthRate) > 0.0001f)
            {
                citizenData.m_BaseBirthRate = expectedBirthRate;
                citizenData.m_AdultFemaleBirthRateBonus = m_OriginalAdultFemaleBonus / targetDivisor;
                m_CitizenParamQuery.SetSingleton(citizenData);

                var healthcareData = m_HealthcareParamQuery.GetSingleton<HealthcareParameterData>();
                AnimationCurve scaledDeathRate = CopyCurve(m_OriginalDeathRateCurve);
                ScaleCurve(scaledDeathRate, targetDivisor);
                healthcareData.m_DeathRate = new AnimationCurve1(scaledDeathRate);

                if (m_OriginalLegacyDeathRateCurve != null && m_OriginalLegacyDeathRateCurve.length > 0)
                {
                    AnimationCurve scaledLegacy = CopyCurve(m_OriginalLegacyDeathRateCurve);
                    ScaleCurve(scaledLegacy, targetDivisor);
                    healthcareData.m_LegacyDeathRate = new AnimationCurve1(scaledLegacy);
                }
                m_HealthcareParamQuery.SetSingleton(healthcareData);

                Mod.log.Info($"[DemographicsSync] Rates FORCED! Divisor: {targetDivisor}. BaseBirthRate applied: {expectedBirthRate}");
                m_LastDivisor = targetDivisor;
            }
        }

        private void ScaleCurve(AnimationCurve curve, float divisor)
        {
            Keyframe[] keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].value = keys[i].value / divisor;
                if (keys[i].value < 0f) keys[i].value = 0f;
            }
            curve.keys = keys;
        }
    }
}