using CommandLine;
using System;
using System.Diagnostics;

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
                    SetLogger(kuduSyncOptions);
                    new KuduSync(kuduSyncOptions).Run();
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

        private static void SetLogger(KuduSyncOptions kuduSyncOptions)
        {
            if (kuduSyncOptions.Quiet)
            {
                Logger.MaxLogLines = -1;
            }
            else if (kuduSyncOptions.Verbose != null)
            {
                Logger.MaxLogLines = kuduSyncOptions.Verbose.Value;
            }
            else
            {
                Logger.MaxLogLines = 0;
            }
        }
    }
}
