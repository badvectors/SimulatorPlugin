﻿using System;
using System.ComponentModel.Composition;
using System.Net.Http;
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
        
        public HttpClient _client = new HttpClient();
        public static string Server = string.Empty;
        public bool _send = false;

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
            if (Network.IsOfficialServer || string.IsNullOrWhiteSpace(Server)) return;

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

        private async Task SendToServer(string callsign)
        {
            try
            {
                await _client.GetAsync($"{Server}/select/{callsign}");
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