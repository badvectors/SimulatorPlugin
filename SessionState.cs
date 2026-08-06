namespace Simulator.Plugin
{
    /// <summary>
    /// What /session answers with - mirrors the server's SessionStateDto. Declared here rather than shared:
    /// the server's contracts assembly targets a framework this plugin can't reference, and two booleans
    /// aren't worth coupling a vatSys plugin to it for.
    /// </summary>
    public class SessionState
    {
        /// <summary>Whether a scenario is loaded at all. A session with none isn't paused, it just hasn't started.</summary>
        public bool ScenarioLoaded { get; set; }

        /// <summary>Whether the simulation is running rather than paused.</summary>
        public bool Running { get; set; }
    }
}
