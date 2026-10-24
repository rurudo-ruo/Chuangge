using System.Drawing;
using System.Windows.Forms;

namespace 窗格
{
    internal class SettingsSectionControl : UserControl
    {
        public SettingsSectionControl()
        {
            AutoSize = true;
            BackColor = FluentTheme.Surface;
            Dock = DockStyle.Top;
            Margin = new Padding(0, 0, 0, 12);
            Padding = new Padding(16);
            Tag = "FluentSurface";
        }
    }
}
