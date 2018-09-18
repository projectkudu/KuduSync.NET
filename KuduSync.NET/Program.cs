using CommandLine;
using System;
using System.Diagnostics;
using System.IO;

namespace KuduSync.NET
{
    class Program
    {
        public const string AppOfflineFileName = "app_offline.htm";
        private const string AppOfflineFileContent =
            "<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Transitional//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd\">" +
            "<html xmlns=\"http://www.w3.org/1999/xhtml\" > " +
            "<head> " +
            "    <title>Site Under Construction</title> " +
            "</head> " +
            "<body>" +
            "<!--                                                                                                                        " +
            "                                                                                                                            " +
            "    Adding additional hidden content so that IE Friendly Errors don't prevent                                               " +
            "    this message from displaying (note: it will show a 'friendly' 404                                                       " +
            "    error if the content isn't of a certain size).                                                                          " +
            "                                                                                                                            " +
            "-->" +
            "</body>" +
            "</html>";
        private const string AppOfflineSetting = "KUDU_APP_OFFLINE_CREATION";

        static int Main(string[] args)
        {
            var stopwatch = Stopwatch.StartNew();
            var kuduSyncOptions = new KuduSyncOptions();
            int exitCode = 0;
            var appOfflineCreated = false;
            var appOfflineSetting = Environment.GetEnvironmentVariable(AppOfflineSetting);
            try
            {
                ICommandLineParser parser = new CommandLineParser();
                if (parser.ParseArguments(args, kuduSyncOptions))
                {
                    using (var logger = GetLogger(kuduSyncOptions))
                    {
                        // The default behavior is to create the app_offline.htm page
                        if (string.IsNullOrWhiteSpace(appOfflineSetting) || !appOfflineSetting.Equals("false", StringComparison.OrdinalIgnoreCase))
                        {
                            appOfflineCreated = PlaceAppOffline(kuduSyncOptions.To, logger);
                        }
                        new KuduSync(kuduSyncOptions, logger, appOfflineCreated).Run();
                        if (appOfflineCreated)
                        {
                            if(!RemoveAppOffline(kuduSyncOptions.To, logger))
                            {
                                exitCode = 1;
                            }
                            appOfflineCreated = false;
                        }
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

                // If we created app_offline.htm but caught some exception while running kudusync, try to remove it.
                if (appOfflineCreated)
                {
                    RemoveAppOffline(kuduSyncOptions.To, GetLogger(kuduSyncOptions));
                }
                exitCode = 1;
            }

            stopwatch.Stop();

            if (kuduSyncOptions.Perf)
            {
                Console.WriteLine("Time " + stopwatch.ElapsedMilliseconds);
            }

            return exitCode;
        }

        private static bool PlaceAppOffline(string toDirectory, Logger logger)
        {
            var appOffline = Path.Combine(toDirectory, AppOfflineFileName);
            if (File.Exists(appOffline))
            {
                return false;
            }
            try
            {
                logger.Log("Creating " + AppOfflineFileName);
                OperationManager.Attempt(() => File.WriteAllText(appOffline, AppOfflineFileContent));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool RemoveAppOffline(string toDirectory, Logger logger)
        {
            var appOffline = Path.Combine(toDirectory, AppOfflineFileName);
            if (!File.Exists(appOffline))
            {
                return true;
            }
            try
            {
                logger.Log("Deleting " + AppOfflineFileName);
                OperationManager.Attempt(() => File.Delete(appOffline));
                return true;
            }
            catch (Exception ex)
            {
                // Panic: app_offline.htm exists (created by us), but cannot be removed
                Console.Error.WriteLine("Error: Failed to delete " + AppOfflineFileName + " from " + toDirectory + " : " + ex.Message);
                return false;
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
