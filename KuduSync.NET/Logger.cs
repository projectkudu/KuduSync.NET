using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KuduSync.NET
{
    public static class Logger
    {
        private static int logCounter = 0;

        /// <summary>
        /// MaxLogLines sets the verbosity, 0 is verbose, less is quiet, more is the number of maximum log lines to write.
        /// </summary>
        public static int MaxLogLines { get; set; }

        public static void Log(string format, params object[] args)
        {
            if (MaxLogLines == 0 || logCounter < MaxLogLines)
            {
                Console.WriteLine(format, args);
            }
            else if (logCounter == MaxLogLines)
            {
                Console.WriteLine("Omitting next output lines...");
            }

            logCounter++;
        }
    }
}
