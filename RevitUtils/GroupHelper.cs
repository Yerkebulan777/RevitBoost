using Autodesk.Revit.DB;
using System.Diagnostics;

namespace RevitUtils
{
    public static class GroupHelper
    {
        /// <summary>
        /// Разгруппировывает все группы и возвращает информацию для восстановления
        /// </summary>
        public static Dictionary<string, List<string>> UngroupAllAndSaveInfo(Document doc)
        {
            Dictionary<string, List<string>> groupInfos = [];

            CleanupUnusedGroupTypes(doc);

            TransactionHelper.CreateTransaction(doc, "UngroupAllGroups", () =>
            {
                List<Group> groups = [.. new FilteredElementCollector(doc).OfClass(typeof(Group)).OfType<Group>()];

                foreach (Group group in groups.Where(el => el.IsValidObject))
                {
                    List<string> memberUniqueIds = [];

                    foreach (ElementId memberId in group.UngroupMembers())
                    {
                        Element element = doc.GetElement(memberId);

                        if (element?.IsValidObject == true)
                        {
                            memberUniqueIds.Add(element.UniqueId);
                        }
                    }

                    groupInfos[group.GroupType.Name] = memberUniqueIds;
                }
            });

            CleanupUnusedGroupTypes(doc);

            return groupInfos;
        }

        /// <summary>
        /// Очищает неиспользуемые типы групп
        /// </summary>
        private static void CleanupUnusedGroupTypes(Document doc)
        {
            TransactionHelper.CreateTransaction(doc, "DeleteUnusedGroupTypes", () =>
            {
                List<GroupType> unusedGroupTypes =
                    [.. new FilteredElementCollector(doc)
                    .OfClass(typeof(GroupType)).OfType<GroupType>()
                    .Where(gt => gt.IsValidObject)];

                foreach (GroupType grt in unusedGroupTypes)
                {
                    try
                    {
                        if (grt.Groups.Size == 0 || grt.Groups.IsEmpty)
                        {
                            doc.Delete(grt.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to delete group type {grt.Name}: {ex.Message}");
                    }
                }

            });
        }

        /// <summary>
        /// Восстанавливает группы
        /// </summary>
        public static void RestoreGroups(Document doc, Dictionary<string, List<string>> groupInfos)
        {
            if (groupInfos == null || groupInfos.Count == 0)
            {
                Debug.WriteLine("No groups to restore.");
                return;
            }

            TransactionHelper.CreateTransaction(doc, "RestoreGroups", () =>
            {
                foreach (KeyValuePair<string, List<string>> kvp in groupInfos)
                {
                    try
                    {
                        List<ElementId> memberIds = [];

                        foreach (string uniqueId in kvp.Value)
                        {
                            if (doc.GetElement(uniqueId) is Element element)
                            {
                                memberIds.Add(element.Id);
                            }
                        }

                        Group newGroup = doc.Create.NewGroup(memberIds);
                        newGroup.GroupType.Name = kvp.Key;
                    }
                    catch (Exception ex)
                    {
                        Debug.Fail($"Failed group: {kvp.Key} {ex.Message}");
                    }
                }
            });
        }



    }
}
