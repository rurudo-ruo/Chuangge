using NoFences.Model;
using NoFences.Util;
using NoFences.Win32;
using Peter;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using static NoFences.Win32.WindowUtil;

namespace NoFences
{
    public partial class FenceWindow : Form
    {
        private int logicalTitleHeight;
        private int titleHeight;
        private const int logicalItemWidth = 75;
        private const int logicalIconSize = 32;
        private const int logicalTextHeight = 35;
        private const int logicalItemPadding = 15;
        private const float logicalShadowDistance = 1.5f;

        private readonly FenceInfo fenceInfo;

        private Font titleFont;
        private Font iconFont;

        private string selectedItem;
        private string hoveringItem;
        private bool shouldUpdateSelection;
        private bool shouldRunDoubleClick;
        private bool hasSelectionUpdated;
        private bool hasHoverUpdated;
        private bool isMinified;
        private int prevHeight;

        private int scrollHeight;
        private int scrollOffset;

        private readonly ThrottledExecution throttledMove = new ThrottledExecution(TimeSpan.FromSeconds(4));
        private readonly ThrottledExecution throttledResize = new ThrottledExecution(TimeSpan.FromSeconds(4));

        private readonly ShellContextMenu shellContextMenu = new ShellContextMenu();

        private readonly ThumbnailProvider thumbnailProvider = new ThumbnailProvider();
        private readonly ToolStripMenuItem locateMissingFileItem = new ToolStripMenuItem("Locate replacement file...");
        private readonly ToolStripMenuItem locateMissingFolderItem = new ToolStripMenuItem("Locate replacement folder...");
        private readonly ToolStripMenuItem openMissingParentItem = new ToolStripMenuItem("Open containing folder");
        private readonly ToolStripSeparator missingItemSeparator = new ToolStripSeparator();
        private string removeItemText;

        private void ReloadFonts()
        {
            var titleSize = logicalTitleHeight / 2.0f;
            var newTitleFont = new Font("Segoe UI", titleSize, FontStyle.Regular, GraphicsUnit.Point);
            var newIconFont = new Font("Segoe UI", 9, FontStyle.Regular, GraphicsUnit.Point);
            var oldTitleFont = titleFont;
            var oldIconFont = iconFont;
            titleFont = newTitleFont;
            iconFont = newIconFont;
            oldTitleFont?.Dispose();
            oldIconFont?.Dispose();
        }

        public FenceWindow(FenceInfo fenceInfo)
        {
            InitializeComponent();
            DropShadow.ApplyShadows(this);
            BlurUtil.EnableBlur(Handle);
            WindowUtil.HideFromAltTab(Handle);
            DesktopUtil.GlueToDesktop(Handle);
            //DesktopUtil.PreventMinimize(Handle);
            logicalTitleHeight = GlobalFenceSettings.TitleHeight;
            titleHeight = GlobalFenceSettings.HideTitleArea ? 0 : LogicalToDeviceUnits(logicalTitleHeight);
            
            this.MouseWheel += FenceWindow_MouseWheel;
            thumbnailProvider.IconThumbnailLoaded += ThumbnailProvider_IconThumbnailLoaded;

            ReloadFonts();

            AllowDrop = true;


            this.fenceInfo = fenceInfo;
            Text = fenceInfo.Name;
            Location = new Point(fenceInfo.PosX, fenceInfo.PosY);

            Width = fenceInfo.Width;
            Height = fenceInfo.Height;

            prevHeight = Height;
            lockedToolStripMenuItem.Checked = fenceInfo.Locked;
            minifyToolStripMenuItem.Checked = fenceInfo.CanMinify;
            minifyToolStripMenuItem.Enabled = !GlobalFenceSettings.HideTitleArea;
            titleSizeToolStripMenuItem.Text = "Global title size...";
            removeItemText = deleteItemToolStripMenuItem.Text;
            appContextMenu.Items.Insert(0, locateMissingFileItem);
            appContextMenu.Items.Insert(1, locateMissingFolderItem);
            appContextMenu.Items.Insert(2, openMissingParentItem);
            appContextMenu.Items.Insert(3, missingItemSeparator);
            locateMissingFileItem.Click += LocateMissingFileItem_Click;
            locateMissingFolderItem.Click += LocateMissingFolderItem_Click;
            openMissingParentItem.Click += OpenMissingParentItem_Click;
            GlobalFenceSettings.Changed += GlobalFenceSettings_Changed;
            Minify();
        }

