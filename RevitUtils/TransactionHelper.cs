using Autodesk.Revit.DB;
using CommonUtils;
using System.Diagnostics;

namespace RevitUtils
{
    public static class TransactionHelper
    {
        public static void CreateTransaction(Document doc, string name, Action action)
        {
            using Transaction trx = new(doc);

            if (TransactionStatus.Started != trx.Start(name))
            {
                throw new InvalidProgramException($"Transaction!");
            }

            try
            {
                action?.Invoke();
                _ = trx.Commit();
            }
            catch (Exception ex)
            {
                if (!trx.HasEnded())
                {
                    _ = trx.RollBack();
                }

                StringHelper.CopyToClipboard($"Failed: {ex.Message}");
                Debug.Fail($"Transaction '{name}' failed: {ex.Message}");
                throw new InvalidOperationException($"Failed: {ex.Message}");
            }
        }


        public static void DeleteElements(Document doc, ICollection<ElementId> elemtIds)
        {
            List<ElementId> idsToDelete = [.. elemtIds];
            using Transaction trx = new(doc, "DeleteElements");
            if (trx.Start() == TransactionStatus.Started)
            {
                try
                {
                    doc.Delete(idsToDelete);
                    trx.Commit();
                }
                catch
                {
                    if (!trx.HasEnded())
                    {
                        trx.RollBack();
                    }
                }
            }
        }



    }
}