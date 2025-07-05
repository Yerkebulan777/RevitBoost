using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System.IO;

namespace RevitBoost
{
    public static class Host
    {
        private static IServiceProvider _services;

        public static void Start()
        {
            if (_services != null)
            {
                return;
            }

            ConfigureSerilog();

            ServiceCollection serviceCollection = new();

            _ = serviceCollection.AddLogging(config =>
            {
                _ = config.ClearProviders();
                _ = config.SetMinimumLevel(LogLevel.Debug);
                _ = config.AddFilter("Microsoft.Extensions", LogLevel.Warning);
                _ = config.AddFilter("System", LogLevel.Warning);

                // Добавляем Serilog как провайдер
                _ = config.AddSerilog();
            });

            _services = serviceCollection.BuildServiceProvider();
        }

        private static void ConfigureSerilog()
        {
            // Создаем директорию для логов
            string logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RevitBoost", "logs");

            _ = Directory.CreateDirectory(logDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft.Extensions", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.WithProperty("MachineName", Environment.MachineName)
                .Enrich.WithProperty("UserName", Environment.UserName)
                .Enrich.WithProperty("Application", "RevitBoost")
                .Enrich.FromLogContext()
                .WriteTo.Debug()
                .WriteTo.File(
                    path: Path.Combine(logDirectory, "revit-boost-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] [Revit {RevitVersion}] [{MachineName}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }

        public static T GetService<T>()
        {
            return _services != null ? _services.GetService<T>() : default;
        }

        public static void Stop()
        {
            if (_services is IDisposable disposable)
            {
                disposable.Dispose();
            }

            Log.CloseAndFlush();
            _services = null;
        }


    }
}