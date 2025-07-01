namespace LevelAssignment
{
    public enum Determination
    {
        ParameterBased,
        GeometricAnalysis,
        SpatialAnalysis,
        Failed,
    }

    public record AssignmentStatus
    {
        public readonly Element Element;
        public readonly float Confidence;
        public readonly Determination Method;

        public string Message { get; set; }
        public FloorInfo AssignedFloor { get; set; }
        
        public AssignmentStatus(Element element)
        {
            Method = Determination.Failed;
            Element = element;
            Confidence = 0;
        }
    }



}