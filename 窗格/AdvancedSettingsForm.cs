using 窗格.Model;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace 窗格
{
    internal sealed class AdvancedSettingsForm : Form
    {
        private readonly TableLayoutPanel contentLayout;
        private readonly Label notice;
        private readonly Label titleSizeHint;

        public AdvancedSettingsForm()
        {
            Text = "窗格 - Advanced settings";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(620, 560);
            MinimumSize = new Size(600, 540);

            contentLayout = new TableLayoutPanel
            {
                AutoSize = false,
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                RowCount = 5,
                AutoScroll = true
            };
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var i = 0; i < contentLayout.RowCount; i++)
                contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var pageHeader = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 1,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 20),
                RowCount = 2
            };
            pageHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pageHeader.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pageHeader.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var pageTitle = new Label
            {
                AutoSize = true,
                Font = FluentTheme.HeadingFont,
                Margin = new Padding(0, 0, 0, 4),
                Text = "Appearance"
            };
            notice = new Label
            {
                AutoSize = true,
                ForeColor = FluentTheme.SecondaryText,
                Margin = new Padding(0),
                Text = "These global appearance settings apply to all fences."
            };
            pageHeader.Controls.Add(pageTitle, 0, 0);
            pageHeader.Controls.Add(notice, 0, 1);

            var appearanceCard = new Panel
            {
                AutoSize = true,
                BackColor = FluentTheme.Surface,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 12),
                Padding = new Padding(16)
            };
            var appearanceLayout = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                Dock = DockStyle.Top,
                Margin = new Padding(0),
                RowCount = 4
            };
            appearanceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            appearanceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            appearanceLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            appearanceLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            appearanceLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            appearanceLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var appearanceHeading = new Label
            {
                AutoSize = true,
                Font = new Font(FluentTheme.BodyFont, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 12),
                Text = "Fence titles"
            };
            appearanceLayout.Controls.Add(appearanceHeading, 0, 0);
            appearanceLayout.SetColumnSpan(appearanceHeading, 2);
            var titleLabel = new Label
            {
                Anchor = AnchorStyles.Left,
                AutoSize = true,
                Margin = new Padding(0, 0, 16, 4),
                Text = "Global title size"
            };
            var titleSize = new NumericUpDown
            {
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 0, 0, 4),
                MinimumSize = new Size(96, 28),
                Minimum = 16,
                Maximum = 100,
                Value = GlobalFenceSettings.TitleHeight
            };

            titleSizeHint = new Label
            {
                AutoSize = true,
                ForeColor = FluentTheme.SecondaryText,
                Margin = new Padding(0, 0, 0, 12),
                Text = "Overrides each fence's saved title size; saved values are preserved."
            };
            appearanceLayout.Controls.Add(titleLabel, 0, 1);
            appearanceLayout.Controls.Add(titleSize, 1, 1);
            appearanceLayout.Controls.Add(titleSizeHint, 0, 2);
            appearanceLayout.SetColumnSpan(titleSizeHint, 2);
            var hideTitles = new CheckBox
            {
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 0),
                Text = "Hide title bars and title text (hold Alt and drag to move)",
                Checked = GlobalFenceSettings.HideTitleArea
            };
            appearanceLayout.Controls.Add(hideTitles, 0, 3);
            appearanceLayout.SetColumnSpan(hideTitles, 2);
            appearanceCard.Controls.Add(appearanceLayout);

            var storageCard = new Panel
            {
                AutoSize = true,
                BackColor = FluentTheme.Surface,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 12),
                Padding = new Padding(16)
            };
            var storageLayout = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 1,
                Dock = DockStyle.Top,
                Margin = new Padding(0),
                RowCount = 3
            };
            storageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            storageLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            storageLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            storageLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var folderHeading = new Label
            {
                AutoSize = true,
                Font = new Font(FluentTheme.BodyFont, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 8),
                Text = "Fence data folder:"
            };
            var folderPath = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                Margin = new Padding(0, 0, 0, 8),
                MinimumSize = new Size(0, 48),
                ScrollBars = ScrollBars.Vertical,
                Text = FenceManager.Instance.DataDirectory
            };
            storageLayout.Controls.Add(folderHeading, 0, 0);
            storageLayout.Controls.Add(folderPath, 0, 1);
            var fitActions = new FlowLayoutPanel
            {
                AutoSize = true,
                BackColor = FluentTheme.Surface,
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0, 0, 0, 12),
                Padding = new Padding(16),
                WrapContents = true
            };
            var fitDescription = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = FluentTheme.SecondaryText,
                Margin = new Padding(0, 6, 16, 0),
                Text = "Fit each fence to its current thumbnail columns and rows:"
            };
            var modifyAllButton = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(112, 34),
                Text = "Modify all"
            };
            modifyAllButton.Click += ModifyAllButton_Click;
            fitActions.Controls.Add(fitDescription);
            fitActions.Controls.Add(modifyAllButton);

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
                MinimumSize = new Size(144, 34),
                Text = "Open data folder"
            };
            openFolderButton.Click += OpenFolderButton_Click;

            var statusLabel = new Label
            {
                AutoSize = true,
                ForeColor = FluentTheme.SecondaryText,
                Margin = new Padding(12, 9, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
            folderActions.Controls.Add(openFolderButton);
            folderActions.Controls.Add(statusLabel);
            storageLayout.Controls.Add(folderActions, 0, 2);
            storageCard.Controls.Add(storageLayout);

            var settingActions = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                Margin = new Padding(0),
                WrapContents = false
            };
            var closeButton = new Button
            {
                AutoSize = true,
                DialogResult = DialogResult.Cancel,
                MinimumSize = new Size(96, 34),
                Margin = new Padding(0, 0, 8, 0),
                Text = "Close"
            };
            var applyButton = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(96, 34),
                Margin = new Padding(0),
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
            var footer = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                Dock = DockStyle.Top,
                Margin = new Padding(0),
                RowCount = 1
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            footer.Controls.Add(settingActions, 1, 0);

            contentLayout.Controls.Add(pageHeader, 0, 0);
            contentLayout.Controls.Add(appearanceCard, 0, 1);
            contentLayout.Controls.Add(storageCard, 0, 2);
            contentLayout.Controls.Add(fitActions, 0, 3);
            contentLayout.Controls.Add(footer, 0, 4);
            contentLayout.SizeChanged += ContentLayout_SizeChanged;
            Controls.Add(contentLayout);
            FluentTheme.ApplyToForm(this, applyButton);
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
