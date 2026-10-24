using System;
using System.Drawing;
using System.Windows.Forms;
using 窗格.Model;
using 窗格.Util;

namespace 窗格
{
    internal sealed class FenceTitleSettingsControl : SettingsSectionControl
    {
        private readonly NumericUpDown titleSize;
        private readonly CheckBox hideTitles;
        private readonly Label titleSizeHint;

        public int TitleHeight => (int)titleSize.Value;
        public bool HideTitleArea => hideTitles.Checked;

        public FenceTitleSettingsControl()
        {
            var layout = CreateLayout(2, 4);
            var heading = CreateHeading("Fence titles");
            layout.Controls.Add(heading, 0, 0);
            layout.SetColumnSpan(heading, 2);

            var titleLabel = new Label
            {
                Anchor = AnchorStyles.Left,
                AutoSize = true,
                Margin = new Padding(0, 0, 16, 4),
                Text = UiText.Get("Global title size")
            };
            titleSize = new NumericUpDown
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
                Text = UiText.Get("Overrides each fence's saved title size; saved values are preserved.")
            };
            layout.Controls.Add(titleLabel, 0, 1);
            layout.Controls.Add(titleSize, 1, 1);
            layout.Controls.Add(titleSizeHint, 0, 2);
            layout.SetColumnSpan(titleSizeHint, 2);

            hideTitles = new CheckBox
            {
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 0),
                Text = UiText.Get("Hide title bars and title text (hold Alt and drag to move)"),
                Checked = GlobalFenceSettings.HideTitleArea
            };
            layout.Controls.Add(hideTitles, 0, 3);
            layout.SetColumnSpan(hideTitles, 2);
            Controls.Add(layout);
        }

        public void SetAvailableWidth(int width)
        {
            titleSizeHint.MaximumSize = new Size(width, 0);
        }

        private static TableLayoutPanel CreateLayout(int columns, int rows)
        {
            var layout = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = columns,
                Dock = DockStyle.Top,
                Margin = new Padding(0),
                RowCount = rows
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            if (columns > 1)
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            for (var i = 0; i < rows; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return layout;
        }

        private static Label CreateHeading(string text)
        {
            return new Label
            {
                AutoSize = true,
                Font = new Font(FluentTheme.BodyFont, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 12),
                Text = UiText.Get(text)
            };
        }
    }

    internal sealed class ContentScaleSettingsControl : SettingsSectionControl
    {
        private readonly TrackBar scale;
        private readonly Label valueLabel;

        public int ScalePercent => scale.Value;

        public ContentScaleSettingsControl()
        {
            var layout = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 1,
                Dock = DockStyle.Top,
                Margin = new Padding(0),
                RowCount = 3
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var i = 0; i < layout.RowCount; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(CreateHeading(UiText.Get("Fence content size")), 0, 0);

            scale = new TrackBar
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                AutoSize = false,
                Dock = DockStyle.Fill,
                LargeChange = 25,
                Maximum = 200,
                Minimum = 50,
                Margin = new Padding(0, 0, 8, 0),
                SmallChange = 12,
                TickFrequency = 25,
                Value = GlobalFenceSettings.ContentScalePercent
            };
            valueLabel = new Label
            {
                Anchor = AnchorStyles.Right,
                AutoSize = true,
                ForeColor = FluentTheme.SecondaryText,
                Margin = new Padding(0),
                Text = scale.Value + "%"
            };
            scale.ValueChanged += (sender, e) => valueLabel.Text = scale.Value + "%";

            var row = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 3,
                Dock = DockStyle.Top,
                Margin = new Padding(0),
                RowCount = 1
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            row.Controls.Add(CreateRowLabel(UiText.Get("Content scale")), 0, 0);
            row.Controls.Add(scale, 1, 0);
            row.Controls.Add(valueLabel, 2, 0);
            layout.Controls.Add(row, 0, 1);
            layout.Controls.Add(CreateHint(UiText.Get("Scales thumbnails, labels, and spacing independently of Windows display scaling. Existing fence dimensions stay unchanged; use Modify all to resize them.")), 0, 2);
            Controls.Add(layout);
        }

        private static Label CreateHeading(string text)
        {
            return new Label
            {
                AutoSize = true,
                Font = new Font(FluentTheme.BodyFont, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 8),
                Text = text
            };
        }

        private static Label CreateHint(string text)
        {
            return new Label
            {
                AutoSize = true,
                ForeColor = FluentTheme.SecondaryText,
                Margin = new Padding(0, 4, 0, 0),
                Tag = "FluentSecondaryText",
                Text = text
            };
        }

        private static Label CreateRowLabel(string text)
        {
            return new Label
            {
                Anchor = AnchorStyles.Left,
                AutoSize = true,
                Margin = new Padding(0),
                Text = text
            };
        }
    }

    internal sealed class StartupSettingsControl : SettingsSectionControl
    {
        private readonly CheckBox startWithWindows;

        public bool IsEnabled => startWithWindows.Checked;

        public StartupSettingsControl()
        {
            var layout = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 1,
                Dock = DockStyle.Top,
                Margin = new Padding(0),
                RowCount = 2
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            startWithWindows = new CheckBox
            {
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4),
                Text = UiText.Get("Start NoFences when I sign in"),
                Checked = StartupRegistration.IsEnabled
            };
            var hint = new Label
            {
                AutoSize = true,
                ForeColor = FluentTheme.SecondaryText,
                Margin = new Padding(0),
                Tag = "FluentSecondaryText",
                Text = UiText.Get("Uses a removable shortcut in your personal Startup folder; no registry entry or service is added.")
            };
            layout.Controls.Add(startWithWindows, 0, 0);
            layout.Controls.Add(hint, 0, 1);
            Controls.Add(layout);
        }
    }

    internal sealed class FenceStorageSettingsControl : SettingsSectionControl
    {
        private readonly Label statusLabel;

        public event EventHandler OpenFolderRequested;

        public FenceStorageSettingsControl()
        {
            var layout = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 1,
                Dock = DockStyle.Top,
                Margin = new Padding(0),
                RowCount = 3
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var i = 0; i < layout.RowCount; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label
            {
                AutoSize = true,
                Font = new Font(FluentTheme.BodyFont, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 8),
                Text = UiText.Get("Fence data folder:")
            }, 0, 0);
            layout.Controls.Add(new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                Margin = new Padding(0, 0, 0, 8),
                MinimumSize = new Size(0, 48),
                ScrollBars = ScrollBars.Vertical,
                Text = FenceManager.Instance.DataDirectory
            }, 0, 1);
            var actions = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0),
                WrapContents = true
            };
            statusLabel = new Label
            {
                AutoSize = true,
                ForeColor = FluentTheme.SecondaryText,
                Margin = new Padding(12, 9, 0, 0),
                Tag = "FluentSecondaryText",
                TextAlign = ContentAlignment.MiddleLeft
            };
            var openFolderButton = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(144, 34),
                Text = UiText.Get("Open data folder")
            };
            openFolderButton.Click += (sender, e) => OpenFolderRequested?.Invoke(this, EventArgs.Empty);
            actions.Controls.Add(openFolderButton);
            actions.Controls.Add(statusLabel);
            layout.Controls.Add(actions, 0, 2);
            Controls.Add(layout);
        }

        public void SetStatus(string text)
        {
            statusLabel.Text = text;
        }
    }
}
