namespace Simulator.Plugin
{
    /// <summary>
    /// What the last poll of the selected server found. Shown in the Simulator window so that "nothing is
    /// happening" can be told apart from "nothing is meant to be happening" - see SimulatorPlugin.Status.
    /// </summary>
    public enum SessionStatus
    {
        /// <summary>No server picked in Settings &gt; Simulator. The default, and silent - nothing is polled and nothing will ever freeze.</summary>
        NoServer,

        /// <summary>Connected to the real network. Nothing here engages against live traffic, by design.</summary>
        OfficialNetwork,

        /// <summary>vatSys isn't connected, so there's no Controller ID to identify a session with.</summary>
        NotConnected,

        /// <summary>The server didn't answer, or didn't answer with anything that made sense.</summary>
        Unreachable,

        /// <summary>The server has no session for this Controller ID - connected to a different sweatbox, or not permitted into one.</summary>
        NoSession,

        /// <summary>In a session, but it has no scenario loaded yet.</summary>
        NoScenario,

        /// <summary>Running normally.</summary>
        Running,

        /// <summary>Paused - the one state that freezes the radar picture.</summary>
        Paused,
    }
}
