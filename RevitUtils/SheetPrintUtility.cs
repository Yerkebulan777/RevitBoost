using Autodesk.Revit.DB;
using CommonUtils;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PaperSize = System.Drawing.Printing.PaperSize;

namespace RevitUtils
{
    public record SheetModel(ElementId ElementId)
    {
        public readonly ElementId ViewSheetId = ElementId;
        public required PaperSize SheetPaperSize { get; init; }
        public required string OrganizationGroupName { get; init; }
        public required PageOrientationType Orientation { get; init; }
        public required double DigitalSheetNumber { get; init; }
        public required bool IsColorEnabled { get; init; }
    }


    public static class SheetPrintUtility
    {
        /// <summary>
        /// Сортирует модели листов 
        /// </summary>
        public static List<SheetModel> SortSheetModels(List<SheetModel> sheetModels)
        {
            return sheetModels?
                .OrderBy(sm => sm.OrganizationGroupName)
                .ThenBy(sm => sm.DigitalSheetNumber).ToList();
        }

        /// <summary>
        /// Получает и группирует данные листов для последующей печати
        /// </summary>
        public static Dictionary<string, List<SheetModel>> GetSheetGroups(Document doc, string printerName, bool сolorEnabled)
        {
            BuiltInCategory bic = BuiltInCategory.OST_TitleBlocks;
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfCategory(bic);
            collector = collector.OfClass(typeof(FamilyInstance)).WhereElementIsNotElementType();

            Dictionary<string, List<SheetModel>> formatGroups = new(StringComparer.OrdinalIgnoreCase);

            foreach (FamilyInstance titleBlock in collector.Cast<FamilyInstance>())
            {
                double widthInMm = UnitManager.FootToMm(titleBlock.get_Parameter(BuiltInParameter.SHEET_WIDTH).AsDouble());
                double heightInMm = UnitManager.FootToMm(titleBlock.get_Parameter(BuiltInParameter.SHEET_HEIGHT).AsDouble());

                string sheetNumber = titleBlock.get_Parameter(BuiltInParameter.SHEET_NUMBER).AsString();

                Element sheetInstance = GetViewSheetByNumber(doc, sheetNumber);

                if (sheetInstance is ViewSheet viewSheet && viewSheet.CanBePrinted && !viewSheet.IsPlaceholder)
                {
                    if (PrinterApiUtility.GetOrCreatePaperSize(printerName, widthInMm, heightInMm, out PaperSize paperSize))
                    {
                        PageOrientationType orientation = GetOrientation(widthInMm, heightInMm);
                        Debug.WriteLine($"Sheet: {viewSheet.Name} ({paperSize.PaperName})");
                        string groupName = GetOrganizationGroupName(doc, viewSheet);
                        double digitNumber = ParseSheetNumber(sheetNumber);

                        if (IsValidSheetModel(groupName, digitNumber))
                        {
                            SheetModel sheetModel = new(viewSheet.Id)
                            {
                                OrganizationGroupName = groupName,
                                DigitalSheetNumber = digitNumber,
                                IsColorEnabled = сolorEnabled,
                                SheetPaperSize = paperSize,
                                Orientation = orientation,
                            };

                            if (!formatGroups.TryGetValue(paperSize.PaperName, out List<SheetModel> sheetList))
                            {
                                sheetList = [];
                                formatGroups[paperSize.PaperName] = sheetList;
                            }

                            sheetList.Add(sheetModel);
                        }
                    }
                }
            }

            return formatGroups;
        }

        /// <summary>
        /// Получает ViewSheet по номеру листа
        /// </summary>
        private static Element GetViewSheetByNumber(Document document, string sheetNumber)
        {
            ParameterValueProvider pvp = new(new ElementId(BuiltInParameter.SHEET_NUMBER));
            FilterStringRule filterRule = new(pvp, new FilterStringEquals(), sheetNumber);
            ElementParameterFilter sheetNumberFilter = new(filterRule);

            return new FilteredElementCollector(document)
                .OfClass(typeof(ViewSheet))
                .WherePasses(sheetNumberFilter)
                .FirstElement();
        }

        /// <summary>
        /// Определяет ориентацию страницы по ширине и высоте
        /// </summary>
        private static PageOrientationType GetOrientation(double width, double height)
        {
            return width > height ? PageOrientationType.Landscape : PageOrientationType.Portrait;
        }

        /// <summary>
        /// Получает имя организационной группы
        /// </summary>
        private static string GetOrganizationGroupName(Document doc, ViewSheet viewSheet)
        {
            Regex matchPrefix = new(@"^(\s*)");
            StringBuilder stringBuilder = new();

            BrowserOrganization organization = BrowserOrganization.GetCurrentBrowserOrganizationForSheets(doc);

            foreach (FolderItemInfo folderInfo in organization.GetFolderItems(viewSheet.Id))
            {
                if (folderInfo.IsValidObject)
                {
                    string folderName = folderInfo.Name;
                    folderName = matchPrefix.Replace(folderName, string.Empty);
                    _ = stringBuilder.Append(folderName);
                }
            }

            return StringHelper.ReplaceInvalidChars(stringBuilder.ToString());
        }

        /// <summary>
        /// Парсит номер листа для получения числового значения
        /// </summary>
        private static double ParseSheetNumber(string sheetNumber)
        {
            string digitNumber = Regex.Replace(sheetNumber, @"[^0-9,.]", string.Empty);
            return double.TryParse(digitNumber, NumberStyles.Any, CultureInfo.InvariantCulture, out double number) ? number : 0;
        }

        /// <summary>
        /// Проверяет валидность модели листа
        /// </summary>
        private static bool IsValidSheetModel(string groupName, double digit)
        {
            return !groupName.StartsWith("#") || digit is > 0 and < 500;
        }


        #region Formatting
        /// <summary>
        /// Форматирует имя листа по заданным параметрам
        /// </summary>
        public static string FormatSheetName(string projectName, string groupName, string sheetNumber, string viewSheetName)
        {
            string number = NormalizeSheetNumber(sheetNumber);

            string sheetTitle = string.IsNullOrWhiteSpace(groupName)
                ? StringHelper.NormalizeLength($"{projectName} - Лист-{number} - {viewSheetName}")
                : StringHelper.NormalizeLength($"{projectName} - Лист-{groupName}-{number} - {viewSheetName}");

            return StringHelper.ReplaceInvalidChars(sheetTitle);
        }

        /// <summary>
        /// Получает чистый номер листа
        /// </summary>
        private static string NormalizeSheetNumber(string inputSheetNumber)
        {
            string sheetNumber = StringHelper.ReplaceInvalidChars(inputSheetNumber);

            sheetNumber = sheetNumber.TrimStart('0');
            sheetNumber = sheetNumber.TrimEnd('.');

            return sheetNumber.Trim();
        }

        #endregion



    }
}
