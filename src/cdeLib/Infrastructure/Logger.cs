using System;

namespace cdeLib.Infrastructure
{
    public interface ILogger
    {
        void LogException(Exception ex, string message);
        void LogInfo(string message);
        void LogDebug(string message);
    }

    /// <summary>
    /// Simple Console Logger.
    /// </summary>
    public class Logger : ILogger
    {
        public void LogException(Exception ex, string message)
        {
            Console.WriteLine("{0}: {1} {2}", ex.GetType(), message, ex.Message);
        }

        public void LogInfo(string message)
        {
            Console.WriteLine(message);
        }

        public void LogDebug(string message)
        {
            Console.WriteLine(message);
        }
    }
}