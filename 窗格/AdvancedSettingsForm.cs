using 窗格.Model;
using 窗格.Util;
using 窗格.Win32;
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
        private static AdvancedSettingsForm currentInstance;
        private readonly TableLayoutPanel contentLayout;
        private readonly Label notice;
        private readonly FenceTitleSettingsControl fenceTitleSettings;
        private readonly ContentScaleSettingsControl contentScaleSettings;
        private readonly StartupSettingsControl startupSettings;
        private readonly CheckBox useSystemTheme;
        private readonly ComboBox displayMode;
        private readonly Button accentColorButton;
        private readonly TrackBar darkOverlayOpacity;
        private readonly TrackBar lightOverlayOpacity;
        private readonly Label darkOverlayValue;
        private readonly Label lightOverlayValue;
        private readonly CheckBox glowEffectsEnabled;
        private readonly Button pointerGlowColorButton;
        private readonly TrackBar pointerGlowIntensity;
        private readonly TrackBar pointerGlowRadius;
        private readonly TrackBar iconGlowIntensity;
        private readonly TrackBar iconGlowRadius;
        private readonly Label pointerGlowIntensityValue;
        private readonly Label pointerGlowRadiusValue;
        private readonly Label iconGlowIntensityValue;
        private readonly Label iconGlowRadiusValue;
        private readonly TrackBar glassIntensity;
        private readonly Label glassIntensityValue;
        private readonly TrackBar glassTexture;
        private readonly Label glassTextureValue;
        private Button primaryActionButton;
        private Color customAccentColor;
        private Color pointerGlowColor;
        private readonly Panel titleBar;
        private readonly Font titleBarFont;
        private bool closeHovered;
        private Rectangle closeBounds;

        public static void ShowSettings(IWin32Window owner = null)
        {
            if (currentInstance == null || currentInstance.IsDisposed)
                currentInstance = new AdvancedSettingsForm();

            if (currentInstance.Visible)
            {
                currentInstance.Activate();
                return;
            }

            var workingArea = owner == null
                ? Screen.FromPoint(Cursor.Position).WorkingArea
                : Screen.FromHandle(owner.Handle).WorkingArea;
            var width = Math.Min(820, Math.Max(360, workingArea.Width - 48));
            var height = Math.Min(860, Math.Max(320, workingArea.Height - 48));
            currentInstance.MinimumSize = new Size(
                Math.Min(640, width),
                Math.Min(600, height));
            currentInstance.Size = new Size(width, height);
            currentInstance.StartPosition = FormStartPosition.Manual;
            currentInstance.Location = new Point(
                workingArea.Left + (workingArea.Width - currentInstance.Width) / 2,
                workingArea.Top + (workingArea.Height - currentInstance.Height) / 2);

            if (owner == null)
                currentInstance.Show();
            else
                currentInstance.Show(owner);
        }

        public AdvancedSettingsForm()
        {
            Text = "窗格 - " + UiText.Get("Advanced settings");
            fenceTitleSettings = new FenceTitleSettingsControl();
            contentScaleSettings = new ContentScaleSettingsControl();
            startupSettings = new StartupSettingsControl();
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Font;
            DoubleBuffered = true;
            ClientSize = new Size(820, 860);

            titleBarFont = new Font(FluentTheme.BodyFont.FontFamily, 11f, FontStyle.Regular, GraphicsUnit.Point);
            titleBar = new GlassPanel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = FluentTheme.Canvas
            };
            titleBar.Paint += TitleBar_Paint;
            titleBar.MouseDown += TitleBar_MouseDown;
            titleBar.MouseMove += TitleBar_MouseMove;
            titleBar.MouseLeave += TitleBar_MouseLeave;
            titleBar.MouseClick += TitleBar_MouseClick;
            DropShadow.ApplyShadows(this);

            contentLayout = new GlassTableLayoutPanel
            {
                AutoSize = false,
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                Padding = new Padding(28, 8, 28, 28),
                RowCount = 7,
                AutoScroll = true
            };
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var i = 0; i < contentLayout.RowCount; i++)
                contentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contentLayout.Paint += ContentLayout_Paint;

            var pageHeader = new GlassTableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 1,
                Dock = DockStyle.Top,
                Margin = new Padding(0),
                Padding = new Padding(28, 16, 28, 12),
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
                Text = UiText.Get("Appearance")
            };
            notice = new Label
            {
                AutoSize = true,
                ForeColor = FluentTheme.SecondaryText,
                Margin = new Padding(0),
                Text = UiText.Get("These global appearance settings apply to all fences.")
            };
            pageHeader.Controls.Add(pageTitle, 0, 0);
            pageHeader.Controls.Add(notice, 0, 1);

            var themeCard = new SettingsSectionControl();
            var themeLayout = new CardFaceTableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 1,
                Dock = DockStyle.Top,
                Margin = new Padding(0),
                RowCount = 6
            };
            themeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var i = 0; i < themeLayout.RowCount; i++)
                themeLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var themeHeading = new Label
            {
                AutoSize = true,
                Font = FluentTheme.BoldBodyFont,
                Margin = new Padding(0, 0, 0, 10),
                Text = UiText.Get("Theme")
            };
            useSystemTheme = new CheckBox
            {
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 12),
                Text = UiText.Get("Use Windows display mode and theme color"),
                Checked = GlobalFenceSettings.UseSystemTheme
            };
            var manualThemeLayout = new CardFaceTableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 4,
                Dock = DockStyle.Top,
                Margin = new Padding(24, 0, 0, 0),
                RowCount = 1
            };
            manualThemeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            manualThemeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            manualThemeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            manualThemeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            var modeLabel = new Label
            {
                Anchor = AnchorStyles.Left,
                AutoSize = true,
                Margin = new Padding(0, 4, 8, 0),
                Text = UiText.Get("Display mode")
            };
            displayMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 16, 0)
            };
            displayMode.Items.AddRange(new object[] { UiText.Get("Light"), UiText.Get("Dark") });
            displayMode.SelectedIndex = GlobalFenceSettings.IsDisplayModeDark(GlobalFenceSettings.DisplayMode) ? 1 : 0;
            var accentLabel = new Label
            {
                Anchor = AnchorStyles.Left,
                AutoSize = true,
                Margin = new Padding(0, 4, 8, 0),
                Text = UiText.Get("Theme color")
            };
            customAccentColor = GlobalFenceSettings.CustomAccentColor;
            accentColorButton = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(104, 30),
                Margin = new Padding(0),
                Tag = "FluentAccentPicker"
            };
            UpdateAccentColorButton();
            accentColorButton.Click += AccentColorButton_Click;
            manualThemeLayout.Controls.Add(modeLabel, 0, 0);
            manualThemeLayout.Controls.Add(displayMode, 1, 0);
            manualThemeLayout.Controls.Add(accentLabel, 2, 0);
            manualThemeLayout.Controls.Add(accentColorButton, 3, 0);
            themeLayout.Controls.Add(themeHeading, 0, 0);
            themeLayout.Controls.Add(useSystemTheme, 0, 1);
            themeLayout.Controls.Add(manualThemeLayout, 0, 2);

            var overlayHeading = new Label
            {
                AutoSize = true,
                Font = FluentTheme.BoldBodyFont,
                Margin = new Padding(0, 16, 0, 8),
                Text = UiText.Get("Overlay intensity")
            };
            var darkOverlayRow = new CardFaceTableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 3,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 4),
                RowCount = 1
            };
            darkOverlayRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148));
            darkOverlayRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            darkOverlayRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            var darkOverlayLabel = new Label
            {
                Anchor = AnchorStyles.Left,
                AutoSize = true,
                Margin = new Padding(0),
                Text = UiText.Get("Dark mode")
            };
            darkOverlayOpacity = CreateOverlaySlider(GlobalFenceSettings.DarkOverlayOpacity);
            darkOverlayValue = CreateOverlayValueLabel(darkOverlayOpacity.Value);
            darkOverlayOpacity.ValueChanged += (sender, e) => UpdateOverlayValue(darkOverlayOpacity, darkOverlayValue);
            darkOverlayRow.Controls.Add(darkOverlayLabel, 0, 0);
            darkOverlayRow.Controls.Add(darkOverlayOpacity, 1, 0);
            darkOverlayRow.Controls.Add(darkOverlayValue, 2, 0);

            var lightOverlayRow = new CardFaceTableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 3,
                Dock = DockStyle.Top,
                Margin = new Padding(0),
                RowCount = 1
            };
            lightOverlayRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148));
            lightOverlayRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            lightOverlayRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            var lightOverlayLabel = new Label
            {
                Anchor = AnchorStyles.Left,
                AutoSize = true,
                Margin = new Padding(0),
                Text = UiText.Get("Light mode")
            };
            lightOverlayOpacity = CreateOverlaySlider(GlobalFenceSettings.LightOverlayOpacity);
            lightOverlayValue = CreateOverlayValueLabel(lightOverlayOpacity.Value);
            lightOverlayOpacity.ValueChanged += (sender, e) => UpdateOverlayValue(lightOverlayOpacity, lightOverlayValue);
            lightOverlayRow.Controls.Add(lightOverlayLabel, 0, 0);
            lightOverlayRow.Controls.Add(lightOverlayOpacity, 1, 0);
            lightOverlayRow.Controls.Add(lightOverlayValue, 2, 0);
            themeLayout.Controls.Add(overlayHeading, 0, 3);
            themeLayout.Controls.Add(darkOverlayRow, 0, 4);
            themeLayout.Controls.Add(lightOverlayRow, 0, 5);
            themeCard.Controls.Add(themeLayout);
            useSystemTheme.CheckedChanged += (sender, e) => UpdateManualThemeEnabled();
            UpdateManualThemeEnabled();

            var glowCard = new SettingsSectionControl();
            var glowLayout = new CardFaceTableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 1,
                Dock = DockStyle.Top,
                Margin = new Padding(0),
                RowCount = 9
            };
            glowLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var i = 0; i < glowLayout.RowCount; i++)
                glowLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var glowHeading = new Label
            {
                AutoSize = true,
                Font = FluentTheme.BoldBodyFont,
                Margin = new Padding(0, 0, 0, 10),
                Text = UiText.Get("Glow effects")
            };
            glowEffectsEnabled = new CheckBox
            {
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8),
                Text = UiText.Get("Enable pointer and icon glow"),
                Checked = GlobalFenceSettings.GlowEffectsEnabled
            };
            var pointerColorRow = new CardFaceTableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 4),
                RowCount = 1
            };
            pointerColorRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pointerColorRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var pointerColorLabel = new Label
            {
                Anchor = AnchorStyles.Left,
                AutoSize = true,
                Margin = new Padding(0, 4, 8, 4),
                Text = UiText.Get("Pointer highlight color")
            };
            pointerGlowColor = GlobalFenceSettings.PointerGlowColor;
            pointerGlowColorButton = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(104, 30),
                Margin = new Padding(0),
                Tag = "FluentGlowColorPicker"
            };
            pointerGlowColorButton.Click += PointerGlowColorButton_Click;
            UpdatePointerGlowColorButton();
            pointerColorRow.Controls.Add(pointerColorLabel, 0, 0);
            pointerColorRow.Controls.Add(pointerGlowColorButton, 1, 0);

            pointerGlowIntensity = CreateGlowSlider(0, 100, GlobalFenceSettings.PointerGlowIntensity, 10);
            pointerGlowIntensityValue = CreateGlowValueLabel(pointerGlowIntensity.Value, "%");
            pointerGlowIntensity.ValueChanged += (sender, e) =>
                UpdateGlowValue(pointerGlowIntensity, pointerGlowIntensityValue, "%");
            pointerGlowRadius = CreateGlowSlider(32, 128, GlobalFenceSettings.PointerGlowRadius, 16);
            pointerGlowRadiusValue = CreateGlowValueLabel(pointerGlowRadius.Value, " px");
            pointerGlowRadius.ValueChanged += (sender, e) =>
                UpdateGlowValue(pointerGlowRadius, pointerGlowRadiusValue, " px");
            iconGlowIntensity = CreateGlowSlider(0, 100, GlobalFenceSettings.IconGlowIntensity, 10);
            iconGlowIntensityValue = CreateGlowValueLabel(iconGlowIntensity.Value, "%");
            iconGlowIntensity.ValueChanged += (sender, e) =>
                UpdateGlowValue(iconGlowIntensity, iconGlowIntensityValue, "%");
            iconGlowRadius = CreateGlowSlider(2, 18, GlobalFenceSettings.IconGlowRadius, 2);
            iconGlowRadiusValue = CreateGlowValueLabel(iconGlowRadius.Value, " px");
            iconGlowRadius.ValueChanged += (sender, e) =>
                UpdateGlowValue(iconGlowRadius, iconGlowRadiusValue, " px");
            glassIntensity = CreateGlowSlider(0, 200, GlobalFenceSettings.GlassIntensity, 20);
            glassIntensityValue = CreateGlowValueLabel(glassIntensity.Value, "%");
            glassIntensity.ValueChanged += (sender, e) =>
                UpdateGlowValue(glassIntensity, glassIntensityValue, "%");
            glassTexture = CreateGlowSlider(0, 100, GlobalFenceSettings.GlassTexture, 10);
            glassTextureValue = CreateGlowValueLabel(glassTexture.Value, "%");
            glassTexture.ValueChanged += (sender, e) =>
                UpdateGlowValue(glassTexture, glassTextureValue, "%");
            glowLayout.Controls.Add(glowHeading, 0, 0);
            glowLayout.Controls.Add(glowEffectsEnabled, 0, 1);
            glowLayout.Controls.Add(pointerColorRow, 0, 2);
            glowLayout.Controls.Add(CreateGlowSliderRow(UiText.Get("Pointer highlight intensity"),
                pointerGlowIntensity, pointerGlowIntensityValue), 0, 3);
            glowLayout.Controls.Add(CreateGlowSliderRow(UiText.Get("Pointer highlight size"),
                pointerGlowRadius, pointerGlowRadiusValue), 0, 4);
            glowLayout.Controls.Add(CreateGlowSliderRow(UiText.Get("Icon glow intensity"),
                iconGlowIntensity, iconGlowIntensityValue), 0, 5);
            glowLayout.Controls.Add(CreateGlowSliderRow(UiText.Get("Icon glow spread"),
                iconGlowRadius, iconGlowRadiusValue), 0, 6);
            glowLayout.Controls.Add(CreateGlowSliderRow(UiText.Get("Glass lighting"),
                glassIntensity, glassIntensityValue), 0, 7);
            glowLayout.Controls.Add(CreateGlowSliderRow(UiText.Get("Glass texture"),
                glassTexture, glassTextureValue), 0, 8);
            glowCard.Controls.Add(glowLayout);
            glowEffectsEnabled.CheckedChanged += (sender, e) => UpdateGlowControlsEnabled();
            UpdateGlowControlsEnabled();

            var storageCard = new FenceStorageSettingsControl();
            storageCard.OpenFolderRequested += OpenFolderButton_Click;

            var fitActions = new GlassFlowLayoutPanel
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
                Tag = "FluentSecondaryText",
                Text = UiText.Get("Fit each fence to its current thumbnail columns and rows:")
            };
            var modifyAllButton = new GlassButton
            {
                AutoSize = true,
                MinimumSize = new Size(112, 34),
                Text = UiText.Get("Modify all")
            };
            modifyAllButton.Click += ModifyAllButton_Click;
            fitActions.Controls.Add(fitDescription);
            fitActions.Controls.Add(modifyAllButton);

            var settingActions = new GlassFlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                Margin = new Padding(0),
                WrapContents = false
            };
            var closeButton = new GlassButton
            {
                AutoSize = true,
                MinimumSize = new Size(96, 34),
                Margin = new Padding(0, 0, 8, 0),
                Text = UiText.Get("Close")
            };
            closeButton.Click += (sender, e) => Close();
            var applyButton = new GlassButton
            {
                AutoSize = true,
                MinimumSize = new Size(96, 34),
                Margin = new Padding(0),
                Text = UiText.Get("Apply")
            };
            primaryActionButton = applyButton;
            applyButton.Click += (sender, e) =>
            {
                try
                {
                    GlobalFenceSettings.Update(fenceTitleSettings.TitleHeight, fenceTitleSettings.HideTitleArea,
                        useSystemTheme.Checked, displayMode.SelectedIndex == 1 ? "Dark" : "Light",
                        customAccentColor.ToArgb(), darkOverlayOpacity.Value, lightOverlayOpacity.Value,
                        glowEffectsEnabled.Checked, pointerGlowColor.ToArgb(), pointerGlowIntensity.Value,
                        pointerGlowRadius.Value, iconGlowIntensity.Value, iconGlowRadius.Value,
                        contentScaleSettings.ScalePercent, glassIntensity.Value, glassTexture.Value);
                    StartupRegistration.SetEnabled(startupSettings.IsEnabled);
                    FluentTheme.ApplyToForm(this, applyButton);
                    UpdatePointerGlowColorButton();
                    storageCard.SetStatus(UiText.Get("Saved."));
                }
                catch (System.Configuration.ConfigurationErrorsException ex)
                {
                    MessageBox.Show(this, ex.Message, UiText.Get("Settings could not be saved"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (System.InvalidOperationException ex)
                {
                    MessageBox.Show(this, ex.Message, UiText.Get("Windows startup could not be updated"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (System.UnauthorizedAccessException ex)
                {
                    MessageBox.Show(this, ex.Message, UiText.Get("Windows startup could not be updated"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (System.IO.IOException ex)
                {
                    MessageBox.Show(this, ex.Message, UiText.Get("Windows startup could not be updated"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    MessageBox.Show(this, ex.Message, UiText.Get("Windows startup could not be updated"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            settingActions.Controls.Add(closeButton);
            settingActions.Controls.Add(applyButton);
            var footer = new GlassTableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 8, 0, 0),
                RowCount = 1
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var buildLabel = new Label
            {
                AutoSize = true,
                Margin = new Padding(0, 6, 0, 0),
                Tag = "FluentSecondaryText",
                Text = "Build " + System.IO.File.GetLastWriteTime(Application.ExecutablePath).ToString("yyyy-MM-dd HH:mm")
            };
            footer.Controls.Add(buildLabel, 0, 0);
            footer.Controls.Add(settingActions, 1, 0);

            contentLayout.Controls.Add(themeCard, 0, 0);
            contentLayout.Controls.Add(glowCard, 0, 1);
            contentLayout.Controls.Add(fenceTitleSettings, 0, 2);
            contentLayout.Controls.Add(contentScaleSettings, 0, 3);
            contentLayout.Controls.Add(startupSettings, 0, 4);
            contentLayout.Controls.Add(storageCard, 0, 5);
            contentLayout.Controls.Add(fitActions, 0, 6);
            contentLayout.SizeChanged += ContentLayout_SizeChanged;
            var settingsLayout = new GlassTableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                RowCount = 3
            };
            settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            settingsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            settingsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            settingsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            settingsLayout.Controls.Add(pageHeader, 0, 0);
            settingsLayout.Controls.Add(contentLayout, 0, 1);
            settingsLayout.Controls.Add(footer, 0, 2);
            Controls.Add(settingsLayout);
            Controls.Add(titleBar);
            fitActions.Tag = "FluentSurface";
            notice.Tag = "FluentSecondaryText";
            FluentTheme.ApplyToForm(this, applyButton);
            UpdatePointerGlowColorButton();
            ContentLayout_SizeChanged(contentLayout, EventArgs.Empty);
            GlobalFenceSettings.Changed += GlobalFenceSettings_Changed;
            FormClosed += (sender, e) => GlobalFenceSettings.Changed -= GlobalFenceSettings_Changed;
            FormClosed += (sender, e) =>
            {
                if (ReferenceEquals(currentInstance, this))
                    currentInstance = null;
            };

            AcceptButton = applyButton;
            CancelButton = closeButton;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            GlassSurface.Paint(e.Graphics, FluentTheme.Canvas);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowUtil.ApplyDwmRoundedCorners(Handle);
            ApplyWindowBorder();
            if (titleBar != null)
            {
                titleBar.Height = LogicalToDeviceUnits(48);
                UpdateCloseBounds();
            }
        }

        private void ApplyWindowBorder()
        {
            WindowUtil.ApplyBorderColor(Handle, FluentTheme.IsDark
                ? Color.FromArgb(58, 58, 62)
                : Color.FromArgb(214, 217, 222));
        }

        protected override void WndProc(ref Message m)
        {
            // The window is borderless and draws its own chrome; suppress the non-client area so
            // the extended frame used for the drop shadow cannot paint a caption strip.
            if (m.Msg == WindowUtil.WM_NCCALCSIZE)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            // The custom title bar reports HTCAPTION, so a double-click would otherwise maximize
            // a window that has no maximize state.
            if (m.Msg == WindowUtil.WM_SYSCOMMAND && (m.WParam.ToInt32() & 0xFFF0) == WindowUtil.SC_MAXIMIZE)
                return;

            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                titleBarFont?.Dispose();

            base.Dispose(disposing);
        }

        private void UpdateCloseBounds()
        {
            closeBounds = new Rectangle(titleBar.Width - titleBar.Height, 0, titleBar.Height, titleBar.Height);
        }

        private void TitleBar_MouseLeave(object sender, EventArgs e)
        {
            if (!closeHovered)
                return;

            closeHovered = false;
            titleBar.Cursor = Cursors.Default;
            titleBar.Invalidate();
        }

        private void TitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            var hovered = closeBounds.Contains(e.Location);
            if (hovered == closeHovered)
                return;

            closeHovered = hovered;
            titleBar.Cursor = hovered ? Cursors.Hand : Cursors.Default;
            titleBar.Invalidate();
        }

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || closeBounds.Contains(e.Location))
                return;

            WindowUtil.DragWindow(Handle);
        }

        private void TitleBar_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && closeBounds.Contains(e.Location))
                Close();
        }

        private void TitleBar_Paint(object sender, PaintEventArgs e)
        {
            UpdateCloseBounds();

            var graphics = e.Graphics;
            var bounds = new Rectangle(0, 0, titleBar.Width, titleBar.Height);
            graphics.Clear(FluentTheme.Canvas);

            var textHeight = titleBar.Height - 1;
            var x = LogicalToDeviceUnits(24);
            var productSize = TextRenderer.MeasureText("窗格", FluentTheme.BoldBodyFont);
            TextRenderer.DrawText(graphics, "窗格", FluentTheme.BoldBodyFont,
                new Rectangle(x, 0, productSize.Width, textHeight), FluentTheme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);

            x += productSize.Width + LogicalToDeviceUnits(10);
            var titleWidth = Math.Max(0, titleBar.Width - x - closeBounds.Width - LogicalToDeviceUnits(8));
            TextRenderer.DrawText(graphics, UiText.Get("Advanced settings"), titleBarFont,
                new Rectangle(x, 0, titleWidth, textHeight), FluentTheme.SecondaryText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding
                | TextFormatFlags.EndEllipsis);

            if (closeHovered)
            {
                using (var hoverBrush = new SolidBrush(Color.FromArgb(72, 196, 43, 28)))
                    graphics.FillRectangle(hoverBrush, closeBounds);
            }

            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var glyphPen = new Pen(FluentTheme.Text, 1.4f))
            {
                var centerX = closeBounds.Left + closeBounds.Width / 2;
                var centerY = closeBounds.Top + closeBounds.Height / 2;
                var half = LogicalToDeviceUnits(5);
                graphics.DrawLine(glyphPen, centerX - half, centerY - half, centerX + half, centerY + half);
                graphics.DrawLine(glyphPen, centerX - half, centerY + half, centerX + half, centerY - half);
            }
        }

        private void ContentLayout_Paint(object sender, PaintEventArgs e)
        {
            var radius = LogicalToDeviceUnits(14);
            foreach (Control control in contentLayout.Controls)
            {
                var card = control as SettingsSectionControl;
                if (card == null || !card.Visible)
                    continue;

                var bounds = card.Bounds;
                var shadow = new Rectangle(bounds.X, bounds.Y + LogicalToDeviceUnits(2),
                    bounds.Width, bounds.Height);
                using (var path = FenceRenderer.CreateRoundedRectangle(shadow, radius))
                using (var brush = new SolidBrush(Color.FromArgb(18, Color.Black)))
                    e.Graphics.FillPath(brush, path);
            }
        }

        private void UpdateManualThemeEnabled()
        {
            var enabled = !useSystemTheme.Checked;
            displayMode.Enabled = enabled;
            accentColorButton.Enabled = enabled;
        }

        private static TrackBar CreateOverlaySlider(int value)
        {
            return new TrackBar
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                AutoSize = false,
                Dock = DockStyle.Fill,
                LargeChange = 16,
                Maximum = 255,
                Minimum = 0,
                Margin = new Padding(0, 0, 8, 0),
                SmallChange = 8,
                TickFrequency = 32,
                Value = value
            };
        }

        private static TrackBar CreateGlowSlider(int minimum, int maximum, int value, int tickFrequency)
        {
            return new TrackBar
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                AutoSize = false,
                Dock = DockStyle.Fill,
                LargeChange = tickFrequency,
                Maximum = maximum,
                Minimum = minimum,
                Margin = new Padding(0, 0, 8, 0),
                SmallChange = Math.Max(1, tickFrequency / 2),
                TickFrequency = tickFrequency,
                Value = Math.Max(minimum, Math.Min(maximum, value))
            };
        }

        private static Label CreateGlowValueLabel(int value, string suffix)
        {
            return new Label
            {
                Anchor = AnchorStyles.Right,
                AutoSize = true,
                ForeColor = FluentTheme.SecondaryText,
                Margin = new Padding(0),
                Text = value + suffix
            };
        }

        private static TableLayoutPanel CreateGlowSliderRow(string labelText, TrackBar slider, Label valueLabel)
        {
            var row = new CardFaceTableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 3,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 4),
                RowCount = 1
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 188));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48));
            row.Controls.Add(new Label
            {
                Anchor = AnchorStyles.Left,
                AutoSize = true,
                Margin = new Padding(0),
                Text = labelText
            }, 0, 0);
            row.Controls.Add(slider, 1, 0);
            row.Controls.Add(valueLabel, 2, 0);
            return row;
        }

        private static void UpdateGlowValue(TrackBar slider, Label valueLabel, string suffix)
        {
            valueLabel.Text = slider.Value + suffix;
        }

        private void UpdateGlowControlsEnabled()
        {
            var enabled = glowEffectsEnabled.Checked;
            pointerGlowColorButton.Enabled = enabled;
            pointerGlowIntensity.Enabled = enabled;
            pointerGlowRadius.Enabled = enabled;
            iconGlowIntensity.Enabled = enabled;
            iconGlowRadius.Enabled = enabled;
        }

        private static Label CreateOverlayValueLabel(int opacity)
        {
            return new Label
            {
                Anchor = AnchorStyles.Right,
                AutoSize = true,
                ForeColor = FluentTheme.SecondaryText,
                Margin = new Padding(0),
                Text = OpacityToPercent(opacity) + "%"
            };
        }

        private static int OpacityToPercent(int opacity)
        {
            return (int)Math.Round(opacity * 100.0 / 255);
        }

        private static void UpdateOverlayValue(TrackBar slider, Label valueLabel)
        {
            valueLabel.Text = OpacityToPercent(slider.Value) + "%";
        }

        private void GlobalFenceSettings_Changed(object sender, EventArgs e)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                if (!IsHandleCreated)
                    return;
                BeginInvoke(new EventHandler(GlobalFenceSettings_Changed), sender, e);
                return;
            }

            FluentTheme.ApplyToForm(this, primaryActionButton);
            ApplyWindowBorder();
            Invalidate(true);
            UpdateAccentColorButton();
            UpdatePointerGlowColorButton();
        }

        private void UpdateAccentColorButton()
        {
            accentColorButton.Text = string.Format("#{0:X2}{1:X2}{2:X2}",
                customAccentColor.R, customAccentColor.G, customAccentColor.B);
            accentColorButton.BackColor = customAccentColor;
            accentColorButton.ForeColor = FluentTheme.GetContrastingText(customAccentColor);
        }

        private void AccentColorButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new ColorDialog
            {
                AllowFullOpen = true,
                AnyColor = true,
                Color = customAccentColor,
                FullOpen = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                customAccentColor = dialog.Color;
                UpdateAccentColorButton();
            }
        }

        private void UpdatePointerGlowColorButton()
        {
            pointerGlowColorButton.Text = string.Format("#{0:X2}{1:X2}{2:X2}",
                pointerGlowColor.R, pointerGlowColor.G, pointerGlowColor.B);
            pointerGlowColorButton.BackColor = pointerGlowColor;
            pointerGlowColorButton.ForeColor = FluentTheme.GetContrastingText(pointerGlowColor);
        }

        private void PointerGlowColorButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new ColorDialog
            {
                AllowFullOpen = true,
                AnyColor = true,
                Color = pointerGlowColor,
                FullOpen = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                pointerGlowColor = dialog.Color;
                UpdatePointerGlowColorButton();
            }
        }

        private void ContentLayout_SizeChanged(object sender, EventArgs e)
        {
            var availableWidth = Math.Max(100, contentLayout.ClientSize.Width - contentLayout.Padding.Horizontal);
            notice.MaximumSize = new Size(availableWidth, 0);
            fenceTitleSettings.SetAvailableWidth(availableWidth);
        }

        private void OpenFolderButton_Click(object sender, EventArgs e)
        {
            ShellLaunch.Open(FenceManager.Instance.DataDirectory);
        }

        private void ModifyAllButton_Click(object sender, EventArgs e)
        {
            var fences = Application.OpenForms.OfType<FenceWindow>().ToArray();
            if (fences.Length == 0)
            {
                MessageBox.Show(this, UiText.Get("There are no loaded fences to modify."), UiText.Get("Modify all fences"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirmation = MessageBox.Show(this,
                UiText.Get("Resize and save all fences to fit their current thumbnail columns and rows? Each fence's current column count will be preserved."),
                UiText.Get("Modify all fences"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
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
                catch (Exception ex) when (FenceViewModel.IsSaveError(ex))
                {
                    errors.AppendLine(fence.FenceName + ": " + ex.Message);
                }
            }

            if (errors.Length > 0)
            {
                MessageBox.Show(this,
                    string.Format(UiText.Get("Modified and saved {0} fence(s). These fences could not be saved:"), modifiedCount) +
                    Environment.NewLine + errors,
                    UiText.Get("Some fences could not be modified"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show(this, string.Format(UiText.Get("Modified and saved {0} fence(s)."), modifiedCount),
                    UiText.Get("Modify all fences"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
