using 窗格.Model;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace 窗格
{
    internal sealed class TrayIconController : IDisposable
    {
        private readonly ContextMenuStrip contextMenu;
        private readonly NotifyIcon notifyIcon;

        public TrayIconController()
        {
            contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("New fence", null, NewFence_Click);
            contextMenu.Items.Add("Advanced settings...", null, AdvancedSettings_Click);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, Exit_Click);

            notifyIcon = new NotifyIcon
            {
                ContextMenuStrip = contextMenu,
                Icon = SystemIcons.Application,
                Text = "窗格",
                Visible = true
            };
            notifyIcon.DoubleClick += AdvancedSettings_Click;
        }

        public void Dispose()
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            contextMenu.Dispose();
        }

        private static void NewFence_Click(object sender, EventArgs e)
        {
            FenceManager.Instance.CreateFence("New fence");
        }

        private static void AdvancedSettings_Click(object sender, EventArgs e)
        {
            using (var settings = new AdvancedSettingsForm())
            {
                settings.ShowDialog();
            }
        }

        private static void Exit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
    }
}
