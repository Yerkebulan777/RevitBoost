using Serilog;

namespace RevitUtils.Logging
{
    public static class Log
    {
        private static readonly string _logFilePath = @"%APPDATA%\RevitBoost\Logs\CommonUtils-.log";

        private static readonly ILogger _logger = new LoggerConfiguration()
            .WriteTo.File(path: _logFilePath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 5)
            .CreateLogger();

        public static void Debug(string message)
        {
            _logger.Debug(message);
        }

        public static void Info(string message)
        {
            _logger.Information(message);
        }

        public static void Error(string message)
        {
            _logger.Error(message);
        }

        public static void Fatal(Exception ex, string message)
        {
            _logger.Fatal(message);
        }

        public static void CloseAndFlush()
        {
            Serilog.Log.CloseAndFlush();
        }
    }
}
