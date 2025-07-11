﻿using Autodesk.Revit.DB;
using CommonUtils;
using System.Diagnostics;

namespace RevitUtils
{
    public static class TransactionHelper
    {
        public static bool TryCreateTransaction(Document doc, string name, Action action, out string error)
        {
            using Transaction trx = new(doc);

            if (TransactionStatus.Started == trx.Start(name))
            {
                try
                {
                    error = string.Empty;
                    action?.Invoke();
                    trx.Commit();
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
                finally
                {
                    if (!trx.HasEnded())
                    {
                        _ = trx.RollBack();
                    }
                }
            }

            throw new InvalidOperationException($"Failed start transaction '{name}'");
        }

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
                catch (Exception ex)
                {
                    StringHelper.CopyToClipboard($"Failed: {ex.Message}");
                    Debug.Fail($"Transaction '{name}' failed: {ex.Message}");
                    throw new InvalidOperationException($"Failed: {ex.Message}");
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