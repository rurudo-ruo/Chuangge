using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using 窗格.Model;

namespace 窗格
{
    internal sealed class FenceItemDragData
    {
        public FenceWindow Source { get; set; }
        public string Path { get; set; }
        public int Index { get; set; }
    }

    /// <summary>
    /// Owns the thumbnail drag gesture, the drag/drop effects and the insertion marker state.
    /// The window forwards its input events here and reads <see cref="InsertionIndex"/> while
    /// painting; item mutations stay in <see cref="FenceViewModel"/>.
    /// </summary>
    internal sealed class FenceDragController
    {
        private readonly FenceWindow owner;
        private Point mouseDown;
        private string dragPath;
        private int dragIndex = -1;

        public FenceDragController(FenceWindow owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public int InsertionIndex { get; private set; } = -1;

        public bool SuppressNextClick { get; set; }

        public bool IsDragging { get; private set; }

        public void OnMouseDown(MouseEventArgs e)
        {
            SuppressNextClick = false;
            if (e.Button != MouseButtons.Left || owner.IsLocked)
                return;

            var itemIndex = owner.GetItemIndexAt(e.Location);
            dragPath = itemIndex >= 0 ? owner.ItemPaths[itemIndex] : null;
            dragIndex = itemIndex;
            mouseDown = e.Location;
        }

        public void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !IsDragging)
            {
                dragPath = null;
                dragIndex = -1;
            }
        }

        public void UpdateDrag(MouseEventArgs e)
        {
            if (owner.IsLocked || e.Button != MouseButtons.Left || dragPath == null || IsDragging)
                return;

            var dragSize = SystemInformation.DragSize;
            var dragRectangle = new Rectangle(
                mouseDown.X - dragSize.Width / 2,
                mouseDown.Y - dragSize.Height / 2,
                dragSize.Width,
                dragSize.Height);
            if (dragRectangle.Contains(e.Location))
                return;

            IsDragging = true;
            SuppressNextClick = true;
            try
            {
                var data = new DataObject();
                data.SetData(typeof(FenceItemDragData), new FenceItemDragData
                {
                    Source = owner,
                    Path = dragPath,
                    Index = dragIndex
                });
                owner.DoDragDrop(data, DragDropEffects.Move);
            }
            finally
            {
                dragPath = null;
                dragIndex = -1;
                IsDragging = false;
                ClearInsertionMarker();
            }
        }

        public void OnDragEnter(DragEventArgs e)
        {
            if (owner.IsLocked)
                return;

            if (e.Data.GetDataPresent(typeof(FenceItemDragData)))
                e.Effect = DragDropEffects.Move;
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        public void OnDragOver(DragEventArgs e)
        {
            if (owner.IsLocked)
            {
                e.Effect = DragDropEffects.None;
                ClearInsertionMarker();
                return;
            }

            if (e.Data.GetDataPresent(typeof(FenceItemDragData)))
            {
                e.Effect = DragDropEffects.Move;
                InsertionIndex = GetDropInsertionIndex(owner.PointToClient(new Point(e.X, e.Y)));
                owner.Invalidate();
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        public void OnDragLeave()
        {
            ClearInsertionMarker();
        }

        public void OnDragDrop(DragEventArgs e)
        {
            try
            {
                if (owner.IsLocked)
                    return;

                var itemDrag = e.Data.GetData(typeof(FenceItemDragData)) as FenceItemDragData;
                if (itemDrag != null)
                {
                    MoveFenceItem(itemDrag, GetDropInsertionIndex(owner.PointToClient(new Point(e.X, e.Y))));
                    return;
                }

                var dropped = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (dropped == null)
                    return;

                try
                {
                    owner.ViewModel.AddExistingItems(dropped);
                    owner.RefreshEntryCache();
                }
                catch (Exception ex) when (FenceViewModel.IsSaveError(ex))
                {
                    MessageBox.Show(owner, ex.Message, UiText.Get("Could not save fence items"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                ClearInsertionMarker();
                owner.Invalidate();
            }
        }

        public void ClearInsertionMarker()
        {
            InsertionIndex = -1;
            owner.Invalidate();
        }

        private int GetDropInsertionIndex(Point point)
        {
            return owner.CreateLayout().GetDropInsertionIndex(point, owner.ItemPaths.Count);
        }

        private void MoveFenceItem(FenceItemDragData dragData, int insertionIndex)
        {
            var source = dragData.Source;
            var sourceFiles = source.ItemPaths;
            var sourceIndex = dragData.Index >= 0 && dragData.Index < sourceFiles.Count
                && string.Equals(sourceFiles[dragData.Index], dragData.Path, StringComparison.OrdinalIgnoreCase)
                ? dragData.Index
                : sourceFiles.FindIndex(path =>
                    string.Equals(path, dragData.Path, StringComparison.OrdinalIgnoreCase));
            if (sourceIndex < 0)
                return;

            try
            {
                if (source == owner)
                {
                    if (!owner.ViewModel.ReorderItem(sourceIndex, insertionIndex))
                        return;
                    owner.SelectedPath = dragData.Path;
                }
                else
                {
                    if (!owner.ViewModel.MoveItemFrom(source.ViewModel, dragData.Path, sourceIndex, insertionIndex))
                        return;
                    owner.SelectedPath = dragData.Path;
                    source.SelectedPath = null;
                    source.RemoveRenderCacheEntry(dragData.Path);
                    source.Invalidate();
                }
                owner.RemoveRenderCacheEntry(dragData.Path);
            }
            catch (Exception ex) when (FenceViewModel.IsSaveError(ex))
            {
                owner.ShowItemSaveError(ex);
            }

            owner.Invalidate();
        }
    }
}
