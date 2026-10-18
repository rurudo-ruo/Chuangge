using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Xml;
using System.Xml.Serialization;

namespace NoFences.Model
{
    public class FenceManager
    {
        public static FenceManager Instance { get; } = new FenceManager();

        private const string MetaFileName = "__fence_metadata.xml";
        private const string JsonFenceFolderName = "Fences";
        private const string JsonFenceFileName = "fence.json";
        private const string BackupDirectoryPattern = "Backup_XML_*";
        private const string BackupFileSuffix = "___fence_metadata";
        private static readonly Regex InvalidJsonEscape = new Regex(
            @"(?<!\\)\\(?![""\\/bfnrtu])",
            RegexOptions.Compiled);

        private readonly string basePath;

        public string DataDirectory => basePath;

        public sealed class FenceLoadResult
        {
            public List<string> SkippedPaths { get; } = new List<string>();
            public int LoadedFromJson { get; internal set; }
            public int LoadedFromBackup { get; internal set; }
        }

        public FenceManager()
        {
            basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoFences");
            EnsureDirectoryExists(basePath);
        }

        public FenceLoadResult LoadFences()
        {
            var result = new FenceLoadResult();
            var loadedIds = new HashSet<Guid>();
            var failedPaths = new Dictionary<Guid, List<string>>();

            foreach (var dir in Directory.EnumerateDirectories(basePath))
            {
                Guid folderId;
                if (!Guid.TryParse(Path.GetFileName(dir), out folderId))
                    continue;

                var metaFile = Path.Combine(dir, MetaFileName);
                FenceInfo fence;

                try
                {
                    if (!File.Exists(metaFile))
                    {
                        RecordFailure(failedPaths, folderId, metaFile);
                        continue;
                    }

                    fence = ReadXmlFence(metaFile, folderId);
                }
                catch (IOException)
                {
                    RecordFailure(failedPaths, folderId, metaFile);
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    RecordFailure(failedPaths, folderId, metaFile);
                    continue;
                }
                catch (InvalidOperationException)
                {
                    RecordFailure(failedPaths, folderId, metaFile);
                    continue;
                }
                catch (XmlException)
                {
                    RecordFailure(failedPaths, folderId, metaFile);
                    continue;
                }

                ShowFence(fence);
                loadedIds.Add(folderId);
            }

            var jsonRoot = Path.Combine(basePath, JsonFenceFolderName);
            if (Directory.Exists(jsonRoot))
            {
                foreach (var dir in Directory.EnumerateDirectories(jsonRoot))
                {
                    Guid folderId;
                    if (!Guid.TryParse(Path.GetFileName(dir), out folderId) || loadedIds.Contains(folderId))
                        continue;

                    var jsonFile = Path.Combine(dir, JsonFenceFileName);
                    FenceInfo fence;
                    try
                    {
                        fence = ReadJsonFence(jsonFile, folderId);
                    }
                    catch (IOException)
                    {
                        RecordFailure(failedPaths, folderId, jsonFile);
                        continue;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        RecordFailure(failedPaths, folderId, jsonFile);
                        continue;
                    }
                    catch (InvalidOperationException)
                    {
                        RecordFailure(failedPaths, folderId, jsonFile);
                        continue;
                    }
                    catch (ArgumentException)
                    {
                        RecordFailure(failedPaths, folderId, jsonFile);
                        continue;
                    }

                    ShowFence(fence);
                    loadedIds.Add(folderId);
                    result.LoadedFromJson++;
                }
            }

            var backupDirectories = Directory.EnumerateDirectories(basePath, BackupDirectoryPattern)
                .OrderByDescending(Path.GetFileName);
            foreach (var backupDirectory in backupDirectories)
            {
                foreach (var backupFile in Directory.EnumerateFiles(backupDirectory, "*" + BackupFileSuffix + ".xml"))
                {
                    var fileName = Path.GetFileNameWithoutExtension(backupFile);
                    var idText = fileName.Substring(0, fileName.Length - BackupFileSuffix.Length);
                    Guid fenceId;
                    if (!Guid.TryParse(idText, out fenceId))
                    {
                        result.SkippedPaths.Add(backupFile);
                        continue;
                    }

                    if (loadedIds.Contains(fenceId))
                        continue;

                    FenceInfo fence;
                    try
                    {
                        fence = ReadXmlFence(backupFile, fenceId);
                    }
                    catch (IOException)
                    {
                        RecordFailure(failedPaths, fenceId, backupFile);
                        continue;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        RecordFailure(failedPaths, fenceId, backupFile);
                        continue;
                    }
                    catch (InvalidOperationException)
                    {
                        RecordFailure(failedPaths, fenceId, backupFile);
                        continue;
                    }
                    catch (XmlException)
                    {
                        RecordFailure(failedPaths, fenceId, backupFile);
                        continue;
                    }

                    ShowFence(fence);
                    loadedIds.Add(fenceId);
                    result.LoadedFromBackup++;
                }
            }

            foreach (var failure in failedPaths)
            {
                if (!loadedIds.Contains(failure.Key))
                    result.SkippedPaths.AddRange(failure.Value);
            }

            return result;
        }

        private static FenceInfo ReadXmlFence(string path, Guid expectedId)
        {
            var serializer = new XmlSerializer(typeof(FenceInfo));
            FenceInfo fence;
            using (var reader = new StreamReader(path))
            {
                fence = serializer.Deserialize(reader) as FenceInfo;
            }

            ValidateFence(fence, expectedId);
            return fence;
        }

        private static FenceInfo ReadJsonFence(string path, Guid expectedId)
        {
            var json = File.ReadAllText(path);
            json = InvalidJsonEscape.Replace(json, @"\\");

            var serializer = new JavaScriptSerializer();
            var fence = serializer.Deserialize<FenceInfo>(json);
            ValidateFence(fence, expectedId);
            return fence;
        }

        private static void ValidateFence(FenceInfo fence, Guid expectedId)
        {
            if (fence == null || fence.Id != expectedId || string.IsNullOrWhiteSpace(fence.Name) || fence.Files == null)
                throw new InvalidOperationException("Fence metadata is invalid.");
        }

        private static void RecordFailure(IDictionary<Guid, List<string>> failures, Guid fenceId, string path)
        {
            List<string> paths;
            if (!failures.TryGetValue(fenceId, out paths))
            {
                paths = new List<string>();
                failures.Add(fenceId, paths);
            }

            paths.Add(path);
        }

        private static void ShowFence(FenceInfo fence)
        {
            new FenceWindow(fence).Show();
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
