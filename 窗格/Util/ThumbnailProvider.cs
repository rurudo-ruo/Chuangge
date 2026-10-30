using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using 窗格.Win32;

namespace 窗格.Util
{
    public class ThumbnailProvider
    {
        public sealed class ThumbnailLoadedEventArgs : EventArgs
        {
            public string Path { get; }

            public ThumbnailLoadedEventArgs(string path)
            {
                Path = path;
            }
        }

        // Supported .NET images as per https://docs.microsoft.com/en-us/dotnet/api/system.drawing.image.fromfile
        private static readonly HashSet<string> SupportedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".bmp",
                ".gif",
                ".jpg",
                ".jpeg",
                ".png",
                ".tiff",
                ".tif"
            };

        // Bounds the decoded-icon cache; entries are evicted least-recently-used first.
        private const int MaximumCachedThumbnails = 512;

        private class ThumbnailState
        {
            public string path;
            public Icon icon;
            public long lastAccess;
            public bool pending;
        }

        // Only allow 4 concurrent images to be decoded to try and prevent OOM errors
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(4);
        private readonly Dictionary<string, ThumbnailState> iconCache =
            new Dictionary<string, ThumbnailState>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Icon> retiredIcons = new List<Icon>();
        private readonly object syncRoot = new object();
        private long accessOrder;
        private bool disposed;
        public event EventHandler<ThumbnailLoadedEventArgs> IconThumbnailLoaded;

        public bool IsSupported(string path)
        {
            return SupportedExtensions.Contains(Path.GetExtension(path));
        }

        public Icon GenerateThumbnail(string path)
        {
            return GenerateThumbnail(path, 32);
        }

        public Icon GenerateThumbnail(string path, int size)
        {
            var cacheKey = path + "|" + size;
            lock (syncRoot)
            {
                if (disposed)
                    return SystemIcons.Application;

                ThumbnailState state;
                if (iconCache.TryGetValue(cacheKey, out state))
                {
                    state.lastAccess = ++accessOrder;
                    return state.icon;
                }

                return SubmitGeneratorTask(cacheKey, path, size).icon;
            }
        }

        public Icon GenerateAssociatedIcon(string path)
        {
            const string associatedIconKeySuffix = "|associated";
            var cacheKey = path + associatedIconKeySuffix;
            lock (syncRoot)
            {
                if (disposed)
                    return SystemIcons.Application;

                ThumbnailState state;
                if (iconCache.TryGetValue(cacheKey, out state))
                {
                    state.lastAccess = ++accessOrder;
                    return state.icon;
                }

                state = AddState(cacheKey, path);
                Task.Run(() => GenerateAssociatedIconAsync(path, state));
                return state.icon;
            }
        }

        /// <summary>
        /// Disposes icons that have been replaced by a newer thumbnail. Call this from the UI
        /// thread (for example from the <see cref="IconThumbnailLoaded"/> handler) so an icon can
        /// never be destroyed while it is being drawn.
        /// </summary>
        public void ReleaseRetiredIcons()
        {
            List<Icon> retired;
            lock (syncRoot)
            {
                if (retiredIcons.Count == 0)
                    return;

                retired = new List<Icon>(retiredIcons);
                retiredIcons.Clear();
            }

            foreach (var icon in retired)
                icon.Dispose();
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (disposed)
                    return;

                disposed = true;
                foreach (var state in iconCache.Values)
                    state.icon?.Dispose();
                foreach (var icon in retiredIcons)
                    icon.Dispose();
                iconCache.Clear();
                retiredIcons.Clear();
            }
        }

        private ThumbnailState SubmitGeneratorTask(string cacheKey, string path, int size)
        {
            var state = AddState(cacheKey, path);
            Task.Run(() => GenerateThumbnailAsync(path, size, state));
            return state;
        }

        private ThumbnailState AddState(string cacheKey, string path)
        {
            TrimCache();
            var state = new ThumbnailState
            {
                path = path,
                icon = (Icon)SystemIcons.Application.Clone(),
                lastAccess = ++accessOrder,
                pending = true
            };
            iconCache[cacheKey] = state;
            return state;
        }

        private void TrimCache()
        {
            while (iconCache.Count >= MaximumCachedThumbnails)
            {
                string oldestKey = null;
                ThumbnailState oldest = null;
                foreach (var item in iconCache)
                {
                    if (item.Value.pending)
                        continue;

                    if (oldest == null || item.Value.lastAccess < oldest.lastAccess)
                    {
                        oldest = item.Value;
                        oldestKey = item.Key;
                    }
                }

                if (oldestKey == null)
                    return; // Everything is still decoding; allow a temporary overshoot.

                iconCache.Remove(oldestKey);
                oldest.icon?.Dispose();
            }
        }

        private void MarkComplete(ThumbnailState state)
        {
            lock (syncRoot)
                state.pending = false;
        }

        private void GenerateThumbnailAsync(string path, int size, ThumbnailState state)
        {
            var semaphoreAcquired = false;
            try
            {
                semaphore.Wait();
                semaphoreAcquired = true;
                using (var ms = new MemoryStream(File.ReadAllBytes(path)))
                {
                    using (var img = Image.FromStream(ms))
                    {
                        using (var thumb = (Bitmap)img.GetThumbnailImage(size, size, () => false, IntPtr.Zero))
                        {
                            var iconHandle = thumb.GetHicon();
                            Icon icon;
                            try
                            {
                                icon = (Icon)Icon.FromHandle(iconHandle).Clone();
                            }
                            finally
                            {
                                IconUtil.DestroyIcon(iconHandle);
                            }

                            lock (syncRoot)
                            {
                                if (disposed)
                                {
                                    icon.Dispose();
                                    return;
                                }

                                retiredIcons.Add(state.icon);
                                state.icon = icon;
                                state.lastAccess = ++accessOrder;
                                state.pending = false;
                            }

                            IconThumbnailLoaded?.Invoke(this, new ThumbnailLoadedEventArgs(state.path));
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException
                || ex is UnauthorizedAccessException
                || ex is ArgumentException
                || ex is ExternalException
                || ex is OutOfMemoryException)
            {
                MarkComplete(state);
                System.Diagnostics.Debug.WriteLine("Could not generate image thumbnail for " + path + ": " + ex);
            }
            finally
            {
                if (semaphoreAcquired)
                    semaphore.Release();
            }
        }

        private async Task GenerateAssociatedIconAsync(string path, ThumbnailState state)
        {
            var semaphoreAcquired = false;
            try
            {
                await semaphore.WaitAsync();
                semaphoreAcquired = true;
                var icon = Icon.ExtractAssociatedIcon(path);
                if (icon == null)
                {
                    MarkComplete(state);
                    return;
                }

                lock (syncRoot)
                {
                    if (disposed)
                    {
                        icon.Dispose();
                        return;
                    }

                    retiredIcons.Add(state.icon);
                    state.icon = icon;
                    state.lastAccess = ++accessOrder;
                    state.pending = false;
                }

                IconThumbnailLoaded?.Invoke(this, new ThumbnailLoadedEventArgs(path));
            }
            catch (Exception ex) when (ex is IOException
                || ex is UnauthorizedAccessException
                || ex is ArgumentException
                || ex is ExternalException
                || ex is OutOfMemoryException
                || ex is System.ComponentModel.Win32Exception
                || ex is System.Security.SecurityException)
            {
                MarkComplete(state);
                System.Diagnostics.Debug.WriteLine("Could not extract file icon for " + path + ": " + ex);
            }
            finally
            {
                if (semaphoreAcquired)
                    semaphore.Release();
            }
        }
    }
}
