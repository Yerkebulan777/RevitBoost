using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace RevitBIM.Config;

/// <summary>
///     Application logging configuration
/// </summary>
/// <example>
/// <code lang="csharp">
/// public class Class(ILogger&lt;Class&gt; logger)
/// {
///     private void Execute()
///     {
///         logger.LogInformation("Message");
///     }
/// }
/// </code>
/// </example>
public static class LoggerConfigurator
{
    public static void AddSerilogConfiguration(this ILoggingBuilder builder)
    {
        var logger = CreateDefaultLogger();

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
        var exception = (Exception)args.ExceptionObject;
        var logger = Host.GetService<ILogger<AppDomain>>();
        logger.LogCritical(exception, "Domain unhandled exception");
    }
}