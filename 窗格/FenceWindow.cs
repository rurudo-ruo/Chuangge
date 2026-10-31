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
        private const int minimumPointerGlowIntervalMilliseconds = 16;

        private readonly FenceViewModel viewModel;
        private FenceInfo fenceInfo => viewModel.Info;

        internal string FenceName => fenceInfo.Name;

        internal bool IsLocked => lockedToolStripMenuItem.Checked;

        internal List<string> ItemPaths => fenceInfo.Files;

        internal FenceViewModel ViewModel => viewModel;

        internal string SelectedPath
        {
            get { return selectedItem; }
            set { selectedItem = value; }
        }

        private Font titleFont;
        private readonly FenceRenderer renderer;
        private readonly FenceDragController drag;

        private string selectedItem;
        private string hoveringItem;
        private bool isMinified;
        private int prevHeight;
        private Point pointerPosition;
        private bool pointerInside;
        private Point glowPosition;
        private bool glowVisible;
        private int lastGlowUpdateTicks;
        private int lastEntryRefreshTicks;
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

        private readonly Dictionary<string, FenceEntry> entryCache =
            new Dictionary<string, FenceEntry>(StringComparer.OrdinalIgnoreCase);
        private long wheelDeltaRemainder;
        private Bitmap pointerGlowBitmap;
        private int pointerGlowRadius;

        private void ReloadFonts()
        {
            var titleSize = logicalTitleHeight / 2.0f;
            var newTitleFont = new Font("Segoe UI", titleSize, FontStyle.Regular, GraphicsUnit.Point);
            var oldTitleFont = titleFont;
            titleFont = newTitleFont;
            oldTitleFont?.Dispose();
        }

        /// <summary>
        /// Asks DWM to round the fence corners. Windows 11 honours this; older versions keep the
        /// square window, which preserves the drop shadow that a clipping region would cost.
        /// </summary>
        private void UpdateWindowCorners()
        {
            const int dwmWindowCornerPreference = 33;
            const int dwmRound = 2;
            var preference = dwmRound;
            DropShadow.DwmSetWindowAttribute(Handle, dwmWindowCornerPreference, ref preference, sizeof(int));
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

            renderer = new FenceRenderer(thumbnailProvider, ContentToDeviceUnits);
            drag = new FenceDragController(this);
            ReloadFonts();
            renderer.ReloadFonts();
            renderer.UpdateThemeBrushes();

            AllowDrop = true;


            // Deliberately not the fence name: Windows can render the window text in drag
            // ghosts, switchers and previews even though this window draws its own title.
            Text = "窗格";
            Location = new Point(fenceInfo.PosX, fenceInfo.PosY);

            Width = fenceInfo.Width;
            Height = fenceInfo.Height;

            UpdateWindowCorners();

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

            // Remove the non-client area entirely. Both WM_NCCALCSIZE passes must be
            // suppressed: the app draws and hit-tests its own title, and letting the
            // wParam == 0 pass reach the default handler makes Windows reserve a native
            // caption, which it then paints with the window text over the fence.
            if (m.Msg == WM_NCCALCSIZE)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            // Prevent maximize. The low four bits of wParam carry system flags, so mask them:
            // this covers both SC_MAXIMIZE (0xF030) and the 0xF032 variant some themes send.
            if (m.Msg == WM_SYSCOMMAND && (m.WParam.ToInt32() & 0xFFF0) == SC_MAXIMIZE)
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
                    EnterFence();
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
            renderer.ReloadFonts();

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
            InvalidateContent();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            GlobalFenceSettings.Changed -= GlobalFenceSettings_Changed;
            thumbnailProvider.IconThumbnailLoaded -= ThumbnailProvider_IconThumbnailLoaded;
            thumbnailProvider.Dispose();
            debouncedMove.Dispose();
            debouncedResize.Dispose();
            renderer.Dispose();
            pointerGlowBitmap?.Dispose();
            pointerGlowBitmap = null;
            titleFont?.Dispose();
            shellContextMenu.Dispose();
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
            catch (Exception ex) when (FenceViewModel.IsSaveError(ex))
            {
                e.Cancel = true;
                AppLogger.Error("Could not save fence before closing", ex);
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
                    AppLogger.Error("Could not remove saved fence data", ex);
                    MessageBox.Show(this, UiText.Get("Could not remove all saved copies of this fence. It may reappear when 窗格 starts again.") +
                        Environment.NewLine + ex.Message, UiText.Get("Fence could not be removed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (UnauthorizedAccessException ex)
                {
                    AppLogger.Error("Could not remove saved fence data", ex);
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

            var removedItem = hoveringItem;
            try
            {
                viewModel.RemoveItemAt(itemIndex);
            }
            catch (Exception ex) when (FenceViewModel.IsSaveError(ex))
            {
                ShowItemSaveError(ex);
                return;
            }

            ForgetEntry(removedItem);
            hoveringItem = null;
            InvalidateContent();
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
            drag.OnDragEnter(e);
        }

        private void FenceWindow_DragOver(object sender, DragEventArgs e)
        {
            drag.OnDragOver(e);
        }

        private void FenceWindow_DragLeave(object sender, EventArgs e)
        {
            drag.OnDragLeave();
        }

        private void FenceWindow_DragDrop(object sender, DragEventArgs e)
        {
            drag.OnDragDrop(e);
        }

        internal void ShowItemSaveError(Exception ex)
        {
            AppLogger.Error("Could not save fence item", ex);
            MessageBox.Show(this, ex.Message, UiText.Get("Could not save fence item"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void ShowDeferredSaveError(Exception ex)
        {
            AppLogger.Error("Could not save fence", ex);
            MessageBox.Show(this, ex.Message, UiText.Get("Fence could not be saved"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void FenceWindow_Resize(object sender, EventArgs e)
        {
            debouncedResize.Schedule(() =>
            {
                fenceInfo.Width = Width;
                fenceInfo.Height = isMinified ? prevHeight : Height;
                Save();
            });

            InvalidateContent();
        }

        private void FenceWindow_MouseMove(object sender, MouseEventArgs e)
        {
            pointerPosition = e.Location;
            pointerInside = true;
            var previousHoveringItem = hoveringItem;
            var hoveredItemIndex = GetItemIndexAt(e.Location);
            hoveringItem = hoveredItemIndex >= 0 ? fenceInfo.Files[hoveredItemIndex] : null;
            if (!string.Equals(previousHoveringItem, hoveringItem, StringComparison.OrdinalIgnoreCase))
            {
                InvalidateItem(previousHoveringItem);
                InvalidateItem(hoveringItem);
            }

            drag.UpdateDrag(e);

            if (GlobalFenceSettings.GlowEffectsEnabled && GlobalFenceSettings.PointerGlowIntensity > 0)
                UpdatePointerGlow(pointerPosition);
        }

        private void FenceWindow_MouseDown(object sender, MouseEventArgs e)
        {
            drag.OnMouseDown(e);
        }

        private void FenceWindow_MouseUp(object sender, MouseEventArgs e)
        {
            drag.OnMouseUp(e);
        }

        internal int GetItemIndexAt(Point point)
        {
            return CreateLayout().GetItemIndexAt(point, fenceInfo.Files.Count);
        }

        private Rectangle GetItemBounds(int index)
        {
            return CreateLayout().GetItemBounds(index);
        }

        internal FenceLayout CreateLayout()
        {
            var itemPadding = ContentToDeviceUnits(logicalItemPadding);
            var itemWidth = ContentToDeviceUnits(logicalItemWidth);
            var itemHeight = ContentToDeviceUnits(logicalIconSize)
                + itemPadding + ContentToDeviceUnits(logicalTextHeight);
            return new FenceLayout(Width, titleHeight, scrollOffset, itemPadding, itemWidth, itemHeight);
        }

        private void FenceWindow_MouseEnter(object sender, EventArgs e)
        {
            EnterFence();
            if (GlobalFenceSettings.GlowEffectsEnabled && GlobalFenceSettings.PointerGlowIntensity > 0)
            {
                glowPosition = pointerPosition;
                glowVisible = true;
                lastGlowUpdateTicks = Environment.TickCount;
                InvalidatePointerGlow(glowPosition);
            }
            else
            {
                glowVisible = false;
            }

            // Bound hover thrash: re-probing a fence that points at a slow share must not
            // happen on every entry.
            var now = Environment.TickCount;
            if (unchecked(now - lastEntryRefreshTicks) >= 1000)
            {
                lastEntryRefreshTicks = now;
                RefreshEntryCache();
            }
        }

        private void EnterFence()
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
            var previousSelectedItem = selectedItem;
            var previousHoveringItem = hoveringItem;
            pointerInside = false;
            if (!drag.IsDragging)
                Minify();
            selectedItem = null;
            hoveringItem = null;
            InvalidateItem(previousHoveringItem);
            if (glowVisible)
            {
                InvalidatePointerGlow(glowPosition);
                glowVisible = false;
            }
            InvalidateItem(previousSelectedItem);
        }

        private void Minify()
        {
            if (!GlobalFenceSettings.HideTitleArea && minifyToolStripMenuItem.Checked && !isMinified)
            {
                isMinified = true;
                prevHeight = Height;
                Height = titleHeight;
                InvalidateContent();
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
            catch (Exception ex) when (FenceViewModel.IsSaveError(ex))
            {
                minifyToolStripMenuItem.Checked = fenceInfo.CanMinify;
                ShowItemSaveError(ex);
            }
        }

        private void FenceWindow_Click(object sender, EventArgs e)
        {
            if (drag.SuppressNextClick)
            {
                drag.SuppressNextClick = false;
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
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            RenderContent(graphics);

            // Keep the halo out of the title area, as it was when items and glow shared a clip.
            graphics.SetClip(new Rectangle(0, titleHeight, Width, Math.Max(0, Height - titleHeight)),
                CombineMode.Intersect);

            if (GlobalFenceSettings.GlowEffectsEnabled && pointerInside)
                DrawPointerGlow(graphics);
        }

        /// <summary>
        /// Marks the fence for a repaint. Kept as the content-change entry point for the drag
        /// controller; the fence draws straight to the window surface so the DWM blur keeps the
        /// per-pixel alpha produced by the translucent fills.
        /// </summary>
        internal void InvalidateContent()
        {
            Invalidate();
        }

        /// <summary>
        /// Draws everything that changes with the fence content: background, title, items, drag
        /// marker and scroll bar. The pointer halo is drawn separately, on top.
        /// </summary>
        private void RenderContent(Graphics graphics)
        {
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
                    graphics.DrawString(fenceInfo.Name, titleFont, titleTextBrush,
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
            scrollHeight = layout.GetContentOverflow(fenceInfo.Files.Count, ClientRectangle.Height - titleHeight);
            var clampedScrollOffset = Math.Max(0, Math.Min(scrollOffset, scrollHeight));
            if (clampedScrollOffset != scrollOffset)
            {
                scrollOffset = clampedScrollOffset;
                layout = CreateLayout();
            }

            var itemWidth = ContentToDeviceUnits(logicalItemWidth);
            var iconSize = ContentToDeviceUnits(logicalIconSize);
            var textHeight = ContentToDeviceUnits(logicalTextHeight);
            var itemHeight = iconSize + ContentToDeviceUnits(logicalItemPadding) + textHeight;
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
                var entry = GetEntry(fenceInfo.Files[i]);
                var isSelected = string.Equals(selectedItem, entry.Path, StringComparison.OrdinalIgnoreCase);
                var isHovered = string.Equals(hoveringItem, entry.Path, StringComparison.OrdinalIgnoreCase);
                renderer.DrawItem(graphics, entry, bounds.Left, bounds.Top,
                    itemWidth, textHeight, iconSize, itemHeight, isSelected, isHovered);
            }

            if (drag.InsertionIndex >= 0)
                DrawDragInsertionMarker(graphics, layout, drag.InsertionIndex);

            // Scroll bars
            if (scrollHeight > 0)
            {
                var contentHeight = Math.Max(0, Height - titleHeight);
                if (contentHeight > 0)
                {
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
                        using (var scrollPath = FenceRenderer.CreateRoundedRectangle(scrollRect, scrollWidth / 2))
                            graphics.FillPath(scrollBrush, scrollPath);
                    }
                }
            }
        }

        private void DrawDragInsertionMarker(Graphics graphics, FenceLayout layout, int insertionIndex)
        {
            if (fenceInfo.Files.Count == 0)
                return;

            var beforeItem = insertionIndex < fenceInfo.Files.Count;
            var markerTarget = beforeItem
                ? layout.GetItemBounds(insertionIndex)
                : layout.GetItemBounds(fenceInfo.Files.Count - 1);
            var x = beforeItem
                ? markerTarget.Left - ContentToDeviceUnits(3)
                : markerTarget.Right + ContentToDeviceUnits(3);
            var markerWidth = ContentToDeviceUnits(3);
            var markerRect = new Rectangle(x - markerWidth / 2, markerTarget.Top,
                markerWidth, markerTarget.Height);
            using (var markerBrush = new SolidBrush(FluentTheme.Accent))
            using (var markerPath = FenceRenderer.CreateRoundedRectangle(markerRect, markerWidth / 2))
                graphics.FillPath(markerBrush, markerPath);
        }

        private void ClearRenderCache()
        {
            renderer.Clear();
        }

        internal void RemoveRenderCacheEntry(string path)
        {
            renderer.Remove(path);
        }

        private FenceEntry GetEntry(string path)
        {
            FenceEntry entry;
            if (!entryCache.TryGetValue(path, out entry))
            {
                entry = FenceEntry.FromPath(path);
                entryCache[path] = entry;
            }

            return entry;
        }

        private void ForgetEntry(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            entryCache.Remove(path);
            RemoveRenderCacheEntry(path);
        }

        /// <summary>
        /// Re-probes the items of this fence off the paint path. Called when the pointer enters
        /// the fence and after content-changing operations.
        /// </summary>
        internal void RefreshEntryCache()
        {
            if (fenceInfo.Files.Count == 0)
            {
                entryCache.Clear();
                return;
            }

            var resolved = new Dictionary<string, FenceEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in fenceInfo.Files)
                resolved[path] = FenceEntry.FromPath(path);

            var changed = false;
            foreach (var pair in resolved)
            {
                FenceEntry existing;
                if (!entryCache.TryGetValue(pair.Key, out existing) || existing.Type != pair.Value.Type)
                {
                    RemoveRenderCacheEntry(pair.Key);
                    changed = true;
                }
            }

            entryCache.Clear();
            foreach (var pair in resolved)
                entryCache[pair.Key] = pair.Value;

            if (changed)
                InvalidateContent();
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
            renderer.RemoveGlow(path);
            InvalidateItem(path);
        }

        private void UpdatePointerGlow(Point position)
        {
            var now = Environment.TickCount;
            if (glowVisible && unchecked(now - lastGlowUpdateTicks) < minimumPointerGlowIntervalMilliseconds)
                return;

            lastGlowUpdateTicks = now;
            InvalidatePointerGlow(glowVisible ? glowPosition : position, position);
            glowPosition = position;
            glowVisible = true;
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

        private void InvalidatePointerGlow(Point previousPosition, Point currentPosition)
        {
            var radius = pointerGlowRadius > 0
                ? pointerGlowRadius
                : LogicalToDeviceUnits(GlobalFenceSettings.PointerGlowRadius);
            var previousBounds = Rectangle.Intersect(ClientRectangle,
                new Rectangle(previousPosition.X - radius, previousPosition.Y - radius, radius * 2, radius * 2));
            var currentBounds = Rectangle.Intersect(ClientRectangle,
                new Rectangle(currentPosition.X - radius, currentPosition.Y - radius, radius * 2, radius * 2));
            var bounds = previousBounds.IsEmpty
                ? currentBounds
                : currentBounds.IsEmpty
                    ? previousBounds
                    : Rectangle.Union(previousBounds, currentBounds);
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
                    glow.FocusScales = new PointF(0.32f, 0.32f);
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
            if (!glowVisible || pointerGlowBitmap == null)
                return;

            graphics.DrawImageUnscaled(pointerGlowBitmap,
                glowPosition.X - pointerGlowRadius, glowPosition.Y - pointerGlowRadius);
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new EditDialog(fenceInfo.Name))
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
                        InvalidateContent();
                        Refresh();
                    }
                    catch (Exception ex) when (FenceViewModel.IsSaveError(ex))
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

            InvalidateContent();
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
            catch (Exception ex) when (FenceViewModel.IsSaveError(ex))
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

            InvalidateContent();
        }

        private void ThumbnailProvider_IconThumbnailLoaded(object sender,
            ThumbnailProvider.ThumbnailLoadedEventArgs e)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new EventHandler<ThumbnailProvider.ThumbnailLoadedEventArgs>(
                        ThumbnailProvider_IconThumbnailLoaded), sender, e);
                }
                catch (InvalidOperationException)
                {
                    // The window was destroyed between the checks above and the marshal.
                }
                return;
            }

            thumbnailProvider.ReleaseRetiredIcons();
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
                try
                {
                    BeginInvoke(new EventHandler(GlobalFenceSettings_Changed), sender, e);
                }
                catch (InvalidOperationException)
                {
                    // The window was destroyed between the checks above and the marshal.
                }
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
            renderer.ReloadFonts();
            renderer.UpdateThemeBrushes();

            if (wasMinified && !GlobalFenceSettings.HideTitleArea)
                Height = titleHeight;

            InvalidateContent();
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
                ShellLaunch.Open(directory);
            }
            catch (Win32Exception ex)
            {
                MessageBox.Show(this, ex.Message, UiText.Get("Could not open containing folder"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ReplaceMissingItem(string replacementPath)
        {
            var previousPath = hoveringItem;

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
            catch (Exception ex) when (FenceViewModel.IsSaveError(ex))
            {
                MessageBox.Show(this, ex.Message, UiText.Get("Could not save fence item"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            selectedItem = replacementPath;
            hoveringItem = replacementPath;
            ForgetEntry(previousPath);
            RefreshEntryCache();
            InvalidateContent();
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
