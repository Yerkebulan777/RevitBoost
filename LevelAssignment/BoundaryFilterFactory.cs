using RevitUtils;

namespace LevelAssignment
{
    public static class BoundaryFilterFactory
    {
        public static LogicalOrFilter CreateIntersectFilter(floorInfo floorInfo, Outline boundary, double offset, double clearance)
        {
            XYZ minPoint = boundary.MinimumPoint;
            XYZ maxPoint = boundary.MaximumPoint;

            minPoint = Transform.Identity.OfPoint(new XYZ(minPoint.X, minPoint.Y, floorInfo.Height + clearance - offset));

            maxPoint = Transform.Identity.OfPoint(new XYZ(maxPoint.X, maxPoint.Y, floorInfo.Height + floorInfo.Height - offset));

            Solid floorSolid = SolidHelper.CreateSolidBoxByPoint(minPoint, maxPoint, floorInfo.Height);

            Outline outline = new(minPoint, maxPoint);

            ElementIntersectsSolidFilter solidFilter = new(floorSolid);
            BoundingBoxIntersectsFilter boundingBoxFilter = new(outline);

            return new LogicalOrFilter(boundingBoxFilter, solidFilter);
        }



    }
}
