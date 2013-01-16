using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;

namespace KuduSync.NET
{
    internal static class FileSystemHelpers
    {
        internal static string GetDestinationPath(string sourceRootPath, string destinationRootPath, FileSystemInfoBase info)
        {
            string sourcePath = info.FullName;
            sourcePath = sourcePath.Substring(sourceRootPath.Length)
                                   .Trim(Path.DirectorySeparatorChar);

            return Path.Combine(destinationRootPath, sourcePath);
        }

        internal static IDictionary<string, FileInfoBase> GetFiles(DirectoryInfoBase info)
        {
            if (info == null)
            {
                return null;
            }
            return info.GetFilesWithRetry().ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        }

        internal static IDictionary<string, DirectoryInfoBase> GetDirectories(DirectoryInfoBase info)
        {
            if (info == null)
            {
                return null;
            }
            return info.GetDirectories().ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        }

        // Call DirectoryInfoBase.GetFiles under a retry loop to make the system
        // more resilient when some files are temporarily in use
        internal static FileInfoBase[] GetFilesWithRetry(this DirectoryInfoBase info)
        {
            return OperationManager.Attempt(() =>
            {
                return info.GetFiles();
            });
        }
    }
}
