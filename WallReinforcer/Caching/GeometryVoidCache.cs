using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

using Line = Autodesk.Revit.DB.Line;

namespace RevitOSA.WallReinforcer.Caching
{
    public class GeometryVoidCache : GeometryFamilyInstanceCache
    {
        // Конструкторы
        public GeometryVoidCache(FamilyInstance voidElem) : base(voidElem)
        {

        }

        public GeometryVoidCache(Wall wall, double hostThickness) : base(wall)
        {
            preCalculations =
            [
                ((wall.Location as LocationCurve).Curve as Line).Direction,
                wall.Orientation,
                ((wall.Location as LocationCurve).Curve as Line).GetEndPoint(0) - BasePoint.Position 
                + wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).AsDouble() * XYZ.BasisZ,
            ];


            Dirs = new Directions
            {
                X = preCalculations[0] as XYZ,
                Y = preCalculations[1] as XYZ,
                Z = (preCalculations[0] as XYZ).CrossProduct(preCalculations[1] as XYZ)
            };

            List<double> HB =
            [
                wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble(),
                hostThickness,
                (wall.Location as LocationCurve).Curve.Length,
            ];

            Dims = new ControlDimensions
            {
                H = HB[0],
                B = HB[1],
                L = HB[2],
                ZBot = (preCalculations[2] as XYZ).Z,
                ZTop = (preCalculations[2] as XYZ).Z + HB.First(),
                OffsetBot = (double)preCalculations[3],
                OffsetTop = (double)preCalculations[3] + HB.First()
            };

            Origins = new ControlPoints
            {
                CenterStartBottom = preCalculations[2] as XYZ,
                CenterMiddleBottom = (preCalculations[2] as XYZ) + Dirs.X * Dims.L / 2,
                CenterEndBottom = (preCalculations[2] as XYZ) + Dirs.X * Dims.L,
                CenterMiddleMiddle = (preCalculations[2] as XYZ) + Dirs.X * Dims.L / 2 + Dirs.Z * Dims.H / 2,
                CenterStartTop = (preCalculations[2] as XYZ) + Dirs.Z * Dims.H,
                CenterMiddleTop = (preCalculations[2] as XYZ) + Dirs.X * Dims.L / 2 + Dirs.Z * Dims.H,
                CenterEndTop = (preCalculations[2] as XYZ) + Dirs.X * Dims.L + Dirs.Z * Dims.H,
            };
        }

        public GeometryVoidCache(FamilyInstance voidElem, GeometryCache geomCache) : base(voidElem)
        {
            HostGeomCache = geomCache;
            GetSketchLines();
        }

        // Методы
        public List<PlanarFace> GetAttachedFaces(WallCache wallCache)
        {
            if (wallCache.Geom.Solid == null) wallCache.Geom.GetSolidData();
            if (Solid == null) GetWallVoidSolid(wallCache);
            List<PlanarFace> attachedFaces = new List<PlanarFace>();

            foreach (PlanarFace face in Faces.FrontContour)
            {
                XYZ center = face.Evaluate(new UV(10 / 304.8, 10 / 304.8));
                Line cutLine = Line.CreateBound(center, center + face.FaceNormal.Normalize() * 10 / 304.8);
                List<Curve> spotLines = wallCache.Geom.Solid.IntersectWithCurve(cutLine, null).ToList();
                if (spotLines.Count == 0) attachedFaces.Add(face);
            }
            return attachedFaces;
        }
        public List<PlanarFace> GetBoundFaces(WallCache wallCache)
        {
            if (wallCache.Geom.Solid == null) wallCache.Geom.GetSolidData();
            if (Solid == null) GetWallVoidSolid(wallCache);
            List<PlanarFace> boundFaces = new List<PlanarFace>();

            foreach (PlanarFace voidFace in Faces.FrontContour)
            {
                XYZ center = voidFace.Evaluate(new UV(10 / 304.8, 10 / 304.8));
                foreach (PlanarFace hostFace in wallCache.Geom.Faces.FrontContour)
                {
                    IntersectionResult project = hostFace.Project(center);
#if REVIT2024 || REVIT2025
                    if (project != null && MathComparisonUtils.IsAlmostZero(project.Distance))
#else
                    if (project != null && project.Distance == 0)
#endif
                    {
                        boundFaces.Add(hostFace);
                        break;
                    }
                }
            }
            return boundFaces;
        }
        public double GetOffsetFromWallBottom()
        {
            double offset = 0;
            if (HostGeomCache != null) offset = Dims.ZBot - HostGeomCache.Dims.ZBot;
            return offset;
        }

        private void GetSketchLines()
        {
            XYZ p0 = Origins.CenterStartBottom + HostGeomCache.Dirs.Y * HostGeomCache.Dims.T / 2;
            XYZ p1 = p0 + HostGeomCache.Dirs.Z * Dims.H;
            XYZ p2 = Origins.CenterEndBottom + HostGeomCache.Dirs.Y * HostGeomCache.Dims.T / 2 + HostGeomCache.Dirs.Z * Dims.H;
            XYZ p3 = p2 - HostGeomCache.Dirs.Z * Dims.H;

            SketchLines = new List<Line>
            {
                Line.CreateBound(p0, p1),
                Line.CreateBound(p1, p2),
                Line.CreateBound(p2, p3),
                Line.CreateBound(p3, p0)
            };
        }

        // Свойства
        public GeometryCache HostGeomCache { get; private set; }
        public List<Line> SketchLines { get; private set; }

    }
}
