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
    public partial class HeightDialog : Form
    {
        public int TitleHeight => trackBarTitleHeight.Value;

        public HeightDialog(int val)
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(460, 196);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var layout = new TableLayoutPanel
            {
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                Padding = new Padding(24),
                RowCount = 2
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            trackBarTitleHeight.Dock = DockStyle.Fill;
            trackBarTitleHeight.Margin = new Padding(0, 0, 0, 12);
            trackBarTitleHeight.TickFrequency = 10;
            labelTitleHeight.AutoSize = true;
            labelTitleHeight.Anchor = AnchorStyles.Right;
            labelTitleHeight.Margin = new Padding(0, 0, 0, 12);

            var sliderRow = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            sliderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            sliderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            sliderRow.Controls.Add(trackBarTitleHeight, 0, 0);
            sliderRow.Controls.Add(labelTitleHeight, 1, 0);

            btnRestore.Size = new Size(112, 34);
            btnRestore.Margin = new Padding(0);
            btnOk.Size = new Size(96, 34);
            btnOk.Margin = new Padding(8, 0, 0, 0);
            btnCancel.Size = new Size(96, 34);
            btnCancel.Margin = new Padding(8, 0, 0, 0);

            var rightActions = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Margin = new Padding(0),
                WrapContents = false
            };
            rightActions.Controls.Add(btnOk);
            rightActions.Controls.Add(btnCancel);

            var footer = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Controls.Add(btnRestore, 0, 0);
            footer.Controls.Add(rightActions, 1, 0);

            layout.Controls.Add(sliderRow, 0, 0);
            layout.Controls.Add(footer, 0, 1);
            Controls.Clear();
            Controls.Add(layout);
            FluentTheme.ApplyToForm(this, btnOk);

            trackBarTitleHeight.Value = val;
            UpdateText();
        }

        private void UpdateText()
        {
            labelTitleHeight.Text = trackBarTitleHeight.Value + "px";
        }

        private void trackBarTitleHeight_Scroll(object sender, EventArgs e)
        {
            UpdateText();
        }

        private void btnRestore_Click(object sender, EventArgs e)
        {
            trackBarTitleHeight.Value = 35;
            UpdateText();
        }

        private void HeightDialog_Load(object sender, EventArgs e)
        {

        }
    }
}
