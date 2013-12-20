using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;

namespace KuduSync.NET
{
    public class KuduSync
    {
        private static readonly string[] _projectFileExtensions = new[] { ".csproj", ".vbproj" };
        private static readonly List<string> _emptyList = Enumerable.Empty<string>().ToList();

        private readonly Logger _logger;

        private string _from;
        private string _to;
        private DeploymentManifest _nextManifest;
        private HashSet<string> _previousManifest;
        private HashSet<string> _ignoreList;
        private bool _whatIf;
        private KuduSyncOptions _options;

        public KuduSync(KuduSyncOptions options, Logger logger)
        {
            _logger = logger;
            _options = options;

            _from = Path.GetFullPath(options.From);
            _to = Path.GetFullPath(options.To);
            _nextManifest = new DeploymentManifest(options.NextManifestFilePath);
            _previousManifest = new HashSet<string>(DeploymentManifest.LoadManifestFile(options.PreviousManifestFilePath).Paths, StringComparer.OrdinalIgnoreCase);
            _ignoreList = BuildIgnoreList(options.Ignore);
            _whatIf = options.WhatIf;

            if (!options.IgnoreManifestFile && string.IsNullOrWhiteSpace(options.NextManifestFilePath))
            {
                throw new InvalidOperationException("The 'nextManifest' option must be specified unless the 'ignoremanifest' option is set.");
            }

            if (_whatIf)
            {
                throw new NotSupportedException("WhatIf flag is currently not supported");
            }

            if (FileSystemHelpers.IsSubDirectory(_from, _to) || FileSystemHelpers.IsSubDirectory(_to, _from))
            {
                throw new InvalidOperationException("Source and destination directories cannot be sub-directories of each other");
            }
        }

        private HashSet<string> BuildIgnoreList(string ignore)
        {
            if (!String.IsNullOrEmpty(ignore))
            {
                var ignoreList = ignore.Split(';').Select(s => s.Trim());

                if (ignoreList.Any(s => s.Contains('*') || s.Contains('/') || s.Contains('\\')))
                {
                    throw new NotSupportedException("Wildcard matching (or \\) is not supported");
                }

                return new HashSet<string>(ignoreList, StringComparer.OrdinalIgnoreCase);
            }

            return new HashSet<string>();
        }

        public void Run()
        {
            _logger.Log("KuduSync.NET from: '{0}' to: '{1}'", _from, _to);

            SmartCopy(_from, _to, new DirectoryInfoWrapper(new DirectoryInfo(_from)), new DirectoryInfoWrapper(new DirectoryInfo(_to)));

            _nextManifest.SaveManifestFile();
        }

        private void SmartCopy(string sourcePath,
                               string destinationPath,
                               DirectoryInfoBase sourceDirectory,
                               DirectoryInfoBase destinationDirectory)
        {
            if (IgnorePath(sourceDirectory))
            {
                return;
            }

            // No need to copy a directory to itself
            if (destinationPath == sourceDirectory.FullName)
            {
                return;
            }

            if (!destinationDirectory.Exists)
            {
                destinationDirectory.Create();
                if (_options.CopyMetaData)
                {
                    destinationDirectory.Attributes = sourceDirectory.Attributes;
                }
            }

            var destFilesLookup = FileSystemHelpers.GetFiles(destinationDirectory);
            var sourceFilesLookup = FileSystemHelpers.GetFiles(sourceDirectory);

            foreach (var destFile in destFilesLookup.Values)
            {
                if (IgnorePath(destFile))
                {
                    continue;
                }

                // If the file doesn't exist in the source, only delete if:
                // 1. We have no previous directory
                // 2. We have a previous directory and the file exists there

                // Trim the start path
                string previousPath = FileSystemHelpers.GetRelativePath(destinationPath, destFile.FullName);
                if (!sourceFilesLookup.ContainsKey(destFile.Name) && DoesPathExistsInManifest(previousPath))
                {
                    _logger.Log("Deleting file: '{0}'", previousPath);
                    OperationManager.Attempt(() => destFile.Delete());
                }
            }

            foreach (var sourceFile in sourceFilesLookup.Values)
            {
                if (IgnorePath(sourceFile))
                {
                    continue;
                }

                _nextManifest.AddPath(sourcePath, sourceFile.FullName);

                // if the file exists in the destination then only copy it again if it's
                // last write time is different than the same file in the source (only if it changed)
                FileInfoBase targetFile;
                if (destFilesLookup.TryGetValue(sourceFile.Name, out targetFile) &&
                    sourceFile.LastWriteTimeUtc == targetFile.LastWriteTimeUtc)
                {
                    continue;
                }

                // Otherwise, copy the file
                string path = FileSystemHelpers.GetDestinationPath(sourcePath, destinationPath, sourceFile);

                var details = FileSystemHelpers.GetRelativePath(sourcePath, sourceFile.FullName) + (_options.CopyMetaData ? " " + ShorthandAttributes(sourceFile) : String.Empty);
                _logger.Log("Copying file: '{0}'", details);
                OperationManager.Attempt(() => SmartCopyFile(sourceFile, path));
            }

            var sourceDirectoryLookup = FileSystemHelpers.GetDirectories(sourceDirectory);
            var destDirectoryLookup = FileSystemHelpers.GetDirectories(destinationDirectory);

            foreach (var destSubDirectory in destDirectoryLookup.Values)
            {
                // If the directory doesn't exist in the source, only delete if:
                // 1. We have no previous directory
                // 2. We have a previous directory and the file exists there

                if (!sourceDirectoryLookup.ContainsKey(destSubDirectory.Name))
                {
                    SmartDirectoryDelete(destSubDirectory, destinationPath);
                }
            }

            foreach (var sourceSubDirectory in sourceDirectoryLookup.Values)
            {
                DirectoryInfoBase targetSubDirectory;
                if (!destDirectoryLookup.TryGetValue(sourceSubDirectory.Name, out targetSubDirectory))
                {
                    string path = FileSystemHelpers.GetDestinationPath(sourcePath, destinationPath, sourceSubDirectory);
                    targetSubDirectory = CreateDirectoryInfo(path);
                }

                _nextManifest.AddPath(sourcePath, sourceSubDirectory.FullName);

                // Sync all sub directories
                SmartCopy(sourcePath, destinationPath, sourceSubDirectory, targetSubDirectory);
            }
        }

