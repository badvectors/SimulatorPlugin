using System;
using System.Linq;
using vatsys;

namespace Simulator.Plugin
{
    public partial class SimulatorWindow : BaseForm
    {
        public SimulatorWindow()
        {
            InitializeComponent();

            BackColor = Colours.GetColour(Colours.Identities.WindowBackground);
            ForeColor = Colours.GetColour(Colours.Identities.InteractiveText);
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
        }
    }
}
