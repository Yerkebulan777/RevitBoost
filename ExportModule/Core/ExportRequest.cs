using Autodesk.Revit.DB;
using CommonUtils;

namespace ExportModule.Core
{
    /// <summary>
    /// Запрос на экспорт
    /// </summary>
    public class ExportRequest
    {
        public string RevitFilePath { get; set; }
        public string RevitFileName { get; set; }
        public string BaseExportDirectory { get; set; }
        public bool OpenFolderAfterExport { get; set; } = true;
        public bool UseColorForPdf { get; set; } = true;

        public static ExportRequest FromDocument(Document doc)
        {
            string revitFilePath = PathHelper.GetRevitFilePath(doc);
            string revitFileName = Path.GetFileNameWithoutExtension(revitFilePath);
            string baseExportDirectory = Path.Combine(Path.GetDirectoryName(revitFilePath), "Export");

            return new ExportRequest
            {
                RevitFilePath = revitFilePath,
                RevitFileName = revitFileName,
                BaseExportDirectory = baseExportDirectory
            };
        }
    }

    /// <summary>
    /// Конфигурация экспорта
    /// </summary>
    public class ExportConfiguration
    {
        public bool ExportPdf { get; set; } = true;
        public bool ExportDwg { get; set; } = true;
        public bool ExportNwc { get; set; } = true;
        public bool StopOnFirstError { get; set; } = false;
    }



}