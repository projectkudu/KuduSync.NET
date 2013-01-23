using CommandLine;
using System;
using System.Diagnostics;
using System.IO;

namespace KuduSync.NET
{
    class Program
    {
        static void Main(string[] args)
        {
            var stopwatch = Stopwatch.StartNew();
            var kuduSyncOptions = new KuduSyncOptions();

            try
            {
                ICommandLineParser parser = new CommandLineParser();
                if (parser.ParseArguments(args, kuduSyncOptions))
                {
                    using (var logger = GetLogger(kuduSyncOptions))
                    {
                        new KuduSync(kuduSyncOptions, logger).Run();
                    }
                }
                else
                {
                    throw new InvalidOperationException("Failed to parse arguments");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error: " + ex.Message);
            }

            stopwatch.Stop();

            if (kuduSyncOptions.Perf)
            {
                Console.WriteLine("Time " + stopwatch.ElapsedMilliseconds);
            }
        }

        private static Logger GetLogger(KuduSyncOptions kuduSyncOptions)
        {
            int maxLogLines;

            if (kuduSyncOptions.Quiet)
            {
                maxLogLines = -1;
            }
            else if (kuduSyncOptions.Verbose != null)
            {
                maxLogLines = kuduSyncOptions.Verbose.Value;
            }
            else
            {
                maxLogLines = 0;
            }

            return new Logger(maxLogLines);
        }
    }
}
