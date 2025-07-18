using Autodesk.Revit.DB;
using CommonUtils;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RevitUtils
{
    public record SheetModel(ElementId ElementId)
    {
        public readonly ElementId SheetId = ElementId;
        public required string SheetName { get; init; }
        public required string OrganizationGroupName { get; init; }
        public required PageOrientationType Orientation { get; init; }
        public required double DigitalSheetNumber { get; init; }
        public required bool IsColorEnabled { get; init; }
        public required double HeightInMm { get; init; }
        public required double WidthInMm { get; init; }
    }


    public static class SheetModelUtility
    {
        private static readonly Regex NumberPattern = new(@"[^0-9,.]", RegexOptions.Compiled);

        /// <summary>
        /// Получает и сортирует модели листов для последующей печати
        /// </summary>
        public static List<SheetModel> GetSortedSheetModels(Document doc, bool colorEnabled, out string output)
        {
            int groupCount = 0;
            StringBuilder builder = new();
            string currentGroup = string.Empty;

            List<SheetModel> sortedSheets = SortSheetModels(GetSheetModels(doc, colorEnabled));

            foreach (SheetModel sheet in sortedSheets)
            {
                if (currentGroup != sheet.OrganizationGroupName)
                {
                    if (groupCount > 0)
                    {
                        _ = builder.AppendLine();
                    }

                    groupCount++;
                    currentGroup = sheet.OrganizationGroupName;
                    _ = builder.AppendLine($"📁 Group: {currentGroup}");
                }

                _ = builder.AppendLine($" 📄 {sheet.DigitalSheetNumber} - {sheet.SheetName} ({sheet.WidthInMm}x{sheet.HeightInMm})");

            }

            output = builder.ToString();

            return sortedSheets;
        }

        /// <summary>
        /// Сортирует модели листов с фильтрацией по группам
        /// </summary>
        public static List<SheetModel> SortSheetModels(IEnumerable<SheetModel> sheetModels)
        {
            if (sheetModels != null)
            {
                if (sheetModels.Any(sm => !string.IsNullOrWhiteSpace(sm.OrganizationGroupName)))
                {
                    sheetModels = sheetModels.Where(sm => !string.IsNullOrWhiteSpace(sm.OrganizationGroupName));
                }

                return [.. sheetModels
                    .OrderBy(sm => sm.OrganizationGroupName)
                    .ThenBy(sm => sm.DigitalSheetNumber)];
            }

            throw new ArgumentNullException(nameof(sheetModels), "Sheet models collection cannot be null.");
        }

        /// <summary>
        /// Получает и группирует данные листов для последующей печати
        /// </summary>
        public static IEnumerable<SheetModel> GetSheetModels(Document doc, bool colorEnabled)
        {
            BuiltInCategory bic = BuiltInCategory.OST_TitleBlocks;
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfCategory(bic);
            collector = collector.OfClass(typeof(FamilyInstance)).WhereElementIsNotElementType();

            foreach (FamilyInstance titleBlock in collector.Cast<FamilyInstance>())
            {
                double widthInMm = UnitManager.FootToMm(titleBlock.get_Parameter(BuiltInParameter.SHEET_WIDTH).AsDouble());
                double heightInMm = UnitManager.FootToMm(titleBlock.get_Parameter(BuiltInParameter.SHEET_HEIGHT).AsDouble());

                string sheetNumber = titleBlock.get_Parameter(BuiltInParameter.SHEET_NUMBER).AsString();
                string sheetName = titleBlock.get_Parameter(BuiltInParameter.SHEET_NAME).AsString();

                Element sheetInstance = GetViewSheetByNumber(doc, sheetNumber);

                if (sheetInstance is ViewSheet viewSheet && viewSheet.IsValidObject) 
                {
                    if (viewSheet.CanBePrinted && !viewSheet.IsPlaceholder)
                    {
                        PageOrientationType orientation = GetOrientation(widthInMm, heightInMm);
                        string groupName = GetOrganizationGroupName(doc, viewSheet);
                        double digitNumber = ParseSheetNumber(sheetNumber);

                        if (IsValidSheet(groupName, digitNumber, sheetName))
                        {
                            yield return new SheetModel(viewSheet.Id)
                            {
                                OrganizationGroupName = groupName,
                                DigitalSheetNumber = digitNumber,
                                IsColorEnabled = colorEnabled,
                                Orientation = orientation,
                                HeightInMm = heightInMm,
                                WidthInMm = widthInMm,
                                SheetName = sheetName,
                            };
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Форматирует имя листа по заданным параметрам
        /// </summary>
        public static string FormatSheetName(string projectName, string groupName, string sheetNumber, string viewSheetName)
        {
            string normalizedNumber = NormalizeSheetNumber(sheetNumber);

            string sheetTitle = string.IsNullOrWhiteSpace(groupName)
                ? $"{projectName} - Лист-{normalizedNumber} - {viewSheetName}"
                : $"{projectName} - Лист-{groupName}-{normalizedNumber} - {viewSheetName}";

            return StringHelper.ReplaceInvalidChars(StringHelper.NormalizeLength(sheetTitle));
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
            BrowserOrganization organization = BrowserOrganization.GetCurrentBrowserOrganizationForSheets(doc);

            IList<FolderItemInfo> folderItems = organization.GetFolderItems(viewSheet.Id);

            if (folderItems.Any())
            {
                StringBuilder builder = new(folderItems.Count());

                foreach (FolderItemInfo folderInfo in folderItems)
                {
                    if (folderInfo != null && folderInfo.IsValidObject)
                    {
                        string folderName = folderInfo.Name;

                        if (!string.IsNullOrWhiteSpace(folderName))
                        {
                            _ = builder.Append(folderName.Trim());
                        }
                    }
                }

                return StringHelper.ReplaceInvalidChars(builder.ToString());
            }

            return string.Empty;
        }

        /// <summary>
        /// Парсит номер листа для получения числового значения
        /// </summary>
        private static double ParseSheetNumber(string sheetNumber)
        {
            string digitNumber = NumberPattern.Replace(sheetNumber, string.Empty);
            return double.TryParse(digitNumber, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ? number : 0;
        }

        /// <summary>
        /// Проверяет валидность модели листа
        /// </summary>
        private static bool IsValidSheet(string groupName, double digitalSheetNumber, string sheetName)
        {
            bool nameCheck = !string.IsNullOrWhiteSpace(sheetName) && sheetName.Length > 5;
            bool symbolCheck = !groupName.Contains("#") && !sheetName.Contains("#");
            bool groupCheck = digitalSheetNumber is > 0 and < 500;

            return symbolCheck && groupCheck && nameCheck;
        }

        /// <summary>
        /// Получает чистый номер листа
        /// </summary>
        private static string NormalizeSheetNumber(string inputSheetNumber)
        {
            return StringHelper.ReplaceInvalidChars(inputSheetNumber).TrimStart('0').TrimEnd('.');
        }



    }
}
