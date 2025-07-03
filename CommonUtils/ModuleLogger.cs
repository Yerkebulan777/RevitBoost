using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace CommonUtils
{
    public sealed class ModuleLogger : IModuleLogger
    {
        private readonly ILogger<ModuleLogger> _msLogger;
        private readonly Serilog.ILogger _serilogLogger;
        private readonly string _moduleName;

        public ModuleLogger(ILogger<ModuleLogger> msLogger, Serilog.ILogger serilogLogger, string moduleName)
        {
            _msLogger = msLogger;
            _serilogLogger = serilogLogger;
            _moduleName = moduleName;
        }

        public void LogDebug(string message, params object[] args)
        {
            using (LogContext.PushProperty("Module", _moduleName))
            {
                _msLogger.LogDebug(message, args);
                _serilogLogger.Debug(message, args);
            }
        }

        public void LogInformation(string message, params object[] args)
        {
            using (LogContext.PushProperty("Module", _moduleName))
            {
                _msLogger.LogInformation(message, args);
                _serilogLogger.Information(message, args);
            }
        }

        public void LogWarning(string message, params object[] args)
        {
            using (LogContext.PushProperty("Module", _moduleName))
            {
                _msLogger.LogWarning(message, args);
                _serilogLogger.Warning(message, args);
            }
        }

        public void LogError(Exception exception, string message, params object[] args)
        {
            using (LogContext.PushProperty("Module", _moduleName))
            {
                _msLogger.LogError(exception, message, args);
                _serilogLogger.Error(exception, message, args);
            }
        }

        public void LogCritical(Exception exception, string message, params object[] args)
        {
            using (LogContext.PushProperty("Module", _moduleName))
            {
                _msLogger.LogCritical(exception, message, args);
                _serilogLogger.Fatal(exception, message, args);
            }
        }

        public IDisposable BeginScope(string name, params (string key, object value)[] properties)
        {
            var disposables = new List<IDisposable>
            {
                _msLogger.BeginScope(name),
                LogContext.PushProperty("Module", _moduleName),
                LogContext.PushProperty("Scope", name)
            };

            foreach (var (key, value) in properties)
            {
                disposables.Add(LogContext.PushProperty(key, value));
            }

            return new CompositeDisposable(disposables);
        }

        private sealed class CompositeDisposable : IDisposable
        {
            private readonly List<IDisposable> _disposables;

            public CompositeDisposable(List<IDisposable> disposables)
            {
                _disposables = disposables;
            }

            public void Dispose()
            {
                foreach (var disposable in _disposables)
                {
                    disposable?.Dispose();
                }
            }
        }
    }
}