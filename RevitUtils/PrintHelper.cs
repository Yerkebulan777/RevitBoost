using Autodesk.Revit.DB;
using CommonUtils;
using System.Diagnostics;
using PaperSize = System.Drawing.Printing.PaperSize;

namespace RevitUtils
{
    public class SheetFormatGroup
    {
        /// <summary>
        /// Имя формата
        /// </summary>
        public string FormatName { get; set; }

        /// <summary>
        /// Размер бумаги
        /// </summary>
        public PaperSize PaperSize { get; set; }

        /// <summary>
        /// Флаг цветной печати
        /// </summary>
        public bool IsColorEnabled { get; set; }

        /// <summary>
        /// Ориентация листа
        /// </summary>
        public PageOrientationType Orientation { get; set; }
    }


    public static class PrintHelper
    {
        /// <summary>
        /// Получает и группирует данные листов для последующей печати
        /// </summary>
        public static Dictionary<string, SheetFormatGroup> GetData(Document doc, string printerName, bool сolorEnabled)
        {
            BuiltInCategory bic = BuiltInCategory.OST_TitleBlocks;
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfCategory(bic);
            collector = collector.OfClass(typeof(FamilyInstance)).WhereElementIsNotElementType();

            Dictionary<string, SheetFormatGroup> formatGroups = new(StringComparer.OrdinalIgnoreCase);

            foreach (FamilyInstance titleBlock in collector.Cast<FamilyInstance>())
            {
                double widthInMm = UnitManager.FootToMm(titleBlock.get_Parameter(BuiltInParameter.SHEET_WIDTH).AsDouble());
                double heightInMm = UnitManager.FootToMm(titleBlock.get_Parameter(BuiltInParameter.SHEET_HEIGHT).AsDouble());

                string sheetNumber = titleBlock.get_Parameter(BuiltInParameter.SHEET_NUMBER).AsString();

                PageOrientationType orientation = GetOrientation(widthInMm, heightInMm);

                Element sheetInstance = GetViewSheetByNumber(doc, sheetNumber);

                if (sheetInstance is ViewSheet viewSheet && viewSheet.CanBePrinted)
                {
                    if (viewSheet.IsPlaceholder)
                    {
                        Debug.Fail("Skipping placeholder sheet: {SheetNumber}", sheetNumber);
                        continue;
                    }

                    string formatName = string.Empty;

                    if (PrinterApiUtility.GetOrCreatePaperSize(printerName, widthInMm, heightInMm, out PaperSize paperSize))
                    {
                        Debug.WriteLine($"Paper size: {paperSize.PaperName} ({paperSize.Width} x {paperSize.Height})");

                        if (!formatGroups.TryGetValue(formatName, out SheetFormatGroup group))
                        {
                            group = new SheetFormatGroup
                            {
                                PaperSize = paperSize,
                                FormatName = formatName,
                                Orientation = orientation,
                                IsColorEnabled = сolorEnabled
                            };

                            formatGroups[formatName] = group;
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


    }
}
