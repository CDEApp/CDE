using Serilog;

namespace cdeWin;

public static class LoggingBootstrap
{
    /// <summary>Sink feeding the Log tab; buffers events until the form attaches.</summary>
    public static InAppLogSink LogSink { get; } = new();

    public static void CreateLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Seq("http://localhost:5341") //todo: put to config.
            .WriteTo.Debug()
            .WriteTo.Sink(LogSink)
            .CreateLogger();
        Log.Logger.Debug("CDE Starting");
    }
}