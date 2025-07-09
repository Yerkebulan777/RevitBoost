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

            TransactionHelper.CreateTransaction(doc, "DeleteUnusedGroups", () =>
            {
                List<GroupType> unusedGroupTypes =
                [.. new FilteredElementCollector(doc)
                    .OfClass(typeof(GroupType))
                    .OfType<GroupType>()];

                foreach (GroupType grt in unusedGroupTypes)
                {
                    if (grt.Groups.Size == 0 || grt.Groups.IsEmpty || grt.Groups.IsReadOnly)
                    {
                        try
                        {
                            _ = doc.Delete(grt.Id);
                        }
                        catch (Exception ex)
                        {
                            Debug.Fail($"Failed group: {grt.Name} {ex.Message}");
                        }
                    }
                }
            });

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

            TransactionHelper.CreateTransaction(doc, "DeleteUnusedGroups", () =>
            {
                List<GroupType> unusedGroupTypes =
                [.. new FilteredElementCollector(doc)
                    .OfClass(typeof(GroupType))
                    .OfType<GroupType>()];

                foreach (GroupType grt in unusedGroupTypes)
                {
                    if (grt.Groups.Size == 0 || grt.Groups.IsEmpty || grt.Groups.IsReadOnly)
                    {
                        try
                        {
                            _ = doc.Delete(grt.Id);
                        }
                        catch (Exception ex)
                        {
                            Debug.Fail($"Failed group: {grt.Name} {ex.Message}");
                        }
                    }
                }
            });

            return groupInfos;
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
