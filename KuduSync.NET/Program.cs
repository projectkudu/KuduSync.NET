using CommandLine;
using System;
using System.Diagnostics;
using System.IO;

namespace KuduSync.NET
{
    class Program
    {
        static int Main(string[] args)
        {
            var stopwatch = Stopwatch.StartNew();
            var kuduSyncOptions = new KuduSyncOptions();
            int exitCode = 0;

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
                    Console.Error.WriteLine(kuduSyncOptions.GetUsage());
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error: " + ex.Message);
                exitCode = 1;
            }

            stopwatch.Stop();

            if (kuduSyncOptions.Perf)
            {
                Console.WriteLine("Time " + stopwatch.ElapsedMilliseconds);
            }

            return exitCode;
        }

        private static Logger GetLogger(KuduSyncOptions kuduSyncOptions)
        {
            int maxLogLines;

            if (kuduSyncOptions.Quiet)
            {
                maxLogLines = -1;
            }
            else if (kuduSyncOptions.Verbose)
            {
                maxLogLines = int.MaxValue;
            }
            else
            {
                maxLogLines = 0;
            }

            return new Logger(maxLogLines);
        }
    }
}
