using Autodesk.Revit.DB;
using System.Diagnostics;

namespace RevitUtils
{
    public static class TransactionHelpers
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
                Debug.WriteLine(ex);
                if (!trx.HasEnded())
                {
                    _ = trx.RollBack();
                }
            }
        }


        public static void DeleteElements(Document doc, ICollection<ElementId> elemtIds)
        {
            using Transaction trx = new(doc, "DeleteElements");
            IEnumerator<ElementId> enm = elemtIds.GetEnumerator();
            TransactionStatus status = trx.Start();
            if (status == TransactionStatus.Started)
            {
                while (enm.MoveNext())
                {
                    using SubTransaction subtrx = new(doc);
                    try
                    {
                        _ = subtrx.Start();
                        _ = doc.Delete(enm.Current);
                        _ = subtrx.Commit();
                    }
                    catch
                    {
                        _ = subtrx.RollBack();
                    }
                }

                enm.Dispose();
                elemtIds.Clear();
                _ = trx.Commit();
            }
        }


    }
}