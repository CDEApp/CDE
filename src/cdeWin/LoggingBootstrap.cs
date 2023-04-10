using Serilog;

namespace cdeWin;

public static class LoggingBootstrap
{
    public static void CreateLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Seq("http://localhost:5341") //todo: put to config.
            .WriteTo.Debug()
            .CreateLogger();
        Log.Logger.Debug("CDE Starting");
    }
}