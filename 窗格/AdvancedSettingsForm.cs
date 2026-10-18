using NoFences.Model;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace NoFences
{
    internal sealed class AdvancedSettingsForm : Form
    {
        private readonly TableLayoutPanel contentLayout;
        private readonly Label notice;
        private readonly Label titleSizeHint;

        public AdvancedSettingsForm()
        {
            Text = "NoFences - Advanced settings";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(520, 390);
            MinimumSize = new Size(480, 420);

            contentLayout = new TableLayoutPanel
            {
                AutoSize = false,
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                RowCount = 9
            };
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            notice = new Label
            {
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 14),
                Text = "These global appearance settings apply to all fences."
            };

            var titleLabel = new Label
            {
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 5),
                Text = "Global title size"
            };

            var titleSize = new NumericUpDown
            {
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 0, 5),
                Minimum = 16,
                Maximum = 100,
                Value = GlobalFenceSettings.TitleHeight
            };

            titleSizeHint = new Label
            {
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 12),
                Text = "Overrides each fence's saved title size; saved values are preserved."
            };

            var hideTitles = new CheckBox
            {
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 14),
                Text = "Hide title bars and title text (hold Alt and drag to move)",
                Checked = GlobalFenceSettings.HideTitleArea
            };

            var folderHeading = new Label
            {
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4),
                Text = "Fence data folder:"
            };

            var folderPath = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Text = FenceManager.Instance.DataDirectory
            };

            var fitActions = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0, 10, 0, 0),
                WrapContents = true
            };
            var fitDescription = new Label
            {
                AutoSize = true,
                Margin = new Padding(0, 8, 10, 0),
                Text = "Fit each fence to its current thumbnail columns and rows:"
            };
            var modifyAllButton = new Button
            {
                AutoSize = true,
                Text = "Modify all"
            };
            modifyAllButton.Click += ModifyAllButton_Click;
            fitActions.Controls.Add(fitDescription);
            fitActions.Controls.Add(modifyAllButton);

            var actions = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 12, 0, 0),
                RowCount = 1
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actions.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var folderActions = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0),
                WrapContents = true
            };
            var openFolderButton = new Button
            {
                AutoSize = true,
                Text = "Open data folder"
            };
            openFolderButton.Click += OpenFolderButton_Click;

            var statusLabel = new Label
            {
                AutoSize = true,
                Margin = new Padding(8, 8, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
            folderActions.Controls.Add(openFolderButton);
            folderActions.Controls.Add(statusLabel);

            var settingActions = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                Margin = new Padding(0),
                WrapContents = true
            };
            var closeButton = new Button
            {
                AutoSize = true,
                DialogResult = DialogResult.Cancel,
                Text = "Close"
            };
            var applyButton = new Button
            {
                AutoSize = true,
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

            settingActions.Controls.Add(closeButton);
            settingActions.Controls.Add(applyButton);
            actions.Controls.Add(folderActions, 0, 0);
            actions.Controls.Add(settingActions, 1, 0);

            contentLayout.Controls.Add(notice, 0, 0);
            contentLayout.Controls.Add(titleLabel, 0, 1);
            contentLayout.Controls.Add(titleSize, 0, 2);
            contentLayout.Controls.Add(titleSizeHint, 0, 3);
            contentLayout.Controls.Add(hideTitles, 0, 4);
            contentLayout.Controls.Add(folderHeading, 0, 5);
            contentLayout.Controls.Add(folderPath, 0, 6);
            contentLayout.Controls.Add(fitActions, 0, 7);
            contentLayout.Controls.Add(actions, 0, 8);
            contentLayout.SizeChanged += ContentLayout_SizeChanged;
            Controls.Add(contentLayout);
            ContentLayout_SizeChanged(contentLayout, EventArgs.Empty);

            AcceptButton = applyButton;
            CancelButton = closeButton;
        }

        private void ContentLayout_SizeChanged(object sender, EventArgs e)
        {
            var availableWidth = Math.Max(100, contentLayout.ClientSize.Width - contentLayout.Padding.Horizontal);
            notice.MaximumSize = new Size(availableWidth, 0);
            titleSizeHint.MaximumSize = new Size(availableWidth, 0);
        }

        private void OpenFolderButton_Click(object sender, EventArgs e)
        {
            Process.Start("explorer.exe", FenceManager.Instance.DataDirectory);
        }

        private void ModifyAllButton_Click(object sender, EventArgs e)
        {
            var fences = Application.OpenForms.OfType<FenceWindow>().ToArray();
            if (fences.Length == 0)
            {
                MessageBox.Show(this, "There are no loaded fences to modify.", "Modify all fences",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirmation = MessageBox.Show(this,
                "Resize and save all fences to fit their current thumbnail columns and rows? Each fence's current column count will be preserved.",
                "Modify all fences", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirmation != DialogResult.Yes)
                return;

            var modifiedCount = 0;
            var errors = new StringBuilder();
            foreach (var fence in fences)
            {
                try
                {
                    if (fence.FitBoundsToCurrentLayout())
                        modifiedCount++;
                }
                catch (Exception ex) when (ex is System.IO.IOException
                    || ex is UnauthorizedAccessException
                    || ex is InvalidOperationException)
                {
                    errors.AppendLine(fence.Text + ": " + ex.Message);
                }
            }

            if (errors.Length > 0)
            {
                MessageBox.Show(this,
                    "Modified and saved " + modifiedCount + " fence(s). These fences could not be saved:" +
                    Environment.NewLine + errors,
                    "Some fences could not be modified", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show(this, "Modified and saved " + modifiedCount + " fence(s).",
                    "Modify all fences", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
