using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
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

        public static Bitmap CreateBlurredTint(Bitmap source, Color tint, int radius)
        {
            return CreateBlurredBitmap(source, tint, radius, 255);
        }

        public static Bitmap CreateBlurredColor(Bitmap source, int radius, int opacity)
        {
            return CreateBlurredBitmap(source, null, radius, Math.Max(0, Math.Min(255, opacity)));
        }

        private static Bitmap CreateBlurredBitmap(Bitmap source, Color? tint, int radius, int opacity)
        {
            radius = Math.Max(1, radius);
            var width = source.Width;
            var height = source.Height;
            var pixelCount = width * height;
            var sourcePixels = new byte[width * height * 4];
            var sourceData = source.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (var y = 0; y < height; y++)
                    Marshal.Copy(IntPtr.Add(sourceData.Scan0, y * sourceData.Stride),
                        sourcePixels, y * width * 4, width * 4);
            }
            finally
            {
                source.UnlockBits(sourceData);
            }

            var kernel = CreateBlurKernel(radius);
            var horizontal = new float[pixelCount * 4];
            var blurred = new float[pixelCount * 4];
            for (var i = 0; i < pixelCount; i++)
            {
                var alpha = sourcePixels[i * 4 + 3] / 255f;
                horizontal[i * 4] = sourcePixels[i * 4] * alpha;
                horizontal[i * 4 + 1] = sourcePixels[i * 4 + 1] * alpha;
                horizontal[i * 4 + 2] = sourcePixels[i * 4 + 2] * alpha;
                horizontal[i * 4 + 3] = sourcePixels[i * 4 + 3];
            }

            var premultipliedPixels = horizontal;
            horizontal = new float[pixelCount * 4];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = (y * width + x) * 4;
                    for (var channel = 0; channel < 4; channel++)
                    {
                        var value = 0f;
                        for (var k = -radius; k <= radius; k++)
                        {
                            var sampleX = x + k;
                            if (sampleX >= 0 && sampleX < width)
                                value += premultipliedPixels[(y * width + sampleX) * 4 + channel]
                                    * kernel[k + radius];
                        }
                        horizontal[index + channel] = value;
                    }
                }
            }

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = (y * width + x) * 4;
                    for (var channel = 0; channel < 4; channel++)
                    {
                        var value = 0f;
                        for (var k = -radius; k <= radius; k++)
                        {
                            var sampleY = y + k;
                            if (sampleY >= 0 && sampleY < height)
                                value += horizontal[(sampleY * width + x) * 4 + channel]
                                    * kernel[k + radius];
                        }
                        blurred[index + channel] = value;
                    }
                }
            }

            var tintedPixels = new byte[sourcePixels.Length];
            for (var i = 0; i < pixelCount; i++)
            {
                var alpha = blurred[i * 4 + 3];
                var outputAlpha = (byte)Math.Max(0, Math.Min(255, alpha * opacity / 255f));
                if (tint.HasValue)
                {
                    tintedPixels[i * 4] = tint.Value.B;
                    tintedPixels[i * 4 + 1] = tint.Value.G;
                    tintedPixels[i * 4 + 2] = tint.Value.R;
                    tintedPixels[i * 4 + 3] = (byte)(outputAlpha * tint.Value.A / 255);
                }
                else if (alpha > 0)
                {
                    tintedPixels[i * 4] = (byte)Math.Min(255, blurred[i * 4] * 255f / alpha);
                    tintedPixels[i * 4 + 1] = (byte)Math.Min(255, blurred[i * 4 + 1] * 255f / alpha);
                    tintedPixels[i * 4 + 2] = (byte)Math.Min(255, blurred[i * 4 + 2] * 255f / alpha);
                    tintedPixels[i * 4 + 3] = outputAlpha;
                }
            }

            var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var resultData = result.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (var y = 0; y < height; y++)
                    Marshal.Copy(tintedPixels, y * width * 4,
                        IntPtr.Add(resultData.Scan0, y * resultData.Stride), width * 4);
            }
            finally
            {
                result.UnlockBits(resultData);
            }

            return result;
        }

        private static float[] CreateBlurKernel(int radius)
        {
            var sigma = Math.Max(1f, radius / 2f);
            var kernel = new float[radius * 2 + 1];
            var total = 0f;
            for (var i = -radius; i <= radius; i++)
            {
                var value = (float)Math.Exp(-(i * i) / (2f * sigma * sigma));
                kernel[i + radius] = value;
                total += value;
            }

            for (var i = 0; i < kernel.Length; i++)
                kernel[i] /= total;
            return kernel;
        }

        public static Color GetContrastingText(Color color)
        {
            var luminance = (color.R * 299 + color.G * 587 + color.B * 114) / 1000;
            return luminance >= 150 ? Color.FromArgb(32, 31, 30) : Color.White;
        }
    }
}
