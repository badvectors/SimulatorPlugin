using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using vatsys;

namespace Simulator.Plugin
{
    /// <summary>
    /// Holds vatSys's radar picture still while the simulator is paused, so a paused session looks stopped
    /// rather than looking like every aircraft in the sector simultaneously decided to hover.
    ///
    /// There is no supported way to do this: IPlugin is two void callbacks fired after RDP has already
    /// processed an update, with no way to decline one, and nothing else in the public API halts
    /// processing. So this detaches RDP's own subscription from the position feed
    /// (Network.OnlinePilotsChanged) by reflection, which stops new returns being processed at all: no
    /// fresh history dots, no groundspeed recomputed, the picture simply stays as it was at the moment of
    /// the pause. It is put back exactly as it was on resume.
    ///
    /// Cutting the feed is not sufficient on its own, because RDP derives a chain of things from it and
    /// each one has to be held in turn:
    ///
    /// Coasting. Absence of returns is radar failure as far as RDP is concerned, so cutting the feed is
    /// exactly what starts it. It's an async chain - ProcessAircraft schedules CheckForStartCoast, which
    /// awaits and calls CheckForContinueCoast, which reschedules itself - so once armed it runs with no
    /// timer of RDP's involved, and stopping RDP's UpdateTimer (which an earlier version of this did)
    /// achieves nothing whatsoever. What it tests is DateTime.UtcNow minus RadarTrack.Timestamp against
    /// COAST_START_MS, 5.3 seconds in this build, so the timestamps are held current below - which is the
    /// truthful thing to say anyway: no time is passing.
    ///
    /// Groundspeed, and the leader line drawn from it. Holding the timestamps forward while the position
    /// stays put means the speed RDP derives is distance zero over a growing interval, which reads as no
    /// valid groundspeed - the label shows ++ and the leader line disappears. So speed, heading and
    /// vertical rate are pinned too.
    ///
    /// Those are pinned from the last poll taken while the session was still running, not from the moment
    /// of the freeze. By the time a pause is noticed the simulator has been sending the same position for
    /// up to a second, RDP has already derived a groundspeed of zero from it, and freezing that would pin
    /// the very zero this is trying to avoid.
    ///
    /// One limit worth being honest about: this freezes radar processing only. FDP2 keeps running, so
    /// estimates and timers carry on against the wall clock. It is not a true pause of vatSys.
    ///
    /// The reflection is bound to private members of one build (0.4.9305), which any release may rename,
    /// so every part of this fails open. It resolves once, up front; if anything is missing, Available is
    /// false and every call here is a no-op - a vatSys that has moved on means no freeze, never a broken
    /// plugin. It never engages against the live network (see Apply), and a freeze that somehow outlives
    /// its session is released by the watchdog.
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

        private static readonly object Gate = new object();

        private static DateTime _frozenSinceUtc;

        /// <summary>
        /// How many running polls to keep. A pause is noticed up to about two seconds after it happens -
        /// one for the simulator's own push to reach the server, one for this poll - and in that window RDP
        /// has already derived a groundspeed of zero from the repeated positions. So the picture that gets
        /// pinned is the oldest of these rather than the newest: taking the last poll before the pause was
        /// noticed captures exactly the zero this exists to avoid. Four seconds back is comfortably clear
        /// of that window, and speeds don't change meaningfully over it.
        /// </summary>
        private const int HistoryPolls = 4;

        /// <summary>Running polls, oldest first - see HistoryPolls.</summary>
        private static readonly Queue<List<Held>> _history = new Queue<List<Held>>();

        /// <summary>What is currently being pinned. Taken from the oldest history entry at the moment of the freeze.</summary>
        private static List<Held> _held = new List<Held>();

        private sealed class Held
        {
            public RDP.RadarTrack Track;
            public double GroundSpeed;
            public double Heading;
            public double VerticalSpeed;
        }

        /// <summary>Set when the watchdog fires, so the freeze isn't simply reapplied on the next poll. Cleared when the session next reports itself running.</summary>
        private static bool _watchdogTripped;

        /// <summary>Whether the private members this needs were found. False means every call here does nothing.</summary>
        public static bool Available { get; }

        public static bool Frozen { get; private set; }

        static RadarFreeze()
        {
            try
            {
                const BindingFlags statics = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

                PositionFeed = typeof(Network).GetEvent("OnlinePilotsChanged", statics);

                var subscriber = typeof(RDP).GetMethod("Network_OnlinePilotsUpdated", statics);

                // A delegate over the same static method compares equal to the one RDP subscribed with,
                // which is what makes removing it possible at all. throwOnBindFailure: false - a signature
                // that no longer matches leaves this null and turns the whole thing off.
                if (PositionFeed != null && subscriber != null)
                    RdpSubscription = Delegate.CreateDelegate(PositionFeed.EventHandlerType, subscriber, false);

                Available = PositionFeed != null && RdpSubscription != null;
            }
            catch
            {
                Available = false;
            }
        }

        /// <summary>
        /// The only entry point, called on every poll. Freezes only while positively told the session is
        /// paused; every other case - running, no session, an unreachable server, a malformed answer, the
        /// live network - is a call with paused false, which releases. Uncertainty always resolves to "not
        /// frozen". Must keep being called while paused, since holding the timestamps off coasting is not
        /// something that can be done once.
        /// </summary>
        public static void Apply(bool paused)
        {
            if (!Available) return;

            if (!paused)
            {
                _watchdogTripped = false;
                Thaw();
                Remember();
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

            Hold();
        }

        private static void Freeze()
        {
            lock (Gate)
            {
                if (Frozen) return;

                try
                {
                    PositionFeed.RemoveEventHandler(null, RdpSubscription);

                    // The oldest running poll, not the newest - see HistoryPolls.
                    _held = _history.Count > 0 ? _history.Peek() : new List<Held>();

                    _frozenSinceUtc = DateTime.UtcNow;
                    Frozen = true;
                }
                catch
                {
                    // Half-applied is the one state that must not persist - put it back.
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

            Frozen = false;
        }

        /// <summary>
        /// Records what the tracks look like while the session is running, keeping the last few polls so
        /// there is a picture from before the pause to pin when one starts - see HistoryPolls. Snapshotted
        /// before iterating: RDP mutates this list from its own threads, and a frozen picture is not worth
        /// an exception on someone's scope.
        /// </summary>
        private static void Remember()
        {
            try
            {
                _history.Enqueue(RDP.RadarTracks.ToList().Select(x => new Held
                {
                    Track = x,
                    GroundSpeed = x.GroundSpeed,
                    Heading = x.Heading,
                    VerticalSpeed = x.VerticalSpeed,
                }).ToList());

                while (_history.Count > HistoryPolls) _history.Dequeue();
            }
            catch { }
        }

        /// <summary>
        /// Keeps the frozen picture standing: every track updated just now (so the coast chain never
        /// trips) and carrying the speed, heading and vertical rate it had before the pause (so the label
        /// and the leader line still have something to draw). Reapplied every poll rather than set once,
        /// because RDP recomputes these from its own timers and would otherwise walk them back.
        /// </summary>
        private static void Hold()
        {
            try
            {
                var now = DateTime.UtcNow;

                foreach (var held in _held)
                {
                    held.Track.Timestamp = now;
                    held.Track.GroundSpeed = held.GroundSpeed;
                    held.Track.Heading = held.Heading;
                    held.Track.VerticalSpeed = held.VerticalSpeed;
                }
            }
            catch { }
        }
    }
}
