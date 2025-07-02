using Autodesk.Revit.DB;
using Document = Autodesk.Revit.DB.Document;

namespace RevitUtils
{
    public static class RevitPurginqHelper
    {
        public static IDictionary<int, ElementId> PurgeAndGetValidConstructionTypeIds(Document doc)
        {
            //  Categories whose types will be purged
            List<BuiltInCategory> purgeBuiltInCats =
            [
                BuiltInCategory.OST_Roofs,
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_Floors,
            ];

            FilteredElementCollector collector;

            ElementMulticategoryFilter multiCat = new(purgeBuiltInCats);

            IDictionary<int, ElementId> validTypeIds = new Dictionary<int, ElementId>(100);
            IDictionary<int, ElementId> invalidTypeIds = new Dictionary<int, ElementId>(25);

            collector = new FilteredElementCollector(doc).WherePasses(multiCat);

            foreach (Element elm in collector.WhereElementIsNotElementType())
            {
                ElementId etypeId = elm.GetTypeId();

                int typeIntId = etypeId.IntegerValue;

                if (!validTypeIds.ContainsKey(typeIntId))
                {
                    validTypeIds[typeIntId] = etypeId;
                }
            }


            collector = new FilteredElementCollector(doc).WherePasses(multiCat);

            foreach (Element etp in collector.WhereElementIsElementType())
            {
                int typeIntId = etp.Id.IntegerValue;

                if (!validTypeIds.ContainsKey(typeIntId))
                {
                    invalidTypeIds[typeIntId] = etp.Id;
                }
            }


            using (TransactionGroup tg = new(doc))
            {
                TransactionStatus status = tg.Start("Purge types");

                foreach (KeyValuePair<int, ElementId> item in invalidTypeIds)
                {
                    if (DocumentValidation.CanDeleteElement(doc, item.Value))
                    {
                        using Transaction trx = new(doc, "DeleteElement type");

                        FailureHandlingOptions failOpt = trx.GetFailureHandlingOptions();
                        failOpt = failOpt.SetFailuresPreprocessor(new WarningSwallower());
                        failOpt = failOpt.SetClearAfterRollback(true);
                        trx.SetFailureHandlingOptions(failOpt);

                        if (TransactionStatus.Started == trx.Start())
                        {
                            try
                            {
                                _ = doc.Delete(item.Value);
                                status = trx.Commit();
                            }
                            finally
                            {
                                if (!trx.HasEnded())
                                {
                                    status = trx.RollBack();
                                }
                            }
                        }
                    }
                }
                status = tg.Assimilate();
            }

            return validTypeIds;
        }
    }




}
