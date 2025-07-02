using Autodesk.Revit.DB;
using RevitUtils;
using System.Text;

namespace LevelAssignment
{
    public sealed class LevelAssignmentProcessor
    {
        private readonly Document _document;
        private readonly FloorInfoGenerator _floorInfoGenerator;
        private readonly BoundaryCalculator _boundaryCalculator;

        public LevelAssignmentProcessor(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));

            _floorInfoGenerator = new FloorInfoGenerator();
            _boundaryCalculator = new BoundaryCalculator();
        }


        private Outline ProjectBoundaryOutline { get; set; }
        private ElementMulticategoryFilter ModelCategoryFilter { get; set; }
        private SharedParameterElement LevelSharedParameter { get; set; }


        /// <summary>
        /// Выполняет полный цикл анализа и назначения элементов к этажам
        /// </summary>
        public string Execute(Guid sharedParameterGuid)
        {
            StringBuilder result = new();

            HashSet<ElementId> elemIdSet = [];

            double elevationOffset = UnitManager.MmToFoot(250);
            double verticalClearance = UnitManager.MmToFoot(100);

            List<FloorInfo> floorModels = _floorInfoGenerator.GenerateFloorModels(_document);

            LevelSharedParameter = SharedParameterElement.Lookup(_document, sharedParameterGuid);

            ProjectBoundaryOutline = _boundaryCalculator.ComputeProjectBoundary(_document, ref floorModels);

            ModelCategoryFilter = new ElementMulticategoryFilter(CollectorHelper.GetModelCategoryIds(_document));

            _ = result.AppendLine($"General parameter: {LevelSharedParameter?.Name}");
            _ = result.AppendLine($"Total number of floors: {floorModels?.Count}");

            foreach (FloorInfo floor in floorModels)
            {
                try
                {
                    _ = result.AppendLine();
                    floor.AggregateLevelFilter();
                    floor.ModelCategoryFilter = ModelCategoryFilter;
                    floor.LevelSharedParameter = LevelSharedParameter;
                    floor.CreateIntersectFilter(ProjectBoundaryOutline, elevationOffset, verticalClearance);

                    elemIdSet = [.. floor.CreateLevelFilteredCollector(_document).ToElementIds()];

#pragma warning disable S3267 // Loops should be simplified with "LINQ" expressions

                    foreach (Element element in floor.CreateExcludedCollector(_document, elemIdSet))
                    {
                        if (floor.IsElementContained(in element))
                        {
                            _ = elemIdSet.Add(element.Id);
                        }
                    }

#pragma warning restore S3267 // Loops should be simplified with "LINQ" expressions

                }
                catch (Exception ex)
                {
                    _ = result.AppendLine($"Error during floor processing: {ex.Message}");
                }
                finally
                {
                    _ = result.AppendLine($"Floor: {floor.DisplayName} ({floor.Index}) ");
                    _ = result.AppendLine($"Floor height: {UnitManager.FootToMm(floor.Height)}");
                    _ = result.AppendLine($"The total number of all elements found:{elemIdSet.Count}");
                    _ = result.AppendLine(ApplyLevelParameter(_document, elemIdSet, floor.Index));
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Устанавливает значение параметра BI_этаж для элементов
        /// </summary>
        public string ApplyLevelParameter(Document doc, HashSet<ElementId> elemIdSet, int levelValue)
        {
            int assignedCount = 0;
            int notModifiableCount = 0;
            int readOnlyParameterCount = 0;

            StringBuilder result = new();

            using (Transaction trx = new(doc, $"Setting the floor number {levelValue}"))
            {
                if (TransactionStatus.Started == trx?.Start())
                {
                    try
                    {
                        InternalDefinition levelParamGuid = LevelSharedParameter.GetDefinition();

                        _ = result.AppendLine($"Shared parameter: {levelParamGuid?.Name}");

                        foreach (ElementId elementId in elemIdSet)
                        {
                            Element element = doc.GetElement(elementId);
                            Parameter param = element?.get_Parameter(levelParamGuid);

                            if (param is not null)
                            {
                                if (param.IsReadOnly)
                                {
                                    readOnlyParameterCount++;
                                    continue;
                                }

                                if (!param.UserModifiable)
                                {
                                    notModifiableCount++;
                                    continue;
                                }

                                if (param.Set(levelValue))
                                {
                                    assignedCount++;
                                    continue;
                                }

                                string elementName = element.Name;
                                string category = element.Category.Name;

                                _ = result.AppendLine($"Failed to set parameter for element {elementName} in category {category}");

                            }
                            else
                            {
                                _ = result.AppendLine($"Parameter not found for element {element.Name}");
                            }
                        }

                        if (TransactionStatus.Committed != trx.Commit())
                        {
                            _ = result.AppendLine("Transaction could not be committed");
                        }
                    }
                    catch (Exception ex)
                    {
                        _ = result.AppendLine($"Error during transaction: {ex.Message}");
                    }
                    finally
                    {
                        if (!trx.HasEnded())
                        {
                            _ = trx.RollBack();
                        }

                        _ = result.AppendLine($"Total elements assigned: {assignedCount}");
                        _ = result.AppendLine($"Read-only elements: {readOnlyParameterCount}");
                        _ = result.AppendLine($"Not modifiable elements: {notModifiableCount}");
                    }
                }
            }

            return result.ToString();
        }



    }
}
