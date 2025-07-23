using Autodesk.Revit.UI;

namespace ExportModule.Core
{
    /// <summary>
    /// Интерфейс для обработчиков экспорта
    /// </summary>
    public interface IExportProcessor
    {
        /// <summary>
        /// Тип экспорта
        /// </summary>
        ExportType Type { get; }

        /// <summary>
        /// Расширение выходного файла
        /// </summary>
        string FileExtension { get; }

        /// <summary>
        /// Название папки для экспорта
        /// </summary>
        string FolderName { get; }

        /// <summary>
        /// Выполнить экспорт
        /// </summary>
        Task<ExportResult> ExecuteAsync(UIDocument uidoc, ExportRequest request);

        /// <summary>
        /// Проверить возможность экспорта
        /// </summary>
        bool CanExport(UIDocument uidoc);
    }

    public enum ExportType
    {
        Pdf,
        Dwg,
        Nwc
    }


}