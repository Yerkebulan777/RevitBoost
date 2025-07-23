using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ExportModule.Core;
using System.Diagnostics;

namespace ExportModule.Processors
{
    /// <summary>
    /// Процессор экспорта в NWC (Navisworks)
    /// </summary>
    public class NwcExportProcessor : IExportProcessor
    {
        public ExportType Type => ExportType.Nwc;
        public string FileExtension => ".nwc";
        public string FolderName => "05_NWC";

        private static readonly BuiltInCategory[] CategoriesToHide = new[]
        {
            BuiltInCategory.OST_MassForm,
            BuiltInCategory.OST_Lines
        };

        public async Task<ExportResult> ExecuteAsync(UIDocument uidoc, ExportRequest request)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                string exportDirectory = Path.Combine(request.BaseExportDirectory, FolderName);
                _ = Directory.CreateDirectory(exportDirectory);

                string outputFilePath = Path.Combine(exportDirectory, $"{request.RevitFileName}{FileExtension}");

                // Подготавливаем документ для экспорта
                PrepareDocumentForNwcExport(uidoc.Document);

                // Получаем или создаем 3D вид для экспорта
                View3D exportView = GetOrCreate3DView(uidoc.Document, "3DNavisView");

                if (exportView == null)
                {
                    return ExportResult.Failure("Cannot create 3D view for NWC export");
                }

                // Настраиваем 3D вид
                Configure3DView(uidoc.Document, exportView);

                // Создаем опции экспорта
                NavisworksExportOptions nwcOptions = CreateNavisworksOptions(exportView.Id);

                // Выполняем экспорт
                string exportDir = Path.GetDirectoryName(outputFilePath);
                string fileName = Path.GetFileNameWithoutExtension(outputFilePath);

                uidoc.Document.Export(exportDir, fileName, nwcOptions);

                stopwatch.Stop();

                if (File.Exists(outputFilePath))
                {
                    if (request.OpenFolderAfterExport)
                    {
                        OpenFolder(exportDirectory);
                    }

                    return ExportResult.Success(outputFilePath, stopwatch.Elapsed);
                }
                else
                {
                    return ExportResult.Failure("NWC file was not created");
                }
            }
            catch (Exception ex)
            {
                return ExportResult.Failure($"NWC export exception: {ex.Message}");
            }
        }

        public bool CanExport(UIDocument uidoc)
        {
            if (uidoc?.Document == null)
            {
                return false;
            }

            // Проверяем что документ содержит 3D геометрию
            IEnumerable<Element> collector = new FilteredElementCollector(uidoc.Document)
                .WhereElementIsNotElementType()
                .Where(e => e.Category != null && e.get_Geometry(new Options()) != null);

            return collector.Any();
        }

        private void PrepareDocumentForNwcExport(Document doc)
        {
            // Удаляем импортированные CAD файлы если они есть
            List<ElementId> cadImportIds = GetLinkedAndImportedCADIds(doc);

            if (cadImportIds.Any())
            {
                using Transaction transaction = new(doc, "Remove CAD imports for NWC export");
                _ = transaction.Start();

                try
                {
                    _ = doc.Delete(cadImportIds);
                    _ = transaction.Commit();
                }
                catch
                {
                    _ = transaction.RollBack();
                }
            }
        }

        private static List<ElementId> GetLinkedAndImportedCADIds(Document doc)
        {
            List<ElementId> cadIds = [];

            ICollection<ElementId> cadLinkTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(CADLinkType)).ToElementIds();

            ICollection<ElementId> cadImports = new FilteredElementCollector(doc)
                .OfClass(typeof(ImportInstance)).ToElementIds();

            cadIds.AddRange(cadLinkTypes);
            cadIds.AddRange(cadImports);

            return cadIds;
        }

        private View3D GetOrCreate3DView(Document doc, string viewName)
        {
            // Ищем существующий 3D вид
            View3D existing3DView = new FilteredElementCollector(doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .FirstOrDefault(v => v.Name.Equals(viewName, StringComparison.OrdinalIgnoreCase));

            if (existing3DView != null)
            {
                return existing3DView;
            }

            // Создаем новый 3D вид
            try
            {
                using Transaction transaction = new(doc, "Create 3D view for NWC export");
                _ = transaction.Start();

                ViewFamilyType viewFamilyType = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.ThreeDimensional);

                if (viewFamilyType != null)
                {
                    View3D view3D = View3D.CreateIsometric(doc, viewFamilyType.Id);
                    view3D.Name = viewName;
                    _ = transaction.Commit();
                    return view3D;
                }

                _ = transaction.RollBack();
            }
            catch
            {
                // Возвращаем любой доступный 3D вид
                return new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .FirstOrDefault(v => !v.IsTemplate);
            }

            return null;
        }

        private void Configure3DView(Document doc, View3D view)
        {
            using Transaction transaction = new(doc, "Configure 3D view for NWC export");
            _ = transaction.Start();

            try
            {
                // Настройки отображения
                view.DetailLevel = ViewDetailLevel.Fine;
                view.DisplayStyle = DisplayStyle.ShadingWithEdges;

                // Скрываем ненужные категории
                foreach (BuiltInCategory category in CategoriesToHide)
                {
                    ElementId categoryId = new(category);
                    if (view.CanCategoryBeHidden(categoryId))
                    {
                        view.SetCategoryHidden(categoryId, true);
                    }
                }

                // Скрываем элементы начинающиеся с #
                HideElementsByPattern(doc, view, @"^#.*");

                _ = transaction.Commit();
            }
            catch
            {
                _ = transaction.RollBack();
            }
        }

        private void HideElementsByPattern(Document doc, View view, string pattern)
        {
            try
            {
                List<ElementId> elementsToHide = new FilteredElementCollector(doc, view.Id)
                    .WhereElementIsNotElementType()
                    .Where(e => e.Name != null && System.Text.RegularExpressions.Regex.IsMatch(e.Name, pattern))
                    .Select(e => e.Id)
                    .ToList();

                if (elementsToHide.Any())
                {
                    view.HideElements(elementsToHide);
                }
            }
            catch
            {
                // Игнорируем ошибки скрытия элементов
            }
        }

        private NavisworksExportOptions CreateNavisworksOptions(ElementId viewId)
        {
            return new NavisworksExportOptions
            {
                ExportScope = NavisworksExportScope.View,
                Coordinates = NavisworksCoordinates.Shared,
                ConvertElementProperties = true,
                ExportRoomAsAttribute = true,
                DivideFileIntoLevels = true,
                ExportRoomGeometry = false,
                ExportParts = false,
                ExportLinks = false,
                ExportUrls = false,
                ViewId = viewId
            };
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