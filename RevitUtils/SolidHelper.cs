using System.Diagnostics;

namespace RevitUtils
{
    public static class SolidHelper
    {
        public static Solid CreateSolidBoxByPoint(XYZ minPoint, XYZ maxPoint, double height)
        {
            XYZ pt0 = new(minPoint.X, minPoint.Y, minPoint.Z);
            XYZ pt1 = new(maxPoint.X, minPoint.Y, minPoint.Z);
            XYZ pt2 = new(maxPoint.X, maxPoint.Y, minPoint.Z);
            XYZ pt3 = new(minPoint.X, maxPoint.Y, minPoint.Z);

            Line edge0 = Line.CreateBound(pt0, pt1);
            Line edge1 = Line.CreateBound(pt1, pt2);
            Line edge2 = Line.CreateBound(pt2, pt3);
            Line edge3 = Line.CreateBound(pt3, pt0);

            List<Curve> profile = [edge0, edge1, edge2, edge3];
            List<CurveLoop> loops = [CurveLoop.Create(profile)];

            Solid solidBox = GeometryCreationUtilities.CreateExtrusionGeometry(loops, XYZ.BasisZ, height);

            return solidBox;
        }


        public static Solid GetSolid(this Element element, in Transform global, double tolerance = 0.005)
        {
            Solid result = null;

            if (element.IsValidObject)
            {
                GeometryElement geomElem = element.get_Geometry(new Options());
                foreach (GeometryObject obj in geomElem.GetTransformed(global))
                {
                    if (obj is Solid solid && solid.Faces.Size > 0)
                    {
                        double volume = solid.Volume;

                        if (volume > tolerance)
                        {
                            tolerance = volume;
                            result = solid;
                            break;
                        }
                    }
                }
            }

            return result;
        }


        public static ISet<XYZ> GetIntersection(this Element element, in Solid solid, in Transform trf, in Options opt, SolidCurveIntersectionOptions interOpt)
        {
            ISet<XYZ> vertices = new HashSet<XYZ>();
            FaceArray faces = element.GetFaces(trf, opt);
            for (int index = 0; index < faces.Size; index++)
            {
                Face face = faces.get_Item(index);
                IList<CurveLoop> loops = face.GetEdgesAsCurveLoops();
                for (int idx = 0; idx < loops.Count; idx++)
                {
                    foreach (Curve curve in loops[idx])
                    {
                        SolidCurveIntersection curves = solid.IntersectWithCurve(curve, interOpt);

                        if (curves != null && curves.SegmentCount > 0)
                        {
                            for (int i = 0; i < curves.SegmentCount; i++)
                            {
                                Curve segment = curves.GetCurveSegment(i);
                                vertices.Add(segment.GetEndPoint(0));
                                vertices.Add(segment.GetEndPoint(1));
                            }
                        }
                    }
                }
            }

            return vertices;
        }


        public static ISet<XYZ> GetVertices(this Solid solid)
        {
            ISet<XYZ> vertices = new HashSet<XYZ>();
            foreach (Face f in solid.Faces)
            {
                Mesh mesh = f.Triangulate();
                int n = mesh.NumTriangles;
                for (int i = 0; i < n; ++i)
                {
                    MeshTriangle triangle = mesh.get_Triangle(i);
                    _ = vertices.Add(triangle.get_Vertex(0));
                    _ = vertices.Add(triangle.get_Vertex(1));
                    _ = vertices.Add(triangle.get_Vertex(2));
                }
            }
            return vertices;
        }


        public static Solid ScaledSolidByOffset(this Solid solid, double offset)
        {
            XYZ centroid = solid.ComputeCentroid();
            XYZ pnt = new XYZ(offset, offset, offset);
            BoundingBoxXYZ bbox = solid.GetBoundingBox();
            XYZ minPnt = bbox.Min; XYZ maxPnt = bbox.Max;
            double minDiagonal = minPnt.DistanceTo(maxPnt);
            double maxDiagonal = (minPnt - pnt).DistanceTo(maxPnt + pnt);
            Transform trans = Transform.CreateTranslation(XYZ.Zero).ScaleBasisAndOrigin(maxDiagonal / minDiagonal);
            solid = SolidUtils.CreateTransformed(solid, trans.Multiply(Transform.CreateTranslation(centroid).Inverse));
            return SolidUtils.CreateTransformed(solid, Transform.CreateTranslation(centroid));
        }


        public static double ComputeIntersectionVolume(Solid solidA, Solid solidB)
        {
            Solid intersect = BooleanOperationsUtils.ExecuteBooleanOperation(solidA, solidB, BooleanOperationsType.Intersect);
            return intersect.Volume;
        }


        public static void CreateDirectShape(this Solid solid, Document doc, BuiltInCategory builtIn = BuiltInCategory.OST_GenericModel)
        {
            using Transaction trx = new Transaction(doc, "CreateDirectShape");
            TransactionStatus status = trx.Start();
            try
            {
                DirectShape ds = DirectShape.CreateElement(doc, new ElementId(builtIn));
                ds.ApplicationDataId = doc.ProjectInformation.UniqueId;
                ds.SetShape(new GeometryObject[] { solid });
                ds.Name = "DirectShapeBySolid";
                status = trx.Commit();
            }
            catch (Exception ex)
            {
                if (!trx.HasEnded())
                {
                    status = trx.RollBack();
                    Debug.WriteLine(ex);
                }
            }
        }


        public static FaceArray GetFaces(this Element element, Transform trf, Options options)
        {
            FaceArray faces = new FaceArray();
            GeometryElement geometryElement = element.get_Geometry(options);
            foreach (GeometryObject geomObj1 in geometryElement.GetTransformed(trf))
            {
                if (geomObj1 is Solid solid1)
                {
                    foreach (Face face in solid1.Faces)
                    {
                        faces.Append(face);
                    }
                }
                else if (geomObj1 is GeometryInstance geometryInstance)
                {
                    foreach (GeometryObject geomObj2 in geometryInstance.SymbolGeometry)
                    {
                        if (geomObj2 is Solid solid2)
                        {
                            foreach (Face face in solid2.Faces)
                            {
                                faces.Append(face);
                            }
                        }
                    }
                }
            }
            return faces;
        }


        public static XYZ ComputeInstanceCentroid(ref Transform global, in Element instance)
        {
            List<XYZ> points = [];

            GeometryElement geometryElement = instance.get_Geometry(new Options());

            foreach (GeometryObject geometryObject in geometryElement.GetTransformed(global))
            {
                if (geometryObject is Curve curve)
                {
                    points.AddRange(curve.Tessellate());
                }
                else if (geometryObject is Solid solid)
                {
                    foreach (Edge edge in solid.Edges)
                    {
                        points.AddRange(edge.Tessellate());
                    }
                }
            }

            XYZ centroid = ComputeCentroidFromPoints(points);

            return centroid;
        }


        private static XYZ ComputeCentroidFromPoints(IEnumerable<XYZ> points)
        {
            XYZ centroid = XYZ.Zero;
            if (points.Any())
            {
                double sumX = 0;
                double sumY = 0;
                double sumZ = 0;
                int count = 0;

                foreach (XYZ point in points)
                {
                    sumX += point.X;
                    sumY += point.Y;
                    sumZ += point.Z;
                    count++;
                }

                centroid = new XYZ(sumX / count, sumY / count, sumZ / count);
            }
            return centroid;
        }



    }
}
