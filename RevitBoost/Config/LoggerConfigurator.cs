using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace RevitBoost.Config;

public static class LoggerConfigurator
{
    public static void AddSerilogConfiguration(this ILoggingBuilder builder)
    {
        Logger logger = CreateDefaultLogger();

        builder.AddSerilog(logger, dispose: true);

        AppDomain.CurrentDomain.UnhandledException += OnOnUnhandledException;
    }

    private static Logger CreateDefaultLogger()
    {
        return new LoggerConfiguration()
            .WriteTo.Debug(LogEventLevel.Debug)
            .MinimumLevel.Debug()
            .CreateLogger();
    }

    private static void OnOnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        Exception exception = (Exception)args.ExceptionObject;
        ILogger<AppDomain> logger = Host.GetService<ILogger<AppDomain>>();
        logger.LogCritical(exception, "Domain unhandled exception");
    }


}