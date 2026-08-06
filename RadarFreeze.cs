using System;
using System.Collections.Generic;
using System.Reflection;
using System.Timers;
using vatsys;

namespace Simulator.Plugin
{
    /// <summary>
    /// Holds vatSys's radar processing still while the simulator is paused, so a paused session looks
    /// stopped rather than looking like every aircraft in the sector simultaneously decided to hover.
    ///
    /// There is no supported way to do this: IPlugin is two void callbacks fired after RDP has already
    /// processed an update, with no way to decline one, and nothing else in the public API halts
    /// processing. So this reaches RDP's internals by reflection - it detaches RDP's own subscription from
    /// the position feed (Network.OnlinePilotsChanged) and stops its timers, which leaves every track's
    /// state intact and is put back exactly as it was on resume. Deliberately NOT RDP.Stop(): there is a
    /// separate ClearRadarTracks, so whether Stop tears down the track list is unknown, and rebuilding
    /// every track from scratch on each un-pause would cost history dots and coupling.
    ///
    /// Two consequences worth being honest about. This freezes radar processing only - FDP2 keeps running,
    /// so estimates and timers carry on against the wall clock; it is not a true pause of vatSys. And it is
    /// bound to private members of one vatSys build (0.4.9305 at the time of writing), which may be
    /// renamed or restructured by any release.
    ///
    /// Which is why every part of this fails open. All the reflection is resolved once, up front; if any
    /// piece is missing, Available is false and every call here is a no-op. A vatSys that has moved on
    /// means no freeze, never a broken plugin. It never engages against the live network (see Apply), and
    /// a freeze that somehow outlives its session is released by the watchdog below.
    /// </summary>
    internal static class RadarFreeze
    {
        /// <summary>
        /// Longest a freeze may last before it is released regardless of what the simulator says. Nothing
        /// should ever reach this - the poll releases the moment a session stops reporting itself paused -
        /// so it exists purely so that a wedged poll can't leave a controller staring at a frozen screen.
        /// </summary>
        private static readonly TimeSpan MaxFreeze = TimeSpan.FromMinutes(10);

        private static readonly EventInfo PositionFeed;
        private static readonly Delegate RdpSubscription;

        // The fields, not the timers in them. RDP creates its timers when it starts processing, which may
        // well be after this type is first touched, and nothing says it can't replace them across a
        // reconnect - so resolve the metadata once (that much never changes) and read the current instance
        // out of it on each call.
        private static readonly FieldInfo UpdateTimerField;
        private static readonly FieldInfo AprTimerField;

        private static readonly object Gate = new object();

        private static DateTime _frozenSinceUtc;

        /// <summary>Set when the watchdog fires, so the freeze isn't simply reapplied on the next poll. Cleared when the session next reports itself running.</summary>
        private static bool _watchdogTripped;

        /// <summary>Whether every private member this needs was found. False means every call here does nothing.</summary>
        public static bool Available { get; }

        public static bool Frozen { get; private set; }

        static RadarFreeze()
        {
            try
            {
                const BindingFlags statics = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

                PositionFeed = typeof(Network).GetEvent("OnlinePilotsChanged", statics);

                var subscriber = typeof(RDP).GetMethod("Network_OnlinePilotsUpdated", statics);

                UpdateTimerField = typeof(RDP).GetField("UpdateTimer", statics);
                AprTimerField = typeof(RDP).GetField("APRTimer", statics);

                // A delegate over the same static method compares equal to the one RDP subscribed with,
                // which is what makes removing it possible at all. throwOnBindFailure: false - a signature
                // that no longer matches leaves this null and turns the whole thing off.
                if (PositionFeed != null && subscriber != null)
                    RdpSubscription = Delegate.CreateDelegate(PositionFeed.EventHandlerType, subscriber, false);

                Available = PositionFeed != null && RdpSubscription != null && UpdateTimerField != null;
            }
            catch
            {
                Available = false;
            }
        }

        /// <summary>
        /// The only entry point. Freezes only while positively told the session is paused; every other
        /// case - running, no session, an unreachable server, a malformed answer, the live network - is a
        /// call with paused false, which releases. Uncertainty always resolves to "not frozen".
        /// </summary>
        public static void Apply(bool paused)
        {
            if (!Available) return;

            if (!paused)
            {
                _watchdogTripped = false;
                Thaw();
                return;
            }

            // Never against the real network. A frozen radar on a live session is a safety issue, not an
            // inconvenience, so this is checked on every call rather than once at startup - the plugin
            // stays loaded across connections and the answer changes underneath it.
            if (Network.IsOfficialServer)
            {
                Thaw();
                return;
            }

            if (Frozen && DateTime.UtcNow - _frozenSinceUtc > MaxFreeze)
            {
                _watchdogTripped = true;
                Thaw();
                return;
            }

            // Stays released until the session is seen running again, so a tripped watchdog doesn't just
            // re-freeze a second later.
            if (_watchdogTripped) return;

            Freeze();
        }

        private static void Freeze()
        {
            lock (Gate)
            {
                if (Frozen) return;

                try
                {
                    PositionFeed.RemoveEventHandler(null, RdpSubscription);

                    Timers().ForEach(x => x.Stop());

                    _frozenSinceUtc = DateTime.UtcNow;
                    Frozen = true;
                }
                catch
                {
                    // Half-applied is the one state that must not persist - put it all back.
                    Restore();
                }
            }
        }

        private static void Thaw()
        {
            lock (Gate)
            {
                if (!Frozen) return;

                Restore();
            }
        }

        private static void Restore()
        {
            // Remove-then-add rather than add: if the remove in Freeze silently did nothing (RDP having
            // subscribed some way this can't match), adding would leave every position processed twice.
            // Removing a subscription that isn't there is a no-op, so this lands on exactly one either way.
            try
            {
                PositionFeed.RemoveEventHandler(null, RdpSubscription);
                PositionFeed.AddEventHandler(null, RdpSubscription);
            }
            catch { }

            try { Timers().ForEach(x => x.Start()); } catch { }

            Frozen = false;
        }

        /// <summary>RDP's timers as they stand right now - see the fields above for why these aren't held on to.</summary>
        private static List<Timer> Timers()
        {
            var timers = new List<Timer>();

            try
            {
                if (UpdateTimerField?.GetValue(null) is Timer update) timers.Add(update);
                if (AprTimerField?.GetValue(null) is Timer apr) timers.Add(apr);
            }
            catch { }

            return timers;
        }
    }
}
