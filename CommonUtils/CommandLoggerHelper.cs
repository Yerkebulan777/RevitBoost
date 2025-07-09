// CommonUtils/CommandLoggerHelper.cs
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace CommonUtils
{
    public static class CommandLoggerHelper
    {
        /// <summary>
        /// Создает временный логгер для команды с отдельным файлом
        /// </summary>
        public static IModuleLogger CreateCommandLogger(string title, string commandName, string logFolderPath)
        {
            string logPath = Path.Combine(logFolderPath, $"{title}.log");

            if (!Directory.Exists(logFolderPath))
            {
                _ = Directory.CreateDirectory(logFolderPath);
            }
            else if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }

            // Создаем логгер для команды с отдельным файлом
            Logger commandLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.WithProperty("UserName", Environment.UserName)
                .Enrich.WithProperty("Application", "RevitBoost")
                .Enrich.WithProperty("Command", commandName)
                .WriteTo.File(path: logPath, shared: true)
                .WriteTo.Debug(LogEventLevel.Debug)
                .CreateLogger();

            return new ModuleLogger(commandLogger, commandName);
        }
    }
}