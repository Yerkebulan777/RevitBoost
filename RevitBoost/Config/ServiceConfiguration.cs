using CommonUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.IO;

namespace RevitBoost.Config
{
    public static class ServiceConfiguration
    {
        public static IServiceCollection ConfigureServices(this IServiceCollection services)
        {
            string logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RevitBoost");

            services.AddLogging(builder =>
            {
                builder.ClearProviders();

                Logger logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .Enrich.WithProcessId()
                    .Enrich.WithProcessName()
                    .Enrich.WithEnvironmentName()
                    .Enrich.WithProperty("UserName", Environment.UserName)
                    .MinimumLevel.Override("System", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.Extensions", LogEventLevel.Warning)
                    .WriteTo.Debug(restrictedToMinimumLevel: LogEventLevel.Debug)
                    .WriteTo.File(
                        path: Path.Combine(logDirectory, "revit-boost-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 7)
                    .CreateLogger();

                builder.AddSerilog(logger, dispose: true);
            });

            services.AddSingleton<IModuleLoggerFactory, ModuleLoggerFactory>();

            return services;
        }
    }

}
