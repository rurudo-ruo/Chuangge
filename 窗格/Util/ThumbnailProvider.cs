using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using 窗格.Win32;

namespace 窗格.Util
{
    public class ThumbnailProvider
    {
        // Supported .NET images as per https://docs.microsoft.com/en-us/dotnet/api/system.drawing.image.fromfile
        private static readonly string[] SupportedExtensions =
        {
            ".bmp",
            ".gif",
            ".jpg",
            ".jpeg",
            ".png",
            ".tiff",
            ".tif"
        };

        private class ThumbnailState
        {
            public Icon icon;
        }

        // Only allow 4 concurrent images to be decoded to try and prevent OOM errors
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(4);
        private readonly IDictionary<string, ThumbnailState> iconCache = new Dictionary<string, ThumbnailState>();
        private readonly List<Icon> retiredIcons = new List<Icon>();
        private readonly object syncRoot = new object();
        private bool disposed;
        public event EventHandler IconThumbnailLoaded;

        public bool IsSupported(string path)
        {
            return SupportedExtensions.Any(ext => path.EndsWith(ext));
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
                    return state.icon;

                return SubmitGeneratorTask(path, size, cacheKey).icon;
            }
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

        private ThumbnailState SubmitGeneratorTask(string path, int size, string cacheKey)
        {
            var fallbackIcon = Icon.ExtractAssociatedIcon(path);
            var state = new ThumbnailState
            {
                icon = fallbackIcon ?? (Icon)SystemIcons.Application.Clone()
            };
            iconCache[cacheKey] = state;

            Task.Run(() => GenerateThumbnailAsync(path, size, state));
            return state;
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
                            }

                            IconThumbnailLoaded?.Invoke(this, EventArgs.Empty);
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
                System.Diagnostics.Debug.WriteLine("Could not generate image thumbnail for " + path + ": " + ex);
            }
            finally
            {
                if (semaphoreAcquired)
                    semaphore.Release();
            }
        }

    }
}
