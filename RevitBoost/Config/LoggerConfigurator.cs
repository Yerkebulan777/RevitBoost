using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.IO;

namespace RevitBoost.Config
{
    public static class LoggerConfigurator
    {
        public static void AddSerilogConfiguration(this ILoggingBuilder builder)
        {
            Logger logger = CreateRevitLogger();
            _ = builder.AddSerilog(logger, dispose: true);

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        private static Logger CreateRevitLogger()
        {
            string appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            return new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProcessId()
                .Enrich.WithProcessName()
                .Enrich.WithProperty("Application", "RevitBoost")
                .WriteTo.Debug(restrictedToMinimumLevel: LogEventLevel.Debug)
                .WriteTo.File(
                    path: Path.Combine(appDataDirectory, "RevitBoost", "logs", "revit-boost-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7)
                .CreateLogger();
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            Exception exception = (Exception)args.ExceptionObject;
            ILogger<AppDomain> logger = Host.GetService<ILogger<AppDomain>>();
            logger?.LogCritical(exception, "Domain unhandled exception");
        }
    }

}
