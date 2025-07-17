using Autodesk.Revit.DB;
using CommonUtils;

namespace RevitUtils
{
    public static class RevitPathHelper
    {

        public static string GetExportDirectory(Document doc, string folderName, out string revitFilePath)
        {
            revitFilePath = GetRevitFilePath(doc);
            return PathHelper.DetermineDirectory(revitFilePath, folderName);
        }


        static string GetRevitFilePath(Document document)
        {
            if (document.IsWorkshared && !document.IsDetached)
            {
                ModelPath modelPath = document.GetWorksharingCentralModelPath();
                return ModelPathUtils.ConvertModelPathToUserVisiblePath(modelPath);
            }

            return document.PathName;
        }


    }
}
