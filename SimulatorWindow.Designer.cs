namespace Simulator.Plugin
{
    partial class SimulatorWindow
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.comboBoxDisplay = new System.Windows.Forms.ComboBox();
            this.labelDisplay = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // comboBoxDisplay
            // 
            this.comboBoxDisplay.BackColor = System.Drawing.SystemColors.Window;
            this.comboBoxDisplay.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxDisplay.Font = new System.Drawing.Font("Terminus (TTF)", 16F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
            this.comboBoxDisplay.FormattingEnabled = true;
            this.comboBoxDisplay.Items.AddRange(new object[] {
            "",
            "Vatpac SweatBox-1",
            "Vatpac SweatBox-2",
            "Vatmex SweatBox"});
            this.comboBoxDisplay.Location = new System.Drawing.Point(16, 31);
            this.comboBoxDisplay.Margin = new System.Windows.Forms.Padding(4);
            this.comboBoxDisplay.Name = "comboBoxDisplay";
            this.comboBoxDisplay.Size = new System.Drawing.Size(255, 25);
            this.comboBoxDisplay.TabIndex = 1;
            this.comboBoxDisplay.SelectedIndexChanged += new System.EventHandler(this.ComboBoxDisplay_SelectedIndexChanged);
            // 
            // labelDisplay
            // 
            this.labelDisplay.AutoSize = true;
            this.labelDisplay.Font = new System.Drawing.Font("Terminus (TTF)", 16F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
            this.labelDisplay.Location = new System.Drawing.Point(16, 12);
            this.labelDisplay.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelDisplay.Name = "labelDisplay";
            this.labelDisplay.Size = new System.Drawing.Size(56, 17);
            this.labelDisplay.TabIndex = 2;
            this.labelDisplay.Text = "Server";
            // 
            // SimulatorWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoValidate = System.Windows.Forms.AutoValidate.Disable;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(284, 72);
            this.Controls.Add(this.labelDisplay);
            this.Controls.Add(this.comboBoxDisplay);
            this.ForeColor = System.Drawing.SystemColors.InfoText;
            this.HasMinimizeButton = false;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MaximumSize = new System.Drawing.Size(288, 100);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(288, 100);
            this.Name = "SimulatorWindow";
            this.Resizeable = false;
            this.Text = "Simulator";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.SimulatorWindow_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private System.Windows.Forms.ComboBox comboBoxDisplay;
        private System.Windows.Forms.Label labelDisplay;
    }
}