        protected override void WndProc(ref Message m)
        {
            //Console.WriteLine(m.Msg.ToString("X4"));

            // Remove border
            if (m.Msg == 0x0083)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            // Mouse leave
            var myrect = new Rectangle(Location, Size);
            if (m.Msg == 0x02a2 && !myrect.IntersectsWith(new Rectangle(MousePosition, new Size(1, 1))))
            {
                Minify();
            }

            // Prevent maximize
            if ((m.Msg == WM_SYSCOMMAND) && m.WParam.ToInt32() == 0xF032)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            // Prevent foreground
            if (m.Msg == WM_SETFOCUS)
            {
                SetWindowPos(Handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
                return;
            }

            // Other messages
            base.WndProc(ref m);

            // If not locked and using the left mouse button
            if (MouseButtons == MouseButtons.Right || lockedToolStripMenuItem.Checked)
                return;

            if (m.Msg == WM_NCHITTEST
                && GlobalFenceSettings.HideTitleArea
                && ModifierKeys.HasFlag(Keys.Alt)
                && (int)m.Result == HTCLIENT)
            {
                m.Result = (IntPtr)HTCAPTION;
                return;
            }

            // Then, allow dragging and resizing
            if (m.Msg == WM_NCHITTEST)
            {

                var pt = PointToClient(new Point(m.LParam.ToInt32()));

                if ((int)m.Result == HTCLIENT && pt.Y < titleHeight)     // drag the form
                {
                    m.Result = (IntPtr)HTCAPTION;
                    FenceWindow_MouseEnter(null, null);
                }

                var resizeBorder = LogicalToDeviceUnits(10);
                if (pt.X < resizeBorder && pt.Y < resizeBorder)
                    m.Result = new IntPtr(HTTOPLEFT);
                else if (pt.X > (Width - resizeBorder) && pt.Y < resizeBorder)
                    m.Result = new IntPtr(HTTOPRIGHT);
                else if (pt.X < resizeBorder && pt.Y > (Height - resizeBorder))
                    m.Result = new IntPtr(HTBOTTOMLEFT);
                else if (pt.X > (Width - resizeBorder) && pt.Y > (Height - resizeBorder))
                    m.Result = new IntPtr(HTBOTTOMRIGHT);
                else if (pt.Y > (Height - resizeBorder))
                    m.Result = new IntPtr(HTBOTTOM);
                else if (pt.X < resizeBorder)
                    m.Result = new IntPtr(HTLEFT);
                else if (pt.X > (Width - resizeBorder))
                    m.Result = new IntPtr(HTRIGHT);
            }
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            var wasMinified = isMinified;
            base.OnDpiChanged(e);

            titleHeight = GlobalFenceSettings.HideTitleArea ? 0 : LogicalToDeviceUnits(logicalTitleHeight);
            ReloadFonts();

            var dpiScale = (double)e.DeviceDpiNew / e.DeviceDpiOld;
            scrollOffset = (int)Math.Round(scrollOffset * dpiScale);
            if (wasMinified)
            {
                prevHeight = (int)Math.Round(prevHeight * dpiScale);
                Height = titleHeight;
            }
            else
            {
                prevHeight = Height;
            }

            Invalidate();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            GlobalFenceSettings.Changed -= GlobalFenceSettings_Changed;
            titleFont?.Dispose();
            iconFont?.Dispose();
            base.OnFormClosed(e);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, "Really remove this fence?", "Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                FenceManager.Instance.RemoveFence(fenceInfo);
                Close();
            }
        }

