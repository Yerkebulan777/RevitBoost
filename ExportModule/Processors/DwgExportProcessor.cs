using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ExportModule.Core;
using RevitUtils;
using System.Diagnostics;
using System.IO.Compression;

namespace ExportModule.Processors
{
    /// <summary>
    /// Процессор экспорта в DWG
    /// </summary>
    public class DwgExportProcessor : IExportProcessor
    {
        public ExportType Type => ExportType.Dwg;
        public string FileExtension => ".zip";
        public string FolderName => "02_DWG";

        private static readonly DWGExportOptions DefaultDwgOptions = new()
        {
            ACAPreference = ACAObjectPreference.Geometry,
            Colors = ExportColorMode.TrueColorPerView,
            PropOverrides = PropOverrideMode.ByEntity,
            ExportOfSolids = SolidGeometry.ACIS,
            TextTreatment = TextTreatment.Exact,
            TargetUnit = ExportUnit.Millimeter,
            FileVersion = ACADVersion.R2007,
            PreserveCoincidentLines = true,
            HideUnreferenceViewTags = true,
            HideReferencePlane = true,
            SharedCoords = true,
            HideScopeBox = true,
            MergedViews = true,
        };

        public ExportResult Execute(UIDocument uidoc, ExportRequest request)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                string exportDirectory = Path.Combine(request.BaseExportDirectory, FolderName);
                _ = Directory.CreateDirectory(exportDirectory);

                // Получаем листы для экспорта
                List<SheetModel> sheets = SheetModelUtility.GetSortedSheetModels(uidoc.Document, true, out string sheetLog);

                if (!sheets.Any())
                {
                    return ExportResult.Failure("No valid sheets found for DWG export");
                }

                // Создаем временную папку для DWG файлов
                string tempFolder = Path.Combine(Path.GetTempPath(), $"{request.RevitFileName}_DWG_{DateTime.Now:yyyyMMdd_HHmmss}");
                _ = Directory.CreateDirectory(tempFolder);

                try
                {
                    // Экспортируем каждый лист в отдельный DWG
                    int exportedCount = ExportSheetsToDwg(uidoc.Document, sheets, tempFolder);

                    if (exportedCount > 0)
                    {
                        // Создаем ZIP архив
                        string zipPath = Path.Combine(exportDirectory, $"{request.RevitFileName}{FileExtension}");
                        CreateZipFromFolder(tempFolder, zipPath);

                        stopwatch.Stop();

                        if (request.OpenFolderAfterExport && File.Exists(zipPath))
                        {
                            OpenFolder(exportDirectory);
                        }

                        return ExportResult.Success(zipPath, stopwatch.Elapsed);
                    }
                    else
                    {
                        return ExportResult.Failure("No sheets were successfully exported to DWG");
                    }
                }
                finally
                {
                    // Очищаем временную папку
                    if (Directory.Exists(tempFolder))
                    {
                        Directory.Delete(tempFolder, true);
                    }
                }
            }
            catch (Exception ex)
            {
                return ExportResult.Failure($"DWG export exception: {ex.Message}");
            }
        }

        public bool CanExport(UIDocument uidoc)
        {
            if (uidoc?.Document == null)
            {
                return false;
            }

            // Проверяем наличие листов
            FilteredElementCollector collector = new FilteredElementCollector(uidoc.Document)
                .OfClass(typeof(ViewSheet));

            return collector.Any();
        }

        private int ExportSheetsToDwg(Document doc, List<SheetModel> sheets, string outputFolder)
        {
            int successCount = 0;
            SpinWait spinWait = new();

            foreach (SheetModel sheet in sheets)
            {
                try
                {
                    List<ElementId> sheetIds = new()
                    { sheet.SheetId };

                    // Создаем безопасное имя файла
                    string safeSheetName = GetSafeFileName(sheet.SheetName);

                    if (doc.Export(outputFolder, safeSheetName, sheetIds, DefaultDwgOptions))
                    {
                        successCount++;
                        spinWait.SpinOnce();

                        // Небольшая задержка для стабильности
                        Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    // Логируем ошибку конкретного листа, но продолжаем экспорт
                    Debug.WriteLine($"Failed to export sheet {sheet.SheetName}: {ex.Message}");
                }
            }

            return successCount;
        }

        private string GetSafeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return "Sheet";
            }

            // Удаляем недопустимые символы для имени файла
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string safeName = new(fileName.Where(c => !invalidChars.Contains(c)).ToArray());

            return string.IsNullOrWhiteSpace(safeName) ? "Sheet" : safeName;
        }

        private void CreateZipFromFolder(string sourceFolder, string zipPath)
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            ZipFile.CreateFromDirectory(sourceFolder, zipPath, CompressionLevel.Optimal, false);
        }

        private void OpenFolder(string folderPath)
        {
            try
            {
                _ = Process.Start("explorer.exe", folderPath);
            }
            catch
            {
                // Игнорируем ошибки открытия папки
            }
        }
    }
}