using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;

namespace KuduSync.NET
{
    internal static class FileSystemHelpers
    {
        public static string GetDestinationPath(string sourceRootPath, string destinationRootPath, FileSystemInfoBase info)
        {
            string sourcePath = info.FullName;
            sourcePath = sourcePath.Substring(sourceRootPath.Length)
                                   .Trim(Path.DirectorySeparatorChar);

            return Path.Combine(destinationRootPath, sourcePath);
        }

        public static IDictionary<string, FileInfoBase> GetFiles(DirectoryInfoBase info)
        {
            if (info == null)
            {
                return null;
            }
            return info.GetFilesWithRetry().ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsEmpty(this DirectoryInfoBase info)
        {
            if (info == null)
            {
                return true;
            }

            FileSystemInfoBase[] fileSystemInfos = OperationManager.Attempt(() => info.GetFileSystemInfos());
            return !fileSystemInfos.Any();
        }

        public static IDictionary<string, DirectoryInfoBase> GetDirectories(DirectoryInfoBase info)
        {
            if (info == null)
            {
                return null;
            }
            return info.GetDirectories().ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        }

        // Call DirectoryInfoBase.GetFiles under a retry loop to make the system
        // more resilient when some files are temporarily in use
        public static FileInfoBase[] GetFilesWithRetry(this DirectoryInfoBase info)
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
    }
}
