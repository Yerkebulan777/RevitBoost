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
        public readonly ElementId ViewSheetId = ElementId;
        public required string OrganizationGroupName { get; init; }
        public required PageOrientationType Orientation { get; init; }
        public required double DigitalSheetNumber { get; init; }
        public required bool IsColorEnabled { get; init; }
        public required double HeightInMm { get; init; }
        public required double WidthInMm { get; init; }
    }


    public static class SheetModelUtility
    {
        /// <summary>
        /// Получает и сортирует модели листов для последующей печати
        /// </summary>
        public static List<SheetModel> GetSortedSheetModels(Document doc, bool colorEnabled, out string output)
        {
            return SortSheetModels(doc, GetSheetModels(doc, colorEnabled), out output);
        }

        /// <summary>
        /// Сортирует модели листов 
        /// </summary>
        /// <summary>
        /// Сортирует модели листов и возвращает информацию о них
        /// </summary>
        public static List<SheetModel> SortSheetModels(Document doc, IEnumerable<SheetModel> sheetModels, out string output)
        {
            int groupCount = 0;
            StringBuilder builder = new();
            string currentGroup = string.Empty;

            List<SheetModel> sortedSheets = new(100);

            foreach (SheetModel sheet in sortedSheets?
                .OrderBy(sm => sm.OrganizationGroupName)
                .ThenBy(sm => sm.DigitalSheetNumber))
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

                Element element = doc.GetElement(sheet.ViewSheetId);

                if (element is ViewSheet viewSheet)
                {
                    string sheetName = viewSheet.get_Parameter(BuiltInParameter.SHEET_NAME).AsString();
                    string sheetNumber = viewSheet.get_Parameter(BuiltInParameter.SHEET_NUMBER).AsString();
                    _ = builder.AppendLine($"  📄 {sheetNumber} - {sheetName} ({sheet.WidthInMm}x{sheet.HeightInMm})");
                }
            }

            output = builder.ToString();
            return sortedSheets;
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

                Element sheetInstance = GetViewSheetByNumber(doc, sheetNumber);

                if (sheetInstance is ViewSheet viewSheet)
                {
                    Debug.WriteLine("Sheet: " + viewSheet.Name);

                    if (viewSheet.CanBePrinted && !viewSheet.IsPlaceholder)
                    {
                        PageOrientationType orientation = GetOrientation(widthInMm, heightInMm);
                        string groupName = GetOrganizationGroupName(doc, viewSheet);
                        double digitNumber = ParseSheetNumber(sheetNumber);

                        if (IsValidSheetModel(groupName, digitNumber))
                        {
                            yield return new SheetModel(viewSheet.Id)
                            {
                                OrganizationGroupName = groupName,
                                DigitalSheetNumber = digitNumber,
                                IsColorEnabled = colorEnabled,
                                Orientation = orientation,
                                HeightInMm = heightInMm,
                                WidthInMm = widthInMm,
                            };
                        }
                    }
                }
            }
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
