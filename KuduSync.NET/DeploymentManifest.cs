using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KuduSync.NET
{
    public class DeploymentManifest
    {
        private readonly string _manifestFilePath;
        private List<string> _paths;

        public DeploymentManifest(string path)
        {
            _manifestFilePath = path;
            _paths = new List<string>();
        }

        public static DeploymentManifest LoadManifestFile(string path)
        {
            var deploymentManifest = new DeploymentManifest(path);

            if (!String.IsNullOrEmpty(path) && File.Exists(path))
            {
                deploymentManifest._paths = File.ReadAllLines(path).ToList();
            }

            return deploymentManifest;
        }

        public void SaveManifestFile()
        {
            File.WriteAllLines(_manifestFilePath, _paths);
        }

        public string ManifestFilePath
        {
            get { return _manifestFilePath;  }
        }

        public void AddPath(string rootPath, string path)
        {
            string relativePath = FileSystemHelpers.GetRelativePath(rootPath, path);
            _paths.Add(relativePath);
        }

        public IEnumerable<string> Paths
        {
            get
            {
                return _paths;
            }
        }
    }
}
