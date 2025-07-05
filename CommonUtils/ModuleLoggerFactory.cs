using Serilog;
using Serilog.Context;


namespace CommonUtils
{
    public class ModuleLoggerFactory : IModuleLoggerFactory
    {
        private readonly ILogger _baseLogger;

        public ModuleLoggerFactory(ILogger baseLogger)
        {
            _baseLogger = baseLogger;
        }

        public IModuleLogger CreateLogger(string moduleName, string projectPath)
        {
            var contextLogger = _baseLogger
                .ForContext("Module", moduleName)
                .ForContext("ProjectPath", projectPath);

            return new ModuleLogger(contextLogger, moduleName);
        }

        public void ConfigureRevitContext(string applicationName)
        {
            using (LogContext.PushProperty("Application", applicationName))
            {
                _baseLogger.Information("Revit context configured for {Application}", applicationName);
            }
        }

        public void FlushAndClose()
        {
            Log.CloseAndFlush();
        }
    }
}