using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace 窗格
{
    public partial class EditDialog : Form
    {
        public EditDialog(string oldName)
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(440, 168);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var layout = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                RowCount = 3
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            lbName.Dock = DockStyle.Fill;
            lbName.Margin = new Padding(0, 0, 0, 6);
            tbName.Dock = DockStyle.Fill;
            tbName.Margin = new Padding(0, 0, 0, 16);
            btnOk.DialogResult = DialogResult.None;
            btnOk.Size = new Size(96, 34);
            btnOk.Margin = new Padding(8, 0, 0, 0);
            btnCancel.Size = new Size(96, 34);
            btnCancel.Margin = new Padding(0);

            var actions = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Margin = new Padding(0),
                WrapContents = false
            };
            actions.Controls.Add(btnOk);
            actions.Controls.Add(btnCancel);
            layout.Controls.Add(lbName, 0, 0);
            layout.Controls.Add(tbName, 0, 1);
            layout.Controls.Add(actions, 0, 2);

            Controls.Clear();
            Controls.Add(layout);
            FluentTheme.ApplyToForm(this, btnOk);
            tbName.Text = oldName;
            tbName.SelectAll();
        }

        public string NewName => tbName.Text;

        private void btnOk_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }
    }
}
