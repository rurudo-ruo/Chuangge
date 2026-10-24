using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using 窗格.Model;

namespace 窗格
{
    internal sealed class FenceViewModel
    {
        private readonly FenceManager fenceManager;

        public FenceInfo Info { get; }

        public FenceViewModel(FenceInfo info, FenceManager fenceManager)
        {
            Info = info ?? throw new ArgumentNullException(nameof(info));
            this.fenceManager = fenceManager ?? throw new ArgumentNullException(nameof(fenceManager));
        }

        public void Save()
        {
            fenceManager.UpdateFence(Info);
        }

        public void Rename(string name)
        {
            var previousName = Info.Name;
            Info.Name = name;
            try
            {
                Save();
            }
            catch
            {
                Info.Name = previousName;
                throw;
            }
        }

        public void SetLocked(bool locked)
        {
            var previousValue = Info.Locked;
            UpdateAndSave(() => Info.Locked = locked, () => Info.Locked = previousValue);
        }

        public void SetCanMinify(bool canMinify)
        {
            var previousValue = Info.CanMinify;
            UpdateAndSave(() => Info.CanMinify = canMinify, () => Info.CanMinify = previousValue);
        }

        public int AddExistingItems(IEnumerable<string> paths)
        {
            var addedPaths = new List<string>();
            foreach (var path in paths)
            {
                if ((!File.Exists(path) && !Directory.Exists(path))
                    || Info.Files.Exists(existing =>
                        string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)))
                    continue;

                Info.Files.Add(path);
                addedPaths.Add(path);
            }

            if (addedPaths.Count == 0)
                return 0;

            try
            {
                Save();
            }
            catch
            {
                foreach (var path in addedPaths)
                    Info.Files.Remove(path);
                throw;
            }

            return addedPaths.Count;
        }

        public void RemoveItemAt(int index)
        {
            var path = Info.Files[index];
            Info.Files.RemoveAt(index);
            try
            {
                Save();
            }
            catch
            {
                Info.Files.Insert(index, path);
                throw;
            }
        }

        public bool ReorderItem(int sourceIndex, int insertionIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= Info.Files.Count)
                return false;

            insertionIndex = Math.Max(0, Math.Min(insertionIndex, Info.Files.Count));
            var targetIndex = insertionIndex;
            if (sourceIndex < targetIndex)
                targetIndex--;
            if (sourceIndex == targetIndex)
                return false;

            var originalFiles = new List<string>(Info.Files);
            var path = Info.Files[sourceIndex];
            Info.Files.RemoveAt(sourceIndex);
            Info.Files.Insert(Math.Max(0, Math.Min(targetIndex, Info.Files.Count)), path);
            try
            {
                Save();
            }
            catch
            {
                RestoreFiles(originalFiles);
                throw;
            }

            return true;
        }

        public bool MoveItemFrom(FenceViewModel source, string path, int sourceIndex, int insertionIndex)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (source == this)
                throw new ArgumentException("A fence cannot be moved into itself through this operation.", nameof(source));

            if (sourceIndex < 0 || sourceIndex >= source.Info.Files.Count
                || !string.Equals(source.Info.Files[sourceIndex], path, StringComparison.OrdinalIgnoreCase))
                sourceIndex = source.Info.Files.FindIndex(existing =>
                    string.Equals(existing, path, StringComparison.OrdinalIgnoreCase));
            if (sourceIndex < 0 || Info.Files.Exists(existing =>
                string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)))
                return false;

            insertionIndex = Math.Max(0, Math.Min(insertionIndex, Info.Files.Count));
            Info.Files.Insert(insertionIndex, path);
            try
            {
                Save();
            }
            catch
            {
                Info.Files.RemoveAt(insertionIndex);
                throw;
            }

            source.Info.Files.RemoveAt(sourceIndex);
            try
            {
                source.Save();
            }
            catch (Exception sourceSaveError)
            {
                source.Info.Files.Insert(sourceIndex, path);
                Info.Files.RemoveAt(insertionIndex);

                var rollbackErrors = new List<Exception>();
                TrySave(source, rollbackErrors);
                TrySave(this, rollbackErrors);
                if (rollbackErrors.Count > 0)
                {
                    rollbackErrors.Insert(0, sourceSaveError);
                    throw new InvalidOperationException(
                        "The fence move failed: " + sourceSaveError.Message
                        + Environment.NewLine + "Rollback save errors: "
                        + string.Join("; ", rollbackErrors.Skip(1).Select(error => error.Message)),
                        new AggregateException(rollbackErrors));
                }

                ExceptionDispatchInfo.Capture(sourceSaveError).Throw();
                throw;
            }

            return true;
        }

        public bool ReplaceItem(string currentPath, string replacementPath)
        {
            var index = Info.Files.IndexOf(currentPath);
            if (index < 0)
                return false;

            var originalPath = Info.Files[index];
            Info.Files[index] = replacementPath;
            try
            {
                Save();
            }
            catch
            {
                Info.Files[index] = originalPath;
                throw;
            }

            return true;
        }

        private void RestoreFiles(IEnumerable<string> files)
        {
            Info.Files.Clear();
            Info.Files.AddRange(files);
        }

        private void UpdateAndSave(Action update, Action rollback)
        {
            update();
            try
            {
                Save();
            }
            catch
            {
                rollback();
                throw;
            }
        }

        private static void TrySave(FenceViewModel viewModel, ICollection<Exception> errors)
        {
            try
            {
                viewModel.Save();
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }
    }
}
