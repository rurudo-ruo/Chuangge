using System;
using System.Drawing;
using System.Windows.Forms;

namespace 窗格
{
    internal static class FluentTheme
    {
        public static readonly Color Canvas = Color.FromArgb(248, 249, 252);
        public static readonly Color Surface = Color.White;
        public static readonly Color Text = Color.FromArgb(32, 31, 30);
        public static readonly Color SecondaryText = Color.FromArgb(96, 94, 92);
        public static readonly Color Border = Color.FromArgb(210, 208, 206);
        public static readonly Color Accent = Color.FromArgb(0, 120, 212);
        public static readonly Font BodyFont = new Font("Segoe UI", 9f, FontStyle.Regular);
        public static readonly Font HeadingFont = new Font("Segoe UI", 16f, FontStyle.Bold);

        public static void ApplyToForm(Form form, Button primaryButton = null)
        {
            form.BackColor = Canvas;
            form.ForeColor = Text;
            form.Font = BodyFont;
            ApplyToControls(form.Controls, primaryButton);
        }

        private static void ApplyToControls(Control.ControlCollection controls, Button primaryButton)
        {
            foreach (Control control in controls)
            {
                var tableLayout = control as TableLayoutPanel;
                var flowLayout = control as FlowLayoutPanel;
                if ((tableLayout != null || flowLayout != null)
                    && control.BackColor == SystemColors.Control
                    && control.Parent != null)
                {
                    control.BackColor = control.Parent.BackColor;
                }

                var button = control as Button;
                if (button != null)
                {
                    StyleButton(button, button == primaryButton);
                }

                var textBox = control as TextBoxBase;
                if (textBox != null)
                {
                    textBox.BackColor = Surface;
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

                var checkBox = control as CheckBox;
                if (checkBox != null)
                {
                    checkBox.BackColor = Color.Transparent;
                    checkBox.ForeColor = Text;
                    checkBox.FlatStyle = FlatStyle.System;
                }

                ApplyToControls(control.Controls, primaryButton);
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
                button.BackColor = Color.FromArgb(240, 240, 240);
                button.ForeColor = SecondaryText;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.MouseOverBackColor = button.BackColor;
                button.FlatAppearance.MouseDownBackColor = button.BackColor;
                return;
            }

            button.BackColor = primary ? Accent : Surface;
            button.ForeColor = primary ? Color.White : Text;
            button.FlatAppearance.BorderColor = primary ? Accent : Border;
            button.FlatAppearance.MouseOverBackColor = button.BackColor;
            button.FlatAppearance.MouseDownBackColor = button.BackColor;
        }
    }
}
