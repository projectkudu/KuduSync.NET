using System;
using System.Collections.Generic;
using System.Configuration;
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
        private readonly string _targetSubFolder;
        private DeploymentManifest _nextManifest;
        private HashSet<string> _previousManifest;
        private HashSet<string> _ignoreList;
        private List<string> _wildcardIgnoreList;
        private bool _whatIf;
        private KuduSyncOptions _options;
        private string _toBeDeletedDirectoryPath;
        private List<Tuple<FileInfoBase, string, string>> _filesToCopyLast = new List<Tuple<FileInfoBase, string, string>>();

        public KuduSync(KuduSyncOptions options, Logger logger)
        {
            _logger = logger;
            _options = options;

            _from = Path.GetFullPath(options.From);
            _to = Path.GetFullPath(options.To);
            _targetSubFolder = options.TargetSubFolder;
            _nextManifest = new DeploymentManifest(options.NextManifestFilePath);
            _previousManifest = new HashSet<string>(DeploymentManifest.LoadManifestFile(options.PreviousManifestFilePath).Paths, StringComparer.OrdinalIgnoreCase);
            BuildIgnoreList(options.Ignore, out _ignoreList, out _wildcardIgnoreList);
            _whatIf = options.WhatIf;
            _toBeDeletedDirectoryPath = Path.Combine(Environment.ExpandEnvironmentVariables(ConfigurationManager.AppSettings["KuduSyncDataDirectory"]), "tobedeleted");

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

            if (!TryCleanupToBeDeletedDirectory())
            {
                _logger.Log("Cannot removed the 'to be deleted' directory, ignoring");
            }

            if (!string.IsNullOrEmpty(_targetSubFolder))
            {
                _to = Path.Combine(_to, _targetSubFolder);
            }
        }

        private bool TryCleanupToBeDeletedDirectory()
        {
            if (Directory.Exists(_toBeDeletedDirectoryPath))
            {
                try
                {
                    OperationManager.Attempt(() => new DirectoryInfo(_toBeDeletedDirectoryPath).Delete(recursive: true));
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        private void BuildIgnoreList(string ignore, out HashSet<string> ignoreSet, out List<string> wildcardIgnoreList)
        {
            ignoreSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            wildcardIgnoreList = new List<string>();
            if (!String.IsNullOrEmpty(ignore))
            {
                string[] ingoreTokens = ignore.Split(';');
                foreach (var token in ingoreTokens)
                {
                    var trimedToken = token.Trim();

                    if (trimedToken.StartsWith("*"))
                    {
                        wildcardIgnoreList.Add(trimedToken.TrimStart('*'));
                    }
                    else if (trimedToken.Contains("*") || trimedToken.Contains('/') || trimedToken.Contains('\\'))
                    {
                        throw new NotSupportedException(@"Wildcard support limited to prefix matching, e.g '*txt'. Other wildcard matching (or \\) is not supported");
                    }
                    else
                    {
                        ignoreSet.Add(trimedToken);
                    }
                }
            }
        }

        public void Run()
        {
            _logger.Log("KuduSync.NET from: '{0}' to: '{1}'", _from, _to);

            SmartCopy(_from, _to, _targetSubFolder, new DirectoryInfoWrapper(new DirectoryInfo(_from)), new DirectoryInfoWrapper(new DirectoryInfo(_to)));
            CopyFilesToCopyLast();

            _nextManifest.SaveManifestFile();

            TryCleanupToBeDeletedDirectory();
        }

        private void SmartCopy(string sourcePath,
                               string destinationPath,
                               string targetSubFolder,
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

                // Trim the start destinationFilePath
                string previousPath = FileSystemHelpers.GetRelativePath(destinationPath, destFile.FullName);
                if (!sourceFilesLookup.ContainsKey(destFile.Name) && DoesPathExistsInManifest(previousPath, targetSubFolder))
                {
                    _logger.Log("Deleting file: '{0}'", previousPath);
                    OperationManager.Attempt(() => SmartDeleteFile(destFile));
                }
            }

            foreach (var sourceFile in sourceFilesLookup.Values)
            {
                if (IgnorePath(sourceFile))
                {
                    continue;
                }

                _nextManifest.AddPath(sourcePath, sourceFile.FullName, targetSubFolder);

                // if the file exists in the destination then only copy it again if it's
                // last write time is different than the same file in the source (only if it changed)
                FileInfoBase targetFile;
                if (destFilesLookup.TryGetValue(sourceFile.Name, out targetFile) &&
                    sourceFile.LastWriteTimeUtc == targetFile.LastWriteTimeUtc)
                {
                    continue;
                }

                string path = FileSystemHelpers.GetDestinationPath(sourcePath, destinationPath, sourceFile);

                var details = FileSystemHelpers.GetRelativePath(sourcePath, sourceFile.FullName) + (_options.CopyMetaData ? " " + ShorthandAttributes(sourceFile) : String.Empty);

                if (sourceFile.IsWebConfig())
                {
                    // If current file is web.config check the content sha1.
                    if (!destFilesLookup.TryGetValue(sourceFile.Name, out targetFile) ||
                        !sourceFile.ComputeSha1().Equals(targetFile.ComputeSha1()))
                    {
                        // Save the file path to copy later for copying web.config forces an appDomain
                        // restart right away without respecting waitChangeNotification
                        _filesToCopyLast.Add(Tuple.Create(sourceFile, path, details));
                    }
                    continue;
                }

                // Otherwise, copy the file
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
                    SmartDirectoryDelete(destSubDirectory, destinationPath, targetSubFolder);
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

                _nextManifest.AddPath(sourcePath, sourceSubDirectory.FullName, targetSubFolder);

                // Sync all sub directories
                SmartCopy(sourcePath, destinationPath, targetSubFolder, sourceSubDirectory, targetSubDirectory);
            }
        }

        private void CopyFilesToCopyLast()
        {
            foreach (var tuple in _filesToCopyLast)
            {
                _logger.Log("Copying file: '{0}'", tuple.Item3);
                OperationManager.Attempt(() => SmartCopyFile(tuple.Item1, tuple.Item2));
            }
        }

        private void SmartDirectoryDelete(DirectoryInfoBase directory, string rootPath, string targetSubFolder)
        {
            if (IgnorePath(directory))
            {
                return;
            }

            string previousDirectoryPath = FileSystemHelpers.GetRelativePath(rootPath, directory.FullName);
            if (!DoesPathExistsInManifest(previousDirectoryPath, targetSubFolder))
            {
                return;
            }

            var files = FileSystemHelpers.GetFiles(directory);
            var subDirectories = FileSystemHelpers.GetDirectories(directory);

            foreach (var file in files.Values)
            {
                string previousFilePath = FileSystemHelpers.GetRelativePath(rootPath, file.FullName);
                if (DoesPathExistsInManifest(previousFilePath, targetSubFolder))
                {
                    _logger.Log("Deleting file: '{0}'", previousFilePath);
                    var inclosuresafe = file;
                    OperationManager.Attempt(() => SmartDeleteFile(inclosuresafe));
                }
            }

            foreach (var subDirectory in subDirectories.Values)
            {
                SmartDirectoryDelete(subDirectory, rootPath, targetSubFolder);
            }

            if (directory.IsEmpty())
            {
                _logger.Log("Deleting directory: '{0}'", previousDirectoryPath);
                OperationManager.Attempt(() => directory.Delete());
            }
        }

        private void SmartCopyFile(FileInfoBase sourceFile, string path)
        {
            var destFile = CopyFileAndMoveOnFailure(sourceFile, path);

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

        private FileInfoBase CopyFileAndMoveOnFailure(FileInfoBase sourceFile, string destinationFilePath)
        {
            return TryFileFuncAndMoveFileOnFailure(() => sourceFile.CopyTo(destinationFilePath, overwrite: true), destinationFilePath);
        }

        private void SmartDeleteFile(FileInfoBase fileToDelete)
        {
            TryFileFuncAndMoveFileOnFailure(() =>
            {
                fileToDelete.Delete();
                return null;
            }, fileToDelete.FullName);
        }

        private FileInfoBase TryFileFuncAndMoveFileOnFailure(Func<FileInfoBase> fileFunc, string destinationFilePath)
        {
            // Use KUDUSYNC_TURNOFFTRYMOVEONERROR environment setting as a kill switch for this feature
            bool tryMoveOnError = String.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUDUSYNC_TURNOFFTRYMOVEONERROR"));

            try
            {
                return fileFunc();
            }
            catch (Exception ex)
            {
                if (tryMoveOnError)
                {
                    if (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        FileInfoBase destFile = new FileInfo(destinationFilePath);
                        if (destFile.Exists)
                        {
                            if (TryMoveFileToBeDeleted(destFile))
                            {
                                return fileFunc();
                            }

                            throw new IOException("Failed to change file that is currently being used \"" + destinationFilePath + '\"', ex);
                        }
                    }
                }

                throw;
            }
        }

        /// <summary>
        /// Move file to the tobedeleted directory
        /// </summary>
        /// <param name="fileToMove">The file to be moved</param>
        /// <returns>true if action was successful otherwise false</returns>
        private bool TryMoveFileToBeDeleted(FileInfoBase fileToMove)
        {
            try
            {
                if (!Directory.Exists(_toBeDeletedDirectoryPath))
                {
                    Directory.CreateDirectory(_toBeDeletedDirectoryPath);
                }

                fileToMove.MoveTo(Path.Combine(_toBeDeletedDirectoryPath, fileToMove.Name + "." + Path.GetRandomFileName()));

                return true;
            }
            catch
            {
                return false;
            }
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
            return (_ignoreList.Contains(fileSystemInfoBase.Name)
                || _wildcardIgnoreList.Any((name) => fileSystemInfoBase.Name.EndsWith(name, StringComparison.OrdinalIgnoreCase)));
        }

        private bool DoesPathExistsInManifest(string path, string targetSubFolder)
        {
            if (!string.IsNullOrEmpty(targetSubFolder))
            {
                path = Path.Combine(targetSubFolder, path);
            }
            return _options.IgnoreManifestFile || _previousManifest.Contains(path);
        }
    }
}
