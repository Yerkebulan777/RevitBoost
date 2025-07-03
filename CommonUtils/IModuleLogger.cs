using Autodesk.Revit.UI;

namespace CommonUtils
{
    public interface IModuleLogger
    {
        void LogDebug(string message, params object[] args);
        void LogInformation(string message, params object[] args);
        void LogWarning(string message, params object[] args);
        void LogError(Exception exception, string message, params object[] args);
        void LogCritical(Exception exception, string message, params object[] args);
        IDisposable BeginScope(string name, params (string key, object value)[] properties);
    }

    public interface IModuleLoggerFactory
    {
        IModuleLogger CreateLogger(string moduleName, string projectPath);
        void ConfigureRevitContext(UIControlledApplication application);
        Task FlushAndCloseAsync();
    }
}
