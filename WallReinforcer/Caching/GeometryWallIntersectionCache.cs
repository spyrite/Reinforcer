using Autodesk.Revit.DB;
using RevitOSA.WallReinforcer.Tools;
using System.Collections.Generic;
using System.Linq;

namespace RevitOSA.WallReinforcer.Caching
{
    public class GeometryWallIntersectionCache : GeometryCache
    {
        // Конструкторы
        public GeometryWallIntersectionCache(GeometryWallCache geometryWallCache, List<GeometryCache> attachedGeometryCaches, XYZ origin)
        {
            DimSets = new List<ControlDimensions>();
            XYZ yDir = geometryWallCache.Dirs.Y;
            double l = geometryWallCache.Dims.T;

            if (attachedGeometryCaches != null && attachedGeometryCaches.Count > 0)
            {
                foreach (GeometryCache attachedGeomCache in attachedGeometryCaches)
                {
                    ControlDimensions dims = new ControlDimensions();
                    if (attachedGeomCache is GeometryWallCache || attachedGeomCache.Dirs.Y.IsAlmostEqualTo(GeometryTools.VecABS(geometryWallCache.Dirs.X)))
                        dims.T = attachedGeomCache.Dims.T;
                    else
                        dims.T = attachedGeomCache.Dims.L;
                    if (attachedGeomCache is GeometryColumnCache)
                        dims.L = attachedGeomCache.Dims.L;
                    DimSets.Add(dims);
                }

                if (GeometryTools.VecABS(Dirs.X).IsAlmostEqualTo(GeometryTools.VecABS(attachedGeometryCaches.First().Dirs.X)))
                {
                    yDir = attachedGeometryCaches.First().Dirs.Y;
                    l = attachedGeometryCaches.First().Dims.L;
                }
                else
                {
                    yDir = attachedGeometryCaches.First().Dirs.Y;
                    l = attachedGeometryCaches.First().Dims.T;
                }
            }

            Dirs = new Directions
            {
                X = geometryWallCache.Dirs.X,
                Y = yDir,
                Z = geometryWallCache.Dirs.Z
            };

            Dims = new ControlDimensions
            {
                L = l,
                H = geometryWallCache.Dims.H,
                T = geometryWallCache.Dims.T
            };

            Origins = new ControlPoints()
            {
                CenterStartBottom = origin - Dirs.X * Dims.L / 2,
                CenterMiddleBottom = origin,
                CenterEndBottom = origin + Dirs.X * Dims.L / 2
            };
        }

        // Методы
        public override void GetSolidData()
        {
            List<List<int>> tokenSets =
            [
                [0, -1],
                [1, -1],
                [1, 1],
                [0, 1]
            ];

            List<XYZ> points = [];
            foreach (List<int> tokens in tokenSets)
            {
                XYZ point = Origins.CenterMiddleBottom + Dirs.X * (Dims.L / 2) * tokens[0] + Dirs.Y * (Dims.T / 2) * tokens[1];
                points.Add(point);
            }

            List<Curve> lines =
            [
                Line.CreateBound(points[0], points[1]),
                Line.CreateBound(points[1], points[2]),
                Line.CreateBound(points[2], points[3]),
                Line.CreateBound(points[3], points[0])
            ];

            Solid = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { CurveLoop.Create(lines) }, Dirs.Z, Dims.H);
        }
        public void GetOutline()
        {
            XYZ p0 = Origins.CenterMiddleBottom - GeometryTools.VecABS(Dirs.X) * Dims.L / 2 - GeometryTools.VecABS(Dirs.Y) * Dims.T / 2;
            XYZ p1 = Origins.CenterMiddleBottom + GeometryTools.VecABS(Dirs.X) * Dims.L / 2 + GeometryTools.VecABS(Dirs.Y) * Dims.T / 2 + Dirs.Z * Dims.H;
            Outline = new Outline(p0, p1);
        }

        // Свойства
        List<ControlDimensions> DimSets { get; set; }
    }
}
