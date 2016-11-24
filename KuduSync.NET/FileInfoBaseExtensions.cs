using System;
using System.IO.Abstractions;
using System.Security.Cryptography;

namespace KuduSync.NET
{
    static class FileInfoBaseExtensions
    {
        public static bool IsWebConfig(this FileInfoBase file)
        {
            return file.Name.Equals("web.config", StringComparison.OrdinalIgnoreCase);
        }

        public static string ComputeSha1(this FileInfoBase file)
        {
            using (var fileStream = file.OpenRead())
            {
                var sha1 = new SHA1Managed();
                return BitConverter.ToString(sha1.ComputeHash(fileStream));
            }
        }

        public static string ComputeSha256(this FileInfoBase file)
        {
            using (var fileStream = file.OpenRead())
            {
                var sha256 = new SHA256Managed();
                return BitConverter.ToString(sha256.ComputeHash(fileStream));
            }
        }
    }
}
