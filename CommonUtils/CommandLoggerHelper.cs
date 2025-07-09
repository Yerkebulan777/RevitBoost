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
        public static IModuleLogger CreateCommandLogger(string title,  string commandName, string logFolderPath)
        {
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
                .WriteTo.Debug(LogEventLevel.Debug)
                .WriteTo.File(
                    Path.Combine(logFolderPath, $"{title}.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 5,
                    shared: true)
                .CreateLogger();

            return new ModuleLogger(commandLogger, commandName);
        }
    }
}