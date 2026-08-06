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

        private static CustomToolStripMenuItem _simulatorMenu;
        private static SimulatorWindow _simulatorWindow;

        public SimulatorPlugin()
        {
            MMI.SelectedTrackChanged += MMI_SelectedTrackChanged;

            MMI.SelectedGroundTrackChanged += MMI_SelectedGroundTrackChanged;

            _simulatorMenu = new CustomToolStripMenuItem(CustomToolStripMenuItemWindowType.Main, CustomToolStripMenuItemCategory.Settings, new ToolStripMenuItem("Simulator"));
            _simulatorMenu.Item.Click += SimulatorMenu_Click;
            MMI.AddCustomMenuItem(_simulatorMenu);
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
