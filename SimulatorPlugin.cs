using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.ComponentModel.Composition;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using vatsys;
using vatsys.Plugin;

namespace Simulator.Plugin
{
    [Export(typeof(IPlugin))]
    public class SimulatorPlugin : IPlugin
    {
        public string Name => "Simulator";

        public static HttpClient _httpClient = new HttpClient();
        public static string _server = string.Empty;
        public static bool _send = false;

        public static List<Server> Servers { get; } = LoadServers();

        /// <summary>
        /// What the last poll found, for the Simulator window to show. Every one of these except Paused
        /// and Running is a reason nothing will freeze, and the point of surfacing it is that they are
        /// otherwise indistinguishable from a broken plugin - NoServer especially, which is the default
        /// state and silent.
        /// </summary>
        public static SessionStatus Status { get; private set; } = SessionStatus.NoServer;

        private static CustomToolStripMenuItem _simulatorMenu;
        private static SimulatorWindow _simulatorWindow;

        /// <summary>
        /// How often the selected server is asked whether its session is paused. One second: this is what
        /// decides how quickly the display settles after the instructor hits pause, and the answer is two
        /// booleans read out of memory. AutoReset is off and the timer is restarted after each poll
        /// finishes, so a slow or hanging server can't stack requests up.
        /// </summary>
        private static readonly System.Timers.Timer _sessionTimer =
            new System.Timers.Timer(TimeSpan.FromSeconds(1).TotalMilliseconds) { AutoReset = false };

        public SimulatorPlugin()
        {
            MMI.SelectedTrackChanged += MMI_SelectedTrackChanged;

            MMI.SelectedGroundTrackChanged += MMI_SelectedGroundTrackChanged;

            _simulatorMenu = new CustomToolStripMenuItem(CustomToolStripMenuItemWindowType.Main, CustomToolStripMenuItemCategory.Settings, new ToolStripMenuItem("Simulator"));
            _simulatorMenu.Item.Click += SimulatorMenu_Click;
            MMI.AddCustomMenuItem(_simulatorMenu);

            _sessionTimer.Elapsed += SessionTimer_Elapsed;
            _sessionTimer.Start();
        }

        private static async void SessionTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                RadarFreeze.Apply(await IsSessionPaused());
            }
            catch
            {
                // Belt and braces - Apply and IsSessionPaused both swallow their own failures, but an
                // unhandled exception on this thread would take vatSys down with it, and a plugin must
                // never do that. Anything unexpected releases the freeze.
                try { RadarFreeze.Apply(false); } catch { }
            }
            finally
            {
                _sessionTimer.Start();
            }
        }

        /// <summary>
        /// Whether the simulator session this vatSys is connected to is loaded but paused. False for every
        /// other case, including all the failure ones: no server selected, the real network, no session for
        /// this CID, an unreachable server, an answer that doesn't parse. RadarFreeze only holds the
        /// display while this keeps saying true, so every one of those releases it.
        /// </summary>
        private static async Task<bool> IsSessionPaused()
        {
            if (string.IsNullOrWhiteSpace(_server))
            {
                Status = SessionStatus.NoServer;
                return false;
            }

            if (Network.IsOfficialServer)
            {
                Status = SessionStatus.OfficialNetwork;
                return false;
            }

            var cid = Network.ControllerId;

            if (string.IsNullOrWhiteSpace(cid))
            {
                Status = SessionStatus.NotConnected;
                return false;
            }

            try
            {
                var response = await _httpClient.GetAsync($"{_server}/session?cid={Uri.EscapeDataString(cid)}");

                if (!response.IsSuccessStatusCode)
                {
                    Status = response.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? SessionStatus.NoSession
                        : SessionStatus.Unreachable;
                    return false;
                }

                var state = JsonConvert.DeserializeObject<SessionState>(await response.Content.ReadAsStringAsync());

                if (state == null)
                {
                    Status = SessionStatus.Unreachable;
                    return false;
                }

                // A session with no scenario loaded isn't paused, it just hasn't started - freezing there
                // would hold the display still before there was ever anything on it.
                if (!state.ScenarioLoaded)
                {
                    Status = SessionStatus.NoScenario;
                    return false;
                }

                Status = state.Running ? SessionStatus.Running : SessionStatus.Paused;

                return !state.Running;
            }
            catch
            {
                Status = SessionStatus.Unreachable;
                return false;
            }
        }

        /// <summary>
        /// Reads the server list from the embedded Servers.json. Embedded rather than read from disk so it
        /// travels with the assembly the launcher installs - there is nothing else to deploy alongside it.
        /// </summary>
        private static List<Server> LoadServers()
        {
            try
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SimulatorPlugin.Servers.json"))
                using (var reader = new StreamReader(stream))
                {
                    return JsonConvert.DeserializeObject<List<Server>>(reader.ReadToEnd());
                }
            }
            catch
            {
                return new List<Server>();
            }
        }

        private void SimulatorMenu_Click(object sender, EventArgs e)
        {
            ShowSimulatorWindow();
        }

        private static void ShowSimulatorWindow()
        {
            MMI.InvokeOnGUI((MethodInvoker)delegate ()
            {
                if (_simulatorWindow == null || _simulatorWindow.IsDisposed)
                {
                    _simulatorWindow = new SimulatorWindow();
                }
                else if (_simulatorWindow.Visible) return;

                _simulatorWindow.Show();
            });
        }

        private async void MMI_SelectedGroundTrackChanged(object sender, EventArgs e)
        {
            if (Network.IsOfficialServer) return;

            var callsign = MMI.SelectedGroundTrack?.GetFDR()?.Callsign;

            if (callsign == null) return;

            await SendToServer(callsign);
        }

        private async void MMI_SelectedTrackChanged(object sender, EventArgs e)
        {
            if (Network.IsOfficialServer) return;

            var callsign = MMI.SelectedTrack?.GetFDR()?.Callsign;

            if (callsign == null) return;

            await SendToServer(callsign);
        }

        /// <summary>
        /// Tells the simulator which aircraft was just selected here.
        ///
        /// The CID matters on the multi world simulator: a server there runs one session per instructor
        /// rather than the single shared simulation the older ones do, and this is what tells it which of
        /// those sessions the selection belongs to - the same CID vatSys connected to that server with, so
        /// it resolves the session exactly as the FSD login did. Older servers route this to a page that
        /// takes the callsign from the path and never looks at the query string, so one request works
        /// against both and there is no per server flag to keep in step.
        /// </summary>
        private async Task SendToServer(string callsign)
        {
            if (string.IsNullOrWhiteSpace(_server)) return;

            var url = $"{_server}/select/{Uri.EscapeDataString(callsign)}";

            var cid = Network.ControllerId;

            if (!string.IsNullOrWhiteSpace(cid)) url += $"?cid={Uri.EscapeDataString(cid)}";

            try
            {
                await _httpClient.GetAsync(url);
            }
            catch { }
        }

        public void OnFDRUpdate(FDP2.FDR updated)
        {
            return;
        }

        public void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {
            return;
        }
    }
}
