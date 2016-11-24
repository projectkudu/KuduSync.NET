using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Remoting.Messaging;
using System.Text;

namespace KuduSync.NET
{
    public class Logger : IDisposable
    {
        private const int KeepAliveLogTimeInSeconds = 20;

        private int _logCounter = 0;
        private StreamWriter _writer;
        private int _maxLogLines;
        private DateTime _nextLogTime;

        /// <summary>
        /// Logger class
        /// </summary>
        /// <param name="maxLogLines">sets the verbosity, 0 is verbose, less is quiet, more is the number of maximum log lines to write.</param>
        public Logger(int maxLogLines)
        {
            Stream stream = Console.OpenStandardOutput();
            _writer = new KuduSyncLogger(stream);
            _maxLogLines = maxLogLines;
        }


        public class KuduSyncLogger : StreamWriter
        {
            public KuduSyncLogger(Stream stream): base(stream)
            {
                

            }

            public override void WriteLine(string value)
            {
                Debug.WriteLine(value);
                base.WriteLine(value);
            }
        }



        public void Log(string format, params object[] args)
        {
            bool logged = false;


            if (_maxLogLines == 0 || _logCounter < _maxLogLines)
            {
                _writer.WriteLine(format, args);
            }
            else if (_logCounter == _maxLogLines)
            {
                _writer.WriteLine("Omitting next output lines...");
                logged = true;
            }
            else
            {
                // Make sure some output is still logged every 20 seconds
                if (DateTime.Now >= _nextLogTime)
                {
                    _writer.WriteLine("Processed {0} files...", _logCounter - 1);
                    logged = true;
                }
            }

            if (logged)
            {
                _writer.Flush();
                _nextLogTime = DateTime.Now.Add(TimeSpan.FromSeconds(KeepAliveLogTimeInSeconds));
            }

            _logCounter++;
        }

        public void Dispose()
        {
            if (_writer != null)
            {
                _writer.Dispose();
                _writer = null;
            }
        }
    }
}
