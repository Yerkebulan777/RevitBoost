// CommonUtils/CommandLoggerHelper.cs
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.IO;

namespace CommonUtils
{
    public static class CommandLoggerHelper
    {
        /// <summary>
        /// Создает временный логгер для команды с отдельным файлом
        /// </summary>
        public static IModuleLogger CreateCommandLogger(string commandName, string logFolderPath = null)
        {
            logFolderPath ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RevitBoost");

            if (!Directory.Exists(logFolderPath))
            {
                _ = Directory.CreateDirectory(logFolderPath);
            }

            // Создаем логгер для команды с отдельным файлом
            Logger commandLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.WithProperty("UserName", Environment.UserName)
                .Enrich.WithProperty("Application", "RevitBoost")
                .Enrich.WithProperty("Command", commandName)
                .WriteTo.Debug()
                .WriteTo.File(
                    Path.Combine(logFolderPath, $"{commandName}.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 5,
                    shared: true)
                .CreateLogger();

            return new ModuleLogger(commandLogger, commandName);
        }

        /// <summary>
        /// Создает логгер команды, получая директорию логов из DI
        /// </summary>
        /// <param name="commandName">Имя команды</param>
        /// <param name="serviceProvider">Провайдер сервисов для получения конфигурации</param>
        /// <returns>Настроенный логгер для команды</returns>
        public static IModuleLogger CreateCommandLogger(string commandName, Func<string> getLogDirectory)
        {
            string logDirectory = getLogDirectory?.Invoke();
            return CreateCommandLogger(commandName, logDirectory);
        }
    }
}