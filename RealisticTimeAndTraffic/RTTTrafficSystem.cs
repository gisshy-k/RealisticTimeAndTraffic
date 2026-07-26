using Game;
using Game.Economy;
using Game.Prefabs;
using System;
using Unity.Entities;
using UnityEngine.Scripting;

namespace RealisticTimeAndTraffic.Systems
{
    public partial class RTTTrafficSystem : GameSystemBase
    {
        private EntityQuery m_EconomyQuery;

        private float m_VanillaTrafficReduction = -1f;
        private float m_LastAppliedTrafficReduction = -1f;

        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            m_EconomyQuery = GetEntityQuery(ComponentType.ReadWrite<EconomyParameterData>());
        }

        [Preserve]
        protected override void OnUpdate()
        {
            var setting = Mod.m_Setting;
            if (setting == null) return;

            if (!m_EconomyQuery.IsEmptyIgnoreFilter)
            {
                var economyData = m_EconomyQuery.GetSingleton<EconomyParameterData>();

                // Store the original vanilla multiplier on the first run.
                if (m_VanillaTrafficReduction < 0f)
                    m_VanillaTrafficReduction = economyData.m_TrafficReduction;

                // Get the slider value (0 to 15). If the feature is disabled, force 10 (vanilla behavior).
                float applyRate = setting.TrafficReduction ? setting.TrafficReductionLevel : 10f;
                float factor = 1f;

                if (applyRate <= 10f)
                {
                    // Values 0 to 10 (Vanilla and below):
                    // Apply the rational function f(x) = x / (4 - 3x) to linearize the UI slider's effect.
                    // This neutralizes the vanilla engine's dynamic population curve for optimal UX.
                    float rawFactor = applyRate / 10f;
                    factor = rawFactor / (4f - 3f * rawFactor);
                }
                else
                {
                    // Values 10.1 to 15 (Ghost Town mode):
                    // Apply an exponential function (4^x) to absolutely crush discretionary trips.
                    float overRate = applyRate - 10f;
                    factor = (float)Math.Pow(4f, overRate);
                }

                float targetTrafficReduction = m_VanillaTrafficReduction * factor;

                // Apply changes to the engine only if the value has actually changed.
                if (Math.Abs(m_LastAppliedTrafficReduction - targetTrafficReduction) > 0.0000001f)
                {
                    economyData.m_TrafficReduction = targetTrafficReduction;
                    m_EconomyQuery.SetSingleton(economyData);
                    m_LastAppliedTrafficReduction = targetTrafficReduction;
                }
            }
        }
    }
}