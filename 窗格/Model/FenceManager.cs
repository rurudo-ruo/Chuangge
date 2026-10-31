using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Xml;
using System.Xml.Serialization;

namespace 窗格.Model
{
    public class FenceManager
    {
        public static FenceManager Instance { get; } = new FenceManager();

        private const string MetaFileName = "__fence_metadata.xml";
        private const string JsonFenceFolderName = "Fences";
        private const string JsonFenceFileName = "fence.json";
        private const string BackupDirectoryPattern = "Backup_XML_*";
        private const string BackupFileSuffix = "___fence_metadata";
        private const string TempFileSuffix = ".tmp";
        private const int MaximumMetadataBytes = 8 * 1024 * 1024;
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(FenceInfo));

        private readonly string basePath;

        public string DataDirectory => basePath;

        public sealed class FenceLoadResult
        {
            public List<FenceInfo> Fences { get; } = new List<FenceInfo>();
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
                if (!File.Exists(metaFile))
                {
                    RecordFailure(failedPaths, folderId, metaFile);
                    continue;
                }

                FenceInfo fence;
                if (!TryReadFence(() => ReadXmlFence(metaFile, folderId), folderId, metaFile, failedPaths, out fence))
                    continue;

                result.Fences.Add(fence);
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
                    if (!TryReadFence(() => ReadJsonFence(jsonFile, folderId), folderId, jsonFile, failedPaths, out fence))
                        continue;

                    result.Fences.Add(fence);
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
                    if (!TryReadFence(() => ReadXmlFence(backupFile, fenceId), fenceId, backupFile, failedPaths, out fence))
                        continue;

                    result.Fences.Add(fence);
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

        private static bool TryReadFence(Func<FenceInfo> read, Guid fenceId, string path,
            IDictionary<Guid, List<string>> failures, out FenceInfo fence)
        {
            try
            {
                fence = read();
                return true;
            }
            catch (Exception ex) when (ex is IOException
                || ex is UnauthorizedAccessException
                || ex is InvalidOperationException
                || ex is ArgumentException
                || ex is XmlException)
            {
                RecordFailure(failures, fenceId, path);
                fence = null;
                return false;
            }
        }

        private static FenceInfo ReadXmlFence(string path, Guid expectedId)
        {
            EnsureMetadataSize(path);
            FenceInfo fence;
            using (var reader = new StreamReader(path))
            {
                fence = Serializer.Deserialize(reader) as FenceInfo;
            }

            ValidateFence(fence, expectedId);
            return fence;
        }

        private static FenceInfo ReadJsonFence(string path, Guid expectedId)
        {
            EnsureMetadataSize(path);
            var json = File.ReadAllText(path);
            var serializer = new JavaScriptSerializer { MaxJsonLength = MaximumMetadataBytes };
            FenceInfo fence = null;
            try
            {
                fence = serializer.Deserialize<FenceInfo>(json);
            }
            catch (ArgumentException)
            {
                // Fall through to the legacy retry below: the file has unescaped backslashes.
            }

            if (fence == null || HasControlCharacters(fence.Files))
            {
                // The legacy writer escaped nothing, so every backslash in the file is literal.
                fence = serializer.Deserialize<FenceInfo>(json.Replace(@"\", @"\\"));
            }

            ValidateFence(fence, expectedId);
            return fence;
        }

        private static void EnsureMetadataSize(string path)
        {
            var length = new FileInfo(path).Length;
            if (length > MaximumMetadataBytes)
                throw new InvalidOperationException(
                    "Fence metadata is too large to load: " + length + " bytes.");
        }

        private static bool HasControlCharacters(IList<string> files)
        {
            if (files == null)
                return false;

            foreach (var file in files)
            {
                if (string.IsNullOrEmpty(file))
                    continue;

                foreach (var character in file)
                {
                    if (character < ' ')
                        return true;
                }
            }

            return false;
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

        public FenceInfo CreateFence(string name)
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
            return fenceInfo;
        }

        public void RemoveFence(FenceInfo info)
        {
            var xmlFolder = GetFolderPath(info);
            if (Directory.Exists(xmlFolder))
                Directory.Delete(xmlFolder, true);

            var jsonFolder = Path.Combine(basePath, JsonFenceFolderName, info.Id.ToString());
            if (Directory.Exists(jsonFolder))
                Directory.Delete(jsonFolder, true);

            foreach (var backupDirectory in Directory.EnumerateDirectories(basePath, BackupDirectoryPattern))
            {
                var backupFile = Path.Combine(backupDirectory, info.Id.ToString() + BackupFileSuffix + ".xml");
                if (File.Exists(backupFile))
                    File.Delete(backupFile);
            }
        }

        public void UpdateFence(FenceInfo fenceInfo)
        {
            var path = GetFolderPath(fenceInfo);
            EnsureDirectoryExists(path);

            var metaFile = Path.Combine(path, MetaFileName);
            var tempFile = metaFile + TempFileSuffix;
            try
            {
                using (var writer = new StreamWriter(tempFile))
                    Serializer.Serialize(writer, fenceInfo);

                // Swap in one step so an interrupted save cannot truncate the only copy.
                if (File.Exists(metaFile))
                    File.Replace(tempFile, metaFile, null);
                else
                    File.Move(tempFile, metaFile);
            }
            catch
            {
                TryDeleteFile(tempFile);
                throw;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
                // Best effort only: the original metadata file is still intact.
            }
            catch (UnauthorizedAccessException)
            {
                // Best effort only: the original metadata file is still intact.
            }
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
