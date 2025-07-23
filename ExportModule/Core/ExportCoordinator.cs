using Autodesk.Revit.UI;
using CommonUtils;
using ExportModule.Processors;
using System.Diagnostics;

namespace ExportModule.Core
{
    /// <summary>
    /// Координатор экспорта - управляет всеми процессорами
    /// </summary>
    public class ExportCoordinator
    {
        private readonly IModuleLogger _logger;
        private readonly Dictionary<ExportType, IExportProcessor> _processors;

        public ExportCoordinator(IModuleLogger logger)
        {
            _logger = logger;
            _processors = new Dictionary<ExportType, IExportProcessor>
            {
                { ExportType.Pdf, new PdfExportProcessor() },
                { ExportType.Dwg, new DwgExportProcessor() },
                { ExportType.Nwc, new NwcExportProcessor() }
            };
        }

        /// <summary>
        /// Экспорт в один формат
        /// </summary>
        public ExportResult Export(UIDocument uidoc, ExportType type, ExportRequest request)
        {
            if (!_processors.TryGetValue(type, out IExportProcessor processor))
            {
                return ExportResult.Failure($"Processor for {type} not found");
            }

            _logger.Information($"Starting {type} export for {request.RevitFileName}");

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                ExportResult result = processor.Execute(uidoc, request);
                stopwatch.Stop();

                _logger.Information($"{type} export completed in {stopwatch.Elapsed.TotalMinutes:F2} min");

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.Error(ex, $"Failed to export {type}");
                return ExportResult.Failure($"{type} export failed: {ex.Message}");
            }
        }



    }
}