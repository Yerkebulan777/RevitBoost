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

            TransactionHelper.CreateTransaction(doc, "UngroupAllGroups", () =>
            {
                DeleteUnusedGroupTypes(doc);

                foreach (Group group in new FilteredElementCollector(doc).OfClass(typeof(Group)).Cast<Group>())
                {
                    List<string> memberUniqueIds = [];

                    foreach (ElementId memberId in group.UngroupMembers())
                    {
                        if (doc.GetElement(memberId) is Element element)
                        {
                            memberUniqueIds.Add(element.UniqueId);
                        }
                    }

                    groupInfos[group.GroupType.Name] = memberUniqueIds;
                }
            });

            return groupInfos;
        }

        /// <summary>
        /// Удаляет неиспользуемые типы групп
        /// </summary>
        private static void DeleteUnusedGroupTypes(Document doc)
        {
            foreach (GroupType groupType in new FilteredElementCollector(doc).OfClass(typeof(GroupType)).Cast<GroupType>())
            {
                if (groupType.Groups.Size == 0 || groupType.Groups.IsEmpty || groupType.Groups.IsReadOnly)
                {
                    try
                    {
                        _ = doc.Delete(groupType.Id);
                    }
                    catch (Exception ex)
                    {
                        Debug.Fail($"Failed group: {groupType.Name} {ex.Message}");
                    }
                }
            }
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
