using System;
using System.Linq;
using System.Windows.Forms;
using vatsys;

namespace Simulator.Plugin
{
    public partial class SimulatorWindow : BaseForm
    {
        /// <summary>
        /// Refreshes the status lines. A WinForms timer, so it ticks on the UI thread and can touch the
        /// label directly. Only runs while the window is open - there's nothing to show otherwise.
        /// </summary>
        private readonly Timer _statusTimer = new Timer { Interval = 1000 };

        public SimulatorWindow()
        {
            InitializeComponent();

            BackColor = Colours.GetColour(Colours.Identities.WindowBackground);
            ForeColor = Colours.GetColour(Colours.Identities.InteractiveText);

            _statusTimer.Tick += (s, e) => ShowStatus();
            FormClosed += (s, e) => _statusTimer.Stop();
        }

        /// <summary>
        /// Says plainly what the plugin is doing, because every way it can do nothing looks identical from
        /// the outside. Not selecting a server is the default state and silent, and it cost a whole
        /// evening of testing a freeze that was never armed.
        /// </summary>
        private void ShowStatus()
        {
            string session;

            switch (SimulatorPlugin.Status)
            {
                case SessionStatus.NoServer: session = "select a server above"; break;
                case SessionStatus.OfficialNetwork: session = "live network - inactive"; break;
                case SessionStatus.NotConnected: session = "vatSys not connected"; break;
                case SessionStatus.Unreachable: session = "server not responding"; break;
                case SessionStatus.NoSession: session = "no session for this CID"; break;
                case SessionStatus.NoScenario: session = "no scenario loaded"; break;
                case SessionStatus.Running: session = "running"; break;
                case SessionStatus.Paused: session = "PAUSED"; break;
                default: session = "-"; break;
            }

            var radar = RadarFreeze.Frozen
                ? "held"
                : RadarFreeze.Available ? "live" : "hold unavailable";

            labelStatus.Text = string.Join(Environment.NewLine, new[]
            {
                "Session:  " + session,
                "Radar:    " + radar,
            });
        }

        private void ComboBoxDisplay_SelectedIndexChanged(object sender, EventArgs e)
        {
            var server = SimulatorPlugin.Servers.FirstOrDefault(x => x.Name == comboBoxDisplay.Text);

            SimulatorPlugin._server = server == null ? string.Empty : server.Url;
        }

        private void SimulatorWindow_Load(object sender, EventArgs e)
        {
            comboBoxDisplay.Items.Clear();

            // The blank first entry is "no server" - selecting it stops anything being sent.
            comboBoxDisplay.Items.Add(string.Empty);

            foreach (var server in SimulatorPlugin.Servers)
            {
                comboBoxDisplay.Items.Add(server.Name);
            }

            var selected = SimulatorPlugin.Servers.FirstOrDefault(x => x.Url == SimulatorPlugin._server);

            comboBoxDisplay.Text = selected == null ? string.Empty : selected.Name;

            ShowStatus();

            _statusTimer.Start();
        }
    }
}
