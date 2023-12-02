﻿using System;
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
            switch (comboBoxDisplay.Text)
            {
                case "SweatBox-1":
                    SimulatorPlugin.Server = "https://sweatbox01-training.vatpac.org";
                    break;
                case "SweatBox-2":
                    SimulatorPlugin.Server = "https://sweatbox02-training.vatpac.org";
                    break;
                default:
                    SimulatorPlugin.Server = string.Empty;
                    break;
            }
        }

        private void SimulatorWindow_Load(object sender, EventArgs e)
        {
            switch (SimulatorPlugin.Server)
            {
                case "https://sweatbox01-training.vatpac.org":
                    comboBoxDisplay.Text = "SweatBox-1";
                    return;
                case "https://sweatbox02-training.vatpac.org":
                    comboBoxDisplay.Text = "SweatBox-2";
                    return;
                default:
                    comboBoxDisplay.Text = "";
                    return;
            }
        }
    }
}