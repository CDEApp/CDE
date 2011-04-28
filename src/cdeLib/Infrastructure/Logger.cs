using System;

namespace cdeLib.Infrastructure
{
    public interface ILogger
    {
        void LogException(Exception ex, string message);
    }

    /// <summary>
    /// Simple Console Logger.
    /// </summary>
    public class Logger : ILogger
    {
        public void LogException(Exception ex, string message)
        {
            Console.WriteLine("{0} {1}",message,ex.Message);
        }
    }
}