using Autodesk.Revit.DB;
using Document = Autodesk.Revit.DB.Document;

namespace RevitUtils
{
    internal static class ClashHelper
    {

        /// <summary>
        ///  Получение воздуховодов и труб, пересекающихся с заданным узлом.
        /// </summary>
        public static FilteredElementCollector GetMepClashes(HostObject host)
        {
            Document doc = host.Document;

            List<BuiltInCategory> cats =
            [
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_PipeCurves,
            ];

            ElementMulticategoryFilter mepfilter = new(cats);

            BoundingBoxXYZ bb = host.get_BoundingBox(null);

            BoundingBoxIsInsideFilter bbfilter = new(new Outline(bb.Min, bb.Max));

            FilteredElementCollector clashingElements
                = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .WherePasses(mepfilter)
                    .WherePasses(bbfilter);

            return clashingElements;
        }
    }
}