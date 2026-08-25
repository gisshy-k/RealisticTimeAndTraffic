using Game;
using Game.Economy;
using Game.Simulation;
using Game.Prefabs;
using System;
using Unity.Entities;
using UnityEngine.Scripting;

namespace RealisticTimeAndTraffic.Systems
{
    // VOLATILE: Game.Economy.EconomyParameterData
    // We update this system to ensure our custom traffic reduction multiplier 
    // is applied to the economy parameters.
    [UpdateBefore(typeof(TimeSystem))]
    public partial class RTTTrafficSystem : GameSystemBase, IModCleanup
    {
        private EntityQuery m_EconomyQuery;

        private float m_VanillaTrafficReduction = -1f;
        private float m_LastAppliedTrafficReduction = -1f;

        // Wait for vanilla data to stabilize after a save load to prevent phase shifts.
        private int m_GraceFrames = 60;
        private bool m_WasModEnabled = false;

        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            m_EconomyQuery = GetEntityQuery(ComponentType.ReadWrite<EconomyParameterData>());
            RequireForUpdate(m_EconomyQuery);
        }

        [Preserve]
        protected override void OnUpdate()
        {
            // Skip processing during the initial grace period to ensure simulation stability.
            if (m_GraceFrames > 0)
            {
                m_GraceFrames--;
                return;
            }

            var setting = Mod.m_Setting;
            if (setting == null) return;

            if (m_EconomyQuery.IsEmptyIgnoreFilter) return;
            var economyData = m_EconomyQuery.GetSingleton<EconomyParameterData>();

            // Cache the original vanilla multiplier on the first valid run for the fallback mechanism.
            if (m_VanillaTrafficReduction < 0f)
            {
                m_VanillaTrafficReduction = economyData.m_TrafficReduction;
            }

            // Vanilla Fallback: Restore original values if the feature is disabled by the user.
            if (!setting.TrafficReduction)
            {
                if (m_WasModEnabled)
                {
                    RevertToVanilla(ref economyData);
                    m_WasModEnabled = false;
                    m_LastAppliedTrafficReduction = -1f;
                }
                return;
            }

            m_WasModEnabled = true;

            // Calculate the target reduction factor based on the UI slider.
            float applyRate = setting.TrafficReductionLevel;
            float factor = 1f;

            if (applyRate <= 10f)
            {
                // Linearize the slider effect against the vanilla dynamic curve.
                float rawFactor = applyRate / 10f;
                factor = rawFactor / (4f - 3f * rawFactor);
            }
            else
            {
                // Exponential increase for extreme traffic suppression (Ghost Town mode).
                float overRate = applyRate - 10f;
                factor = (float)Math.Pow(4f, overRate);
            }

            float targetTrafficReduction = m_VanillaTrafficReduction * factor;

            // Edge Trigger: Apply the new multiplier to the ECS data only if the value has changed.
            if (Math.Abs(m_LastAppliedTrafficReduction - targetTrafficReduction) > 0.0000001f)
            {
                economyData.m_TrafficReduction = targetTrafficReduction;
                m_EconomyQuery.SetSingleton(economyData);
                m_LastAppliedTrafficReduction = targetTrafficReduction;
            }
        }

        // Ensures the simulation is returned to its original state when the mod is destroyed/unloaded.
        [Preserve]
        protected override void OnDestroy()
        {
            Cleanup();
            base.OnDestroy();
        }

        public void Cleanup()
        {
            if (m_WasModEnabled && m_VanillaTrafficReduction >= 0f && EntityManager.Exists(m_EconomyQuery.GetSingletonEntity()))
            {
                var economyData = m_EconomyQuery.GetSingleton<EconomyParameterData>();
                RevertToVanilla(ref economyData);
            }
        }

        // Restores the original traffic reduction multiplier.
        private void RevertToVanilla(ref EconomyParameterData economyData)
        {
            economyData.m_TrafficReduction = m_VanillaTrafficReduction;
            m_EconomyQuery.SetSingleton(economyData);
        }
    }

    // Interface to ensure standard cleanup behavior across all mod systems.
    public interface IModCleanup
    {
        void Cleanup();
    }
}