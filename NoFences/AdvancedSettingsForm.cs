using NoFences.Model;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace NoFences
{
    internal sealed class AdvancedSettingsForm : Form
    {
        public AdvancedSettingsForm()
        {
            Text = "NoFences - Advanced settings";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(430, 155);

            var notice = new Label
            {
                AutoSize = false,
                Location = new Point(16, 14),
                Size = new Size(398, 38),
                Text = "Global preferences are not available yet. Existing options are in each fence's right-click menu."
            };

            var pathLabel = new Label
            {
                AutoSize = false,
                Location = new Point(16, 57),
                Size = new Size(398, 38),
                Text = "Fence data folder:" + Environment.NewLine + FenceManager.Instance.DataDirectory
            };

            var openFolderButton = new Button
            {
                Location = new Point(16, 108),
                Size = new Size(145, 28),
                Text = "Open data folder"
            };
            openFolderButton.Click += OpenFolderButton_Click;

            var closeButton = new Button
            {
                DialogResult = DialogResult.Cancel,
                Location = new Point(334, 108),
                Size = new Size(80, 28),
                Text = "Close"
            };

            Controls.Add(notice);
            Controls.Add(pathLabel);
            Controls.Add(openFolderButton);
            Controls.Add(closeButton);
            CancelButton = closeButton;
        }

        private void OpenFolderButton_Click(object sender, EventArgs e)
        {
            Process.Start("explorer.exe", FenceManager.Instance.DataDirectory);
        }
    }
}
