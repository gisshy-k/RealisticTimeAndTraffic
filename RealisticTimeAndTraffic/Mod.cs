using Colossal.Logging;
using Colossal.IO.AssetDatabase;
using Game;
using Game.Modding;
using Game.SceneFlow;
using RealisticTimeAndTraffic.Systems;

namespace RealisticTimeAndTraffic
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger(nameof(RealisticTimeAndTraffic)).SetShowsErrorsInUI(false);
        public static Setting m_Setting;

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            // Initialize settings and register them in the options UI
            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();

            // Load previously saved settings from the local database and apply them to m_Setting.
            // Falls back to default values (new Setting) if no saved data is found.
            AssetDatabase.global.LoadSettings(nameof(RealisticTimeAndTraffic), m_Setting, new Setting(this));

            // Register localization dictionary
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));

            // Register simulation systems
            updateSystem.UpdateAt<RTTTimeSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<RTTTimeSystem>(SystemUpdatePhase.EditorSimulation);
            updateSystem.UpdateAt<RTTTrafficSystem>(SystemUpdatePhase.GameSimulation);

            // Register UI injection system
            updateSystem.UpdateAt<RTTTimeUISystem>(SystemUpdatePhase.UIUpdate);
        }

        public void OnDispose()
        {
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }
    }
}