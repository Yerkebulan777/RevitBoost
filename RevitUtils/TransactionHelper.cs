using Autodesk.Revit.DB;

namespace RevitUtils
{
    public static class TransactionHelper
    {
        public static void CreateTransaction(Document doc, string name, Action action)
        {
            using Transaction trx = new(doc);

            if (TransactionStatus.Started == trx.Start(name))
            {
                try
                {
                    action?.Invoke();
                    _ = trx.Commit();
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    if (!trx.HasEnded())
                    {
                        _ = trx.RollBack();
                    }
                }
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
                    _ = doc.Delete(idsToDelete);
                    _ = trx.Commit();
                }
                catch
                {
                    if (!trx.HasEnded())
                    {
                        _ = trx.RollBack();
                    }
                }
            }
        }


        /// <summary>
        /// Проверяет, можно ли выполнить транзакцию в документе
        /// </summary>
        public static bool CanCreateTransaction(Document doc)
        {
            return doc.IsValidObject && !doc.IsModified;
        }

    }
}