using Autodesk.Revit.DB;
using Serilog;
using Serilog.Context;

namespace CommonUtils
{
    public sealed class ModuleLogger : IModuleLogger
    {
        private readonly ILogger _logger;
        private readonly string _moduleName;
        public string LogFilePath { get; set; }

        private ModuleLogger(ILogger logger, string moduleName)
        {
            _logger = logger.ForContext("Module", moduleName);
            _moduleName = moduleName;
        }

        /// <summary>
        /// Создает логгер для Revit команды
        /// </summary>
        public static  IModuleLogger Create(Document document, Type callerType)
        {
            string moduleName = callerType.Name.Replace("Command", string.Empty);
            string projectDirectory = PathHelper.GetProjectDirectory(document, out string revitFilePath);
            string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
            string logDirectory = Path.Combine(projectDirectory, "Log", moduleName);
            string logFile = Path.Combine(logDirectory, $"{revitFileName}.log");

            PathHelper.EnsureDirectory(logDirectory);
            PathHelper.DeleteExistsFile(logFile);

            Serilog.Core.Logger logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(logFile, shared: true)
                .Enrich.WithProperty("Module", moduleName)
                .Enrich.WithProperty("Document", revitFileName)
                .CreateLogger();

            LogFilePath = logFile;

            return new ModuleLogger(logger, moduleName);
        }


        public void Debug(string message, params object[] args)
        {
            _logger.Debug(message, args);
        }

        public void Information(string message, params object[] args)
        {
            _logger.Information(message, args);
        }

        public void Warning(string message, params object[] args)
        {
            _logger.Warning(message, args);
        }

        public void Error(Exception exception, string message, params object[] args)
        {
            _logger.Error(exception, message, args);
        }

        public void Fatal(Exception exception, string message, params object[] args)
        {
            _logger.Fatal(exception, message, args);
        }

        public IDisposable BeginScope(string name, params (string key, object value)[] properties)
        {
            List<IDisposable> disposables =
            [
                LogContext.PushProperty("Scope", name),
                LogContext.PushProperty("Module", _moduleName)
            ];

            foreach ((string key, object value) in properties)
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
                foreach (IDisposable disposable in _disposables)
                {
                    disposable?.Dispose();
                }
            }
        }
    }
}