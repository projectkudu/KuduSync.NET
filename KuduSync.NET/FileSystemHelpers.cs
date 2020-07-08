using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;

namespace KuduSync.NET
{
    internal static class FileSystemHelpers
    {
        public static string GetDestinationPath(string sourceRootPath, string destinationRootPath, IFileSystemInfo info)
        {
            string sourcePath = info.FullName;
            sourcePath = sourcePath.Substring(sourceRootPath.Length)
                                   .Trim(Path.DirectorySeparatorChar);

            return Path.Combine(destinationRootPath, sourcePath);
        }

        public static IDictionary<string, IFileInfo> GetFiles(IDirectoryInfo info)
        {
            if (info == null)
            {
                return null;
            }
            return info.GetFilesWithRetry().ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsEmpty(this IDirectoryInfo info)
        {
            if (info == null)
            {
                return true;
            }

            IFileSystemInfo[] fileSystemInfos = OperationManager.Attempt(() => info.GetFileSystemInfos());
            return fileSystemInfos.Length == 0;
        }

        public static IDictionary<string, IDirectoryInfo> GetDirectories(IDirectoryInfo info)
        {
            if (info == null)
            {
                return null;
            }
            return info.GetDirectories().ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        }

        // Call DirectoryInfoBase.GetFiles under a retry loop to make the system
        // more resilient when some files are temporarily in use
        public static IFileInfo[] GetFilesWithRetry(this IDirectoryInfo info)
        {
            return OperationManager.Attempt(() =>
            {
                return info.GetFiles();
            });
        }

        public static string GetRelativePath(string rootPath, string path)
        {
            if (String.IsNullOrEmpty(rootPath))
            {
                throw new ArgumentNullException("rootPath");
            }

            if (String.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException("path");
            }

            return path.Substring(rootPath.Length).TrimStart('\\');
        }

        public static bool IsSubDirectory(string path1, string path2)
        {
            // Avoid false positives when comparing source and destination names.
            // i.e. Compare 'directory\' to 'directory23\'
            // rather than 'directory' to 'directory23'
            char separator = Path.DirectorySeparatorChar;
            if (path1 != null && path2 != null)
            {
                path1 = Path.GetFullPath(path1) + separator;
                path2 = Path.GetFullPath(path2) + separator;
                return path2.StartsWith(path1, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}
