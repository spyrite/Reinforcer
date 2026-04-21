using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace RevitOSA.WallReinforcer.Caching
{
    public class BoxCache
    {
        private protected List<object> preCalculations;

        public GeometryCache.Directions Dirs { get; set; }
        public BoxDimensions Dims { get; set; }
        public BoxPoints Points { get; set; }
        public BoxLines Lines { get; set; }
        public BoxPlanes Planes { get; set; }

        public Solid CreateBoxSolid(double dst)
        {
            List<XYZ> points = new List<XYZ> { Points.Origin + Dirs.Z * Dims.H };
            points.Add(points.Last() + Dirs.Y * Dims.B);
            points.Add(points.Last() + Dirs.X * Dims.L);
            points.Add(points.Last() - Dirs.Y * Dims.B);
            List<Curve> lines = new List<Curve>
            {
                Line.CreateBound(points.Last(), points[0]),
                Line.CreateBound(points[0], points[1]),
                Line.CreateBound(points[1], points[2]),
                Line.CreateBound(points[2], points[3])
            };
            List<CurveLoop> cls = new List<CurveLoop> { CurveLoop.Create(lines) };
            Solid solid = GeometryCreationUtilities.CreateExtrusionGeometry(cls, -Dirs.Z, dst);
            return solid;
        }

        public class BoxDimensions
        {
            public double Diagonal { get; set; }
            public double L { get; set; }
            public double B { get; set; }
            public double H { get; set; }
        }
        public class BoxPoints
        {
            public XYZ Origin { get; set; }
            public XYZ Centroid { get; set; }
            public XYZ Far { get; set; }
        }
        public class BoxLines
        {
            public Line Bot { get; set; }
            public Line Left { get; set; }
            public Line Top { get; set; }
            public Line Right { get; set; }
            public Line CentroidDepth { get; set; }
            public Line CentroidDepthUnbound { get; set; }
            public Line Diagonal { get; set; }
        }
        public class BoxPlanes
        {
            public Plane Front { get; set; }
            public Plane Center { get; set; }
            public Plane Rear { get; set; }
        }
    }
}
