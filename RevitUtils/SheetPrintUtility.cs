using Autodesk.Revit.DB;
using CommonUtils;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PaperSize = System.Drawing.Printing.PaperSize;


namespace RevitUtils
{
    public record SheetModel
    {
        public required ElementId ViewSheetId { get; init; }
        public required PaperSize SheetPaperSize { get; init; }
        public required string OrganizationGroupName { get; init; }
        public required PageOrientationType Orientation { get; init; }
        public required double DigitalSheetNumber { get; init; }
        public required bool IsColorEnabled { get; init; }

        public bool IsValidSheetModel()
        {
            if (!OrganizationGroupName.StartsWith("#"))
            {
                if (DigitalSheetNumber is > 0 and < 500)
                {
                    return true;
                }
            }
            return false;
        }


    }


    public static class SheetPrintUtility
    {
        /// <summary>
        /// Сортирует модели листов 
        /// </summary>
        public static List<SheetModel> SortSheetModels(List<SheetModel> sheetModels)
        {
            return sheetModels?.Where(sm => sm.IsValidSheetModel())
                        .OrderBy(sm => sm.OrganizationGroupName)
                        .ThenBy(sm => sm.DigitalSheetNumber).ToList();
        }

        /// <summary>
        /// Получает и группирует данные листов для последующей печати
        /// </summary>
        public static Dictionary<string, SheetModel> GetData(Document doc, string printerName, bool сolorEnabled)
        {
            BuiltInCategory bic = BuiltInCategory.OST_TitleBlocks;
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfCategory(bic);
            collector = collector.OfClass(typeof(FamilyInstance)).WhereElementIsNotElementType();

            Dictionary<string, SheetModel> formatGroups = new(StringComparer.OrdinalIgnoreCase);

            foreach (FamilyInstance titleBlock in collector.Cast<FamilyInstance>())
            {
                double widthInMm = UnitManager.FootToMm(titleBlock.get_Parameter(BuiltInParameter.SHEET_WIDTH).AsDouble());
                double heightInMm = UnitManager.FootToMm(titleBlock.get_Parameter(BuiltInParameter.SHEET_HEIGHT).AsDouble());

                string sheetNumber = titleBlock.get_Parameter(BuiltInParameter.SHEET_NUMBER).AsString();

                PageOrientationType orientation = GetOrientation(widthInMm, heightInMm);

                Element sheetInstance = GetViewSheetByNumber(doc, sheetNumber);

                if (sheetInstance is ViewSheet viewSheet && viewSheet.CanBePrinted && !viewSheet.IsPlaceholder)
                {
                    if (PrinterApiUtility.GetOrCreatePaperSize(printerName, widthInMm, heightInMm, out PaperSize paperSize))
                    {
                        Debug.WriteLine($"Paper size: {paperSize.PaperName} ({paperSize.Width} x {paperSize.Height})");

                        string groupName = GetOrganizationGroupName(doc, viewSheet);

                        double digitNumber = ParseSheetNumber(sheetNumber);
                        if (formatGroups.TryGetValue(paperSize.PaperName, out _))
                        {

                        }
                        else
                        {
                            SheetModel group = new()
                            {
                                OrganizationGroupName = groupName,
                                DigitalSheetNumber = digitNumber,
                                IsColorEnabled = сolorEnabled,
                                SheetPaperSize = paperSize,
                                Orientation = orientation,
                                ViewSheetId = viewSheet.Id,

                            };

                            formatGroups.Add(paperSize.PaperName, group);
                        }

                    }
                }
            }

            return formatGroups;
        }

        /// <summary>
        /// Определяет ориентацию страницы по ширине и высоте
        /// </summary>
        private static PageOrientationType GetOrientation(double width, double height)
        {
            return width > height ? PageOrientationType.Landscape : PageOrientationType.Portrait;
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


        #region Formatting

        /// <summary>
        /// Получает имя организационной группы
        /// </summary>
        public static string GetOrganizationGroupName(Document doc, ViewSheet viewSheet)
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
        /// Получает чистый номер листа
        /// </summary>
        public static string GetSheetNumber(string inputSheetNumber)
        {
            string sheetNumber = StringHelper.ReplaceInvalidChars(inputSheetNumber);

            sheetNumber = sheetNumber.TrimStart('0');
            sheetNumber = sheetNumber.TrimEnd('.');

            return sheetNumber.Trim();
        }

        /// <summary>
        /// Форматирует имя листа по заданным параметрам
        /// </summary>
        public static string FormatSheetName(string projectName, string groupName, string sheetNumber, string viewSheetName)
        {
            string sheetTitle = string.IsNullOrWhiteSpace(groupName)
                ? StringHelper.NormalizeLength($"{projectName} - Лист-{sheetNumber} - {viewSheetName}")
                : StringHelper.NormalizeLength($"{projectName} - Лист-{groupName}-{sheetNumber} - {viewSheetName}");

            return StringHelper.ReplaceInvalidChars(sheetTitle);
        }

        /// <summary>
        /// Парсит номер листа для получения числового значения
        /// </summary>
        public static double ParseSheetNumber(string sheetNumber)
        {
            if (string.IsNullOrEmpty(sheetNumber))
            {
                return 0;
            }

            string digitNumber = Regex.Replace(sheetNumber, @"[^0-9,.]", string.Empty);

            return double.TryParse(digitNumber, NumberStyles.Any, CultureInfo.InvariantCulture, out double number) ? number : 0;
        }

        #endregion



    }
}
