using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace NoFences.Model
{
    public class FenceManager
    {
        public static FenceManager Instance { get; } = new FenceManager();

        private const string MetaFileName = "__fence_metadata.xml";

        private readonly string basePath;

        public FenceManager()
        {
            basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoFences");
            EnsureDirectoryExists(basePath);
        }

        public List<string> LoadFences()
        {
            var skippedFolders = new List<string>();

            foreach (var dir in Directory.EnumerateDirectories(basePath))
            {
                var metaFile = Path.Combine(dir, MetaFileName);
                FenceInfo fence;

                try
                {
                    if (!File.Exists(metaFile))
                    {
                        skippedFolders.Add(dir);
                        continue;
                    }

                    var serializer = new XmlSerializer(typeof(FenceInfo));
                    using (var reader = new StreamReader(metaFile))
                    {
                        fence = serializer.Deserialize(reader) as FenceInfo;
                    }

                    if (fence == null || string.IsNullOrWhiteSpace(fence.Name) || fence.Files == null)
                        throw new InvalidOperationException("Fence metadata is incomplete.");
                }
                catch (IOException)
                {
                    skippedFolders.Add(dir);
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    skippedFolders.Add(dir);
                    continue;
                }
                catch (InvalidOperationException)
                {
                    skippedFolders.Add(dir);
                    continue;
                }
                catch (XmlException)
                {
                    skippedFolders.Add(dir);
                    continue;
                }

                new FenceWindow(fence).Show();
            }

            return skippedFolders;
        }

        public void CreateFence(string name)
        {
            var fenceInfo = new FenceInfo(Guid.NewGuid())
            {
                Name = name,
                PosX = 100,
                PosY = 250,
                Height = 300,
                Width = 300
            };

            UpdateFence(fenceInfo);
            new FenceWindow(fenceInfo).Show();
        }

        public void RemoveFence(FenceInfo info)
        {
            Directory.Delete(GetFolderPath(info), true);
        }

        public void UpdateFence(FenceInfo fenceInfo)
        {
            var path = GetFolderPath(fenceInfo);
            EnsureDirectoryExists(path);

            var metaFile = Path.Combine(path, MetaFileName);
            var serializer = new XmlSerializer(typeof(FenceInfo));
            var writer = new StreamWriter(metaFile);
            serializer.Serialize(writer, fenceInfo);
            writer.Close();
        }

        private void EnsureDirectoryExists(string dir)
        {
            var di = new DirectoryInfo(dir);
            if (!di.Exists)
                di.Create();
        }

        private string GetFolderPath(FenceInfo fenceInfo)
        {
            return Path.Combine(basePath, fenceInfo.Id.ToString());
        }
    }
}