        private void SmartDirectoryDelete(DirectoryInfoBase directory, string rootPath)
        {
            if (IgnorePath(directory))
            {
                return;
            }

            string previousDirectoryPath = FileSystemHelpers.GetRelativePath(rootPath, directory.FullName);
            if (!DoesPathExistsInManifest(previousDirectoryPath))
            {
                return;
            }

            var files = FileSystemHelpers.GetFiles(directory);
            var subDirectories = FileSystemHelpers.GetDirectories(directory);

            foreach (var file in files.Values)
            {
                string previousFilePath = FileSystemHelpers.GetRelativePath(rootPath, file.FullName);
                if (DoesPathExistsInManifest(previousFilePath))
                {
                    _logger.Log("Deleting file: '{0}'", previousFilePath);
                    var inclosuresafe = file;
                    OperationManager.Attempt(() => inclosuresafe.Delete());
                }
            }

            foreach (var subDirectory in subDirectories.Values)
            {
                SmartDirectoryDelete(subDirectory, rootPath);
            }

            if (directory.IsEmpty())
            {
                _logger.Log("Deleting directory: '{0}'", previousDirectoryPath);
                OperationManager.Attempt(() => directory.Delete());
            }
        }

        private void SmartCopyFile(FileInfoBase sourceFile, string path)
        {
            var destFile = sourceFile.CopyTo(path, overwrite: true);

            if (!_options.CopyMetaData)
            {
                return;
            }

            // we remove the existing attributes, as 'read-only' will cause an exception when writing 'creationtime' an others.
            var removeattr = sourceFile.Attributes;
            destFile.Attributes = 0;

            destFile.CreationTimeUtc = sourceFile.CreationTimeUtc;
            destFile.LastWriteTimeUtc = sourceFile.LastWriteTimeUtc;
            destFile.LastAccessTimeUtc = sourceFile.LastAccessTimeUtc;
            destFile.Attributes = removeattr;
        }

        private string ShorthandAttributes(FileSystemInfoBase sourceFile)
        {
            var sb = new StringBuilder("[");
            var sfa = sourceFile.Attributes;

            if ((sfa & FileAttributes.Archive) == FileAttributes.Archive)
            {
                sb.Append("A");
            }

            if ((sfa & FileAttributes.Hidden) == FileAttributes.Hidden)
            {
                sb.Append("H");
            }

            if ((sfa & FileAttributes.System) == FileAttributes.System)
            {
                sb.Append("S");
            }

            if ((sfa & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                sb.Append("R");
            }

            if ((sfa & FileAttributes.Compressed) == FileAttributes.Compressed)
            {
                sb.Append("C");
            }

            if ((sfa & FileAttributes.Encrypted) == FileAttributes.Encrypted)
            {
                sb.Append("E");
            }

            if (sb.Length == 1)
            {
                return String.Empty;
            }

            sb.Append("]");
            return sb.ToString();
        }

        private DirectoryInfoBase CreateDirectoryInfo(string path)
        {
            return new DirectoryInfoWrapper(new DirectoryInfo(path));
        }

        private bool IgnorePath(FileSystemInfoBase fileSystemInfoBase)
        {
            return _ignoreList.Contains(fileSystemInfoBase.Name);
        }

        private bool DoesPathExistsInManifest(string path)
        {
            return _options.IgnoreManifestFile || _previousManifest.Contains(path);
        }
    }
}