        private void deleteItemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            fenceInfo.Files.Remove(hoveringItem);
            hoveringItem = null;
            Save();
            Refresh();
        }

        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var missingItem = hoveringItem != null && !ItemExists(hoveringItem);
            locateMissingFileItem.Visible = missingItem;
            locateMissingFolderItem.Visible = missingItem;
            openMissingParentItem.Visible = missingItem;
            missingItemSeparator.Visible = missingItem;
            deleteItemToolStripMenuItem.Visible = hoveringItem != null;
            deleteItemToolStripMenuItem.Text = missingItem ? "Remove missing item" : removeItemText;
            openMissingParentItem.Enabled = missingItem && GetExistingParentDirectory(hoveringItem) != null;
        }

        private void FenceWindow_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && !lockedToolStripMenuItem.Checked)
                e.Effect = DragDropEffects.Move;
        }

        private void FenceWindow_DragDrop(object sender, DragEventArgs e)
        {
            var dropped = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (var file in dropped)
                if (!fenceInfo.Files.Contains(file) && ItemExists(file))
                    fenceInfo.Files.Add(file);
            Save();
            Refresh();
        }

        private void FenceWindow_Resize(object sender, EventArgs e)
        {
            throttledResize.Run(() =>
            {
                fenceInfo.Width = Width;
                fenceInfo.Height = isMinified ? prevHeight : Height;
                Save();
            });

            Refresh();
        }

        private void FenceWindow_MouseMove(object sender, MouseEventArgs e)
        {
            Refresh();
        }

        private void FenceWindow_MouseEnter(object sender, EventArgs e)
        {
            if (minifyToolStripMenuItem.Checked && isMinified)
            {
                isMinified = false;
                Height = prevHeight;
            }
        }

        private void FenceWindow_MouseLeave(object sender, EventArgs e)
        {
            Minify();
            selectedItem = null;
            Refresh();
        }

        private void Minify()
        {
            if (!GlobalFenceSettings.HideTitleArea && minifyToolStripMenuItem.Checked && !isMinified)
            {
                isMinified = true;
                prevHeight = Height;
                Height = titleHeight;
                Refresh();
            }
        }

        private void minifyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (isMinified)
            {
                Height = prevHeight;
                isMinified = false;
            }
            fenceInfo.CanMinify = minifyToolStripMenuItem.Checked;
            Save();

        }

        private void FenceWindow_Click(object sender, EventArgs e)
        {
            shouldUpdateSelection = true;
            Refresh();
        }

        private void FenceWindow_DoubleClick(object sender, EventArgs e)
        {
            shouldRunDoubleClick = true;
            Refresh();
        }

        private void FenceWindow_Paint(object sender, PaintEventArgs e)
        {
            var graphics = e.Graphics;
            graphics.SetClip(ClientRectangle);
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using (var backgroundBrush = new SolidBrush(Color.FromArgb(100, Color.Black)))
            using (var titleBrush = new SolidBrush(Color.FromArgb(50, Color.Black)))
            using (var titleFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            {
                graphics.FillRectangle(backgroundBrush, ClientRectangle);
                if (!GlobalFenceSettings.HideTitleArea)
                {
                    graphics.FillRectangle(titleBrush, new Rectangle(0, 0, Width, titleHeight));
                    graphics.DrawString(Text, titleFont, Brushes.White,
                        new RectangleF(0, 0, Width, titleHeight), titleFormat);
                }
            }

            var itemPadding = LogicalToDeviceUnits(logicalItemPadding);
            var itemWidth = LogicalToDeviceUnits(logicalItemWidth);
            var iconSize = LogicalToDeviceUnits(logicalIconSize);
            var textHeight = LogicalToDeviceUnits(logicalTextHeight);
            var itemHeight = iconSize + itemPadding + textHeight;
            var x = itemPadding;
            var y = itemPadding;
            scrollHeight = 0;
            graphics.SetClip(new Rectangle(0, titleHeight, Width, Math.Max(0, Height - titleHeight)));
            foreach (var file in fenceInfo.Files)
            {
                var entry = FenceEntry.FromPath(file);
                if (entry == null)
                    continue;

                RenderEntry(graphics, entry, x, y + titleHeight - scrollOffset, itemWidth, textHeight, iconSize);

                var itemBottom = y + itemHeight;
                if (itemBottom > scrollHeight)
                    scrollHeight = itemBottom;

                x += itemWidth + itemPadding;
                if (x + itemWidth > Width)
                {
                    x = itemPadding;
                    y += itemHeight + itemPadding;
                }
            }

            scrollHeight -= (ClientRectangle.Height - titleHeight);

            // Scroll bars
            if (scrollHeight > 0)
            {
                var contentHeight = Height - titleHeight;
                var scrollbarHeight = contentHeight - scrollHeight;
                using (var scrollBrush = new SolidBrush(Color.FromArgb(150, Color.Black)))
                {
                    var scrollWidth = LogicalToDeviceUnits(5);
                    graphics.FillRectangle(scrollBrush,
                        new Rectangle(Width - scrollWidth, titleHeight + scrollOffset, scrollWidth, scrollbarHeight));
                }

                scrollOffset = Math.Min(scrollOffset, scrollHeight);
            }



            // Click handlers
            if (shouldUpdateSelection && !hasSelectionUpdated)
                selectedItem = null;

            if (!hasHoverUpdated)
                hoveringItem = null;

            shouldRunDoubleClick = false;
            shouldUpdateSelection = false;
            hasSelectionUpdated = false;
            hasHoverUpdated = false;
        }

        private void RenderEntry(Graphics g, FenceEntry entry, int x, int y, int itemWidth, int textHeight, int iconSize)
        {
            var icon = entry.ExtractIcon(thumbnailProvider, iconSize);
            var name = entry.Name;

            var textPosition = new PointF(x, y + iconSize + LogicalToDeviceUnits(5));
            var textMaxSize = new SizeF(itemWidth, textHeight);

            using (var stringFormat = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            using (var shadowBrush = new SolidBrush(Color.FromArgb(180, 15, 15, 15)))
            using (var selectionPen = new Pen(Color.FromArgb(120, SystemColors.ActiveBorder)))
            using (var selectedHoverBrush = new SolidBrush(Color.FromArgb(100, SystemColors.GradientActiveCaption)))
            using (var selectedBrush = new SolidBrush(Color.FromArgb(80, SystemColors.GradientInactiveCaption)))
            using (var hoverBrush = new SolidBrush(Color.FromArgb(80, SystemColors.ActiveCaption)))
            {
                var textSize = g.MeasureString(name, iconFont, textMaxSize, stringFormat);
                var borderWidth = LogicalToDeviceUnits(2);
                var outlineRect = new Rectangle(x - borderWidth, y - borderWidth,
                    itemWidth + borderWidth, iconSize + (int)textSize.Height + LogicalToDeviceUnits(7));
                var outlineRectInner = outlineRect.Shrink(LogicalToDeviceUnits(1));

                var mousePos = PointToClient(MousePosition);
                var mouseOver = mousePos.X >= x && mousePos.Y >= y
                    && mousePos.X < x + outlineRect.Width && mousePos.Y < y + outlineRect.Height;

                if (mouseOver)
                {
                    hoveringItem = entry.Path;
                    hasHoverUpdated = true;
                }

                if (mouseOver && shouldUpdateSelection)
                {
                    selectedItem = entry.Path;
                    shouldUpdateSelection = false;
                    hasSelectionUpdated = true;
                }

                if (mouseOver && shouldRunDoubleClick)
                {
                    shouldRunDoubleClick = false;
                    entry.Open();
                }

                if (selectedItem == entry.Path)
                {
                    if (mouseOver)
                    {
                        g.DrawRectangle(selectionPen, outlineRectInner);
                        g.FillRectangle(selectedHoverBrush, outlineRect);
                    }
                    else
                    {
                        g.DrawRectangle(selectionPen, outlineRectInner);
                        g.FillRectangle(selectedBrush, outlineRect);
                    }
                }
                else if (mouseOver)
                {
                    g.DrawRectangle(selectionPen, outlineRectInner);
                    g.FillRectangle(hoverBrush, outlineRect);
                }

                g.DrawIcon(icon, new Rectangle(x + (itemWidth - iconSize) / 2, y, iconSize, iconSize));
                var shadowDistance = LogicalToDeviceUnits(3) * (logicalShadowDistance / 3);
                g.DrawString(name, iconFont, shadowBrush,
                    new RectangleF(textPosition.Move(shadowDistance, shadowDistance), textMaxSize), stringFormat);
                g.DrawString(name, iconFont, Brushes.White, new RectangleF(textPosition, textMaxSize), stringFormat);
            }
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dialog = new EditDialog(Text);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                Text = dialog.NewName;
                fenceInfo.Name = Text;
                Refresh();
                Save();
            }
        }

        private void newFenceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FenceManager.Instance.CreateFence("New fence");
        }

        private void FenceWindow_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (Application.OpenForms.Count == 0)
                Application.Exit();
        }

        private readonly object saveLock = new object();
        private void Save()
        {
            lock (saveLock)
            {
                FenceManager.Instance.UpdateFence(fenceInfo);
            }
        }

        private void FenceWindow_LocationChanged(object sender, EventArgs e)
        {
            throttledMove.Run(() =>
            {
                fenceInfo.PosX = Location.X;
                fenceInfo.PosY = Location.Y;
                Save();
            });
        }

        private void lockedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            fenceInfo.Locked = lockedToolStripMenuItem.Checked;
            Save();
        }

        private void FenceWindow_Load(object sender, EventArgs e)
        {

        }

        private void titleSizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new AdvancedSettingsForm())
            {
                dialog.ShowDialog(this);
            }
        }

        private void FenceWindow_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            if (hoveringItem != null && !ModifierKeys.HasFlag(Keys.Shift))
            {
                if (ItemExists(hoveringItem))
                    shellContextMenu.ShowContextMenu(new[] { new FileInfo(hoveringItem) }, MousePosition);
                else
                    appContextMenu.Show(this, e.Location);
            }
            else
            {
                appContextMenu.Show(this, e.Location);
            }
        }

        private void FenceWindow_MouseWheel(object sender, MouseEventArgs e)
        {
            if (scrollHeight < 1)
                return;

            scrollOffset -= Math.Sign(e.Delta) * 10;
            if (scrollOffset < 0)
                scrollOffset = 0;
            if (scrollOffset > scrollHeight)
                scrollOffset = scrollHeight;

            Invalidate();
        }

        private void ThumbnailProvider_IconThumbnailLoaded(object sender, EventArgs e)
        {
            Invalidate();
        }

        private bool ItemExists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        private void GlobalFenceSettings_Changed(object sender, EventArgs e)
        {
            var wasMinified = isMinified;
            if (wasMinified && GlobalFenceSettings.HideTitleArea)
            {
                isMinified = false;
                Height = prevHeight;
            }

            logicalTitleHeight = GlobalFenceSettings.TitleHeight;
            titleHeight = GlobalFenceSettings.HideTitleArea ? 0 : LogicalToDeviceUnits(logicalTitleHeight);
            minifyToolStripMenuItem.Enabled = !GlobalFenceSettings.HideTitleArea;
            ReloadFonts();

            if (wasMinified && !GlobalFenceSettings.HideTitleArea)
                Height = titleHeight;

            Invalidate();
        }

        private void LocateMissingFileItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { CheckFileExists = true, Title = "Locate replacement file" })
            {
                var directory = GetExistingParentDirectory(hoveringItem);
                if (directory != null)
                    dialog.InitialDirectory = directory;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                    ReplaceMissingItem(dialog.FileName);
            }
        }

        private void LocateMissingFolderItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog { Description = "Select the replacement folder" })
            {
                var directory = GetExistingParentDirectory(hoveringItem);
                if (directory != null)
                    dialog.SelectedPath = directory;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                    ReplaceMissingItem(dialog.SelectedPath);
            }
        }

        private void OpenMissingParentItem_Click(object sender, EventArgs e)
        {
            var directory = GetExistingParentDirectory(hoveringItem);
            if (directory == null)
                return;

            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + directory + "\"") { UseShellExecute = true });
            }
            catch (Win32Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Could not open containing folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ReplaceMissingItem(string replacementPath)
        {
            if (!ItemExists(replacementPath))
            {
                MessageBox.Show(this, "The selected file or folder no longer exists.", "Cannot update fence item",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (fenceInfo.Files.Exists(path => string.Equals(path, replacementPath, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "That file or folder is already in this fence.", "Cannot update fence item",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var index = fenceInfo.Files.IndexOf(hoveringItem);
            if (index < 0)
                return;

            var previousPath = fenceInfo.Files[index];
            fenceInfo.Files[index] = replacementPath;
            try
            {
                Save();
            }
            catch (IOException ex)
            {
                fenceInfo.Files[index] = previousPath;
                MessageBox.Show(this, ex.Message, "Could not save fence item", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                fenceInfo.Files[index] = previousPath;
                MessageBox.Show(this, ex.Message, "Could not save fence item", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            selectedItem = replacementPath;
            hoveringItem = replacementPath;
            Invalidate();
        }

        private static string GetExistingParentDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            return directory != null && Directory.Exists(directory) ? directory : null;
        }
    }

}
