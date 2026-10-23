using 窗格.Model;
using 窗格.Util;
using 窗格.Win32;
using Peter;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using static 窗格.Win32.WindowUtil;

namespace 窗格
{
    public partial class FenceWindow : Form
    {
        private int logicalTitleHeight;
        private int titleHeight;
        private const int logicalItemWidth = 88;
        private const int logicalIconSize = 32;
        private const int logicalTextHeight = 36;
        private const int logicalItemPadding = 12;

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
        private Point thumbnailMouseDown;
        private string thumbnailDragPath;
        private int thumbnailDragIndex = -1;
        private bool isDraggingThumbnail;
        private bool suppressNextClick;
        private int dragInsertionIndex = -1;
        private Point pointerPosition;
        private bool pointerInside;

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

        private sealed class FenceItemDragData
        {
            public FenceWindow Source { get; set; }
            public string Path { get; set; }
            public int Index { get; set; }
        }

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
            appContextMenu.RenderMode = ToolStripRenderMode.System;
            appContextMenu.Font = FluentTheme.BodyFont;
            foreach (ToolStripItem item in appContextMenu.Items)
                item.Padding = new Padding(10, 5, 10, 5);
            appContextMenu.Opened += AppContextMenu_Opened;
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
            thumbnailProvider.IconThumbnailLoaded -= ThumbnailProvider_IconThumbnailLoaded;
            thumbnailProvider.Dispose();
            titleFont?.Dispose();
            iconFont?.Dispose();
            base.OnFormClosed(e);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this,
                "Really remove this fence and its saved copies, including recovery backups?",
                "Remove fence", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    FenceManager.Instance.RemoveFence(fenceInfo);
                    Close();
                }
                catch (IOException ex)
                {
                    MessageBox.Show(this, "Could not remove all saved copies of this fence. It may reappear when 窗格 starts again." +
                        Environment.NewLine + ex.Message, "Fence could not be removed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (UnauthorizedAccessException ex)
                {
                    MessageBox.Show(this, "Could not remove all saved copies of this fence. It may reappear when 窗格 starts again." +
                        Environment.NewLine + ex.Message, "Fence could not be removed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void deleteItemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var itemIndex = fenceInfo.Files.IndexOf(hoveringItem);
            if (itemIndex < 0)
                return;

            var removedItem = fenceInfo.Files[itemIndex];
            fenceInfo.Files.RemoveAt(itemIndex);
            try
            {
                Save();
            }
            catch (IOException ex)
            {
                fenceInfo.Files.Insert(itemIndex, removedItem);
                ShowItemSaveError(ex);
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                fenceInfo.Files.Insert(itemIndex, removedItem);
                ShowItemSaveError(ex);
                return;
            }
            catch (InvalidOperationException ex)
            {
                fenceInfo.Files.Insert(itemIndex, removedItem);
                ShowItemSaveError(ex);
                return;
            }

            hoveringItem = null;
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

        private void AppContextMenu_Opened(object sender, EventArgs e)
        {
            WindowUtil.ApplyRoundedCorners(appContextMenu.Handle, appContextMenu.Width,
                appContextMenu.Height, LogicalToDeviceUnits(8));
        }

        private void FenceWindow_DragEnter(object sender, DragEventArgs e)
        {
            if (lockedToolStripMenuItem.Checked)
                return;

            if (e.Data.GetDataPresent(typeof(FenceItemDragData)))
                e.Effect = DragDropEffects.Move;
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void FenceWindow_DragOver(object sender, DragEventArgs e)
        {
            if (lockedToolStripMenuItem.Checked)
            {
                e.Effect = DragDropEffects.None;
                ClearDragInsertionMarker();
                return;
            }

            if (e.Data.GetDataPresent(typeof(FenceItemDragData)))
            {
                e.Effect = DragDropEffects.Move;
                dragInsertionIndex = GetDropInsertionIndex(PointToClient(new Point(e.X, e.Y)));
                Invalidate();
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void FenceWindow_DragLeave(object sender, EventArgs e)
        {
            ClearDragInsertionMarker();
        }

        private void FenceWindow_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (lockedToolStripMenuItem.Checked)
                    return;

                var itemDrag = e.Data.GetData(typeof(FenceItemDragData)) as FenceItemDragData;
                if (itemDrag != null)
                {
                    MoveFenceItem(itemDrag, GetDropInsertionIndex(PointToClient(new Point(e.X, e.Y))));
                    return;
                }

                var dropped = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (dropped == null)
                    return;

                var addedPaths = new System.Collections.Generic.List<string>();
                foreach (var file in dropped)
                {
                    if (!ItemExists(file) || fenceInfo.Files.Exists(path =>
                        string.Equals(path, file, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    fenceInfo.Files.Add(file);
                    addedPaths.Add(file);
                }

                if (addedPaths.Count > 0)
                {
                    try
                    {
                        Save();
                    }
                    catch (IOException ex)
                    {
                        RemoveDroppedItems(addedPaths);
                        MessageBox.Show(this, ex.Message, "Could not save fence items", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        RemoveDroppedItems(addedPaths);
                        MessageBox.Show(this, ex.Message, "Could not save fence items", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    catch (InvalidOperationException ex)
                    {
                        RemoveDroppedItems(addedPaths);
                        MessageBox.Show(this, ex.Message, "Could not save fence items", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            finally
            {
                ClearDragInsertionMarker();
                Invalidate();
            }
        }

        private void RemoveDroppedItems(System.Collections.Generic.List<string> paths)
        {
            foreach (var path in paths)
                fenceInfo.Files.Remove(path);
        }

        private void MoveFenceItem(FenceItemDragData dragData, int insertionIndex)
        {
            var source = dragData.Source;
            var sourceIndex = dragData.Index >= 0 && dragData.Index < source.fenceInfo.Files.Count
                && string.Equals(source.fenceInfo.Files[dragData.Index], dragData.Path, StringComparison.OrdinalIgnoreCase)
                ? dragData.Index
                : source.fenceInfo.Files.FindIndex(path =>
                    string.Equals(path, dragData.Path, StringComparison.OrdinalIgnoreCase));
            if (sourceIndex < 0)
                return;

            insertionIndex = Math.Max(0, Math.Min(insertionIndex, fenceInfo.Files.Count));
            if (source == this)
            {
                var originalFiles = new System.Collections.Generic.List<string>(fenceInfo.Files);
                var targetIndex = insertionIndex;
                if (sourceIndex < targetIndex)
                    targetIndex--;

                if (sourceIndex == targetIndex)
                    return;

                var path = fenceInfo.Files[sourceIndex];
                fenceInfo.Files.RemoveAt(sourceIndex);
                fenceInfo.Files.Insert(Math.Max(0, Math.Min(targetIndex, fenceInfo.Files.Count)), path);
                try
                {
                    Save();
                    selectedItem = path;
                }
                catch (IOException ex)
                {
                    RestoreFileOrder(originalFiles);
                    ShowItemSaveError(ex);
                }
                catch (UnauthorizedAccessException ex)
                {
                    RestoreFileOrder(originalFiles);
                    ShowItemSaveError(ex);
                }
                catch (InvalidOperationException ex)
                {
                    RestoreFileOrder(originalFiles);
                    ShowItemSaveError(ex);
                }

                Invalidate();
                return;
            }

            if (fenceInfo.Files.Exists(path =>
                string.Equals(path, dragData.Path, StringComparison.OrdinalIgnoreCase)))
                return;

            var originalSourceIndex = sourceIndex;
            fenceInfo.Files.Insert(insertionIndex, dragData.Path);
            try
            {
                Save();
            }
            catch (IOException ex)
            {
                fenceInfo.Files.RemoveAt(insertionIndex);
                ShowItemSaveError(ex);
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                fenceInfo.Files.RemoveAt(insertionIndex);
                ShowItemSaveError(ex);
                return;
            }
            catch (InvalidOperationException ex)
            {
                fenceInfo.Files.RemoveAt(insertionIndex);
                ShowItemSaveError(ex);
                return;
            }

            source.fenceInfo.Files.RemoveAt(originalSourceIndex);
            try
            {
                source.Save();
            }
            catch (IOException ex)
            {
                RollBackCrossFenceMove(source, originalSourceIndex, dragData.Path);
                ShowItemSaveError(ex);
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                RollBackCrossFenceMove(source, originalSourceIndex, dragData.Path);
                ShowItemSaveError(ex);
                return;
            }
            catch (InvalidOperationException ex)
            {
                RollBackCrossFenceMove(source, originalSourceIndex, dragData.Path);
                ShowItemSaveError(ex);
                return;
            }

            selectedItem = dragData.Path;
            source.selectedItem = null;
            source.Invalidate();
        }

        private void RollBackCrossFenceMove(FenceWindow source, int sourceIndex, string path)
        {
            source.fenceInfo.Files.Insert(sourceIndex, path);
            try
            {
                source.Save();
            }
            catch (IOException ex)
            {
                MessageBox.Show(source, "The item could not be restored to its original fence: " + ex.Message,
                    "Fence move could not be rolled back", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show(source, "The item could not be restored to its original fence: " + ex.Message,
                    "Fence move could not be rolled back", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(source, "The item could not be restored to its original fence: " + ex.Message,
                    "Fence move could not be rolled back", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            var destinationIndex = fenceInfo.Files.FindIndex(item =>
                string.Equals(item, path, StringComparison.OrdinalIgnoreCase));
            if (destinationIndex >= 0)
                fenceInfo.Files.RemoveAt(destinationIndex);

            try
            {
                Save();
            }
            catch (IOException ex)
            {
                MessageBox.Show(this, "The item could not be removed from this fence during rollback: " + ex.Message,
                    "Fence move could not be rolled back", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show(this, "The item could not be removed from this fence during rollback: " + ex.Message,
                    "Fence move could not be rolled back", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(this, "The item could not be removed from this fence during rollback: " + ex.Message,
                    "Fence move could not be rolled back", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            source.Invalidate();
        }

        private void RestoreFileOrder(System.Collections.Generic.List<string> originalFiles)
        {
            fenceInfo.Files.Clear();
            fenceInfo.Files.AddRange(originalFiles);
        }

        private void ShowItemSaveError(Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not save fence item", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void ClearDragInsertionMarker()
        {
            dragInsertionIndex = -1;
            Invalidate();
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
            pointerPosition = e.Location;
            pointerInside = true;

            if (!lockedToolStripMenuItem.Checked && e.Button == MouseButtons.Left && thumbnailDragPath != null
                && !isDraggingThumbnail)
            {
                var dragSize = SystemInformation.DragSize;
                var dragRectangle = new Rectangle(
                    thumbnailMouseDown.X - dragSize.Width / 2,
                    thumbnailMouseDown.Y - dragSize.Height / 2,
                    dragSize.Width,
                    dragSize.Height);
                if (!dragRectangle.Contains(e.Location))
                {
                    isDraggingThumbnail = true;
                    suppressNextClick = true;
                    try
                    {
                        var dragData = new FenceItemDragData
                        {
                            Source = this,
                            Path = thumbnailDragPath,
                            Index = thumbnailDragIndex
                        };
                        var data = new DataObject();
                        data.SetData(typeof(FenceItemDragData), dragData);
                        DoDragDrop(data, DragDropEffects.Move);
                    }
                    finally
                    {
                        thumbnailDragPath = null;
                        thumbnailDragIndex = -1;
                        isDraggingThumbnail = false;
                        ClearDragInsertionMarker();
                    }
                }
            }

            Invalidate();
        }

        private void FenceWindow_MouseDown(object sender, MouseEventArgs e)
        {
            suppressNextClick = false;
            if (e.Button != MouseButtons.Left || lockedToolStripMenuItem.Checked)
                return;

            var itemIndex = GetItemIndexAt(e.Location);
            thumbnailDragPath = itemIndex >= 0 ? fenceInfo.Files[itemIndex] : null;
            thumbnailDragIndex = itemIndex;
            thumbnailMouseDown = e.Location;
        }

        private void FenceWindow_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !isDraggingThumbnail)
            {
                thumbnailDragPath = null;
                thumbnailDragIndex = -1;
            }
        }

        private int GetItemIndexAt(Point point)
        {
            for (var i = 0; i < fenceInfo.Files.Count; i++)
                if (GetItemBounds(i).Contains(point))
                    return i;
            return -1;
        }

        private int GetDropInsertionIndex(Point point)
        {
            if (fenceInfo.Files.Count == 0)
                return 0;

            var itemPadding = LogicalToDeviceUnits(logicalItemPadding);
            var itemWidth = LogicalToDeviceUnits(logicalItemWidth);
            var iconSize = LogicalToDeviceUnits(logicalIconSize);
            var textHeight = LogicalToDeviceUnits(logicalTextHeight);
            var itemHeight = iconSize + itemPadding + textHeight;
            var rowTop = GetItemBounds(0).Top;
            var closestRowDistance = int.MaxValue;

            for (var i = 0; i < fenceInfo.Files.Count; i++)
            {
                var bounds = GetItemBounds(i);
                var centerY = bounds.Top + itemHeight / 2;
                var distance = Math.Abs(point.Y - centerY);
                if (distance < closestRowDistance)
                {
                    closestRowDistance = distance;
                    rowTop = bounds.Top;
                }
            }

            var insertionIndex = 0;
            for (var i = 0; i < fenceInfo.Files.Count; i++)
            {
                var bounds = GetItemBounds(i);
                if (bounds.Top != rowTop)
                    continue;

                if (point.X < bounds.Left + bounds.Width / 2)
                    return i;
                insertionIndex = i + 1;
            }

            if (point.Y > rowTop + itemHeight / 2)
            {
                for (var i = 0; i < fenceInfo.Files.Count; i++)
                    if (GetItemBounds(i).Top > rowTop)
                        return i;
            }

            return insertionIndex;
        }

        private Rectangle GetItemBounds(int index)
        {
            var itemPadding = LogicalToDeviceUnits(logicalItemPadding);
            var itemWidth = LogicalToDeviceUnits(logicalItemWidth);
            var iconSize = LogicalToDeviceUnits(logicalIconSize);
            var textHeight = LogicalToDeviceUnits(logicalTextHeight);
            var itemHeight = iconSize + itemPadding + textHeight;
            var x = itemPadding;
            var y = itemPadding;

            for (var i = 0; i < index; i++)
            {
                x += itemWidth + itemPadding;
                if (x + itemWidth > Width)
                {
                    x = itemPadding;
                    y += itemHeight + itemPadding;
                }
            }

            return new Rectangle(x, y + titleHeight - scrollOffset, itemWidth, itemHeight);
        }

        private void FenceWindow_MouseEnter(object sender, EventArgs e)
        {
            pointerPosition = PointToClient(MousePosition);
            pointerInside = true;
            if (minifyToolStripMenuItem.Checked && isMinified)
            {
                isMinified = false;
                Height = prevHeight;
            }
        }

        private void FenceWindow_MouseLeave(object sender, EventArgs e)
        {
            pointerInside = false;
            if (!isDraggingThumbnail)
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
            if (suppressNextClick)
            {
                suppressNextClick = false;
                return;
            }

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
            var backgroundAlpha = GlobalFenceSettings.GetOverlayOpacity(92, 174);
            var titleAlpha = GlobalFenceSettings.GetOverlayOpacity(36, 92);

            using (var backgroundBrush = new SolidBrush(Color.FromArgb(backgroundAlpha,
                FluentTheme.IsDark ? Color.Black : Color.White)))
            using (var titleBrush = new SolidBrush(Color.FromArgb(titleAlpha,
                FluentTheme.IsDark ? Color.Black : Color.White)))
            using (var titleFormat = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            })
            {
                graphics.FillRectangle(backgroundBrush, ClientRectangle);
                if (!GlobalFenceSettings.HideTitleArea)
                {
                    graphics.FillRectangle(titleBrush, new Rectangle(0, 0, Width, titleHeight));
                    using (var titleTextBrush = new SolidBrush(FluentTheme.Text))
                    graphics.DrawString(Text, titleFont, titleTextBrush,
                        new RectangleF(LogicalToDeviceUnits(12), 0,
                            Math.Max(0, Width - LogicalToDeviceUnits(24)), titleHeight), titleFormat);
                    using (var dividerPen = new Pen(Color.FromArgb(FluentTheme.IsDark ? 52 : 72, FluentTheme.Text)))
                    {
                        var dividerY = titleHeight - 1;
                        graphics.DrawLine(dividerPen, LogicalToDeviceUnits(12), dividerY,
                            Math.Max(LogicalToDeviceUnits(12), Width - LogicalToDeviceUnits(12)), dividerY);
                    }
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

            if (dragInsertionIndex >= 0)
                DrawDragInsertionMarker(graphics);

            scrollHeight -= (ClientRectangle.Height - titleHeight);

            // Scroll bars
            if (scrollHeight > 0)
            {
                var contentHeight = Math.Max(0, Height - titleHeight);
                if (contentHeight > 0)
                {
                    scrollOffset = Math.Max(0, Math.Min(scrollOffset, scrollHeight));
                    var totalContentHeight = (long)contentHeight + scrollHeight;
                    var scrollbarHeight = Math.Min(contentHeight,
                        Math.Max(LogicalToDeviceUnits(12), (int)((long)contentHeight * contentHeight / totalContentHeight)));
                    var scrollRange = contentHeight - scrollbarHeight;
                    var scrollbarTop = titleHeight
                        + (int)((long)scrollOffset * scrollRange / scrollHeight);
                    using (var scrollBrush = new SolidBrush(Color.FromArgb(150, FluentTheme.Text)))
                    {
                        var scrollWidth = LogicalToDeviceUnits(4);
                        var scrollRect = new Rectangle(Width - LogicalToDeviceUnits(7),
                            scrollbarTop + LogicalToDeviceUnits(2), scrollWidth,
                            Math.Max(LogicalToDeviceUnits(8), scrollbarHeight - LogicalToDeviceUnits(4)));
                        using (var scrollPath = CreateRoundedRectangle(scrollRect, scrollWidth / 2))
                            graphics.FillPath(scrollBrush, scrollPath);
                    }
                }
            }

            if (GlobalFenceSettings.GlowEffectsEnabled && pointerInside)
                DrawPointerGlow(graphics);

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

        private void DrawDragInsertionMarker(Graphics graphics)
        {
            if (fenceInfo.Files.Count == 0)
                return;

            var beforeItem = dragInsertionIndex < fenceInfo.Files.Count;
            var markerTarget = beforeItem
                ? GetItemBounds(dragInsertionIndex)
                : GetItemBounds(fenceInfo.Files.Count - 1);
            var x = beforeItem
                ? markerTarget.Left - LogicalToDeviceUnits(3)
                : markerTarget.Right + LogicalToDeviceUnits(3);
            var markerWidth = Math.Max(3, LogicalToDeviceUnits(3));
            var markerRect = new Rectangle(x - markerWidth / 2, markerTarget.Top,
                markerWidth, markerTarget.Height);
            using (var markerBrush = new SolidBrush(FluentTheme.Accent))
            using (var markerPath = CreateRoundedRectangle(markerRect, markerWidth / 2))
                graphics.FillPath(markerBrush, markerPath);
        }

        private void RenderEntry(Graphics g, FenceEntry entry, int x, int y, int itemWidth, int textHeight, int iconSize)
        {
            var icon = entry.ExtractIcon(thumbnailProvider, iconSize);
            var name = entry.Name;

            var textMaxSize = new SizeF(itemWidth, textHeight);

            using (var stringFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Near,
                Trimming = StringTrimming.EllipsisCharacter
            })
            {
                var textSize = g.MeasureString(name, iconFont, textMaxSize, stringFormat);
                var inset = LogicalToDeviceUnits(4);
                var itemHeight = iconSize + LogicalToDeviceUnits(logicalItemPadding) + textHeight;
                var cardRect = new Rectangle(x - inset, y,
                    itemWidth + inset * 2, itemHeight);
                var iconTextGap = LogicalToDeviceUnits(5);
                var contentHeight = iconSize + iconTextGap + (int)textSize.Height;
                var contentOffset = Math.Max(0, (itemHeight - contentHeight) / 2);
                var iconY = y + contentOffset;
                var textPosition = new PointF(x, iconY + iconSize + iconTextGap);

                var mousePos = PointToClient(MousePosition);
                var mouseOver = mousePos.X >= x && mousePos.Y >= y
                    && mousePos.X < x + itemWidth && mousePos.Y < y + itemHeight;

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

                var isSelected = string.Equals(selectedItem, entry.Path, StringComparison.OrdinalIgnoreCase);
                if (isSelected)
                {
                    using (var cardBrush = new SolidBrush(FluentTheme.AccentOverlay(FluentTheme.IsDark ? 64 : 42)))
                    using (var cardPath = CreateRoundedRectangle(cardRect, LogicalToDeviceUnits(7)))
                        g.FillPath(cardBrush, cardPath);

                    using (var borderPen = new Pen(Color.FromArgb(210, FluentTheme.Accent)))
                    using (var cardPath = CreateRoundedRectangle(cardRect, LogicalToDeviceUnits(7)))
                        g.DrawPath(borderPen, cardPath);
                }

                var iconRect = new Rectangle(x + (itemWidth - iconSize) / 2, iconY, iconSize, iconSize);
                if (GlobalFenceSettings.GlowEffectsEnabled && GlobalFenceSettings.IconGlowIntensity > 0)
                {
                    var glowRadius = LogicalToDeviceUnits(GlobalFenceSettings.IconGlowRadius);
                    var glowOpacity = (int)Math.Round(GlobalFenceSettings.IconGlowIntensity * 255.0 / 100);
                    using (var iconLayer = new Bitmap(iconSize + glowRadius * 2, iconSize + glowRadius * 2))
                    using (var iconGraphics = Graphics.FromImage(iconLayer))
                    {
                        iconGraphics.DrawIcon(icon, new Rectangle(glowRadius, glowRadius, iconSize, iconSize));
                        using (var iconGlow = FluentTheme.CreateBlurredColor(iconLayer, glowRadius, glowOpacity))
                            g.DrawImageUnscaled(iconGlow, iconRect.X - glowRadius, iconRect.Y - glowRadius);
                    }
                }
                g.DrawIcon(icon, iconRect);

                var shadowRadius = LogicalToDeviceUnits(3);
                var shadowOffset = LogicalToDeviceUnits(1);
                using (var textLayer = new Bitmap(
                    Math.Max(1, (int)Math.Ceiling(textMaxSize.Width) + shadowRadius * 2),
                    Math.Max(1, (int)Math.Ceiling(textMaxSize.Height) + shadowRadius * 2)))
                using (var textGraphics = Graphics.FromImage(textLayer))
                using (var shadowBrush = new SolidBrush(Color.FromArgb(FluentTheme.IsDark ? 82 : 58, Color.Black)))
                {
                    textGraphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    textGraphics.DrawString(name, iconFont, shadowBrush,
                        new RectangleF(shadowRadius + shadowOffset, shadowRadius + shadowOffset,
                            textMaxSize.Width, textMaxSize.Height), stringFormat);
                    using (var blurredText = FluentTheme.CreateBlurredTint(textLayer, Color.Black, shadowRadius))
                        g.DrawImageUnscaled(blurredText,
                            (int)textPosition.X - shadowRadius, (int)textPosition.Y - shadowRadius);
                }
                using (var nameBrush = new SolidBrush(entry.Type == EntryType.Missing
                    ? Color.FromArgb(255, 224, 176)
                    : FluentTheme.Text))
                    g.DrawString(name, iconFont, nameBrush, new RectangleF(textPosition, textMaxSize), stringFormat);
            }
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            var diameter = Math.Max(1, radius * 2);
            if (bounds.Width <= diameter || bounds.Height <= diameter)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void DrawPointerGlow(Graphics graphics)
        {
            var radius = LogicalToDeviceUnits(GlobalFenceSettings.PointerGlowRadius);
            var bounds = new Rectangle(pointerPosition.X - radius, pointerPosition.Y - radius,
                radius * 2, radius * 2);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(bounds);
                using (var glow = new PathGradientBrush(path))
                {
                    var highlight = GlobalFenceSettings.PointerGlowColor;
                    var baseOpacity = FluentTheme.IsDark ? 42 : 30;
                    var opacity = (int)Math.Round(baseOpacity * GlobalFenceSettings.PointerGlowIntensity / 100.0);
                    glow.CenterColor = Color.FromArgb(opacity, highlight);
                    glow.SurroundColors = new[] { Color.FromArgb(0, highlight) };
                    glow.FocusScales = new PointF(0.12f, 0.12f);
                    graphics.FillPath(glow, path);
                }
            }
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new EditDialog(Text))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    if (string.IsNullOrWhiteSpace(dialog.NewName))
                    {
                        MessageBox.Show(this, "A fence name cannot be empty.", "Invalid fence name",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    Text = dialog.NewName;
                    fenceInfo.Name = Text;
                    Refresh();
                    Save();
                }
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

        internal bool FitBoundsToCurrentLayout()
        {
            var itemPadding = LogicalToDeviceUnits(logicalItemPadding);
            var itemWidth = LogicalToDeviceUnits(logicalItemWidth);
            var iconSize = LogicalToDeviceUnits(logicalIconSize);
            var textHeight = LogicalToDeviceUnits(logicalTextHeight);
            var itemHeight = iconSize + itemPadding + textHeight;
            var columns = Math.Max(1, (Width - itemPadding) / (itemWidth + itemPadding));
            var rows = (fenceInfo.Files.Count + columns - 1) / columns;
            var targetWidth = itemPadding + columns * (itemWidth + itemPadding);
            var targetHeight = titleHeight + (Math.Max(1, rows) + 1) * itemPadding
                + rows * itemHeight;
            var currentExpandedHeight = isMinified ? prevHeight : Height;
            if (Width == targetWidth && currentExpandedHeight == targetHeight
                && fenceInfo.Width == targetWidth && fenceInfo.Height == targetHeight)
                return false;

            var previousWidth = Width;
            var previousHeight = Height;
            var previousExpandedHeight = prevHeight;
            var previousSavedWidth = fenceInfo.Width;
            var previousSavedHeight = fenceInfo.Height;

            Width = targetWidth;
            if (isMinified)
                prevHeight = targetHeight;
            else
                Height = targetHeight;

            fenceInfo.Width = targetWidth;
            fenceInfo.Height = targetHeight;

            var saved = false;
            try
            {
                Save();
                saved = true;
            }
            finally
            {
                if (!saved)
                {
                    Width = previousWidth;
                    Height = previousHeight;
                    prevHeight = previousExpandedHeight;
                    fenceInfo.Width = previousSavedWidth;
                    fenceInfo.Height = previousSavedHeight;
                }
            }

            Invalidate();
            return true;
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
            AdvancedSettingsForm.ShowSettings(this);
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
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                if (!IsHandleCreated)
                    return;
                BeginInvoke(new EventHandler(GlobalFenceSettings_Changed), sender, e);
                return;
            }

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
