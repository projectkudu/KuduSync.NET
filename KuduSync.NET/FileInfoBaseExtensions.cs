using System;
using System.IO.Abstractions;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace KuduSync.NET
{
    static class FileInfoBaseExtensions
    {
        public static bool IsFullTextCompareFile(this IFileSystemInfo file, KuduSyncOptions kuduSyncOptions)
        {
            var matched = kuduSyncOptions.GetFullTextCompareFilePatterns()
                .Any(fileMatchPattern => Regex.IsMatch(file.Name, WildCardToRegular(fileMatchPattern), RegexOptions.IgnoreCase));

            return matched;
        }

        private static string WildCardToRegular(string value)
        {
            return "^" + Regex.Escape(value).Replace("\\?", ".").Replace("\\*", ".*") + "$";
        }        

        public static string ComputeSha1(this IFileInfo file)
        {
            using (var fileStream = file.OpenRead())
            {
                var sha1 = new SHA1Managed();
                return BitConverter.ToString(sha1.ComputeHash(fileStream));
            }
        }
    }
}
