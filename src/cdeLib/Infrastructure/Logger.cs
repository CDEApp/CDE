using System;
using cdeLib.Infrastructure.Config;

namespace cdeLib.Infrastructure
{
    public interface ILogger
    {
        void LogException(Exception ex, string format, params object[] paramList);
        void LogInfo(string format, params object[] paramList);
        void LogDebug(string format, params object[] paramList);
    }

    /// <summary>
    /// Simple Console Logger.
    /// </summary>
    public class Logger : ILogger
    {
        private readonly bool _consoleLogToSeq;
        private readonly Serilog.ILogger _logger;

        public Logger(IConfiguration configuration, Serilog.ILogger logger)
        {
            _consoleLogToSeq = configuration.Config.Display.ConsoleLogToSeq;
            _logger = logger;
        }

        public void LogException(Exception ex, string format, params object[] paramList)
        {
            if (_consoleLogToSeq)
                _logger.Error(ex, $"{string.Format(format, paramList)}{ex.Message}");
            else
                Console.WriteLine($"{ex.GetType()}: {string.Format(format, paramList)} {ex.Message}");
        }

        public void LogInfo(string format, params object[] paramList)
        {
            if (_consoleLogToSeq)
                _logger.Information(format, paramList);
            else
                Console.WriteLine(format, paramList);
        }

        public void LogDebug(string format, params object[] paramList)
        {
            if (_consoleLogToSeq)
                _logger.Debug(format, paramList);
            else
                Console.WriteLine(format, paramList);
        }
    }
}