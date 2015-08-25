using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KuduSync.NET
{
    public class KuduSyncOptions : CommandLineOptionsBase
    {
        [Option("f", "from", Required = true, HelpText = "Source directory to sync")]
        public string From { get; set; }

        [Option("t", "to", Required = true, HelpText = "Destination directory to sync")]
        public string To { get; set; }

        [Option("s", "targetsubfolder", Required = false, HelpText = "A relative sub folder in the destination to create and copy files to")]
        public string TargetSubFolder { get; set; }

        [Option("n", "nextManifest", Required = false, HelpText = "Next manifest file path")]
        public string NextManifestFilePath { get; set; }

        [Option("p", "previousManifest", Required = false, HelpText = "Previous manifest file path")]
        public string PreviousManifestFilePath { get; set; }

        [Option("x", "ignoremanifest", Required = false, HelpText = "Disables the processing and generation of the manifest files.")]
        public bool IgnoreManifestFile { get; set; }

        [Option("m", "copymetadata", Required = false, HelpText = "Additionally copies 'last write/create/access times' for files and the 'filesystem attributes' for directories")]
        public bool CopyMetaData { get; set; }

        [Option("i", "ignore", Required = false, HelpText = "List of files/directories to ignore and not sync, delimited by ;")]
        public string Ignore { get; set; }

        [Option("q", "quiet", Required = false, HelpText = "No logging")]
        public bool Quiet { get; set; }

        [Option("v", "verbose", Required = false, HelpText = "Verbose logging with maximum number of output lines")]
        public int? Verbose { get; set; }

        [Option("w", "whatIf", Required = false, HelpText = "Only log without actual copy/remove of files")]
        public bool WhatIf { get; set; }

        [Option("", "perf", Required = false, HelpText = "Print out the time it took to complete KuduSync operation")]
        public bool Perf { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this, (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
