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
        private readonly Dictionary<ExportType, IExportProcessor> _processors;
        private readonly IModuleLogger _logger;

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
        public async Task<ExportResult> ExportSingleAsync(UIDocument uidoc, ExportType type, ExportRequest request)
        {
            if (!_processors.TryGetValue(type, out IExportProcessor processor))
            {
                return ExportResult.Failure($"Processor for {type} not found");
            }

            _logger.Information($"Starting {type} export for {request.RevitFileName}");

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                ExportResult result = await processor.ExecuteAsync(uidoc, request);
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

        /// <summary>
        /// Экспорт во все форматы
        /// </summary>
        public async Task<Dictionary<ExportType, ExportResult>> ExportAllAsync(
            UIDocument uidoc,
            ExportRequest request,
            ExportConfiguration config = null)
        {
            config ??= new ExportConfiguration();

            Dictionary<ExportType, ExportResult> results = new();
            List<(ExportType Type, Task<ExportResult> Task)> exportTasks = new();

            _logger.Information($"Starting multi-format export for {request.RevitFileName}");
            _logger.Information($"Parallel execution: {config.RunInParallel}");

            // Определяем какие форматы экспортировать
            List<ExportType> typesToExport = GetExportTypes(config);

            if (config.RunInParallel)
            {
                // Параллельный экспорт (рискованно для Revit API)
                foreach (ExportType type in typesToExport)
                {
                    if (_processors[type].CanExport(uidoc))
                    {
                        Task<ExportResult> task = ExportSingleAsync(uidoc, type, request);
                        exportTasks.Add((type, task));
                    }
                }

                _ = await Task.WhenAll(exportTasks.Select(t => t.Task));

                foreach ((ExportType type, Task<ExportResult> task) in exportTasks)
                {
                    results[type] = await task;
                }
            }
            else
            {
                // Последовательный экспорт (рекомендуется)
                foreach (ExportType type in typesToExport)
                {
                    if (!_processors[type].CanExport(uidoc))
                    {
                        results[type] = ExportResult.Failure($"Cannot export {type} - preconditions not met");
                        continue;
                    }

                    ExportResult result = await ExportSingleAsync(uidoc, type, request);
                    results[type] = result;

                    if (!result.IsSuccess && config.StopOnFirstError)
                    {
                        _logger.Warning($"Stopping export due to {type} failure");
                        break;
                    }
                }
            }

            return results;
        }

        private List<ExportType> GetExportTypes(ExportConfiguration config)
        {
            List<ExportType> types = new();

            if (config.ExportPdf)
            {
                types.Add(ExportType.Pdf);
            }

            if (config.ExportDwg)
            {
                types.Add(ExportType.Dwg);
            }

            if (config.ExportNwc)
            {
                types.Add(ExportType.Nwc);
            }

            return types;
        }


    }
}