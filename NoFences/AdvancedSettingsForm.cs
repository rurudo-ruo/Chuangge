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
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(450, 300);

            var notice = new Label
            {
                AutoSize = false,
                Location = new Point(16, 14),
                Size = new Size(418, 36),
                Text = "These global appearance settings apply to all fences."
            };

            var titleLabel = new Label
            {
                AutoSize = true,
                Location = new Point(16, 62),
                Text = "Global title size"
            };

            var titleSize = new NumericUpDown
            {
                Location = new Point(16, 88),
                Size = new Size(90, 24),
                Minimum = 16,
                Maximum = 100,
                Value = GlobalFenceSettings.TitleHeight
            };

            var titleSizeHint = new Label
            {
                AutoSize = false,
                Location = new Point(116, 88),
                Size = new Size(318, 24),
                Text = "Overrides each fence's saved title size (saved values are preserved)."
            };

            var hideTitles = new CheckBox
            {
                AutoSize = true,
                Location = new Point(16, 130),
                Text = "Hide title bars and title text (hold Alt and drag to move)",
                Checked = GlobalFenceSettings.HideTitleArea
            };

            var dataFolderLabel = new Label
            {
                AutoSize = false,
                Location = new Point(16, 180),
                Size = new Size(418, 42),
                Text = "Fence data folder:" + Environment.NewLine + FenceManager.Instance.DataDirectory
            };

            var statusLabel = new Label
            {
                AutoSize = false,
                Location = new Point(170, 240),
                Size = new Size(88, 28),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var openFolderButton = new Button
            {
                Location = new Point(16, 240),
                Size = new Size(145, 28),
                Text = "Open data folder"
            };
            openFolderButton.Click += OpenFolderButton_Click;

            var applyButton = new Button
            {
                Location = new Point(264, 240),
                Size = new Size(80, 28),
                Text = "Apply"
            };
            applyButton.Click += (sender, e) =>
            {
                try
                {
                    GlobalFenceSettings.Update((int)titleSize.Value, hideTitles.Checked);
                    statusLabel.Text = "Saved.";
                }
                catch (System.Configuration.ConfigurationErrorsException ex)
                {
                    MessageBox.Show(this, ex.Message, "Settings could not be saved", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var closeButton = new Button
            {
                DialogResult = DialogResult.Cancel,
                Location = new Point(354, 240),
                Size = new Size(80, 28),
                Text = "Close"
            };

            Controls.Add(notice);
            Controls.Add(titleLabel);
            Controls.Add(titleSize);
            Controls.Add(titleSizeHint);
            Controls.Add(hideTitles);
            Controls.Add(dataFolderLabel);
            Controls.Add(openFolderButton);
            Controls.Add(applyButton);
            Controls.Add(statusLabel);
            Controls.Add(closeButton);
            CancelButton = closeButton;
        }

        private void OpenFolderButton_Click(object sender, EventArgs e)
        {
            Process.Start("explorer.exe", FenceManager.Instance.DataDirectory);
        }
    }
}
