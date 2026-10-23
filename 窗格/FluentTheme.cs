using System;
using System.Drawing;
using System.Windows.Forms;

namespace 窗格
{
    internal static class FluentTheme
    {
        private static readonly Color LightCanvas = Color.FromArgb(248, 249, 252);
        private static readonly Color LightSurface = Color.White;
        private static readonly Color LightText = Color.FromArgb(32, 31, 30);
        private static readonly Color LightSecondaryText = Color.FromArgb(96, 94, 92);
        private static readonly Color LightBorder = Color.FromArgb(210, 208, 206);
        private static readonly Color DarkCanvas = Color.FromArgb(32, 32, 32);
        private static readonly Color DarkSurface = Color.FromArgb(44, 44, 44);
        private static readonly Color DarkText = Color.FromArgb(243, 243, 243);
        private static readonly Color DarkSecondaryText = Color.FromArgb(200, 200, 200);
        private static readonly Color DarkBorder = Color.FromArgb(80, 80, 80);
        private static Color accent = Color.FromArgb(0, 120, 212);
        public static readonly Font BodyFont = new Font("Segoe UI", 9f, FontStyle.Regular);
        public static readonly Font HeadingFont = new Font("Segoe UI", 16f, FontStyle.Bold);

        public static bool IsDark { get; private set; }
        public static Color Canvas => IsDark ? DarkCanvas : LightCanvas;
        public static Color Surface => IsDark ? DarkSurface : LightSurface;
        public static Color Text => IsDark ? DarkText : LightText;
        public static Color SecondaryText => IsDark ? DarkSecondaryText : LightSecondaryText;
        public static Color Border => IsDark ? DarkBorder : LightBorder;
        public static Color Accent => accent;

        public static void Configure(bool isDark, Color accentColor)
        {
            IsDark = isDark;
            accent = Color.FromArgb(255, accentColor.R, accentColor.G, accentColor.B);
        }

        public static void ApplyToForm(Form form, Button primaryButton = null)
        {
            form.BackColor = Canvas;
            form.ForeColor = Text;
            form.Font = BodyFont;
            ApplyToControls(form.Controls, primaryButton, form.BackColor);
        }

        private static void ApplyToControls(Control.ControlCollection controls, Button primaryButton, Color inheritedBackColor)
        {
            foreach (Control control in controls)
            {
                var tableLayout = control as TableLayoutPanel;
                var flowLayout = control as FlowLayoutPanel;
                if (tableLayout != null || flowLayout != null)
                {
                    control.BackColor = inheritedBackColor;
                }

                var isSurface = string.Equals(control.Tag as string, "FluentSurface", StringComparison.Ordinal);
                if (isSurface)
                    control.BackColor = Surface;

                var button = control as Button;
                if (button != null)
                {
                    StyleButton(button, button == primaryButton);
                }

                var textBox = control as TextBoxBase;
                if (textBox != null)
                {
                    textBox.BackColor = IsDark ? Color.FromArgb(36, 36, 36) : Surface;
                    textBox.ForeColor = Text;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                }

                var numericUpDown = control as NumericUpDown;
                if (numericUpDown != null)
                {
                    numericUpDown.BackColor = Surface;
                    numericUpDown.ForeColor = Text;
                    numericUpDown.BorderStyle = BorderStyle.FixedSingle;
                }

                var comboBox = control as ComboBox;
                if (comboBox != null)
                {
                    comboBox.BackColor = Surface;
                    comboBox.ForeColor = Text;
                    comboBox.FlatStyle = FlatStyle.Flat;
                }

                var checkBox = control as CheckBox;
                if (checkBox != null)
                {
                    checkBox.BackColor = inheritedBackColor;
                    checkBox.ForeColor = Text;
                    checkBox.FlatStyle = FlatStyle.System;
                }

                var label = control as Label;
                if (label != null && label.Tag is string && (string)label.Tag == "FluentSecondaryText")
                    label.ForeColor = SecondaryText;
                else if (label != null)
                    label.ForeColor = Text;

                var childBackColor = isSurface ? Surface : control.BackColor;
                ApplyToControls(control.Controls, primaryButton, childBackColor);
            }
        }

        public static void StyleButton(Button button, bool primary)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.FlatAppearance.BorderSize = 1;
            button.Padding = new Padding(12, 5, 12, 5);
            button.Cursor = Cursors.Default;

            if (!button.Enabled)
            {
                button.BackColor = IsDark ? Color.FromArgb(56, 56, 56) : Color.FromArgb(240, 240, 240);
                button.ForeColor = SecondaryText;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.MouseOverBackColor = button.BackColor;
                button.FlatAppearance.MouseDownBackColor = button.BackColor;
                return;
            }

            button.BackColor = primary ? Accent : Surface;
            button.ForeColor = primary ? GetContrastingText(Accent) : Text;
            button.FlatAppearance.BorderColor = primary ? Accent : Border;
            button.FlatAppearance.MouseOverBackColor = button.BackColor;
            button.FlatAppearance.MouseDownBackColor = button.BackColor;
        }

        public static Color AccentOverlay(int alpha)
        {
            return Color.FromArgb(alpha, Accent.R, Accent.G, Accent.B);
        }

        public static Color GetContrastingText(Color color)
        {
            var luminance = (color.R * 299 + color.G * 587 + color.B * 114) / 1000;
            return luminance >= 150 ? Color.FromArgb(32, 31, 30) : Color.White;
        }
    }
}
