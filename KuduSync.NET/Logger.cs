using System;
using System.IO;
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
            var stream = Console.OpenStandardOutput();
            _writer = new StreamWriter(stream);
            _maxLogLines = maxLogLines;
        }

        public void Log(string format, params object[] args)
        {
            if (_maxLogLines == 0 || _logCounter < _maxLogLines)
            {
                _writer.WriteLine(format, args);
            }
            else if (_logCounter == _maxLogLines)
            {
                _writer.WriteLine("Omitting next output lines...");
            }
            else
            {
                // Make sure some output is still logged every 20 seconds
                if (DateTime.Now >= _nextLogTime)
                {
                    _writer.WriteLine("Working...");
                    _nextLogTime = DateTime.Now.Add(TimeSpan.FromSeconds(KeepAliveLogTimeInSeconds));
                }
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
