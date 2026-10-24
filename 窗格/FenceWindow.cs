using 窗格.Model;
using 窗格.Util;
using 窗格.Win32;
using System;
using System.Collections.Generic;
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
        private const float logicalIconFontSize = 9;
        private const int maximumCachedItemEffects = 128;

        private readonly FenceViewModel viewModel;
        private FenceInfo fenceInfo => viewModel.Info;

        private Font titleFont;
        private Font iconFont;

        private string selectedItem;
        private string hoveringItem;
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
        private bool removedFromStorage;

        private int scrollHeight;
        private int scrollOffset;

        private readonly DebouncedAction debouncedMove;
        private readonly DebouncedAction debouncedResize;

        private readonly ShellContextMenu shellContextMenu = new ShellContextMenu();

        private readonly ThumbnailProvider thumbnailProvider = new ThumbnailProvider();
        private readonly ToolStripMenuItem locateMissingFileItem = new ToolStripMenuItem(UiText.Get("Locate replacement file..."));
        private readonly ToolStripMenuItem locateMissingFolderItem = new ToolStripMenuItem(UiText.Get("Locate replacement folder..."));
        private readonly ToolStripMenuItem openMissingParentItem = new ToolStripMenuItem(UiText.Get("Open containing folder"));
        private readonly ToolStripSeparator missingItemSeparator = new ToolStripSeparator();
        private string removeItemText;

        private sealed class FenceItemDragData
        {
            public FenceWindow Source { get; set; }
            public string Path { get; set; }
            public int Index { get; set; }
        }

        private sealed class ItemRenderCache : IDisposable
        {
            public Bitmap IconGlow;
            public int IconSize;
            public int IconGlowRadius;
            public int IconGlowOpacity;
            public EntryType IconType;
            public Bitmap TextShadow;
            public string Text;
            public int TextWidth;
            public int TextHeight;
            public float FontSize;
            public int ShadowRadius;
            public int ShadowOffset;
            public int ShadowOpacity;
            public long LastAccess;

            public void Dispose()
            {
                IconGlow?.Dispose();
                TextShadow?.Dispose();
            }
        }

        private readonly Dictionary<string, ItemRenderCache> itemRenderCache =
            new Dictionary<string, ItemRenderCache>(StringComparer.OrdinalIgnoreCase);
        private long wheelDeltaRemainder;
        private long renderCacheAccessOrder;
        private Bitmap pointerGlowBitmap;
        private int pointerGlowRadius;

        private void ReloadFonts()
        {
            var titleSize = logicalTitleHeight / 2.0f;
            var iconFontSize = logicalIconFontSize * GlobalFenceSettings.ContentScalePercent / 100.0f;
            var newTitleFont = new Font("Segoe UI", titleSize, FontStyle.Regular, GraphicsUnit.Point);
            var newIconFont = new Font("Segoe UI", iconFontSize, FontStyle.Regular, GraphicsUnit.Point);
            var oldTitleFont = titleFont;
            var oldIconFont = iconFont;
            titleFont = newTitleFont;
            iconFont = newIconFont;
            oldTitleFont?.Dispose();
            oldIconFont?.Dispose();
        }

        public FenceWindow(FenceInfo fenceInfo)
        {
            viewModel = new FenceViewModel(fenceInfo, FenceManager.Instance);
            debouncedMove = new DebouncedAction(TimeSpan.FromMilliseconds(500), ShowDeferredSaveError);
            debouncedResize = new DebouncedAction(TimeSpan.FromMilliseconds(500), ShowDeferredSaveError);
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


            Text = fenceInfo.Name;
            Location = new Point(fenceInfo.PosX, fenceInfo.PosY);

            Width = fenceInfo.Width;
            Height = fenceInfo.Height;

            prevHeight = Height;
            lockedToolStripMenuItem.Checked = fenceInfo.Locked;
            minifyToolStripMenuItem.Checked = fenceInfo.CanMinify;
            minifyToolStripMenuItem.Enabled = !GlobalFenceSettings.HideTitleArea;
            titleSizeToolStripMenuItem.Text = UiText.Get("Global title size...");
            removeItemText = deleteItemToolStripMenuItem.Text;
            appContextMenu.Items.Insert(0, locateMissingFileItem);
            appContextMenu.Items.Insert(1, locateMissingFolderItem);
            appContextMenu.Items.Insert(2, openMissingParentItem);
            appContextMenu.Items.Insert(3, missingItemSeparator);
            locateMissingFileItem.Click += LocateMissingFileItem_Click;
            locateMissingFolderItem.Click += LocateMissingFolderItem_Click;
            openMissingParentItem.Click += OpenMissingParentItem_Click;
            GlobalFenceSettings.Changed += GlobalFenceSettings_Changed;
            UpdatePointerGlowBitmap();
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
            ClearRenderCache();
            if (wasMinified)
            {
                prevHeight = (int)Math.Round(prevHeight * dpiScale);
                Height = titleHeight;
            }
            else
            {
                prevHeight = Height;
            }

            UpdatePointerGlowBitmap();
            Invalidate();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            GlobalFenceSettings.Changed -= GlobalFenceSettings_Changed;
            thumbnailProvider.IconThumbnailLoaded -= ThumbnailProvider_IconThumbnailLoaded;
            thumbnailProvider.Dispose();
            debouncedMove.Dispose();
            debouncedResize.Dispose();
            ClearRenderCache();
            pointerGlowBitmap?.Dispose();
            pointerGlowBitmap = null;
            titleFont?.Dispose();
            iconFont?.Dispose();
            base.OnFormClosed(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel || removedFromStorage)
                return;

            try
            {
                debouncedMove.Cancel();
                debouncedResize.Cancel();
                fenceInfo.PosX = Location.X;
                fenceInfo.PosY = Location.Y;
                fenceInfo.Width = Width;
                fenceInfo.Height = isMinified ? prevHeight : Height;
                Save();
            }
            catch (Exception ex) when (IsFenceSaveError(ex))
            {
                e.Cancel = true;
                MessageBox.Show(this, UiText.Get("Could not save this fence before closing. The fence was kept open to protect its saved data.")
                    + Environment.NewLine + ex.Message, UiText.Get("Fence could not be saved"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this,
                UiText.Get("Really remove this fence and its saved copies, including recovery backups?"),
                UiText.Get("Remove fence"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    FenceManager.Instance.RemoveFence(fenceInfo);
                    removedFromStorage = true;
                    Close();
                }
                catch (IOException ex)
                {
                    MessageBox.Show(this, UiText.Get("Could not remove all saved copies of this fence. It may reappear when 窗格 starts again.") +
                        Environment.NewLine + ex.Message, UiText.Get("Fence could not be removed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (UnauthorizedAccessException ex)
                {
                    MessageBox.Show(this, UiText.Get("Could not remove all saved copies of this fence. It may reappear when 窗格 starts again.") +
                        Environment.NewLine + ex.Message, UiText.Get("Fence could not be removed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void deleteItemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var itemIndex = fenceInfo.Files.IndexOf(hoveringItem);
            if (itemIndex < 0)
                return;

            try
            {
                viewModel.RemoveItemAt(itemIndex);
            }
            catch (Exception ex) when (IsFenceSaveError(ex))
            {
                ShowItemSaveError(ex);
                return;
            }

            ClearRenderCache();
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
            deleteItemToolStripMenuItem.Text = missingItem ? UiText.Get("Remove missing item") : removeItemText;
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

                try
                {
                    viewModel.AddExistingItems(dropped);
                    ClearRenderCache();
                }
                catch (Exception ex) when (IsFenceSaveError(ex))
                {
                    MessageBox.Show(this, ex.Message, UiText.Get("Could not save fence items"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                ClearDragInsertionMarker();
                Invalidate();
            }
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

            try
            {
                if (source == this)
                {
                    if (!viewModel.ReorderItem(sourceIndex, insertionIndex))
                        return;
                    selectedItem = dragData.Path;
                }
                else
                {
                    if (!viewModel.MoveItemFrom(source.viewModel, dragData.Path, sourceIndex, insertionIndex))
                        return;
                    selectedItem = dragData.Path;
                    source.selectedItem = null;
                    source.ClearRenderCache();
                    source.Invalidate();
                }
                ClearRenderCache();
            }
            catch (Exception ex) when (IsFenceSaveError(ex))
            {
                ShowItemSaveError(ex);
            }

            Invalidate();
        }

        private void ShowItemSaveError(Exception ex)
        {
            MessageBox.Show(this, ex.Message, UiText.Get("Could not save fence item"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void ShowDeferredSaveError(Exception ex)
        {
            MessageBox.Show(this, ex.Message, UiText.Get("Fence could not be saved"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static bool IsFenceSaveError(Exception ex)
        {
            return ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException;
        }

        private void ClearDragInsertionMarker()
        {
            dragInsertionIndex = -1;
            Invalidate();
        }

        private void FenceWindow_Resize(object sender, EventArgs e)
        {
            debouncedResize.Schedule(() =>
            {
                fenceInfo.Width = Width;
                fenceInfo.Height = isMinified ? prevHeight : Height;
                Save();
            });

            Refresh();
        }

        private void FenceWindow_MouseMove(object sender, MouseEventArgs e)
        {
            var previousPointerPosition = pointerPosition;
            pointerPosition = e.Location;
            pointerInside = true;
            var hoveredItemIndex = GetItemIndexAt(e.Location);
            hoveringItem = hoveredItemIndex >= 0 ? fenceInfo.Files[hoveredItemIndex] : null;

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

            if (GlobalFenceSettings.GlowEffectsEnabled && GlobalFenceSettings.PointerGlowIntensity > 0)
            {
                InvalidatePointerGlow(previousPointerPosition);
                InvalidatePointerGlow(pointerPosition);
            }
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
            return CreateLayout().GetItemIndexAt(point, fenceInfo.Files.Count);
        }

        private int GetDropInsertionIndex(Point point)
        {
            return CreateLayout().GetDropInsertionIndex(point, fenceInfo.Files.Count);
        }

        private Rectangle GetItemBounds(int index)
        {
            return CreateLayout().GetItemBounds(index);
        }

        private FenceLayout CreateLayout()
        {
            var itemPadding = ContentToDeviceUnits(logicalItemPadding);
            var itemWidth = ContentToDeviceUnits(logicalItemWidth);
            var itemHeight = ContentToDeviceUnits(logicalIconSize)
                + itemPadding + ContentToDeviceUnits(logicalTextHeight);
            return new FenceLayout(Width, titleHeight, scrollOffset, itemPadding, itemWidth, itemHeight);
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
            var previousPointerPosition = pointerPosition;
            var previousSelectedItem = selectedItem;
            pointerInside = false;
            if (!isDraggingThumbnail)
                Minify();
            selectedItem = null;
            hoveringItem = null;
            InvalidatePointerGlow(previousPointerPosition);
            InvalidateItem(previousSelectedItem);
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
            try
            {
                viewModel.SetCanMinify(minifyToolStripMenuItem.Checked);
            }
            catch (Exception ex) when (IsFenceSaveError(ex))
            {
                minifyToolStripMenuItem.Checked = fenceInfo.CanMinify;
                ShowItemSaveError(ex);
            }
        }

        private void FenceWindow_Click(object sender, EventArgs e)
        {
            if (suppressNextClick)
            {
                suppressNextClick = false;
                return;
            }

            var previousSelectedItem = selectedItem;
            var itemIndex = GetItemIndexAt(PointToClient(MousePosition));
            selectedItem = itemIndex >= 0 ? fenceInfo.Files[itemIndex] : null;
            InvalidateItem(previousSelectedItem);
            InvalidateItem(selectedItem);
        }

        private void FenceWindow_DoubleClick(object sender, EventArgs e)
        {
            var itemIndex = GetItemIndexAt(PointToClient(MousePosition));
            if (itemIndex >= 0)
                FenceEntry.FromPath(fenceInfo.Files[itemIndex]).Open();
        }

        private void FenceWindow_Paint(object sender, PaintEventArgs e)
        {
            var graphics = e.Graphics;
            graphics.SetClip(ClientRectangle, CombineMode.Intersect);
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

            var layout = CreateLayout();
            var itemWidth = ContentToDeviceUnits(logicalItemWidth);
            var iconSize = ContentToDeviceUnits(logicalIconSize);
            var textHeight = ContentToDeviceUnits(logicalTextHeight);
            graphics.SetClip(new Rectangle(0, titleHeight, Width, Math.Max(0, Height - titleHeight)),
                CombineMode.Intersect);
            var visibleBounds = Rectangle.Ceiling(graphics.VisibleClipBounds);
            var effectPadding = Math.Max(ContentToDeviceUnits(GlobalFenceSettings.IconGlowRadius),
                ContentToDeviceUnits(4));
            int firstVisibleIndex;
            int lastVisibleIndex;
            layout.GetVisibleItemRange(visibleBounds, fenceInfo.Files.Count, effectPadding,
                out firstVisibleIndex, out lastVisibleIndex);
            for (var i = firstVisibleIndex; i <= lastVisibleIndex; i++)
            {
                var bounds = layout.GetItemBounds(i);
                var entry = FenceEntry.FromPath(fenceInfo.Files[i]);
                RenderEntry(graphics, entry, bounds.Left, bounds.Top, itemWidth, textHeight, iconSize);
            }

            if (dragInsertionIndex >= 0)
                DrawDragInsertionMarker(graphics);

            scrollHeight = layout.GetContentOverflow(fenceInfo.Files.Count, ClientRectangle.Height - titleHeight);

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
                ? markerTarget.Left - ContentToDeviceUnits(3)
                : markerTarget.Right + ContentToDeviceUnits(3);
            var markerWidth = ContentToDeviceUnits(3);
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
                var inset = ContentToDeviceUnits(4);
                var itemHeight = iconSize + ContentToDeviceUnits(logicalItemPadding) + textHeight;
                var cardRect = new Rectangle(x - inset, y,
                    itemWidth + inset * 2, itemHeight);
                var iconTextGap = ContentToDeviceUnits(5);
                var contentHeight = iconSize + iconTextGap + (int)textSize.Height;
                var contentOffset = Math.Max(0, (itemHeight - contentHeight) / 2);
                var iconY = y + contentOffset;
                var textPosition = new PointF(x, iconY + iconSize + iconTextGap);

                var isSelected = string.Equals(selectedItem, entry.Path, StringComparison.OrdinalIgnoreCase);
                if (isSelected)
                {
                    using (var cardBrush = new SolidBrush(FluentTheme.AccentOverlay(FluentTheme.IsDark ? 64 : 42)))
                    using (var cardPath = CreateRoundedRectangle(cardRect, ContentToDeviceUnits(7)))
                        g.FillPath(cardBrush, cardPath);

                    using (var borderPen = new Pen(Color.FromArgb(210, FluentTheme.Accent)))
                    using (var cardPath = CreateRoundedRectangle(cardRect, ContentToDeviceUnits(7)))
                        g.DrawPath(borderPen, cardPath);
                }

                var iconRect = new Rectangle(x + (itemWidth - iconSize) / 2, iconY, iconSize, iconSize);
                if (GlobalFenceSettings.GlowEffectsEnabled && GlobalFenceSettings.IconGlowIntensity > 0)
                {
                    var glowRadius = ContentToDeviceUnits(GlobalFenceSettings.IconGlowRadius);
                    var glowOpacity = (int)Math.Round(GlobalFenceSettings.IconGlowIntensity * 255.0 / 100);
                    var iconGlow = GetIconGlow(entry, icon, iconSize, glowRadius, glowOpacity);
                    g.DrawImageUnscaled(iconGlow, iconRect.X - glowRadius, iconRect.Y - glowRadius);
                }
                g.DrawIcon(icon, iconRect);

                var shadowRadius = ContentToDeviceUnits(3);
                var shadowOffset = ContentToDeviceUnits(1);
                var shadowOpacity = FluentTheme.IsDark ? 82 : 58;
                var blurredText = GetTextShadow(entry, name, textMaxSize, shadowRadius,
                    shadowOffset, shadowOpacity, stringFormat);
                g.DrawImageUnscaled(blurredText,
                    (int)textPosition.X - shadowRadius, (int)textPosition.Y - shadowRadius);
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

        private ItemRenderCache GetRenderCache(string path)
        {
            ItemRenderCache cache;
            if (!itemRenderCache.TryGetValue(path, out cache))
            {
                if (itemRenderCache.Count >= maximumCachedItemEffects)
                {
                    string leastRecentlyUsedPath = null;
                    ItemRenderCache leastRecentlyUsed = null;
                    foreach (var item in itemRenderCache)
                    {
                        if (leastRecentlyUsed == null || item.Value.LastAccess < leastRecentlyUsed.LastAccess)
                        {
                            leastRecentlyUsedPath = item.Key;
                            leastRecentlyUsed = item.Value;
                        }
                    }

                    itemRenderCache.Remove(leastRecentlyUsedPath);
                    leastRecentlyUsed.Dispose();
                }

                cache = new ItemRenderCache();
                itemRenderCache.Add(path, cache);
            }

            cache.LastAccess = ++renderCacheAccessOrder;
            return cache;
        }

        private Bitmap GetIconGlow(FenceEntry entry, Icon icon, int iconSize, int glowRadius, int glowOpacity)
        {
            var cache = GetRenderCache(entry.Path);
            if (cache.IconGlow != null && cache.IconSize == iconSize
                && cache.IconGlowRadius == glowRadius && cache.IconGlowOpacity == glowOpacity
                && cache.IconType == entry.Type)
                return cache.IconGlow;

            using (var iconLayer = new Bitmap(iconSize + glowRadius * 2, iconSize + glowRadius * 2))
            using (var iconGraphics = Graphics.FromImage(iconLayer))
            {
                iconGraphics.DrawIcon(icon, new Rectangle(glowRadius, glowRadius, iconSize, iconSize));
                var iconGlow = FluentTheme.CreateBlurredColor(iconLayer, glowRadius, glowOpacity);
                cache.IconGlow?.Dispose();
                cache.IconGlow = iconGlow;
                cache.IconSize = iconSize;
                cache.IconGlowRadius = glowRadius;
                cache.IconGlowOpacity = glowOpacity;
                cache.IconType = entry.Type;
            }

            return cache.IconGlow;
        }

        private Bitmap GetTextShadow(FenceEntry entry, string text, SizeF textSize,
            int shadowRadius, int shadowOffset, int shadowOpacity, StringFormat stringFormat)
        {
            var cache = GetRenderCache(entry.Path);
            var width = Math.Max(1, (int)Math.Ceiling(textSize.Width) + shadowRadius * 2);
            var height = Math.Max(1, (int)Math.Ceiling(textSize.Height) + shadowRadius * 2);
            if (cache.TextShadow != null && cache.Text == text
                && cache.TextWidth == width && cache.TextHeight == height
                && cache.FontSize == iconFont.Size && cache.ShadowRadius == shadowRadius
                && cache.ShadowOffset == shadowOffset && cache.ShadowOpacity == shadowOpacity)
                return cache.TextShadow;

            using (var textLayer = new Bitmap(width, height))
            using (var textGraphics = Graphics.FromImage(textLayer))
            using (var shadowBrush = new SolidBrush(Color.FromArgb(shadowOpacity, Color.Black)))
            {
                textGraphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                textGraphics.DrawString(text, iconFont, shadowBrush,
                    new RectangleF(shadowRadius + shadowOffset, shadowRadius + shadowOffset,
                        textSize.Width, textSize.Height), stringFormat);
                var blurredText = FluentTheme.CreateBlurredTint(textLayer, Color.Black, shadowRadius);
                cache.TextShadow?.Dispose();
                cache.TextShadow = blurredText;
                cache.Text = text;
                cache.TextWidth = width;
                cache.TextHeight = height;
                cache.FontSize = iconFont.Size;
                cache.ShadowRadius = shadowRadius;
                cache.ShadowOffset = shadowOffset;
                cache.ShadowOpacity = shadowOpacity;
            }

            return cache.TextShadow;
        }

        private void ClearRenderCache()
        {
            foreach (var cache in itemRenderCache.Values)
                cache.Dispose();
            itemRenderCache.Clear();
        }

        private void InvalidateItem(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            var itemIndex = fenceInfo.Files.FindIndex(item =>
                string.Equals(item, path, StringComparison.OrdinalIgnoreCase));
            if (itemIndex < 0)
                return;

            var padding = Math.Max(ContentToDeviceUnits(GlobalFenceSettings.IconGlowRadius),
                ContentToDeviceUnits(4));
            Invalidate(Rectangle.Inflate(CreateLayout().GetItemBounds(itemIndex), padding, padding));
        }

        private void InvalidateIconGlow(string path)
        {
            ItemRenderCache cache;
            if (itemRenderCache.TryGetValue(path, out cache))
            {
                cache.IconGlow?.Dispose();
                cache.IconGlow = null;
            }

            InvalidateItem(path);
        }

        private void InvalidatePointerGlow(Point position)
        {
            var radius = pointerGlowRadius > 0
                ? pointerGlowRadius
                : LogicalToDeviceUnits(GlobalFenceSettings.PointerGlowRadius);
            var bounds = Rectangle.Intersect(ClientRectangle,
                new Rectangle(position.X - radius, position.Y - radius, radius * 2, radius * 2));
            if (!bounds.IsEmpty)
                Invalidate(bounds);
        }

        private void UpdatePointerGlowBitmap()
        {
            pointerGlowBitmap?.Dispose();
            pointerGlowBitmap = null;
            pointerGlowRadius = LogicalToDeviceUnits(GlobalFenceSettings.PointerGlowRadius);
            if (!GlobalFenceSettings.GlowEffectsEnabled || GlobalFenceSettings.PointerGlowIntensity <= 0)
                return;

            var diameter = pointerGlowRadius * 2;
            var bitmap = new Bitmap(diameter, diameter);
            using (var graphics = Graphics.FromImage(bitmap))
            using (var path = new GraphicsPath())
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                path.AddEllipse(0, 0, diameter - 1, diameter - 1);
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

            pointerGlowBitmap = bitmap;
        }

        private int ContentToDeviceUnits(int logicalUnits)
        {
            var scaledUnits = (int)Math.Round(logicalUnits * GlobalFenceSettings.ContentScalePercent / 100.0);
            return LogicalToDeviceUnits(Math.Max(1, scaledUnits));
        }

        private void DrawPointerGlow(Graphics graphics)
        {
            if (pointerGlowBitmap != null)
                graphics.DrawImageUnscaled(pointerGlowBitmap,
                    pointerPosition.X - pointerGlowRadius, pointerPosition.Y - pointerGlowRadius);
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new EditDialog(Text))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    if (string.IsNullOrWhiteSpace(dialog.NewName))
                    {
                        MessageBox.Show(this, UiText.Get("A fence name cannot be empty."), UiText.Get("Invalid fence name"),
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        viewModel.Rename(dialog.NewName);
                        Text = dialog.NewName;
                        Refresh();
                    }
                    catch (Exception ex) when (IsFenceSaveError(ex))
                    {
                        MessageBox.Show(this, ex.Message, UiText.Get("Fence could not be saved"),
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void newFenceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FenceWindowCoordinator.CreateFence(UiText.Get("New fence"));
        }

        private void FenceWindow_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (Application.OpenForms.Count == 0)
                Application.Exit();
        }

        private void Save()
        {
            viewModel.Save();
        }

        internal bool FitBoundsToCurrentLayout()
        {
            var targetSize = CreateLayout().GetFitSize(fenceInfo.Files.Count);
            var targetWidth = targetSize.Width;
            var targetHeight = targetSize.Height;
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
            debouncedMove.Schedule(() =>
            {
                fenceInfo.PosX = Location.X;
                fenceInfo.PosY = Location.Y;
                Save();
            });
        }

        private void lockedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                viewModel.SetLocked(lockedToolStripMenuItem.Checked);
            }
            catch (Exception ex) when (IsFenceSaveError(ex))
            {
                lockedToolStripMenuItem.Checked = fenceInfo.Locked;
                ShowItemSaveError(ex);
            }
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

            var wheelDelta = Math.Max(1, SystemInformation.MouseWheelScrollDelta);
            var accumulatedDelta = wheelDeltaRemainder + e.Delta;
            var wheelNotches = accumulatedDelta / wheelDelta;
            wheelDeltaRemainder = accumulatedDelta % wheelDelta;
            if (wheelNotches == 0)
                return;

            var scrollLines = SystemInformation.MouseWheelScrollLines;
            if (scrollLines == 0)
                return;

            var scrollDistance = scrollLines == -1
                ? Math.Max(1, ClientRectangle.Height - titleHeight)
                : wheelNotches * scrollLines * ContentToDeviceUnits(16);
            var newScrollOffset = Math.Max(0L, Math.Min(scrollHeight, (long)scrollOffset - scrollDistance));
            if (newScrollOffset == scrollOffset)
                return;

            scrollOffset = (int)newScrollOffset;
            var hoveredItemIndex = GetItemIndexAt(PointToClient(MousePosition));
            hoveringItem = hoveredItemIndex >= 0 ? fenceInfo.Files[hoveredItemIndex] : null;

            Invalidate(new Rectangle(0, titleHeight, Width, Math.Max(0, Height - titleHeight)));
        }

        private void ThumbnailProvider_IconThumbnailLoaded(object sender,
            ThumbnailProvider.ThumbnailLoadedEventArgs e)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new EventHandler<ThumbnailProvider.ThumbnailLoadedEventArgs>(
                    ThumbnailProvider_IconThumbnailLoaded), sender, e);
                return;
            }

            InvalidateIconGlow(e.Path);
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
            ClearRenderCache();
            UpdatePointerGlowBitmap();
            ReloadFonts();

            if (wasMinified && !GlobalFenceSettings.HideTitleArea)
                Height = titleHeight;

            Invalidate();
        }

        private void LocateMissingFileItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { CheckFileExists = true, Title = UiText.Get("Locate replacement file") })
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
            using (var dialog = new FolderBrowserDialog { Description = UiText.Get("Select the replacement folder") })
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
                MessageBox.Show(this, ex.Message, UiText.Get("Could not open containing folder"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ReplaceMissingItem(string replacementPath)
        {
            if (!ItemExists(replacementPath))
            {
                MessageBox.Show(this, UiText.Get("The selected file or folder no longer exists."), UiText.Get("Cannot update fence item"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (fenceInfo.Files.Exists(path => string.Equals(path, replacementPath, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, UiText.Get("That file or folder is already in this fence."), UiText.Get("Cannot update fence item"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                if (!viewModel.ReplaceItem(hoveringItem, replacementPath))
                    return;
            }
            catch (Exception ex) when (IsFenceSaveError(ex))
            {
                MessageBox.Show(this, ex.Message, UiText.Get("Could not save fence item"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            selectedItem = replacementPath;
            hoveringItem = replacementPath;
            ClearRenderCache();
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
