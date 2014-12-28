using System;
using System.Reflection;

namespace cdeLib.Infrastructure
{
    public interface ILogger
    {
        void LogException(Exception ex, string format, params object[] paramList);
        void LogInfo(string format, params object[] paramList);
        void LogDebug(string format, params object[] paramList);
        /// <summary>
        /// For logging progress no new newline output.
        /// </summary>
        void LogInfoP(string format, params object[] paramList);
    }

    /// <summary>
    /// Simple Console Logger.
    /// </summary>
    public class Logger : ILogger
    {
        private static readonly Logger _instance = new Logger();

        public void LogException(Exception ex, string format, params object[] paramList)
        {
            Console.WriteLine("{0}: {1} {2}", ex.GetType(), string.Format(format, paramList), ex.Message);
        }

        public void LogInfo(string format, params object[] paramList)
        {
            Console.WriteLine(format, paramList);
        }

        public void LogInfoP(string format, params object[] paramList)
        {
            Console.Write(format, paramList);
        }


        public void LogDebug(string format, params object[] paramList)
        {
            Console.WriteLine(format, paramList);
        }

        public static Logger Instance
        {
            get { return _instance; }
        }
    }
}