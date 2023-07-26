﻿using CommandLine;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using CommandLine.Text;

namespace KuduSync.NET
{
    class Program
    {
        public const string AppOfflineFileName = "app_offline.htm";

        // "Created by kudu" text is used by kudu to identify that this is kudusync's app_offline
        // If this changes, kudu will need an update as well.
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
            "    - Created by kudu                                                                                                       " +
            "-->" +
            "</body>" +
            "</html>";
        private const string AppOfflineSetting = "SCM_CREATE_APP_OFFLINE";

        static int Main(string[] args)
        {
            var stopwatch = Stopwatch.StartNew();
            int exitCode = 0;
            var appOfflineCreated = false;
            var appOfflineSetting = Environment.GetEnvironmentVariable(AppOfflineSetting);
            KuduSyncOptions kuduSyncOptions = new KuduSyncOptions();
            try
            {
                Parser.Default.ParseArguments<KuduSyncOptions>(args)
                    .WithParsed(parserResult =>
                        {
                            kuduSyncOptions = parserResult;
                            using (var logger = GetLogger(kuduSyncOptions))
                            {
                                // The default behavior is to create the app_offline.htm page
                                if (string.IsNullOrWhiteSpace(appOfflineSetting) || !appOfflineSetting.Equals("0"))
                                {
                                    appOfflineCreated = CreateAppOffline(kuduSyncOptions.To, logger);
                                }

                                new KuduSync(kuduSyncOptions, logger, new FileSystem(), appOfflineCreated).Run();
                                if (appOfflineCreated)
                                {
                                    if (!RemoveAppOffline(kuduSyncOptions.To, logger))
                                    {
                                        exitCode = 1;
                                    }

                                    appOfflineCreated = false;
                                }
                            }
                        }
                    );
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

        private static bool CreateAppOffline(string toDirectory, Logger logger)
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
            // If app_offline.htm does not exist or if it's overwritten, we don't have to delete it
            if (!File.Exists(appOffline) || !File.ReadAllText(appOffline).Equals(AppOfflineFileContent))
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
