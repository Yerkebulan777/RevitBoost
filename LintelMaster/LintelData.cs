using Autodesk.Revit.DB;

namespace LintelMaster
{
    /// <summary>
    /// Represents lintel dimensions data
    /// </summary>
    public class LintelData
    {
        public readonly SizeKey GroupKey;
        public readonly FamilyInstance Instance;

        public LintelData(FamilyInstance lintel, int thick, int width, int height)
        {
            GroupKey = new SizeKey(thick, width, height);
            Instance = lintel;
        }

        /// Наименование группы  ///
        public SizeKey GroupName { get; set; }

    }
}
